using System;
using System.IO;
using System.Reflection;
using Interactables;
using Player;
using Player.Movement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class SolderingTrialVerification
{
    private static int checks;
    private static readonly System.Text.StringBuilder report = new System.Text.StringBuilder();
    private const string LogPath = "Logs/Codex-M2-verification.log";

    [MenuItem("Tools/Development Sandbox/Install M2 Once")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Stop Play Mode and save scene work first.");
        var scene = EditorSceneManager.OpenScene(DevelopmentSandboxBuilder.ScenePath);
        if (UnityEngine.Object.FindAnyObjectByType<SolderingTrial>() != null)
            throw new InvalidOperationException("M2 already installed; do not recreate authored work.");
        var go = new GameObject("M2 - Soldering trial - zone 06");
        go.transform.position = new Vector3(0, 0, -48);
        var trial = go.AddComponent<SolderingTrial>();
        var sandbox = UnityEngine.Object.FindAnyObjectByType<DevelopmentSandbox>();
        var sandboxData = new SerializedObject(sandbox);
        var serialized = new SerializedObject(trial);
        serialized.FindProperty("player").objectReferenceValue = sandboxData.FindProperty("player").objectReferenceValue;
        serialized.FindProperty("view").objectReferenceValue = Camera.main;
        serialized.FindProperty("deal").objectReferenceValue = UnityEngine.Object.FindAnyObjectByType<FirstDealView>();
        serialized.FindProperty("sandbox").objectReferenceValue = sandbox;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(scene);
    }

    [MenuItem("Tools/Development Sandbox/Verify M2 State")]
    public static void VerifyState()
    {
        Begin();
        try
        {
            var s = new SolderingTrialState();
            s.Tick(10, true, true);
            Check(s.Progress == 0 && !s.HasSolder && s.Heat == 0, "empty hands cannot prepare or solder");
            s.SelectTool(SolderingTrialState.Tool.Iron);
            s.Tick(1, true, true);
            Check(!s.Ready && !s.Working && s.Progress == 0, "cold iron cannot solder");
            s.Tick(4, false, true);
            s.Tick(4, true, true);
            Check(s.Ready && s.Progress == 0, "hot contact without solder cannot solder");
            s.SelectTool(SolderingTrialState.Tool.Solder);
            s.Tick(1, false, true);
            Check(!s.HasSolder, "solder requires contact");
            s.Tick(1, true, false);
            Check(!s.HasSolder, "paused solder cannot prepare");
            s.Tick(0.02f, true, true);
            Check(s.HasSolder && s.Progress == 0, "solder prepares without soldering");
            s.SelectTool(SolderingTrialState.Tool.Iron);
            s.Tick(1, false, true);
            Check(s.Progress == 0 && !s.Working, "no contact gives no progress");
            s.Tick(1, true, true);
            var partial = s.Progress;
            Check(Mathf.Approximately(partial, 0.4f) && s.Working, "one second actual hot prepared contact");
            s.Tick(5, false, true);
            Check(s.Progress == partial && !s.Working, "withdrawal preserves partial result and stops work");
            s.Tick(5, true, false);
            Check(s.Progress == partial && !s.Working, "pause/focus/leave stops progress");
            s.SelectTool(SolderingTrialState.Tool.Solder);
            s.Tick(5, true, true);
            Check(s.Progress == partial, "wrong tool cannot advance solder");
            s.SelectTool(SolderingTrialState.Tool.Iron);
            foreach (var invalid in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
            {
                s.Tick(invalid, true, true);
                Check(s.Progress == partial, "invalid interval rejected: " + invalid);
            }
            s.Tick(1.5f, true, true);
            Check(s.Complete && s.CompletionCount == 1 && !s.Working, "2.5 seconds completes once and stops work");
            for (var i = 0; i < 10; i++) s.Tick(1, true, true);
            s.Tick(5, false, true);
            Check(s.Complete && s.CompletionCount == 1, "result survives holding and withdrawal without repeated completion");
            s.Reset();
            Check(!s.HasSolder && !s.Ready && !s.Powered && !s.Complete && s.Progress == 0 &&
                s.HeldTool == SolderingTrialState.Tool.None && s.CompletionCount == 0, "reset restores full cold trial");
            s.SelectTool(SolderingTrialState.Tool.Solder);
            s.Tick(0.02f, true, true);
            s.SelectTool(SolderingTrialState.Tool.Iron);
            s.Tick(4, false, true);
            s.Tick(2.5f, true, true);
            Check(s.Complete && s.CompletionCount == 1, "entire trial can repeat after reset");
            Finish("STATE");
        }
        catch (Exception e) { Fail(e); throw; }
    }

    [MenuItem("Tools/Development Sandbox/Verify M2 In Play Mode")]
    public static void VerifyPlayMode()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter DevelopmentSandbox Play Mode first.");
        Begin();
        try
        {
            var trial = UnityEngine.Object.FindAnyObjectByType<SolderingTrial>();
            Check(trial != null && trial.gameObject.scene.path == DevelopmentSandboxBuilder.ScenePath, "trial belongs only to sandbox");
            var data = new SerializedObject(trial);
            var player = (PlayerController)data.FindProperty("player").objectReferenceValue;
            var deal = (FirstDealView)data.FindProperty("deal").objectReferenceValue;
            if (deal.MenuOpen) deal.CloseMenu();
            player.RestorePose(new Vector3(0, 0.05f, -49.6f), 0, 28);
            player.SetCursorLocked(true);
            trial.Interact();
            Check(trial.Active && !player.GetComponent<Player.PlayerInput>().enabled && !player.CanInteract,
                "bench takes exclusive input, no parallel E/R/carry");
            var tick = typeof(SolderingTrial).GetMethod("AdvanceTool", BindingFlags.NonPublic | BindingFlags.Instance);
            var aim = typeof(SolderingTrial).GetField("aim", BindingFlags.NonPublic | BindingFlags.Instance);
            Action<float, bool, bool> step = (dt, pressing, operating) => tick.Invoke(trial, new object[] { dt, pressing, operating });
            trial.ResetTrial();
            trial.State.SelectTool(SolderingTrialState.Tool.Iron);
            aim.SetValue(trial, Vector2.zero);
            step(0.3f, true, true);
            Check(trial.HasContact && trial.State.Progress == 0, "visible tip contact while cold does not solder");
            step(4, false, true);
            step(0.3f, true, true);
            Check(trial.State.Progress == 0 && !trial.SmokePlaying && !trial.SoundPlaying, "unprepared contact has no work feedback");
            Check(trial.Instruction == "Сначала нужен припой. Положите паяльник — E",
                "hot iron without solder explains the missing material and tool change");
            trial.State.SelectTool(SolderingTrialState.Tool.None);
            step(0, false, true);
            Check(trial.Instruction == "Возьмите припой слева — E", "empty hands are directed to solder spool");
            trial.State.SelectTool(SolderingTrialState.Tool.Solder);
            step(0, false, true);
            Check(trial.Instruction == "Удерживайте ЛКМ — уложить припой", "wire over joint explains the lowering button");
            step(0.3f, true, true);
            Check(trial.State.HasSolder, "wireEnd must physically reach joint");
            Check(trial.Instruction == "Припой уложен. Положите катушку — E", "placed solder confirms success and next tool change");
            trial.State.SelectTool(SolderingTrialState.Tool.None);
            step(0, false, true);
            Check(trial.Instruction == "Возьмите паяльник справа — E", "iron is requested only after material is placed");
            trial.State.SelectTool(SolderingTrialState.Tool.Iron);
            aim.SetValue(trial, new Vector2(0.2f, 0));
            step(1, true, true);
            Check(!trial.HasContact && trial.State.Progress == 0, "lowered tool away from joint cannot progress");
            aim.SetValue(trial, Vector2.zero);
            step(0.5f, false, true);
            Check(!trial.HasContact && trial.State.Progress == 0, "aim alone without lowering cannot progress");
            step(0.5f, true, true);
            Check(trial.HasContact && trial.State.Working && trial.SmokePlaying && trial.SoundPlaying,
                "actual prepared hot tip contact starts smoke and audio");
            var partial = trial.State.Progress;
            step(0.5f, false, true);
            Check(!trial.HasContact && !trial.State.Working && !trial.SmokePlaying && !trial.SoundPlaying &&
                trial.State.Progress == partial, "withdrawal stops feedback/progress immediately");
            step(0.2f, true, true);
            partial = trial.State.Progress;
            step(0, false, false);
            Check(!trial.HasContact && !trial.SmokePlaying && !trial.SoundPlaying && trial.State.Progress == partial,
                "focus loss/pause retracts tool and stops feedback");
            step(2.5f, true, true);
            Check(trial.State.Complete && trial.State.CompletionCount == 1 && !trial.SmokePlaying && !trial.SoundPlaying,
                "completion stops feedback once");
            Check(trial.Instruction == "Пайка готова — отпустите ЛКМ и отведите жало", "completion explains withdrawal");
            step(5, true, true);
            trial.Exit();
            Check(IronDocked(trial), "leaving returns visible iron to its dock immediately");
            Check(trial.State.Complete && trial.State.CompletionCount == 1 && player.GetComponent<Player.PlayerInput>().enabled &&
                player.CanInteract && deal.enabled, "leave preserves result and restores ordinary controls");
            trial.Interact();
            Check(trial.Active && trial.State.Complete, "return preserves result");
            trial.ResetTrial();
            Check(!trial.State.HasSolder && trial.State.Progress == 0 && !trial.State.Ready, "in-scene reset starts cold and unprepared");
            Check(IronDocked(trial), "reset returns visible iron to its dock immediately");
            trial.State.SelectTool(SolderingTrialState.Tool.Solder);
            step(0.3f, true, true);
            trial.State.SelectTool(SolderingTrialState.Tool.Iron);
            step(4, false, true);
            step(2.5f, true, true);
            Check(trial.State.Complete && trial.State.CompletionCount == 1, "second full in-scene trial completes");
            trial.Exit();
            trial.Interact();
            trial.ResetTrial();
            Finish("PLAY MODE (geometry/state adapter; mouse/keyboard observation separately)");
        }
        catch (Exception e) { Fail(e); throw; }
    }

    [MenuItem("Tools/Development Sandbox/Verify M2 Controls In Play Mode")]
    public static void VerifyControls()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isPaused)
            throw new InvalidOperationException("Run unpaused DevelopmentSandbox Play Mode first.");
        Begin();
        var trial = UnityEngine.Object.FindAnyObjectByType<SolderingTrial>();
        if (trial == null) throw new InvalidOperationException("Open DevelopmentSandbox first.");
        trial.Exit();
        trial.ResetTrial();
        var data = new SerializedObject(trial);
        var player = (PlayerController)data.FindProperty("player").objectReferenceValue;
        var deal = (FirstDealView)data.FindProperty("deal").objectReferenceValue;
        if (deal.MenuOpen) deal.CloseMenu();
        player.RestorePose(new Vector3(0, 0.05f, -49.6f), 0, 28);
        player.SetCursorLocked(true);
        // Virtual devices exercise the actual action maps without altering the user's native devices.
        var keyboard = InputSystem.AddDevice<Keyboard>();
        var mouse = InputSystem.AddDevice<Mouse>();
        keyboard.MakeCurrent();
        mouse.MakeCurrent();
        var background = Application.runInBackground;
        Application.runInBackground = true; // Only for this editor check; never changes PlayerSettings.
        var steps = new System.Collections.Generic.List<(Action action, double delay)>();
        Action<Key?> key = value => InputSystem.QueueStateEvent(keyboard,
            value.HasValue ? new KeyboardState(value.Value) : new KeyboardState());
        Action<float> guide = value => InputSystem.QueueDeltaStateEvent(mouse.delta, new Vector2(value, 0));
        Action<bool> click = down => InputSystem.QueueStateEvent(mouse, new MouseState { buttons = (ushort)(down ? 1 : 0) });
        float partial = 0;
        steps.Add((() => key(Key.E), 0.25));
        steps.Add((() => { Check(trial.Active, "world E ray enters bench"); key(null); }, 0.25));
        steps.Add((() => { guide(430); key(Key.E); }, 0.25));
        steps.Add((() =>
        {
            Check(trial.State.HeldTool == SolderingTrialState.Tool.Iron &&
                trial.Instruction == "Сначала нужен припой. Положите паяльник — E",
                "mouse + E picks iron and immediately explains solder/tool change");
            key(null);
        }, 0.25));
        steps.Add((() => key(Key.E), 0.25));
        steps.Add((() => { key(null); guide(-860); }, 0.25));
        steps.Add((() => key(Key.E), 0.25));
        steps.Add((() => { Check(trial.State.HeldTool == SolderingTrialState.Tool.Solder, "E returns iron and picks solder spool"); key(null); guide(430); }, 0.25));
        steps.Add((() => click(true), 0.45));
        steps.Add((() => { Check(trial.State.HasSolder && trial.State.Progress == 0, "LMB lowers wire and deposits solder"); click(false); key(Key.E); }, 0.25));
        steps.Add((() => { key(null); guide(430); }, 0.25));
        steps.Add((() => key(Key.E), 0.25));
        steps.Add((() => { Check(trial.State.HeldTool == SolderingTrialState.Tool.Iron, "E takes heated iron again"); key(null); guide(-430); }, 1.5));
        steps.Add((() => { Check(trial.State.Ready, "heat advances with real frame time"); click(true); }, 0.8));
        steps.Add((() =>
        {
            Check(trial.HasContact && trial.State.Working && trial.State.Progress > 0 && trial.State.Progress < 1 &&
                trial.SmokePlaying && trial.SoundPlaying, "held LMB makes actual contact with smoke and AudioSource playing");
            partial = trial.State.Progress;
            click(false);
        }, 0.25));
        steps.Add((() =>
        {
            Check(trial.State.Progress == partial && !trial.HasContact && !trial.State.Working &&
                !trial.SmokePlaying && !trial.SoundPlaying, "LMB release preserves partial and stops work/feedback");
            click(true);
        }, 3));
        steps.Add((() =>
        {
            Check(trial.State.Complete && trial.State.CompletionCount == 1 && !trial.SmokePlaying && !trial.SoundPlaying,
                "continued contact completes once and stops feedback");
            click(false);
            key(Key.R);
        }, 0.25));
        steps.Add((() =>
        {
            Check(!trial.State.HasSolder && trial.State.Heat == 0 && trial.State.Progress == 0 && IronDocked(trial) &&
                trial.State.HeldTool == SolderingTrialState.Tool.None, "R resets all trial state");
            key(null);
        }, 0.25));
        steps.Add((() => key(Key.Escape), 0.25));
        steps.Add((() =>
        {
            Check(!trial.Active && player.CanInteract && IronDocked(trial) && player.GetComponent<Player.PlayerInput>().enabled &&
                !deal.MenuOpen, "Escape restores ordinary controls without opening another menu");
            key(null);
        }, 0.25));
        steps.Add((() => key(Key.E), 0.25));
        steps.Add((() => { Check(trial.Active, "world E can enter again after Escape"); key(null); }, 0.25));
        var index = 0;
        var frame = Time.frameCount;
        var next = EditorApplication.timeSinceStartup;
        var deadline = next + 45;
        EditorApplication.CallbackFunction runner = null;
        Action cleanup = () =>
        {
            EditorApplication.update -= runner;
            InputSystem.RemoveDevice(keyboard);
            InputSystem.RemoveDevice(mouse);
            Application.runInBackground = background;
            if (trial != null) trial.Exit();
        };
        runner = () =>
        {
            try
            {
                if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup > deadline)
                    throw new InvalidOperationException("Input check stopped or timed out; inspect focus/Play Mode.");
                // Wait for actual gameplay frames to process the previous queued input.
                if (Time.frameCount < frame + 3 || EditorApplication.timeSinceStartup < next) return;
                if (index == steps.Count) { cleanup(); Finish("INPUT SYSTEM"); return; }
                var step = steps[index++];
                step.action();
                frame = Time.frameCount;
                next = EditorApplication.timeSinceStartup + step.delay;
            }
            catch (Exception error) { cleanup(); Fail(error); Debug.LogException(error); }
        };
        EditorApplication.update += runner;
    }

    private static bool IronDocked(SolderingTrial trial) =>
        Vector3.Distance(trial.TipPosition, trial.transform.TransformPoint(new Vector3(0.56f, 1.115f, 0))) < 0.001f;

    private static void Begin() { checks = 0; report.Clear(); }
    private static void Check(bool condition, string message)
    {
        report.AppendLine((condition ? "PASS " : "FAIL ") + message);
        if (!condition) throw new InvalidOperationException(message);
        checks++;
    }
    private static void Finish(string mode)
    {
        report.AppendLine("M2 " + mode + ": PASS " + checks + " assertions; Unity " + Application.unityVersion);
        Directory.CreateDirectory("Logs");
        File.AppendAllText(LogPath, report.ToString());
        Debug.Log("M2 " + mode + ": PASS " + checks + "; full log: " + LogPath);
    }
    private static void Fail(Exception error)
    {
        Directory.CreateDirectory("Logs");
        File.AppendAllText(LogPath, report + "FAIL " + error + Environment.NewLine);
    }
}
