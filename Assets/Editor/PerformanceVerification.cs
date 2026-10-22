using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Interactables;
using Player;
using Player.Movement;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

// Bounded Editor experiment. Player frame pacing is measured separately by CitySliceProfile.
[InitializeOnLoad]
public static class PerformanceVerification
{
    private const string Override = "HustleScale.S2SaveOverride";
    private const string Key = "HustleScale.PerformanceVerification";
    private static IEnumerator routine;
    private static string output;
    private static int checks;
    private static double started;

    static PerformanceVerification() => EditorApplication.playModeStateChanged += StateChanged;

    private static void StateChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            output = SessionState.GetString(Key + ".Output", "");
            checks = 0;
            started = EditorApplication.timeSinceStartup;
            routine = Scenario();
            EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update -= Tick;
            routine = null;
            SessionState.SetString(Override, SessionState.GetString(Key + ".PreviousOverride", ""));
            SessionState.EraseBool(Key);
        }
    }

    [Serializable]
    private sealed class Sample
    {
        public string command;
        public double milliseconds;
        // Mono in this Editor returns zero for GetAllocatedBytesForCurrentThread.
        // Do not serialize those zeros as measured allocations.
        public int menuVersions;
    }

    [Serializable]
    private sealed class Report
    {
        public string unity;
        public int assertions;
        public Sample[] samples;
    }

    [MenuItem("Tools/Performance/Verify Command Menu Updates")]
    public static void RunMenu() => Run("Temp/PerformanceAudit/commands-editor-" + Guid.NewGuid().ToString("N"));

    public static void Run(string destination)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || routine != null)
            throw new InvalidOperationException("Stop Play Mode before the performance experiment.");
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != "Assets/Scenes/CityGreybox.unity" ||
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Open the saved CityGreybox first.");
        output = Path.GetFullPath(destination);
        Directory.CreateDirectory(output);
        SessionState.SetString(Key + ".PreviousOverride", SessionState.GetString(Override, ""));
        SessionState.SetString(Key + ".Output", output);
        SessionState.SetBool(Key, true);
        SessionState.SetString(Override, Path.Combine(output, "city-save.json"));
        EditorApplication.isPlaying = true;
    }

    private static void Check(bool value, string message)
    {
        checks++;
        if (!value) throw new InvalidOperationException(message);
    }

    private static void Tick()
    {
        EditorApplication.QueuePlayerLoopUpdate();
        if (!EditorApplication.isPlaying) return;
        try
        {
            if (EditorApplication.timeSinceStartup - started > 60) throw new TimeoutException("Command experiment timed out.");
            if (routine.MoveNext()) return;
            Debug.Log("PERFORMANCE VERIFICATION PASS: " + checks);
            Finish();
        }
        catch (Exception exception)
        {
            File.WriteAllText(Path.Combine(output, "failure.txt"), exception.ToString());
            Debug.LogException(exception);
            Finish();
        }
    }

    private static void Finish()
    {
        EditorApplication.update -= Tick;
        routine = null;
        EditorApplication.isPlaying = false;
    }

    private static IEnumerator Scenario()
    {
        // Editor update can run more than once per game frame.
        var startFrame = Time.frameCount;
        while (Time.frameCount < startFrame + 4) yield return null;
        var trade = UnityEngine.Object.FindAnyObjectByType<CityTradeView>();
        var save = trade.GetComponent<CitySaveController>();
        var walk = trade.GetComponent<CityGreyboxWalk>();
        var player = trade.GetComponent<PlayerController>();
        player.GetComponent<PlayerInput>().MouseInputEnabled = false;
        var seller = trade.Points.First(p => p.Kind == CityTradeKind.Seller);
        var samples = new List<Sample>();
        for (var run = 0; run < 9; run++)
        {
            trade.CloseMenu();
            save.NewGame();
            walk.SetPaused(false);
            var position = seller.transform.position;
            player.RestorePose(new Vector3(position.x, 0.08f, position.z - 1.5f), 0, 0);
            Physics.SyncTransforms();
            trade.Open(seller);
            Check(trade.MenuOpen, "seller menu opens");
            // Let deferred destruction/layout finish before timing the synchronous command.
            var frame = Time.frameCount;
            while (Time.frameCount < frame + 2) yield return null;
            foreach (var command in new[] { "purchase", "purchase", "battery" })
            {
                // The field is intentionally private; this harness observes menu identity without runtime instrumentation.
                var menu = (RectTransform)typeof(CityTradeView).GetField("menuContent",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(trade);
                var versions = new HashSet<Transform>();
                Action changed = () => { if (menu.childCount > 0) versions.Add(menu.GetChild(menu.childCount - 1)); };
                trade.Session.Changed += changed;
                var balance = trade.Session.Balance;
                var watch = Stopwatch.StartNew();
                var success = trade.Execute(command);
                watch.Stop();
                trade.Session.Changed -= changed;
                if (menu.childCount > 0) versions.Add(menu.GetChild(menu.childCount - 1));
                Check(versions.Count <= 2, "menu must retain its controls during model events and rebuild once after the command");
                var rejected = command == "purchase" && balance == 120;
                Check(success != rejected, "success/refusal result");
                Check(trade.Session.Balance == (rejected ? balance : balance - (command == "purchase" ? 180 : 50)), "one payment");
                Check(trade.Session.IsPaused && walk.IsPaused && trade.MenuOpen, "command returns to modal pause");
                Check(EventSystem.current.currentSelectedGameObject != null && EventSystem.current.currentSelectedGameObject.activeInHierarchy,
                    "focus remains on an active menu control");
                if (run > 0) samples.Add(new Sample
                {
                    command = rejected ? "purchase-refused" : command,
                    milliseconds = watch.Elapsed.TotalMilliseconds, menuVersions = versions.Count
                });
                frame = Time.frameCount;
                while (Time.frameCount < frame + 2) yield return null;
            }
        }
        File.WriteAllText(Path.Combine(output, "commands.json"), JsonUtility.ToJson(new Report
        { unity = Application.unityVersion, assertions = checks, samples = samples.ToArray() }, true));
    }
}
