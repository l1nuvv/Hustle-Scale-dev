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
        private static bool commandsOnly;
        private ProfilerRecorder mainThread, draws, gc;
        private Camera profileCamera;
        private RenderTexture profileTexture;
        private RenderPipeline.StandardRequest renderRequest;
        private double lastRenderSubmitMs;
        private long lastDrawCalls;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            SavePath = null; output = null; commandsOnly = false;
            if (!Debug.isDebugBuild) return;
            foreach (var argument in Environment.GetCommandLineArgs())
            {
                if (argument == "-e5-command-profile") commandsOnly = true;
                if (argument.StartsWith("-e5-profile=", StringComparison.Ordinal)) output = Path.GetFullPath(argument.Substring(12));
            }
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

        [Serializable]
        private sealed class CommandSample
        {
            public string command;
            public double milliseconds;
            public long frameGcBytes;
        }

        [Serializable]
        private sealed class CommandReport
        {
            public string unity, conditions;
            public bool gcAvailable;
            public CommandSample[] samples;
        }

        private IEnumerator ProfileCommands(CityTradeView trade, CityGreyboxWalk walk, PlayerController player)
        {
            var samples = new List<CommandSample>();
            CityTradePoint seller = null;
            foreach (var point in trade.Points) if (point.Kind == CityTradeKind.Seller) { seller = point; break; }
            var save = trade.GetComponent<CitySaveController>();
            for (var run = 0; run < 9; run++)
            {
                trade.CloseMenu(); save.NewGame(); walk.SetPaused(false);
                var position = seller.transform.position;
                player.RestorePose(new Vector3(position.x, 0.08f, position.z - 1.5f), 0, 0);
                Physics.SyncTransforms(); trade.Open(seller);
                if (!trade.MenuOpen) throw new InvalidOperationException("Command profile seller is unreachable.");
                yield return null; yield return null;
                foreach (var command in new[] { "purchase", "purchase", "battery" })
                {
                    var refused = command == "purchase" && trade.Session.LotPurchased;
                    var start = Time.realtimeSinceStartupAsDouble;
                    var success = trade.Execute(command);
                    var duration = (Time.realtimeSinceStartupAsDouble - start) * 1000;
                    if (success == refused || !trade.Session.IsPaused) throw new InvalidOperationException("Command profile state regression.");
                    // Next frame reads the entire command frame, including deferred UI work and autosave.
                    yield return null;
                    if (run > 0) samples.Add(new CommandSample
                    {
                        command = refused ? "purchase-refused" : command,
                        milliseconds = duration, frameGcBytes = gc.Valid ? gc.LastValue : -1
                    });
                    yield return null;
                }
            }
            File.WriteAllText(Path.Combine(output, "commands.json"), JsonUtility.ToJson(new CommandReport
            {
                unity = Application.unityVersion, gcAvailable = gc.Valid, samples = samples.ToArray(),
                conditions = "Development Player; same offscreen URP target as station profile; 1 warmup + 8 repetitions. Synchronous Execute duration excludes deferred rendering/save. GC covers the entire command frame, not Execute alone. Isolated new game per repetition. No deep profiling."
            }, true));
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
            if (commandsOnly)
            {
                yield return ProfileCommands(trade, walk, player);
                Application.Quit(0); yield break;
            }
            var report = new Report
            {
                unity = Application.unityVersion, platform = Application.platform.ToString(), cpu = SystemInfo.processorType,
                gpu = SystemInfo.graphicsDeviceName, graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                ramMB = SystemInfo.systemMemorySize, gpuMB = SystemInfo.graphicsMemorySize, width = Screen.width, height = Screen.height,
                quality = QualitySettings.GetQualityLevel(), vSync = QualitySettings.vSyncCount, targetFps = Application.targetFrameRate,
                development = Debug.isDebugBuild, mainThreadAvailable = mainThread.Valid, drawsAvailable = draws.Valid, gcAvailable = gc.Valid,
                conditions = "Hidden development Player: explicit URP StandardRequest to screen-sized RenderTexture each frame; Canvas uses ScreenSpaceCamera for offscreen capture. Scripted camera stations; 3s warmup + 8s samples each; no deep profiling. Editor idle and open. Main Thread includes waits. This is an offscreen rendered workload, not presentation/display FPS. GPU time unavailable. Saves isolated beside report. Memory values are station-end snapshots, not process peaks."
            };
            var segments = new List<Segment>();
            var positions = new[] { new Vector3(87, 0.08f, -30), new Vector3(-62, 0.08f, 4), new Vector3(-69, 0.08f, 30), new Vector3(-28, 0.08f, 5), new Vector3(-28, 0.08f, 5), new Vector3(-38, 0.08f, 0), new Vector3(-38, 0.08f, 0) };
            var targets = new[] { new Vector3(88, 1.8f, -36.9f), new Vector3(-65, 1.2f, 18), new Vector3(-72, 1.2f, 34), new Vector3(-27.5f, 1.7f, 11), new Vector3(-27.5f, 1.7f, 11), new Vector3(-42, 2.4f, 3), new Vector3(-42, 2.4f, 3) };
            var names = new[] { "seller", "yard", "workshop", "buyer-A", "buyer-menu-150", "wayfinding", "pause-menu-150" };
            for (var station = 0; station < names.Length; station++)
            {
                trade.CloseMenu();
                trade.SetTextScale(station >= 4 ? 1.5f : 1f);
                walk.SetPaused(false);
                var index = Mathf.Min(station, positions.Length - 1);
                var angles = Quaternion.LookRotation(targets[index] - (positions[index] + Vector3.up * 1.65f)).eulerAngles;
                player.RestorePose(positions[index], angles.y, Mathf.DeltaAngle(0, angles.x));
                Physics.SyncTransforms();
                if (station == 4)
                {
                    foreach (var point in trade.Points)
                        if (point.Kind == CityTradeKind.A)
                        {
                            var feet = new Vector3(point.transform.position.x, 0.08f, point.transform.position.z - 2.1f);
                            var aim = Quaternion.LookRotation(point.GetComponent<Collider>().bounds.center - (feet + Vector3.up * 1.65f)).eulerAngles;
                            player.RestorePose(feet, aim.y, Mathf.DeltaAngle(0, aim.x));
                            Physics.SyncTransforms();
                            trade.Open(point);
                            break;
                        }
                    if (!trade.MenuOpen)
                    { Debug.LogError("E5 profile buyer menu did not open."); Application.Quit(3); yield break; }
                    trade.SetTextScale(1.5f);
                }
                if (station == 6) walk.SetPaused(true);
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
