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
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;

public sealed class PhysicsInteractionTests
{
    private const string Key = "HustleScale.Physics.NUnit";
    private const string SaveOverride = "HustleScale.S2SaveOverride";
    private const string KeyboardName = "Physics NUnit keyboard";

    [UnityTest]
    public IEnumerator CityPhysicsAndInteractionInPlayMode()
    {
        Assert.That(EditorApplication.isPlayingOrWillChangePlaymode, Is.False);
        Assert.That(SceneManager.sceneCount == 1 && !SceneManager.GetActiveScene().isDirty, Is.True,
            "Save the single open scene before this integration test.");
        SessionState.SetString(Key + ".Scene", SceneManager.GetActiveScene().path);
        SessionState.SetString(Key + ".Override", SessionState.GetString(SaveOverride, ""));
        SessionState.SetInt(Key + ".Background", (int)InputSystem.settings.backgroundBehavior);
        SessionState.SetInt(Key + ".EditorInput", (int)InputSystem.settings.editorInputBehaviorInPlayMode);
        SessionState.SetBool(Key + ".RunInBackground", Application.runInBackground);
        SessionState.SetBool(Key, true);
        var output = Path.GetFullPath("Temp/PhysicsAudit/NUnit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        SessionState.SetString(Key + ".Output", output);
        SessionState.SetString(SaveOverride, Path.Combine(output, "city-save.json"));
        EditorSceneManager.OpenScene("Assets/Scenes/CityGreybox.unity");
        yield return new EnterPlayMode();
        // Create assertion/log closures only after the domain reload. UTF restores
        // iterator state across EnterPlayMode, but cannot restore a C# display class.
        var checks = RunChecks();
        try { while (checks.MoveNext()) yield return checks.Current; }
        finally { (checks as IDisposable)?.Dispose(); }
    }

    private static IEnumerator RunChecks()
    {
        Application.runInBackground = true;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        yield return null; yield return null; yield return null;
        var keyboard = InputSystem.AddDevice<Keyboard>(KeyboardName);
        // asmdef tests cannot reference predefined Assembly-CSharp. Reflect only this
        // public shared entry point, keeping the runtime assembly boundaries unchanged.
        var type = Type.GetType("Player.CityPlayerVerification, Assembly-CSharp", true);
        var view = SceneManager.GetActiveScene().GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Component>())
            .Single(c => c != null && c.GetType().FullName == "Interactables.CityTradeView");
        var logPath = Path.Combine(SessionState.GetString(Key + ".Output", ""), "checks.log");
        var method = type.GetMethod("PhysicsChecks", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        Assert.That(method, Is.Not.Null, "Shared physics entry point in " + type.Assembly.FullName);
        File.AppendAllText(logPath, "UTF invokes " + method + " on " + view.name + "\n");
        var routine = (IEnumerator)method.Invoke(null, new object[] {
            view, keyboard, new Action<bool, string>((value, message) => Assert.That(value, Is.True, message)),
            new Action<string>(message => File.AppendAllText(logPath, message + "\n"))
        });
        Assert.That(routine, Is.Not.Null);
        try { while (routine.MoveNext()) yield return routine.Current; }
        finally { (routine as IDisposable)?.Dispose(); }
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTearDown]
    public IEnumerator RestoreEditor()
    {
        if (!SessionState.GetBool(Key, false)) yield break;
        foreach (var device in InputSystem.devices.Where(d => d.name == KeyboardName).ToArray()) InputSystem.RemoveDevice(device);
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
