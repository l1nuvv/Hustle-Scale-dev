using System;
using System.IO;
using System.Linq;
using Interactables;
using Inventory;
using Player.Movement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

[InitializeOnLoad]
public static class T03Verification
{
    private const string LogPath = "Logs/Codex-T03-verification.log";
    private const string RunningKey = "T03.running";
    private static WorkshopView view;
    private static Keyboard keyboard;
    private static int phase;
    private static int frame;
    private static float started;
    private static bool failed;
    private static bool completed;
    private static bool previousRunInBackground;
    private static double runStarted;
    private static InputSettings previousInputSettings;
    private static InputSettings testInputSettings;

    static T03Verification() => EditorApplication.playModeStateChanged += PlayState;

    [MenuItem("Tools/T03/Verify Workshop")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Save your scene first.");
        File.WriteAllText(LogPath, "T03 Unity " + Application.unityVersion + "\n");
        VerifyDomain();
        EditorSceneManager.OpenScene(WorkshopSceneBuilder.ScenePath);
        SessionState.SetBool(RunningKey, true);
        EditorApplication.isPlaying = true;
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
        File.AppendAllText(LogPath, "PASS " + message + "\n");
    }

    private static void VerifyDomain()
    {
        var session = new WorkshopSession();
        var ids = session.Units.Select(u => u.InstanceId).ToArray();
        Check(ids.Distinct().Count() == 3 && ids.All(id => !string.IsNullOrEmpty(id)), "three unique identities");
        Check(session.Units.Count(u => u.Condition == FlashlightCondition.Working) == 2, "two working, one battery fault");
        Check(session.Units.All(u => u.KnownCondition == "Not tested"), "condition hidden before testing");
        Check(!session.Test(out _) && !session.Repair(out _), "empty bench rejects test and repair");
        Check(!session.ToggleUnit(-1, out _) && !session.ToggleUnit(3, out _), "invalid identity lookup rejected");
        Check(session.ToggleCrate(out _) && !session.ToggleUnit(0, out _) && !session.ToggleBench(out _), "carried crate locks individual transfer");
        session.ToggleCrate(out _);
        for (var i = 0; i < 3; i++)
        {
            Check(session.ToggleUnit(i, out _) && !session.ToggleUnit((i + 1) % 3, out _), "single hand capacity " + i);
            Check(!session.ToggleCrate(out _), "cannot carry crate and loose lamp " + i);
            Check(session.ToggleBench(out _) && session.OnBench.InstanceId == ids[i], "bench keeps identity " + i);
            Check(!session.ToggleUnit(i, out _), "bench lamp cannot duplicate into hand " + i);
            Check(session.Test(out _) && session.OnBench.LastTestPassed == (i != 1), "diagnosis matches actual condition " + i);
            Check(session.Test(out _) && session.OnBench.IsTested, "repeat test is stable " + i);
            session.ToggleBench(out _);
            session.ToggleUnit(i, out _);
            Check(session.Units[i].IsTested, "transport preserves diagnosis " + i);
        }
        session.ToggleUnit(1, out _);
        session.ToggleBench(out _);
        Check(!session.Repair(out _) && session.RepairSeconds == 0 && session.Balance == 120, "missing battery rejects atomically");
        Check(session.BuyBattery(out _) && session.Balance == 70 && session.SpareBatteries == 1, "battery purchase costs exactly 50 and stocks consumable");
        Check(session.OnBench.Condition == FlashlightCondition.BatteryFault, "buying battery alone does not repair");
        Check(session.Repair(out _) && session.SpareBatteries == 0 && session.Balance == 70 && session.RepairSeconds == 240, "repair consumes once and adds exactly four minutes");
        Check(!session.OnBench.IsTested && !session.OnBench.LastTestPassed && session.OnBench.KnownCondition == "Not tested", "repair invalidates old test");
        Check(!session.Repair(out _) && session.RepairSeconds == 240 && session.SpareBatteries == 0, "repeated repair changes no resources");
        Check(session.Test(out _) && session.OnBench.LastTestPassed, "repaired lamp passes a new test");
        Check(!session.Repair(out _) && session.RepairSeconds == 240, "working lamp repair rejected");
        Check(session.BuyBattery(out _) && session.Balance == 20 && !session.BuyBattery(out _) && session.Balance == 20 && session.SpareBatteries == 1, "insufficient money leaves wallet and stock intact");
        Check(session.Units.Select(u => u.InstanceId).SequenceEqual(ids) && session.Units.Count == 3, "all identities conserved after transfers and repair");
        var poor = new WorkshopSession(49);
        Check(!poor.BuyBattery(out _) && poor.Balance == 49 && poor.SpareBatteries == 0, "purchase lower boundary");
        var exact = new WorkshopSession(50);
        Check(exact.BuyBattery(out _) && exact.Balance == 0, "purchase exact balance boundary");
    }

