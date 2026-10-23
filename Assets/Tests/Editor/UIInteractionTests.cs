using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class UIInteractionTests
{
    private const string Key = "HustleScale.UIAudit.NUnit";
    private const string SaveOverride = "HustleScale.S2SaveOverride";

    [UnityTest]
    public IEnumerator PauseShortcutsStayInPauseMenu() => Run("Shortcuts");
    [UnityTest]
    public IEnumerator ClosingMenusDoesNotDispatchWorldInput() => Run("Transitions");
    [UnityTest]
    public IEnumerator CityScreensAtDiagnosticResolutions() => Run("Screens");
    [UnityTest]
    public IEnumerator ClosingMapDoesNotEnterVehicle() => Run("VehicleEntry");
    [UnityTest]
    public IEnumerator ResumingVehicleDoesNotExitIt() => Run("VehiclePause");
    [UnityTest]
    public IEnumerator InventoryThumbnailsFollowItemsAndModalInput() => Run("Inventory");
    [UnityTest]
    public IEnumerator NeighbourhoodMapShowsActualSceneTerrain() => Run("Map");

    private static IEnumerator Run(string scenario)
    {
        Assert.That(EditorApplication.isPlayingOrWillChangePlaymode, Is.False);
        Assert.That(SceneManager.sceneCount == 1 && !SceneManager.GetActiveScene().isDirty, Is.True);
        SessionState.SetString(Key + ".Scene", SceneManager.GetActiveScene().path);
        SessionState.SetString(Key + ".Override", SessionState.GetString(SaveOverride, ""));
        SessionState.SetInt(Key + ".Background", (int)InputSystem.settings.backgroundBehavior);
        SessionState.SetInt(Key + ".EditorInput", (int)InputSystem.settings.editorInputBehaviorInPlayMode);
        SessionState.SetBool(Key + ".RunInBackground", Application.runInBackground);
        SessionState.SetString(Key + ".Scenario", scenario);
        SessionState.SetBool(Key, true);
        var output = Path.GetFullPath("Temp/UIAudit/" + scenario + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        SessionState.SetString(Key + ".Output", output);
        SessionState.SetString(SaveOverride, Path.Combine(output, "city-save.json"));
        EditorSceneManager.OpenScene("Assets/Scenes/CityGreybox.unity");
        yield return new EnterPlayMode();
        var checks = Checks();
        try { while (checks.MoveNext()) yield return checks.Current; }
        finally { (checks as IDisposable)?.Dispose(); }
    }

    private static IEnumerator Checks()
    {
        Application.runInBackground = true;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        yield return null; yield return null; yield return null;
        var method = Type.GetType("UIInteractionVerification, Assembly-CSharp-Editor", true).GetMethod("RunChecks");
        var output = SessionState.GetString(Key + ".Output", "");
        var count = 0;
        var routine = (IEnumerator)method.Invoke(null, new object[] {
            SessionState.GetString(Key + ".Scenario", ""), output,
            new Action<bool, string>((value, message) => { count++; File.AppendAllText(Path.Combine(output, "checks.log"),
                (value ? "PASS " : "FAIL ") + message + "\n"); Assert.That(value, Is.True, message); }),
            new Action<string>(message => File.AppendAllText(Path.Combine(output, "checks.log"), message + "; assertions=" + count + "\n"))
        });
        try { while (routine.MoveNext()) yield return routine.Current; }
        finally { (routine as IDisposable)?.Dispose(); }
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTearDown]
    public IEnumerator RestoreEditor()
    {
        if (!SessionState.GetBool(Key, false)) yield break;
        foreach (var device in InputSystem.devices.Where(d => d.name.StartsWith("UI audit", StringComparison.Ordinal)).ToArray())
            InputSystem.RemoveDevice(device);
        InputSystem.settings.backgroundBehavior = (InputSettings.BackgroundBehavior)SessionState.GetInt(Key + ".Background", 0);
        InputSystem.settings.editorInputBehaviorInPlayMode = (InputSettings.EditorInputBehaviorInPlayMode)SessionState.GetInt(Key + ".EditorInput", 0);
        Application.runInBackground = SessionState.GetBool(Key + ".RunInBackground", false);
        if (EditorApplication.isPlaying) yield return new ExitPlayMode();
        SessionState.SetString(SaveOverride, SessionState.GetString(Key + ".Override", ""));
        var scene = SessionState.GetString(Key + ".Scene", "");
        if (scene.Length > 0) EditorSceneManager.OpenScene(scene);
        SessionState.EraseBool(Key);
    }
}
