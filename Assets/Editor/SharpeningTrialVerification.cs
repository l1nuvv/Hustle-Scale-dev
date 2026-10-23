using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Interactables;
using Player;
using Player.Movement;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class SharpeningTrialVerification
{
    private const string Log = "Logs/Codex-M6-verification.log";
    private static int checks;
    private static void Check(bool condition, string label)
    {
        File.AppendAllText(Log, (condition ? "PASS " : "FAIL ") + label + "\n");
        if (!condition) throw new InvalidOperationException(label);
        checks++;
    }
    private static float Sum(SharpeningTrialState state) => Enumerable.Range(0, SharpeningTrialState.Sections).Sum(state.Coverage);
    private static Vector3 Point(float x, float z = 0.012f) => new Vector3(x, 1.27f, z);
    private static void Pass(SharpeningTrialState state, float z, float angle, bool held = true)
    {
        for (var i = 0; i < 160; i++)
        {
            var x = -0.20f + i * .0025f;
            state.Sweep(Point(x, z), Point(x + .0025f, z), angle, .025f, held, 20, 5, .04f, .12f);
        }
    }

    [MenuItem("Tools/Development Sandbox/Verify M6 State")]
    public static void VerifyState()
    {
        checks = 0;
        Directory.CreateDirectory("Logs");
        File.WriteAllText(Log, "M6 Unity " + Application.unityVersion + " / state checks\n");
        var state = new SharpeningTrialState();
        Pass(state, .012f, 20);
        Check(Sum(state) == 0, "unplaced / motor off cannot process");
        state.Place(); state.ToggleMotor();
        Pass(state, -.10f, 20);
        Check(Sum(state) == 0 && !state.Contact, "manual pass away from wheel gives no progress");
        Pass(state, .012f, 35);
        Check(Sum(state) == 0, "wrong angle gives no progress");
        Pass(state, .012f, 20, false);
        Check(Sum(state) == 0, "released knife gives no progress");
        for (var i = 0; i < 100; i++) state.Sweep(Point(0), Point(0), 20, .1f, true, 20, 5, .04f, .12f);
        Check(Sum(state) == 0 && state.Contact && !state.Cutting, "holding still in real contact cannot process");
        state.Sweep(Point(-.2f), Point(.2f), 20, .1f, true, 20, 5, .04f, .12f);
        Check(Sum(state) == 0, "teleport cannot complete a pass");
        state.Sweep(Point(0), Point(.01f), 20, .1f, true, 20, 5, .04f, .12f);
        Check(Sum(state) > 0 && !state.Complete && state.Cutting, "correct short pass processes only touched sections");
        for (var i = 0; i < 20; i++) state.Sweep(Point(0), Point(.01f), 20, .1f, true, 20, 5, .04f, .12f);
        var beforeRepeat = Sum(state);
        state.Sweep(Point(0), Point(.01f), 20, .1f, true, 20, 5, .04f, .12f);
        Check(Sum(state) == beforeRepeat && !state.Complete && !state.Cutting,
            "revisiting finished sections does not report cutting while other sections still need work");
        Check(state.Contact && !state.ContactNeedsWork && state.FinishedSections > 0 && state.Progress < 1,
            "contact with finished spot is distinguishable from remaining edge work");
        Pass(state, .012f, 20); Pass(state, .012f, 20);
        Check(state.Complete, "whole-edge passes complete all sections");
        state.StopMotor();
        Check(!state.Running && !state.Contact && !state.Cutting && state.Complete, "stop clears contact and preserves result");
        state.Reset();
        Check(!state.Complete && !state.Placed && !state.Running && Sum(state) == 0, "reset clears trial");
        Check(state.Progress == 0 && state.FinishedSections == 0 && !state.ContactNeedsWork,
            "reset clears section feedback");
        state.Place(); state.ToggleMotor(); Pass(state, .012f, 20); Pass(state, .012f, 20);
        Check(state.Complete, "second complete operation after reset");
        File.AppendAllText(Log, "STATE PASS " + checks + " assertions\n");
        Debug.Log("M6 STATE PASS " + checks);
    }

    [MenuItem("Tools/Development Sandbox/Verify M6 Controls In Play Mode")]
    public static void VerifyControls()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isPaused) throw new InvalidOperationException("Run sandbox unpaused first.");
        var trial = UnityEngine.Object.FindAnyObjectByType<SharpeningTrial>();
        var player = (PlayerController)new SerializedObject(trial).FindProperty("player").objectReferenceValue;
        var deal = UnityEngine.Object.FindAnyObjectByType<FirstDealView>();
        if (deal.MenuOpen) deal.CloseMenu();
        trial.Exit(); trial.ResetTrial();
        player.RestorePose(trial.transform.TransformPoint(new Vector3(0, .05f, -1.25f)), trial.transform.eulerAngles.y, 22);
        player.SetCursorLocked(true);
        var fov = Camera.main.fieldOfView;
        var keyboard = InputSystem.AddDevice<Keyboard>();
        var mouse = InputSystem.AddDevice<Mouse>();
        keyboard.MakeCurrent(); mouse.MakeCurrent();
        var background = Application.runInBackground;
        Application.runInBackground = true;
        Action<Key?> key = k => InputSystem.QueueStateEvent(keyboard, k.HasValue ? new KeyboardState(k.Value) : new KeyboardState());
        Action<bool, Vector2> pointer = (down, delta) => InputSystem.QueueStateEvent(mouse, new MouseState { buttons = (ushort)(down ? 1 : 0), delta = delta });
        var steps = new List<Action>();
        steps.Add(() => key(Key.E));
        steps.Add(() => { Check(trial.Active, "world ray E enters M6"); Check(!player.enabled && !deal.enabled, "player and deal suspended"); key(null); });
        steps.Add(() => pointer(true, Vector2.zero));
        steps.Add(() => { Check(trial.State.Placed, "LMB places knife"); pointer(false, Vector2.zero); });
        steps.Add(() => key(Key.Space));
        steps.Add(() => { Check(trial.State.Running, "space starts motor"); key(null); });
        steps.Add(() =>
        {
            var data = new SerializedObject(trial);
            var sections = data.FindProperty("edgeSections");
            var pivot = (Transform)data.FindProperty("knife").objectReferenceValue;
            for (var i = 0; i < sections.arraySize; i++)
            {
                var renderer = (Renderer)sections.GetArrayElementAtIndex(i).objectReferenceValue;
                var expected = pivot.localPosition + pivot.localRotation * (Vector3.right * ((i + .5f) / sections.arraySize - .5f) * SharpeningTrialState.EdgeLength);
                Check(Vector3.Distance(trial.transform.InverseTransformPoint(renderer.bounds.center), expected) < .006f,
                    "authored visible edge matches physical section " + i);
            }
        });
        steps.Add(() => pointer(true, new Vector2(8, 0)));
        steps.Add(() => key(Key.Q));
        steps.Add(() => { Check((float)Get(trial, "angle") < 35, "Q changes held knife angle through Input System"); key(null); });
        steps.Add(() => { Check(Sum(trial.State) == 0, "real held input away gives no progress"); pointer(false, Vector2.zero); });
        // Inject the physical pose only; actual InputAction processing still owns cutting.
        steps.Add(() => { Set(trial, "origin", Point(-.10f)); Set(trial, "angle", 35f); pointer(true, new Vector2(10, 0)); });
        steps.Add(() =>
        {
            Check(trial.State.Contact && Sum(trial.State) == 0, "runtime wrong angle contact, no processing");
            Check(trial.GetComponentInChildren<ParticleSystem>().isEmitting && trial.GetComponent<AudioSource>().isPlaying,
                "physical contact emits sparks and abrasive sound");
            Set(trial, "angle", 20f); pointer(true, new Vector2(10, 0));
        });
        steps.Add(() =>
        {
            Check(Sum(trial.State) > 0, "real mouse sweep at correct contact processes");
            // Reproduce mouse-event frames followed by zero-delta frames without granting extra work.
            Set(trial, "lastCutTime", Time.unscaledTime);
            var before = Sum(trial.State);
            trial.State.Sweep(trial.EdgeOrigin, trial.EdgeOrigin, 20, .016f, true, 20, 5, .04f, .12f);
            trial.SendMessage("RefreshInstruction");
            Check(!trial.State.Cutting && Sum(trial.State) == before && trial.Instruction.Contains("Обрабатываем"),
                "zero-delta input frame preserves recent-cut wording without creating progress");
            Set(trial, "lastCutTime", Time.unscaledTime - 1);
            trial.SendMessage("RefreshInstruction");
            Check(trial.Instruction.Contains("ведите"), "stationary contact eventually asks to move again");
            var renderer = (Renderer)new SerializedObject(trial).FindProperty("edgeSections").GetArrayElementAtIndex(0).objectReferenceValue;
            var properties = new MaterialPropertyBlock(); renderer.GetPropertyBlock(properties);
            Check(properties.GetColor("_EmissionColor").maxColorComponent > 0 && renderer.sharedMaterial.IsKeywordEnabled("_EMISSION"),
                "unfinished authored edge has luminous coating");
            pointer(false, Vector2.zero);
        });
        steps.Add(() => Time.timeScale = 0);
        steps.Add(() => { Check(!trial.State.Contact && !trial.EffectsPlaying, "pause clears contact and effects"); Time.timeScale = 1; });
        steps.Add(() => { Check(!trial.State.Contact, "release clears contact"); Check(!trial.GetComponentInChildren<ParticleSystem>().isEmitting, "release stops sparks"); key(Key.F); });
        steps.Add(() => { Check(((string)Get(trial, "result")).Contains("отведите"), "inspection refuses knife at wheel"); key(null); Set(trial, "origin", Point(0, -.1f)); });
        steps.Add(() => key(Key.F));
        steps.Add(() => { Check(((string)Get(trial, "result")).Contains("не полностью"), "inspection reports unfinished edge"); key(null); });
        steps.Add(() => { Pass(trial.State, .012f, 20); Pass(trial.State, .012f, 20); key(Key.F); });
        steps.Add(() => { Check(trial.State.Complete && ((string)Get(trial, "result")).Contains("Нож готов"), "finished inspection after real coverage"); key(null); });
        steps.Add(() => key(Key.R));
        steps.Add(() => { Check(!trial.State.Placed && !trial.State.Running && !trial.EffectsPlaying, "R resets state and effects"); key(null); });
        steps.Add(() => InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 2 }));
        steps.Add(() => { Check(Camera.main.fieldOfView < fov * .7f, "RMB magnifies work FOV"); pointer(false, Vector2.zero); });
        steps.Add(() => Check(Mathf.Abs(Camera.main.fieldOfView - fov * .8f) < .01f, "release RMB restores work FOV"));
        steps.Add(() => key(Key.Escape));
        steps.Add(() => { Check(!trial.Active && player.enabled && deal.enabled && player.CanInteract, "Escape restores player and deal"); Check(Mathf.Abs(Camera.main.fieldOfView - fov) < .01f, "exit restores FOV"); Check(!trial.EffectsPlaying && !trial.State.Running, "exit clears motor and effects"); Check(Cursor.lockState == CursorLockMode.Locked && !Cursor.visible, "exit restores cursor lock"); key(null); });
        steps.Add(() => { trial.Interact(); Check(trial.Active, "re-entry works"); trial.State.Place(); trial.State.ToggleMotor(); trial.SendMessage("OnApplicationFocus", false); Check(!trial.State.Running && !trial.EffectsPlaying, "focus loss stops motor and effects"); trial.SendMessage("OnApplicationFocus", true); trial.enabled = false; Check(!trial.Active && player.enabled && !trial.EffectsPlaying, "disable exits safely"); trial.enabled = true; });
        var index = 0;
        var frame = Time.frameCount;
        var deadline = EditorApplication.timeSinceStartup + 50;
        EditorApplication.CallbackFunction runner = null;
        Action cleanup = () =>
        {
            EditorApplication.update -= runner;
            trial.Exit(); InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse);
            Application.runInBackground = background;
            Time.timeScale = 1;
        };
        runner = () =>
        {
            try
            {
                if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup > deadline) throw new InvalidOperationException("M6 control check interrupted or timed out");
                if (Time.frameCount < frame + 4) return;
                if (index == steps.Count)
                {
                    cleanup(); File.AppendAllText(Log, "PLAY MODE PASS " + checks + " assertions\n");
                    Debug.Log("M6 PLAY MODE PASS " + checks); return;
                }
                steps[index++](); frame = Time.frameCount;
            }
            catch (Exception error) { cleanup(); File.AppendAllText(Log, "FAIL " + error + "\n"); Debug.LogException(error); }
        };
        EditorApplication.update += runner;
    }
    private static object Get(object instance, string field) => instance.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(instance);
    private static void Set(object instance, string field, object value) => instance.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(instance, value);
}