    private static void PlayState(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(RunningKey, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            failed = false;
            completed = false;
            phase = 0;
            frame = -1;
            started = Time.time;
            previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            previousInputSettings = InputSystem.settings;
            // InputManager destroys its transient default settings when replacing them.
            // Preserve a copy in that case so teardown can restore the original values.
            if (previousInputSettings.hideFlags == HideFlags.HideAndDontSave)
                previousInputSettings = UnityEngine.Object.Instantiate(previousInputSettings);
            testInputSettings = UnityEngine.Object.Instantiate(previousInputSettings);
            testInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            testInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = testInputSettings;
            runStarted = EditorApplication.timeSinceStartup;
            Application.logMessageReceived += Error;
            EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            EditorApplication.update -= Tick;
            Application.runInBackground = previousRunInBackground;
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            InputSystem.settings = previousInputSettings;
            if (testInputSettings != null) UnityEngine.Object.DestroyImmediate(testInputSettings);
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            Application.logMessageReceived -= Error;
            SessionState.SetBool(RunningKey, false);
            var pass = completed && !failed;
            File.AppendAllText(LogPath, pass ? "T03 RESULT PASS\n" : "T03 RESULT FAIL\n");
            Debug.Log(pass ? "T03 RESULT PASS" : "T03 RESULT FAIL");
        }
    }

    private static void Error(string message, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        failed = true;
        File.AppendAllText(LogPath, "ERROR " + message + "\n" + stack + "\n");
    }

    private static void Next() { phase++; started = Time.time; }

    private static Light Indicator(int index) => GameObject.Find("Lamp " + (index + 1)).GetComponentInChildren<Light>();

    private static void Tick()
    {
        EditorApplication.QueuePlayerLoopUpdate();
        if (EditorApplication.timeSinceStartup - runStarted > 45)
        {
            failed = true;
            File.AppendAllText(LogPath, "FAIL Play Mode verification timed out\n");
            EditorApplication.isPlaying = false;
            return;
        }
        if (!EditorApplication.isPlaying || frame == Time.frameCount || Time.time - started < 0.9f) return;
        frame = Time.frameCount;
        try
        {
            switch (phase)
            {
                case 0:
                    view = UnityEngine.Object.FindAnyObjectByType<WorkshopView>();
                    Check(view != null && view.Session.Units.Count == 3, "workshop composition initializes in Play Mode");
                    var player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
                    player.GetComponent<Player.PlayerInput>().MouseInputEnabled = false;
                    player.SetCursorLocked(true);
                    GameObject.Find("Carry / dock crate").transform.position = new Vector3(0, 1.65f, -4);
                    Physics.SyncTransforms();
                    keyboard = InputSystem.AddDevice<Keyboard>("T03Keyboard");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
                    Next(); break;
                case 1:
                    File.AppendAllText(LogPath, "INFO keyboard enabled=" + keyboard.enabled + " E=" + keyboard.eKey.isPressed +
                        " focus=" + Application.isFocused + " prompt=" + UnityEngine.Object.FindAnyObjectByType<Player.PlayerRaycast>().FocusLabel + "\n");
                    Check(view.Session.IsCrateCarried, "held E routes once through world interaction contract");
                    Check(GameObject.Find("Carry crate").transform.parent == Camera.main.transform, "carried crate attached to camera");
                    Check(view.Session.Units.All(u => u.Location == FlashlightLocation.Crate), "carrying crate conserves stored lamps");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    Next(); break;
                case 2:
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
                    Next(); break;
                case 3:
                    Check(!view.Session.IsCrateCarried, "second E docks crate");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    view.Perform(WorkshopActionKind.Unit, 0);
                    view.Perform(WorkshopActionKind.Bench, 0);
                    view.Perform(WorkshopActionKind.Test, 0);
                    Check(Indicator(0).enabled, "working test produces steady world light");
                    view.Perform(WorkshopActionKind.Bench, 0);
                    view.Perform(WorkshopActionKind.Unit, 0);
                    view.Perform(WorkshopActionKind.Unit, 1);
                    view.Perform(WorkshopActionKind.Bench, 0);
                    view.Perform(WorkshopActionKind.Test, 0);
                    Check(Indicator(1).enabled && !view.Session.OnBench.LastTestPassed, "faulty test starts visible blink");
                    Next(); break;
                case 4:
                    Check(!Indicator(1).enabled, "faulty test ends with lamp off");
                    view.Perform(WorkshopActionKind.BuyBattery, 0);
                    view.Perform(WorkshopActionKind.Repair, 0);
                    Check(!Indicator(1).enabled && !view.Session.OnBench.IsTested, "repair does not show untested lamp as passed");
                    view.Perform(WorkshopActionKind.Test, 0);
                    Check(Indicator(1).enabled && view.Session.OnBench.LastTestPassed, "repaired lamp lights after retest");
                    Check(GameObject.Find("Workshop state").GetComponent<UnityEngine.UI.Text>().text.Contains("04:00"), "HUD displays repair time from model");
                    Check(view.Session.Units.Select(u => u.InstanceId).Distinct().Count() == 3, "scene retains all three identities");
                    completed = true;
                    EditorApplication.isPlaying = false;
                    break;
            }
        }
        catch (Exception exception)
        {
            failed = true;
            File.AppendAllText(LogPath, "FAIL " + exception + "\n");
            EditorApplication.isPlaying = false;
        }
    }
}
