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

// Exercises the real scene adapters and Unity audio playback, without saving scene changes.
[InitializeOnLoad]
public static class F1Verification
{
    private const string Key = "F1.verify";
    private const string LogPath = "Logs/Codex-F1-verification.log";
    private static IEnumerator steps;
    private static int checks, frame;
    private static double next, timeout;
    private static bool previousBackground, failed, completed;
    private static WorkshopView workshop;
    private static FirstDealView deal;
    private static FirstDealAudio audio;
    private static AudioSource world, confirmations, ambience;
    private static WorkshopSession session;
    private static bool expectedPause;

    static F1Verification() => EditorApplication.playModeStateChanged += StateChanged;

    [MenuItem("Tools/F1/Verify Audio and Pause")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Stop Play Mode and save the scene first.");
        EditorSceneManager.OpenScene(FirstDealTradeBuilder.ScenePath);
        Directory.CreateDirectory("Logs");
        File.WriteAllText(LogPath, "F1 Unity " + Application.unityVersion + " UTC " + DateTime.UtcNow.ToString("O") + "\n");
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
            expectedPause = true;
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
            var result = "F1 RESULT " + (completed && !failed ? "PASS" : "FAIL") + ": " + checks + " assertions";
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
        next = EditorApplication.timeSinceStartup + 0.18;
        try
        {
            // Desktop tooling can change Game View focus between scenario steps.
            // Explicit pause and focus probes below still run without intervention.
            if (audio != null) audio.SendMessage("OnApplicationFocus", true);
            if (deal != null) deal.SendMessage("OnApplicationFocus", true);
            if (session != null && session.IsPaused && !expectedPause && !session.ShiftEnded)
                deal.CloseMenu();
            Check(EditorApplication.timeSinceStartup < timeout, "frame timeout guard");
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

    private static void Cue(AudioSource source, FirstDealSound sound) =>
        Check(source.clip != null && source.clip.name == "F1 " + sound && source.isPlaying, "playing " + sound);

    private static IEnumerator Scenario()
    {
        workshop = UnityEngine.Object.FindAnyObjectByType<WorkshopView>();
        deal = UnityEngine.Object.FindAnyObjectByType<FirstDealView>();
        audio = workshop.GetComponent<FirstDealAudio>();
        session = workshop.Session;
        Check(audio != null && UnityEngine.Object.FindObjectsByType<FirstDealAudio>().Length == 1, "one runtime audio component");
        Check(UnityEngine.Object.FindObjectsByType<AudioListener>().Count(l => l.isActiveAndEnabled) == 1, "one active listener");
        world = workshop.transform.Find("F1 Audio - Actions").GetComponent<AudioSource>();
        confirmations = workshop.transform.Find("F1 Audio - Confirmations").GetComponent<AudioSource>();
        ambience = workshop.transform.Find("F1 Audio - Workshop and yard").GetComponent<AudioSource>();
        audio.SendMessage("OnApplicationFocus", true);
        deal.SendMessage("OnApplicationFocus", true);
        Check(session.IsPaused && !ambience.isPlaying, "initial menu pauses ambience");
        deal.CloseMenu();
        expectedPause = false;
        yield return null;
        Check(ambience.isPlaying && ambience.loop && ambience.volume < world.volume, "quiet looping location bed resumes");
        var output = new float[1024];
        // The first GetOutputData call initializes DSP history and can return zeroes.
        // Poll across real frames, with a deadline; a genuinely silent channel still fails.
        var outputDeadline = EditorApplication.timeSinceStartup + 3;
        do
        {
            ambience.GetOutputData(output, 0);
            if (output.Any(v => Mathf.Abs(v) > 0.00001f)) break;
            yield return null;
        } while (EditorApplication.timeSinceStartup < outputDeadline);
        Check(output.Any(v => Mathf.Abs(v) > 0.00001f), "Unity audio channel produces non-zero output");
        var ambientSamples = new float[ambience.clip.samples];
        Check(ambience.clip.GetData(ambientSamples, 0) && ambientSamples.Any(v => Mathf.Abs(v) > 0.01f), "ambience contains signal");
        Check(Mathf.Abs(ambientSamples[0] - ambientSamples[ambientSamples.Length - 1]) < 0.01f, "ambience loop boundary continuous");
        deal.OpenMenu(true);
        deal.Purchase(false);
        Cue(confirmations, FirstDealSound.Purchase);
        Check(session.Balance == 120 && session.LotPurchased, "purchase keeps ledger correct");
        yield return null;
        deal.Purchase(false);
        Cue(confirmations, FirstDealSound.Warning);
        Check(session.Balance == 120, "duplicate purchase rejected without spending");
        yield return null;
        deal.OpenMenu(true);
        deal.Purchase(true);
        Cue(confirmations, FirstDealSound.Purchase);
        Check(session.Balance == 70 && session.SpareBatteries == 1, "battery purchase confirmed");
        Check(session.ToggleUnit(1, out _), "prepare faulty lamp in hands");
        workshop.Perform(WorkshopActionKind.Bench, 0);
        Cue(world, FirstDealSound.Contact);
        yield return null;
        workshop.Perform(WorkshopActionKind.Test, 0);
        Cue(world, FirstDealSound.TestFailed);
        Check(!session.OnBench.LastTestPassed, "faulty test stays faulty");
        yield return null;
        workshop.Perform(WorkshopActionKind.Repair, 0);
        Cue(world, FirstDealSound.Repair);
        Check(session.SpareBatteries == 0 && session.RepairSeconds == 240 && !session.OnBench.IsTested, "repair consumes once and requires retest");
        yield return null;
        workshop.Perform(WorkshopActionKind.Test, 0);
        Cue(world, FirstDealSound.TestPassed);
        expectedPause = true;
        deal.OpenMenu(false, true);
        var worldTime = world.timeSamples;
        var ambientTime = ambience.timeSamples;
        Check(!world.isPlaying && !ambience.isPlaying, "menu pauses world audio");
        yield return null;
        Check(world.timeSamples == worldTime && ambience.timeSamples == ambientTime, "paused audio cursors do not advance");
        deal.CloseMenu();
        expectedPause = false;
        Check(world.isPlaying && ambience.isPlaying, "resume continues paused voices");
        yield return null;
        deal.OpenMenu(false);
        deal.Accept(1);
        deal.CloseMenu();
        Check(session.ToggleBench(out _), "take tested repaired lamp");
        deal.Perform(FirstDealActionKind.DeliverB);
        Cue(world, FirstDealSound.Sale);
        Check(session.Balance == 195 && session.Order(DealOrderId.B).State == DealOrderState.Completed, "delivery pays once");
        yield return null;
        deal.Perform(FirstDealActionKind.DeliverB);
        Cue(confirmations, FirstDealSound.Warning);
        Check(session.Balance == 195, "repeated delivery warns without payout");
        yield return null;
        Check(session.ToggleUnit(0, out _) && session.ToggleBench(out _) && session.Test(out _) && session.ToggleBench(out _), "prepare buyback lamp");
        deal.OpenMenu(true);
        deal.SellBack();
        expectedPause = true;
        Cue(confirmations, FirstDealSound.Sale);
        Check(session.IsPaused && !ambience.isPlaying && session.Balance == 255, "buyback confirms in paused seller screen");
        yield return null;
        deal.CloseMenu();
        expectedPause = false;
        session.StartShift();
        for (var i = 0; i < 4; i++) yield return null;
        session.AdvanceActiveSeconds(1100);
        Cue(confirmations, FirstDealSound.Warning);
        yield return null;
        var warningTime = confirmations.timeSamples;
        session.AdvanceActiveSeconds(1);
        Check(confirmations.timeSamples >= warningTime, "clock refresh does not restart expiry warning");
        audio.SendMessage("OnApplicationFocus", false);
        Check(!world.isPlaying && !ambience.isPlaying && !confirmations.isPlaying, "focus loss silences all channels");
        audio.SendMessage("OnApplicationFocus", true);
        Check(ambience.isPlaying, "focus regain resumes location bed");
        // Inspect every generated cue for finite bounded signal and distinct PCM.
        var previous = new System.Collections.Generic.List<float[]>();
        foreach (FirstDealSound sound in Enum.GetValues(typeof(FirstDealSound)))
        {
            yield return null;
            audio.Play(sound, true);
            var source = confirmations;
            Cue(source, sound);
            var data = new float[source.clip.samples];
            Check(source.clip.GetData(data, 0) && data.All(v => !float.IsNaN(v) && Mathf.Abs(v) <= 1) &&
                data.Any(v => Mathf.Abs(v) > 0.05f), sound + " finite non-silent PCM without clipping");
            Check(previous.All(p => !p.SequenceEqual(data)), sound + " distinct waveform");
            previous.Add(data);
        }
        session.EndShift(false, out _);
        Check(session.ShiftEnded && !world.isPlaying && !ambience.isPlaying && !confirmations.isPlaying, "shift end silences audio");
    }
}
