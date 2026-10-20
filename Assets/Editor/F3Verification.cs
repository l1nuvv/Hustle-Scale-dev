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

// Combined feedback checks use real frames and the scene adapters without saving assets.
[InitializeOnLoad]
public static class F3Verification
{
    private const string Key = "F3.verify";
    private const string LogPath = "Logs/Codex-F3-verification.log";
    private static IEnumerator steps;
    private static readonly System.Collections.Generic.Stack<IEnumerator> routines = new System.Collections.Generic.Stack<IEnumerator>();
    private static int checks, frame;
    private static double next, timeout;
    private static bool previousBackground, failed, completed;
    private static WorkshopView workshop;
    private static FirstDealView deal;
    private static WorkshopSession session;
    private static bool expectedPause;

    static F3Verification() => EditorApplication.playModeStateChanged += StateChanged;

    [MenuItem("Tools/F3/Verify Combined Feedback")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Stop Play Mode and save the scene first.");
        EditorSceneManager.OpenScene(FirstDealTradeBuilder.ScenePath);
        Directory.CreateDirectory("Logs");
        File.WriteAllText(LogPath, "F3 Unity " + Application.unityVersion + " UTC " + DateTime.UtcNow.ToString("O") + "\n");
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
            timeout = next + 90;
            steps = Scenario();
            routines.Clear();
            routines.Push(steps);
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
            var result = "F3 RESULT " + (completed && !failed ? "PASS" : "FAIL") + ": " + checks + " assertions";
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
            var current = routines.Peek();
            if (!current.MoveNext()) routines.Pop();
            else if (current.Current is IEnumerator nested) routines.Push(nested);
            if (routines.Count == 0) { completed = true; EditorApplication.isPlaying = false; }
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
        var audio = workshop.GetComponent<FirstDealAudio>();
        var world = workshop.transform.Find("F1 Audio - Actions").GetComponent<AudioSource>();
        var confirmations = workshop.transform.Find("F1 Audio - Confirmations").GetComponent<AudioSource>();
        var ambience = workshop.transform.Find("F1 Audio - Workshop and yard").GetComponent<AudioSource>();
        var label = Field<UnityEngine.UI.Text>("feedbackLabel");
        var status = Field<UnityEngine.UI.Text>("status");
        var lamps = Field<Transform[]>("lamps");
        var lights = Field<Light[]>("indicators");
        audio.SendMessage("OnApplicationFocus", true);
        deal.SendMessage("OnApplicationFocus", true);
        deal.CloseMenu();
        deal.Purchase(false);
        Check(label.text.Contains("−$180") && status.text.Contains("$120") &&
            confirmations.clip.name == "F1 Purchase" && confirmations.isPlaying,
            "purchase text, balance and sound update in the action frame");
        expectedPause = true;
        deal.OpenMenu(false, true);
        var message = label.text;
        var pauseUntil = EditorApplication.timeSinceStartup + 8.4;
        while (EditorApplication.timeSinceStartup < pauseUntil) yield return null;
        Check(label.text == message, "result text survives more than eight seconds of pause");
        deal.CloseMenu();
        expectedPause = false;
        yield return Wait(8.5f);
        Check(label.text.Length == 0, "result text expires after eight active seconds");
        deal.Purchase(true);
        Check(label.text.Contains("−$50") && session.SpareBatteries == 1, "battery has immediate written receipt");

        // Remove audio and coloured indicators as information channels for the entire trade.
        foreach (var source in new[] { world, confirmations, ambience }) source.mute = true;
        foreach (var light in lights) light.gameObject.SetActive(false);
        Check(world.mute && confirmations.mute && ambience.mute && lights.All(l => !l.gameObject.activeInHierarchy),
            "silent route with all diagnostic indicator objects disabled");
        Check(session.ToggleUnit(1, out _), "prepare faulty lamp");
        workshop.Perform(WorkshopActionKind.Bench, 0);
        Check(label.text.Contains("на столе") && session.OnBench == session.Units[1], "placement is readable immediately");
        yield return Wait(0.4f);
        workshop.Perform(WorkshopActionKind.Test, 0);
        Check(label.text.Contains("неисправна батарея") && world.clip.name == "F1 TestFailed" && world.isPlaying,
            "failed test text and cue are synchronous without needing colour or audible output");
        yield return null;
        Check(lamps[1].localPosition.y > 0.001f, "failed diagnosis also moves the lamp");
        expectedPause = true;
        deal.OpenMenu(false, true);
        var pose = lamps[1].localPosition;
        var sample = world.timeSamples;
        var indicator = lights[1].enabled;
        var clock = session.ElapsedSeconds;
        yield return WaitRealtime(0.35f);
        Check(lamps[1].localPosition == pose && world.timeSamples == sample && lights[1].enabled == indicator &&
            session.ElapsedSeconds == clock, "one pause freezes motion, sound cursor, blink and clock together");
        deal.CloseMenu();
        expectedPause = false;
        yield return Wait(0.85f);
        var before = session.ElapsedSeconds;
        workshop.Perform(WorkshopActionKind.Repair, 0);
        Check(label.text.Contains("+04:00") && label.text.Contains("снова") && !session.OnBench.IsTested &&
            session.ElapsedSeconds - before >= 240 && session.ElapsedSeconds - before < 241 &&
            world.clip.name == "F1 Repair", "repair text, time charge and sound update together; retest is explicit");
        yield return null;
        Check(lamps[1].localPosition.y > 0.001f, "repair motion begins on next rendered frame");
        yield return Wait(1.2f);
        workshop.Perform(WorkshopActionKind.Repair, 0);
        Check(label.text.StartsWith("Нельзя:") && session.RepairSeconds == 240 && session.SpareBatteries == 0 &&
            lamps[1].localPosition == Vector3.zero, "rejected repair gives reason without replaying motion or cost");
        var warningClip = confirmations.clip;
        var warningSample = confirmations.timeSamples;
        workshop.Perform(WorkshopActionKind.Repair, 0);
        Check(confirmations.clip == warningClip && confirmations.timeSamples == warningSample,
            "same-frame repeated rejection does not restart warning");
        yield return Wait(0.7f);
        Check(!confirmations.isPlaying && !world.isPlaying, "no stacked or delayed cues after rejected repair");
        workshop.Perform(WorkshopActionKind.Test, 0);
        Check(label.text.Contains("исправен") && !label.text.Contains("неисправна") &&
            session.OnBench.LastTestPassed && world.clip.name == "F1 TestPassed", "retest replaces stale failure text and cue");
        Check(session.ToggleBench(out _) && session.ToggleUnit(1, out _), "return repaired lamp");
        for (var i = 0; i < 3; i += 2)
        {
            Check(session.ToggleUnit(i, out _), "take remaining lamp " + i);
            workshop.Perform(WorkshopActionKind.Bench, 0);
            workshop.Perform(WorkshopActionKind.Test, 0);
            Check(label.text.Contains("исправен") && session.OnBench.LastTestPassed, "working lamp has written result " + i);
            Check(session.ToggleBench(out _) && session.ToggleUnit(i, out _), "return working lamp " + i);
        }
        Check(session.AcceptOrder(DealOrderId.A, out _) && session.AcceptOrder(DealOrderId.B, out _) &&
            session.ToggleCrate(out _), "prepare A and B delivery");
        yield return Wait(0.7f);
        deal.Perform(FirstDealActionKind.DeliverA);
        Check(label.text.Contains("+$260") && status.text.Contains("$330") && world.clip.name == "F1 Sale" &&
            lamps[0].gameObject.activeSelf && lamps[1].gameObject.activeSelf,
            "silent A sale explains payout immediately while handoff starts");
        yield return Wait(0.6f);
        Check(!lamps[0].gameObject.activeSelf && !lamps[1].gameObject.activeSelf, "handoff ends without waiting for input");
        deal.Perform(FirstDealActionKind.DeliverA);
        Check(session.Balance == 330 && label.text.Length > 0 && !lamps[0].gameObject.activeSelf,
            "duplicate delivery has written rejection without payout or visual replay");
        deal.Perform(FirstDealActionKind.DeliverB);
        Check(label.text.Contains("+$125") && status.text.Contains("$455") && session.OwnedLampCount == 0,
            "silent B sale completes original A+B route at $455");
        yield return Wait(0.8f);
        Check(!world.isPlaying && !confirmations.isPlaying && lamps.All(l => !l.gameObject.activeSelf),
            "all short cues and handoffs finish without a delayed queue");
        session.EndShift(false, out _);
        Check(!world.isPlaying && !confirmations.isPlaying && !ambience.isPlaying, "shift end stops all audio channels");
    }

    private static IEnumerator Wait(float seconds)
    {
        var until = Time.time + seconds;
        while (Time.time < until) yield return null;
    }

    private static IEnumerator WaitRealtime(float seconds)
    {
        var until = EditorApplication.timeSinceStartup + seconds;
        while (EditorApplication.timeSinceStartup < until) yield return null;
    }
}
