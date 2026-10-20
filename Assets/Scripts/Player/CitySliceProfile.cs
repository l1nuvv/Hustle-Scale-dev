using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Interactables;
using Player.Movement;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace Player
{
    // Opt-in E5.8 development-player measurement. Never runs during normal play.
    public sealed class CitySliceProfile : MonoBehaviour
    {
        public static string SavePath { get; private set; }
        private static string output;
        private ProfilerRecorder mainThread, draws, gc;
        private Camera profileCamera;
        private RenderTexture profileTexture;
        private RenderPipeline.StandardRequest renderRequest;
        private double lastRenderSubmitMs;
        private long lastDrawCalls;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            SavePath = null; output = null;
            if (!Debug.isDebugBuild) return;
            foreach (var argument in Environment.GetCommandLineArgs())
                if (argument.StartsWith("-e5-profile=", StringComparison.Ordinal)) output = Path.GetFullPath(argument.Substring(12));
            if (output == null) return;
            Directory.CreateDirectory(output);
            SavePath = Path.Combine(output, "city-save.json");
            DontDestroyOnLoad(new GameObject("E5 development profile", typeof(CitySliceProfile)));
        }

        [Serializable]
        private sealed class Segment
        {
            public string name;
            public int frames;
            public double meanMs, p50Ms, p95Ms, p99Ms, maxMs, mainThreadMeanMs, renderSubmitMeanMs, drawCallsMean, gcBytesMean;
            public long allocatedBytes, reservedBytes, managedBytes;
        }

        [Serializable]
        private sealed class Report
        {
            public string unity, platform, cpu, gpu, graphicsApi;
            public int ramMB, gpuMB, width, height, quality, vSync, targetFps;
            public bool development, mainThreadAvailable, drawsAvailable, gcAvailable;
            public string conditions;
            public Segment[] segments;
        }

        private IEnumerator Start()
        {
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
            yield return null;
            var trade = FindAnyObjectByType<CityTradeView>();
            var walk = FindAnyObjectByType<CityGreyboxWalk>();
            var player = FindAnyObjectByType<PlayerController>();
            if (trade == null || walk == null || player == null)
            { Debug.LogError("E5 profile requires CityGreybox."); Application.Quit(2); yield break; }
            player.GetComponent<PlayerInput>().MouseInputEnabled = false;
            // A hidden Windows backbuffer can skip rendering entirely. Submit a real
            // URP camera request to an offscreen target, including the city Canvas.
            profileCamera = player.GetComponentInChildren<Camera>();
            profileTexture = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32);
            profileTexture.Create();
            renderRequest = new RenderPipeline.StandardRequest { destination = profileTexture };
            profileCamera.enabled = false;
            foreach (var canvas in FindObjectsByType<Canvas>())
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = profileCamera;
                canvas.planeDistance = 0.5f;
            }
            mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
            draws = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count", 1);
            gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
            var report = new Report
            {
                unity = Application.unityVersion, platform = Application.platform.ToString(), cpu = SystemInfo.processorType,
                gpu = SystemInfo.graphicsDeviceName, graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                ramMB = SystemInfo.systemMemorySize, gpuMB = SystemInfo.graphicsMemorySize, width = Screen.width, height = Screen.height,
                quality = QualitySettings.GetQualityLevel(), vSync = QualitySettings.vSyncCount, targetFps = Application.targetFrameRate,
                development = Debug.isDebugBuild, mainThreadAvailable = mainThread.Valid, drawsAvailable = draws.Valid, gcAvailable = gc.Valid,
                conditions = "Hidden development Player: explicit URP StandardRequest to 1600x900 RenderTexture each frame; Canvas uses ScreenSpaceCamera for offscreen capture. Scripted camera stations; 3s warmup + 8s samples each; no deep profiling. Editor idle and open. Main Thread includes waits. This is an offscreen rendered workload, not presentation/display FPS. GPU time unavailable. Saves isolated beside report."
            };
            var segments = new List<Segment>();
            var positions = new[] { new Vector3(87, 0.08f, -40), new Vector3(-65, 0.08f, 10), new Vector3(-69, 0.08f, 30), new Vector3(-29, 0.08f, 8.4f) };
            var targets = new[] { new Vector3(88, 1.3f, -36.9f), new Vector3(-65, 2, 25), new Vector3(-72, 1.2f, 34), new Vector3(-29, 1.3f, 10.5f) };
            var names = new[] { "seller", "yard", "workshop", "buyer-A", "buyer-menu-150" };
            for (var station = 0; station < names.Length; station++)
            {
                walk.SetPaused(false);
                var index = Mathf.Min(station, positions.Length - 1);
                var angles = Quaternion.LookRotation(targets[index] - (positions[index] + Vector3.up * 1.65f)).eulerAngles;
                player.RestorePose(positions[index], angles.y, Mathf.DeltaAngle(0, angles.x));
                Physics.SyncTransforms();
                if (station == 4)
                {
                    foreach (var point in trade.Points)
                        if (point.Kind == CityTradeKind.A) { trade.Open(point); break; }
                    trade.SetTextScale(1.5f);
                }
                yield return new WaitForSecondsRealtime(3);
                CaptureStation(names[station]);
                yield return null;
                var samples = new List<double>(4096);
                double main = 0, render = 0, drawCount = 0, garbage = 0;
                var end = Time.realtimeSinceStartupAsDouble + 8;
                while (Time.realtimeSinceStartupAsDouble < end)
                {
                    yield return null;
                    samples.Add(Time.unscaledDeltaTime * 1000.0);
                    if (mainThread.Valid) main += mainThread.LastValue / 1000000.0;
                    render += lastRenderSubmitMs;
                    if (draws.Valid) drawCount += lastDrawCalls;
                    if (gc.Valid) garbage += gc.LastValue;
                }
                samples.Sort(); double total = 0; foreach (var sample in samples) total += sample;
                if (drawCount <= 0) report.drawsAvailable = false;
                segments.Add(new Segment
                {
                    name = names[station], frames = samples.Count, meanMs = total / samples.Count,
                    p50Ms = samples[(int)((samples.Count - 1) * 0.50)], p95Ms = samples[(int)((samples.Count - 1) * 0.95)],
                    p99Ms = samples[(int)((samples.Count - 1) * 0.99)], maxMs = samples[samples.Count - 1],
                    mainThreadMeanMs = main / samples.Count, renderSubmitMeanMs = render / samples.Count,
                    drawCallsMean = drawCount > 0 ? drawCount / samples.Count : -1, gcBytesMean = garbage / samples.Count,
                    allocatedBytes = Profiler.GetTotalAllocatedMemoryLong(), reservedBytes = Profiler.GetTotalReservedMemoryLong(),
                    managedBytes = Profiler.GetMonoUsedSizeLong()
                });
            }
            report.segments = segments.ToArray();
            File.WriteAllText(Path.Combine(output, "profile.json"), JsonUtility.ToJson(report, true));
            Debug.Log("E5 PROFILE COMPLETE " + output);
            Application.Quit(0);
        }

        private void OnDestroy()
        {
            mainThread.Dispose(); draws.Dispose(); gc.Dispose();
            if (profileTexture != null) { profileTexture.Release(); Destroy(profileTexture); }
        }

        private void LateUpdate()
        {
            if (profileCamera != null && renderRequest != null)
            {
                var start = Time.realtimeSinceStartupAsDouble;
                RenderPipeline.SubmitRenderRequest(profileCamera, renderRequest);
                lastRenderSubmitMs = (Time.realtimeSinceStartupAsDouble - start) * 1000;
                if (draws.Valid) lastDrawCalls = draws.CurrentValue;
            }
        }

        private void CaptureStation(string name)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = profileTexture;
            var pixels = new Texture2D(profileTexture.width, profileTexture.height, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, pixels.width, pixels.height), 0, 0); pixels.Apply();
            File.WriteAllBytes(Path.Combine(output, name + ".png"), pixels.EncodeToPNG());
            Destroy(pixels); RenderTexture.active = previous;
        }
    }
}
