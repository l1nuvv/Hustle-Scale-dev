using System;
using System.Collections.Generic;
using System.IO;
using Interactables;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Player
{
    // Explicit opt-in observer, also usable in a non-development Player. No profiler,
    // rendering, pause, background-mode or quality overrides. Saves are isolated.
    public sealed class CityStartupDiagnostics : MonoBehaviour
    {
        public static string SavePath { get; private set; }
        private static string output;
        private static double beforeSplash, beforeScene;
        private static string beforeSplashUtc, beforeSceneUtc;
        private static bool manual;
        private static CityStartupDiagnostics observer;
        private readonly Report report = new Report();
        private readonly List<Stamp> stamps = new List<Stamp>();
        private readonly List<double> frames = new List<double>(4096);
        private double previousFrame, readyAt = -1;
        private bool finished;

        [Serializable]
        private sealed class Stamp
        {
            public string name, utc;
            public double runtimeSeconds;
        }

        [Serializable]
        private sealed class Report
        {
            public string buildGuid, unity, cpu, gpu, graphicsApi, scene, result, qualityName, pipeline;
            public string beforeSplashUtc, beforeSceneUtc;
            public bool development, batchMode, background, focused, paused, canInteract;
            public int ramMB, width, height, quality, vSync, targetFps, frameCount;
            public double beforeSplashSeconds, beforeSceneSeconds, readySeconds = -1, firstRenderSubmissionSeconds = -1;
            public double p95Ms, p99Ms, worstMs, firstUpdateSeconds;
            public long allocatedBytes, reservedBytes, managedBytes;
            public int errors, warnings;
            public Stamp[] events;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void BeforeSplash()
        {
            beforeSplash = Time.realtimeSinceStartupAsDouble;
            beforeSplashUtc = DateTime.UtcNow.ToString("O");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            SavePath = null; output = null; manual = false;
            if (Application.isEditor) return;
            beforeScene = Time.realtimeSinceStartupAsDouble;
            beforeSceneUtc = DateTime.UtcNow.ToString("O");
            foreach (var argument in Environment.GetCommandLineArgs())
            {
                if (argument == "-lifecycle-manual") manual = true;
                if (argument.StartsWith("-lifecycle-audit=", StringComparison.Ordinal))
                    output = Path.GetFullPath(argument.Substring(17));
            }
            if (output == null) return;
            Directory.CreateDirectory(output);
            SavePath = Path.Combine(output, "city-save.json");
            DontDestroyOnLoad(new GameObject("City startup diagnostics", typeof(CityStartupDiagnostics)));
        }

        private void Awake()
        {
            observer = this;
            report.buildGuid = Application.buildGUID;
            report.unity = Application.unityVersion;
            report.development = Debug.isDebugBuild;
            report.batchMode = Application.isBatchMode;
            report.cpu = SystemInfo.processorType; report.gpu = SystemInfo.graphicsDeviceName;
            report.graphicsApi = SystemInfo.graphicsDeviceType.ToString();
            report.ramMB = SystemInfo.systemMemorySize;
            report.beforeSplashSeconds = beforeSplash; report.beforeSceneSeconds = beforeScene;
            report.beforeSplashUtc = beforeSplashUtc; report.beforeSceneUtc = beforeSceneUtc;
            StampEvent("diagnostic Awake (managed runtime, not process start)");
            SceneManager.sceneLoaded += Loaded;
            RenderPipelineManager.endContextRendering += Rendered;
            Application.logMessageReceived += Log;
        }

        private void StampEvent(string name) => stamps.Add(new Stamp
        {
            name = name, utc = DateTime.UtcNow.ToString("O"), runtimeSeconds = Time.realtimeSinceStartupAsDouble
        });

        public static void Stage(string name)
        {
            if (observer != null) observer.StampEvent(name);
        }

        private void Loaded(Scene scene, LoadSceneMode mode) => StampEvent("sceneLoaded: " + scene.name);

        private void Rendered(ScriptableRenderContext context, List<Camera> cameras)
        {
            if (report.firstRenderSubmissionSeconds >= 0 || cameras.Count == 0) return;
            report.firstRenderSubmissionSeconds = Time.realtimeSinceStartupAsDouble;
            StampEvent("first SRP context complete (submission, not display presentation)");
        }

        private void Log(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errors++;
            if (type == LogType.Warning) report.warnings++;
        }

        private void LateUpdate()
        {
            var now = Time.realtimeSinceStartupAsDouble;
            if (previousFrame > 0 && frames.Count < 4096) frames.Add((now - previousFrame) * 1000);
            if (previousFrame == 0) report.firstUpdateSeconds = now;
            previousFrame = now;
            if (readyAt < 0)
            {
                var trade = FindAnyObjectByType<CityTradeView>();
                var save = trade != null ? trade.GetComponent<CitySaveController>() : null;
                if (trade != null && save.Session != null && trade.Session == save.Session &&
                    GameObject.Find("City trading UI") != null)
                {
                    readyAt = report.readySeconds = now;
                    StampEvent("city session and UI ready; starts paused");
                }
            }
            // Bounded diagnostic run. Never waits indefinitely for a rendered frame.
            if (!manual && !finished && (readyAt >= 0 ? now - readyAt >= 5 : now >= 20))
            {
                finished = true;
                report.result = readyAt < 0 ? "NOT READY" : report.errors == 0 ? "READY" : "RUNTIME ERRORS";
                StampEvent("diagnostic requests Application.Quit");
                WriteReport();
                Application.Quit(readyAt >= 0 && report.errors == 0 ? 0 : 2);
            }
        }

        private void OnApplicationFocus(bool focused) => StampEvent("OS focus: " + focused);
        private void OnApplicationPause(bool paused) => StampEvent("OS pause: " + paused);

        private void WriteReport()
        {
            report.scene = SceneManager.GetActiveScene().name;
            report.width = Screen.width; report.height = Screen.height;
            report.quality = QualitySettings.GetQualityLevel(); report.vSync = QualitySettings.vSyncCount;
            report.qualityName = QualitySettings.names[report.quality];
            report.pipeline = GraphicsSettings.currentRenderPipeline != null ? GraphicsSettings.currentRenderPipeline.name : "built-in";
            report.targetFps = Application.targetFrameRate; report.background = Application.runInBackground;
            report.focused = Application.isFocused;
            var walk = FindAnyObjectByType<CityGreyboxWalk>();
            report.paused = walk != null && walk.IsPaused;
            report.canInteract = walk != null && walk.GetComponent<Movement.PlayerController>().CanInteract;
            report.allocatedBytes = Profiler.GetTotalAllocatedMemoryLong();
            report.reservedBytes = Profiler.GetTotalReservedMemoryLong();
            report.managedBytes = GC.GetTotalMemory(false);
            report.frameCount = frames.Count;
            if (frames.Count > 0)
            {
                var sorted = frames.ToArray(); Array.Sort(sorted);
                report.p95Ms = sorted[Math.Max(0, (int)Math.Ceiling(sorted.Length * 0.95) - 1)];
                report.p99Ms = sorted[Math.Max(0, (int)Math.Ceiling(sorted.Length * 0.99) - 1)];
                report.worstMs = sorted[sorted.Length - 1];
            }
            report.events = stamps.ToArray();
            File.WriteAllText(Path.Combine(output, "startup.json"), JsonUtility.ToJson(report, true));
        }

        private void OnApplicationQuit()
        {
            if (report.result == null) report.result = readyAt < 0 ? "NOT READY" : report.errors == 0 ? "READY" : "RUNTIME ERRORS";
            StampEvent("OnApplicationQuit (callback reached; external observer must verify process exit)");
            WriteReport();
        }

        private void OnDestroy()
        {
            if (observer == this) observer = null;
            SceneManager.sceneLoaded -= Loaded;
            RenderPipelineManager.endContextRendering -= Rendered;
            Application.logMessageReceived -= Log;
        }
    }
}
