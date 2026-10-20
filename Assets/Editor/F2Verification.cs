using System;
using System.Collections;
using System.IO;
using System.Linq;
using Interactables;
using Inventory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Exercises real rendered frames, session events and interruption without saving the scene.
[InitializeOnLoad]
public static class F2Verification
{
    private const string Key = "F2.verify";
    private const string LogPath = "Logs/Codex-F2-verification.log";
    private static IEnumerator steps;
    private static int checks, frame;
    private static double next, timeout;
    private static bool previousBackground, failed, completed;
    private static WorkshopView workshop;
    private static FirstDealView deal;
    private static WorkshopSession session;
    private static bool expectedPause;

    static F2Verification() => EditorApplication.playModeStateChanged += StateChanged;

    [MenuItem("Tools/F2/Verify Motion and Trade")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Stop Play Mode and save the scene first.");
        EditorSceneManager.OpenScene(FirstDealTradeBuilder.ScenePath);
        Directory.CreateDirectory("Logs");
        File.WriteAllText(LogPath, "F2 Unity " + Application.unityVersion + " UTC " + DateTime.UtcNow.ToString("O") + "\n");
        SessionState.SetBool(Key, true);
        EditorApplication.isPlaying = true;
    }

    private static void StateChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            checks = 0; frame = -1; failed = completed = false;
            previousBackground = Application.runInBackground;
            Application.runInBackground = true;
            session = null;
            expectedPause = false;
            next = EditorApplication.timeSinceStartup + 1;
            timeout = next + 60;
            steps = Scenario();
            EditorApplication.update += Tick;
            Application.logMessageReceived += Error;
        }
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= Error;
            Application.runInBackground = previousBackground;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key, false);
            var result = "F2 RESULT " + (completed && !failed ? "PASS" : "FAIL") + ": " + checks + " assertions";
            File.AppendAllText(LogPath, result + "\n");
            Debug.Log(result);
        }
    }

    private static void Error(string message, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        failed = true;
        File.AppendAllText(LogPath, "ERROR " + message + "\n" + stack + "\n");
    }

    private static void Tick()
    {
        EditorApplication.QueuePlayerLoopUpdate();
        if (EditorApplication.timeSinceStartup >= timeout)
        {
            failed = true;
            File.AppendAllText(LogPath, "FAIL frame timeout\n");
            EditorApplication.isPlaying = false;
            return;
        }
        if (EditorApplication.timeSinceStartup < next || Time.frameCount == frame) return;
        frame = Time.frameCount;
        next = EditorApplication.timeSinceStartup + 0.06;
        try
        {
            // The desktop can lose game focus while tools render or inspect it.
            // Preserve deliberate pause probes; resume incidental focus-loss menus.
            if (session != null && session.IsPaused && !expectedPause && !session.ShiftEnded)
            {
                File.AppendAllText(LogPath, "INFO resume desktop focus-loss pause\n");
                deal.SendMessage("OnApplicationFocus", true);
                deal.CloseMenu();
            }
            if (!steps.MoveNext()) { completed = true; EditorApplication.isPlaying = false; }
        }
        catch (Exception e)
        {
            failed = true;
            File.AppendAllText(LogPath, "FAIL " + e + "\n");
            EditorApplication.isPlaying = false;
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        checks++;
        File.AppendAllText(LogPath, "PASS " + message + "\n");
    }

    private static T Field<T>(string name) => (T)typeof(WorkshopView).GetField(name,
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(workshop);

    private static IEnumerator Scenario()
    {
        workshop = UnityEngine.Object.FindAnyObjectByType<WorkshopView>();
        deal = UnityEngine.Object.FindAnyObjectByType<FirstDealView>();
        session = workshop.Session;
        var lamps = Field<Transform[]>("lamps");
        var crate = Field<Transform>("crate");
        var bench = Field<Transform>("bench");
        var lights = Field<Light[]>("indicators");
        var label = Field<UnityEngine.UI.Text>("feedbackLabel");
        deal.SendMessage("OnApplicationFocus", true);
        deal.CloseMenu();
        deal.Purchase(false);
        deal.Purchase(true);
        Check(session.Balance == 70, "original lot and battery costs");
        Check(session.ToggleCrate(out _), "carry crate");
        var crateStart = crate.localPosition;
        yield return null;
        Check(Vector3.Distance(crateStart, crate.localPosition) > 0.0001f, "crate moves across frames");
        expectedPause = true;
        deal.OpenMenu(false, true);
        var pausedCrate = crate.localPosition;
        yield return null;
        yield return null;
        Check(crate.localPosition == pausedCrate, "menu freezes transfer");
        expectedPause = false;
        deal.CloseMenu();
        for (var i = 0; i < 9; i++) yield return null;
        Check(Vector3.Distance(crate.localPosition, new Vector3(0, -1, 1.2f)) < 0.001f, "crate settles in hands");
        Check(session.ToggleCrate(out var dockMessage), "dock crate: " + dockMessage);
        for (var i = 0; i < 9; i++) yield return null;
        Check(crate.localPosition == Vector3.zero, "crate settles on dock");
        for (var index = 0; index < 3; index++)
        {
            Check(session.ToggleUnit(index, out _), "take lamp " + index);
            for (var i = 0; i < 9; i++) yield return null;
            Check(Vector3.Distance(lamps[index].localPosition, new Vector3(0.35f, -0.4f, 0.65f)) < 0.001f,
                "lamp settles in hands " + index);
            workshop.Perform(WorkshopActionKind.Bench, 0);
            Check(session.OnBench == session.Units[index] && lamps[index].parent == bench, "bench ownership immediate " + index);
            var placementStart = lamps[index].localPosition;
            yield return null;
            Check(Vector3.Distance(lamps[index].localPosition, placementStart) > 0.0001f, "placement animates " + index);
            // Rapid interaction replaces a transfer and still settles at the authoritative destination.
            if (index == 0)
            {
                Check(session.ToggleBench(out _) && session.ToggleBench(out _), "rapid retrieve and replace");
            }
            for (var i = 0; i < 9; i++) yield return null;
            Check(lamps[index].localPosition == Vector3.zero, "placement settles " + index);
            workshop.Perform(WorkshopActionKind.Test, 0);
            yield return null;
            Check(lamps[index].localPosition.y > 0.001f, "test visibly lifts lamp " + index);
            if (index == 1)
            {
                Check(!session.OnBench.LastTestPassed, "fault remains diagnosed");
                expectedPause = true;
                deal.OpenMenu(false, true);
                var pose = lamps[index].localPosition;
                var rotation = lamps[index].localRotation;
                var light = lights[index].enabled;
                yield return null;
                yield return null;
                Check(lamps[index].localPosition == pose && lamps[index].localRotation == rotation && lights[index].enabled == light,
                    "pause freezes test pose and failed-test indicator");
                expectedPause = false;
                deal.CloseMenu();
                for (var i = 0; i < 15; i++) yield return null;
                var before = session.ElapsedSeconds;
                workshop.Perform(WorkshopActionKind.Repair, 0);
                Check(session.RepairSeconds == 240 && session.ElapsedSeconds - before >= 240 && session.ElapsedSeconds - before < 241 &&
                    session.SpareBatteries == 0 && !session.OnBench.IsTested, "repair adds exactly four minutes and consumes battery once");
                Check(label.text.Contains("+04:00"), "repair time cost readable without sound or colour");
                yield return null;
                Check(lamps[index].localPosition.y > 0.001f && Quaternion.Angle(lamps[index].localRotation, Quaternion.identity) > 0.01f,
                    "repair lifts and turns lamp");
                Capture("repair", bench.position);
                for (var i = 0; i < 24; i++) yield return null;
                Check(lamps[index].localPosition == Vector3.zero && lamps[index].localRotation == Quaternion.identity, "repair settles");
                workshop.Perform(WorkshopActionKind.Repair, 0);
                yield return null;
                Check(session.RepairSeconds == 240 && lamps[index].localPosition == Vector3.zero, "rejected repair neither repeats motion nor charges time");
                workshop.Perform(WorkshopActionKind.Test, 0);
            }
            for (var i = 0; i < 15; i++) yield return null;
            Check(lights[index].enabled && lamps[index].localPosition == Vector3.zero, "test settles with steady success indicator " + index);
            Check(session.ToggleBench(out _) && session.ToggleUnit(index, out _), "return lamp " + index);
            for (var i = 0; i < 9; i++) yield return null;
            Check(lamps[index].localPosition == Vector3.zero, "return settles " + index);
        }
        Check(session.AcceptOrder(DealOrderId.A, out _) && session.AcceptOrder(DealOrderId.B, out _), "accept both orders");
        Check(session.ToggleCrate(out _), "carry tested batch");
        for (var i = 0; i < 9; i++) yield return null;
        deal.Perform(FirstDealActionKind.DeliverA);
        Check(session.Balance == 330 && lamps[0].gameObject.activeSelf && lamps[1].gameObject.activeSelf,
            "A pays atomically before visual handoff ends");
        Check(lamps[0].GetComponentsInChildren<Collider>().All(c => !c.enabled), "sold visual cannot receive ray interactions");
        var saleStart = lamps[0].localPosition;
        yield return null;
        Check(lamps[0].localPosition.z > saleStart.z, "sold lamps move forward to recipient");
        Capture("handoff", lamps[0].position);
        expectedPause = true;
        deal.OpenMenu(false, true);
        var salePaused = lamps[0].localPosition;
        yield return null;
        Check(lamps[0].localPosition == salePaused, "pause freezes handoff");
        expectedPause = false;
        deal.CloseMenu();
        for (var i = 0; i < 12; i++) yield return null;
        Check(!lamps[0].gameObject.activeSelf && !lamps[1].gameObject.activeSelf, "sold A lamps disappear after handoff");
        deal.Perform(FirstDealActionKind.DeliverA);
        Check(session.Balance == 330 && !lamps[0].gameObject.activeSelf, "duplicate delivery cannot replay handoff or payout");
        deal.Perform(FirstDealActionKind.DeliverB);
        Check(session.Balance == 455, "complete A+B repaired route retains original $455");
        workshop.enabled = false;
        Check(!lamps[2].gameObject.activeSelf, "disable settles pending sale");
        workshop.enabled = true;
        yield return null;
        Check(lamps.All(l => !l.gameObject.activeSelf), "reenable cannot resurrect sold lamps");
        Check(session.ToggleCrate(out _), "start final docking motion");
        session.EndShift(false, out _);
        yield return null;
        Check(session.ShiftEnded && session.Balance == 455 && crate.localPosition == Vector3.zero,
            "shift end settles motion without extra mutations");
    }

    private static void Capture(string name, Vector3 target)
    {
        var go = new GameObject("F2 verification camera", typeof(Camera));
        var camera = go.GetComponent<Camera>();
        var texture = new RenderTexture(1000, 700, 24);
        var pixels = new Texture2D(1000, 700, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            camera.CopyFrom(Camera.main);
            camera.enabled = false;
            camera.fieldOfView = 55;
            camera.transform.position = target + new Vector3(0.7f, 0.6f, -1.2f);
            camera.transform.LookAt(target);
            camera.targetTexture = texture;
            camera.Render();
            RenderTexture.active = texture;
            pixels.ReadPixels(new Rect(0, 0, 1000, 700), 0, 0);
            pixels.Apply();
            File.WriteAllBytes("Logs/F2-" + name + ".png", pixels.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            camera.targetTexture = null;
            texture.Release();
            UnityEngine.Object.DestroyImmediate(pixels);
            UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
