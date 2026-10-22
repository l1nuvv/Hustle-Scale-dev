using System;
using System.IO;
using System.Linq;
using Inventory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Player;
using Player.Movement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

[InitializeOnLoad]
public static class S2Verification
{
    private static int checks;
    private static string folder;
    private static int runtimeStep, runtimeTicks;
    private static string expectedRuntime;
    private static Keyboard runtimeKeyboard;
    private static InputSettings.BackgroundBehavior oldBackground;
    private static InputSettings.EditorInputBehaviorInPlayMode oldInputBehavior;
    private static bool oldRunInBackground;
    private static int runtimeFrame;
    private static double runtimeStarted;
    private const string RuntimeKey = "HustleScale.S2Runtime";
    private const string OverrideKey = "HustleScale.S2SaveOverride";
    private const string SceneKey = "HustleScale.S2OriginalScene";

    static S2Verification() => EditorApplication.playModeStateChanged += RuntimeState;

    [MenuItem("Tools/S2/Verify City Persistence in Play Mode")]
    public static void RunPlayMode()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || UnityEngine.SceneManagement.SceneManager.sceneCount != 1 ||
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Stop Play Mode and save the single open scene first.");
        SessionState.SetString(SceneKey, UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
        var directory = System.IO.Path.GetFullPath("Temp/S2Runtime-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        SessionState.SetString(OverrideKey, System.IO.Path.Combine(directory, "city-save.json"));
        SessionState.SetBool(RuntimeKey, true);
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/Codex-S2-playmode.log", "S2 Play Mode started; isolated save path\n");
        EditorSceneManager.OpenScene("Assets/Scenes/CityGreybox.unity");
        EditorApplication.isPlaying = true;
    }

    private static void RuntimeState(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(RuntimeKey, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            oldRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            oldBackground = InputSystem.settings.backgroundBehavior;
            oldInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            runtimeKeyboard = InputSystem.AddDevice<Keyboard>();
            runtimeFrame = -1;
            runtimeStarted = EditorApplication.timeSinceStartup;
            checks = 0; runtimeStep = 0; runtimeTicks = 0;
            EditorApplication.update += RuntimeTick;
        }
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            if (runtimeKeyboard != null && runtimeKeyboard.added) InputSystem.RemoveDevice(runtimeKeyboard);
            InputSystem.settings.backgroundBehavior = oldBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = oldInputBehavior;
            Application.runInBackground = oldRunInBackground;
        }
        if (state != PlayModeStateChange.EnteredEditMode) return;
        EditorApplication.update -= RuntimeTick;
        var original = SessionState.GetString(SceneKey, "");
        var directory = System.IO.Path.GetDirectoryName(SessionState.GetString(OverrideKey, ""));
        SessionState.EraseBool(RuntimeKey);
        SessionState.EraseString(OverrideKey);
        SessionState.EraseString(SceneKey);
        if (!string.IsNullOrEmpty(original)) EditorSceneManager.OpenScene(original);
        if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory)) Directory.Delete(directory, true);
    }

    private static void RuntimeTick()
    {
        EditorApplication.QueuePlayerLoopUpdate();
        if (!EditorApplication.isPlaying || ++runtimeTicks < 5 || runtimeFrame == Time.frameCount) return;
        runtimeFrame = Time.frameCount;
        try
        {
            Check(EditorApplication.timeSinceStartup - runtimeStarted < 30, "runtime timeout");
            var walk = UnityEngine.Object.FindAnyObjectByType<CityGreyboxWalk>();
            var controller = walk.GetComponent<CitySaveController>();
            var player = walk.GetComponent<PlayerController>();
            var store = new CitySaveStore(SessionState.GetString(OverrideKey, ""));
            if (runtimeStep == 0)
            {
                Check(walk.IsPaused && controller.Session.IsPaused && controller.Session.Balance == 300, "runtime starts paused new city");
                Check(store.Exists, "runtime initial save exists");
                walk.SetPaused(false);
                SeedCity(controller.Session);
                Must(controller.Session.PurchaseLot(out _));
                Must(controller.Session.ToggleUnit(1, out _));
                Must(controller.Session.ToggleBench(out _)); Must(controller.Session.Test(out _));
                Must(controller.Session.BuyBattery(out _)); Must(controller.Session.Repair(out _));
                walk.SetPaused(true);
                player.RestorePose(new Vector3(-65, 0.03f, 25), 73, -35);
                controller.RequestSave();
                runtimeStep++;
                return;
            }
            if (runtimeStep >= 3)
            {
                if (runtimeStep == 3)
                {
                    InputSystem.QueueStateEvent(runtimeKeyboard, new KeyboardState(Key.N));
                    ScreenCapture.CaptureScreenshot("Logs/S2-new-game-confirmation.png");
                }
                if (runtimeStep == 4)
                {
                    InputSystem.QueueStateEvent(runtimeKeyboard, new KeyboardState(Key.Escape));
                }
                if (runtimeStep == 5)
                {
                    Check(walk.IsPaused && controller.Session.Balance == 70, "Escape cancels new game without resetting state");
                    InputSystem.QueueStateEvent(runtimeKeyboard, new KeyboardState(Key.N));
                }
                if (runtimeStep == 6) InputSystem.QueueStateEvent(runtimeKeyboard, new KeyboardState(Key.Enter));
                if (runtimeStep == 7)
                {
                    Check(controller.Session.Balance == 300 && !controller.Session.LotPurchased && walk.IsPaused,
                        "keyboard confirms new game and pauses fresh state");
                    InputSystem.QueueStateEvent(runtimeKeyboard, new KeyboardState(Key.F5));
                }
                if (runtimeStep == 8)
                {
                    Check(controller.Message == "Город сохранён.", "keyboard F5 saves");
                    InputSystem.QueueStateEvent(runtimeKeyboard, new KeyboardState(Key.F9));
                }
                if (runtimeStep == 9)
                {
                    Check(controller.Session.Balance == 300 && walk.IsPaused && controller.Message.Contains("восстановлено"),
                        "keyboard F9 loads paused state");
                    ScreenCapture.CaptureScreenshot("Logs/S2-pause.png");
                    InputSystem.QueueStateEvent(runtimeKeyboard, new KeyboardState());
                }
                if (runtimeStep++ < 11) return;
                var result = "S2 PLAYMODE PASS: " + checks + " assertions; Unity " + Application.unityVersion;
                File.AppendAllText("Logs/Codex-S2-playmode.log", result + "\n");
                Debug.Log(result);
                EditorApplication.update -= RuntimeTick;
                EditorApplication.isPlaying = false;
                return;
            }
            if (runtimeStep == 1)
            {
                Must(store.TryLoad(false, out var data, out var saved, out _));
                Check(saved.Balance == 70 && saved.RepairSeconds == 240 && saved.OnBench != null && !saved.OnBench.IsTested,
                    "runtime transaction autosave has repaired bench lamp");
                Check(data.World.PlayerYaw == 73 && data.World.CameraPitch == -35, "autosave captures player camera");
                expectedRuntime = JsonUtility.ToJson(data);
                walk.SetPaused(false);
                Must(controller.Session.BuyBattery(out _));
                player.RestorePose(new Vector3(-60, 0.03f, 25), 10, 42);
                Must(controller.Load(false));
                Check(walk.IsPaused && controller.Session.IsPaused && !player.CanInteract, "runtime load pauses controls");
                Check(controller.Session.Balance == 70 && controller.Session.SpareBatteries == 0, "runtime load swaps full model");
                Check(Vector3.Distance(walk.transform.position, data.World.PlayerPosition) < 0.001f &&
                    Mathf.Abs(player.CameraPitch + 35) < 0.001f, "runtime restores position and internal camera pitch");
                Check(expectedRuntime == JsonUtility.ToJson(controller.Session.CaptureCity(controller.World)), "runtime exact restored values");
                runtimeStep++;
                return;
            }
            Check(controller.Session.ElapsedSeconds == JsonUtility.FromJson<CitySaveData>(expectedRuntime).ElapsedSeconds,
                "runtime paused time remains frozen across frames");
            var current = controller.Session;
            File.WriteAllText(store.Path, "broken");
            Check(!controller.Load(false) && ReferenceEquals(current, controller.Session), "runtime failed load retains live session");
            Check(controller.Message.Contains("Текущее состояние не изменено"), "runtime failure feedback visible");
            Check(controller.HasBackup, "runtime backup offered");
            Must(controller.Load(true));
            Check(walk.IsPaused && controller.Session.IsPaused, "runtime explicit backup loads paused");
            runtimeStep++;
        }
        catch (Exception exception)
        {
            File.AppendAllText("Logs/Codex-S2-playmode.log", "S2 PLAYMODE FAIL: " + exception + "\n");
            Debug.LogException(exception);
            EditorApplication.update -= RuntimeTick;
            EditorApplication.isPlaying = false;
        }
    }

    [MenuItem("Tools/S2/Verify City Save and Load")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        checks = 0;
        folder = System.IO.Path.GetFullPath("Temp/S2-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        Directory.CreateDirectory("Logs");
        try
        {
            VerifyRoundTrips();
            VerifyFractionalDeadline();
            VerifyTime();
            VerifyInvalidSnapshots();
            VerifyFiles();
            var result = "S2 RESULT PASS: " + checks + " assertions; Unity " + Application.unityVersion;
            File.WriteAllText("Logs/Codex-S2-verification.log", result + "\n");
            Debug.Log(result);
        }
        catch (Exception exception)
        {
            File.WriteAllText("Logs/Codex-S2-verification.log", "S2 RESULT FAIL: " + exception + "\n");
            throw;
        }
        finally { Directory.Delete(folder, true); }
    }

    private static void Check(bool value, string context)
    {
        checks++;
        if (!value) throw new InvalidOperationException("S2: " + context);
    }

    // Historical route fixtures explicitly retain W/F/W; live purchases use independent RNG.
    internal static void SeedCity(WorkshopSession session) => typeof(WorkshopSession)
        .GetField("cityRandom", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
        .SetValue(session, new System.Random(41));

    private static void Must(bool value) => Check(value, "fixture operation");
    private static WorkshopSession City()
    {
        var session = WorkshopSession.CreateCity(41);
        session.SetPaused(false);
        return session;
    }

    private static CityWorldData World(WorkshopSession session) => new CityWorldData
    {
        PlayerPosition = new Vector3(-65, 0.08f, 25), PlayerYaw = 180, CameraPitch = -24,
        CrateDock = session.HasTransportedCrate ? CityCrateDock.Workshop : CityCrateDock.Seller,
        PlacedLamps = session.Units.Where(unit => unit.Location == FlashlightLocation.World)
            .Select(unit => new CityPlacedLamp { InstanceId = unit.InstanceId, Position = new Vector3(-70, 1, 32), Yaw = 73 }).ToArray()
    };

    private static WorkshopSession RoundTrip(WorkshopSession session, string context)
    {
        var store = new CitySaveStore(System.IO.Path.Combine(folder, "round-trip.json"));
        var snapshot = session.CaptureCity(World(session));
        Check(store.TrySave(snapshot, out var message), context + " save: " + message);
        Check(store.TryLoad(false, out var loaded, out var restored, out message), context + " load: " + message);
        Check(restored.IsPaused && restored.IsCity && !restored.IsFirstDeal && !restored.ShiftEnded, context + " loaded paused city");
        Check(JsonUtility.ToJson(snapshot) == JsonUtility.ToJson(restored.CaptureCity(loaded.World)), context + " exact values/identities/poses");
        var time = restored.ElapsedSeconds;
        restored.AdvanceActiveSeconds(10000);
        Check(restored.ElapsedSeconds == time, context + " no offline/paused ticking");
        restored.SetPaused(false);
        if (!restored.LotPurchased) SeedCity(restored);
        return restored;
    }

    private static void Diagnose(WorkshopSession session, int index, bool repair = false)
    {
        Must(session.ToggleUnit(index, out _));
        Must(session.ToggleBench(out _));
        Must(session.Test(out _));
        if (repair)
        {
            Must(session.BuyBattery(out _)); Must(session.Repair(out _));
            Check(!session.OnBench.IsTested && !session.OnBench.LastTestPassed, "repair invalidates diagnosis");
            RoundTrip(session, "after repair before retest");
            Must(session.Test(out _));
        }
        Must(session.ToggleBench(out _));
        Must(session.ToggleUnit(index, out _));
    }

    private static void VerifyRoundTrips()
    {
        var session = RoundTrip(City(), "before purchase");
        Must(session.AcceptOrder(DealOrderId.B, out _));
        RoundTrip(session, "accepted before purchase");
        Must(session.PurchaseLot(out _));
        var balance = session.Balance;
        var loaded = RoundTrip(session, "after purchase");
        Check(!loaded.PurchaseLot(out _) && loaded.Balance == balance, "cannot buy lot twice after reload");
        Must(session.ToggleCrate(out _)); RoundTrip(session, "carried crate");
        Must(session.ToggleCrate(out _));
        Must(session.ToggleUnit(0, out _)); RoundTrip(session, "in hands");
        Must(session.StowHeld(out _)); RoundTrip(session, "in pocket");
        Must(session.EquipSlot(0, out _));
        Must(session.PlaceHeld(out _)); RoundTrip(session, "in world with pose");
        Must(session.PickUpPlaced(0, out _));
        Must(session.ToggleBench(out _)); RoundTrip(session, "on bench");
        Must(session.Test(out _)); RoundTrip(session, "powered tested bench");
        Must(session.ToggleBench(out _));
        var id = session.InHands.InstanceId;
        var before = session.Balance;
        Check(!session.DeliverOrder(DealOrderId.B, new[] { id, id }, out _) && session.Balance == before, "duplicate IDs reject atomically");
        Must(session.DeliverOrder(DealOrderId.B, new[] { id }, out _));
        loaded = RoundTrip(session, "after B sale");
        Check(!loaded.DeliverOrder(DealOrderId.B, new[] { id }, out _) && loaded.Balance == session.Balance, "no duplicate payout after reload");
        Check(!loaded.AcceptOrder(DealOrderId.RepeatB, out _) && !loaded.EndShift(false, out _), "city has no repeat/shift end");
        Diagnose(session, 1, true); Diagnose(session, 2);
        Must(session.AcceptOrder(DealOrderId.A, out _));
        Must(session.ToggleCrate(out _));
        Must(session.DeliverOrder(DealOrderId.A, new[] { session.Units[1].InstanceId, session.Units[2].InstanceId }, out _));
        RoundTrip(session, "B then A repaired stock exhausted");

        session = City(); Must(session.PurchaseLot(out _));
        for (var i = 0; i < 3; i++) Diagnose(session, i);
        Must(session.ToggleCrate(out _));
        var ids = session.Units.Select(unit => unit.InstanceId).ToArray();
        Must(session.SellBack(ids, out _));
        loaded = RoundTrip(session, "early buyback all stock");
        Check(!loaded.SellBack(ids, out _) && loaded.Balance == session.Balance, "no duplicate buyback after reload");
        Check(session.Balance == 275 && session.OwnedLampCount == 0, "buyback accounting");
    }

    private static void VerifyFractionalDeadline()
    {
        var session = City();
        session.AdvanceActiveSeconds(240.37627213913947);
        Must(session.AcceptOrder(DealOrderId.B, out _));
        var store = new CitySaveStore(System.IO.Path.Combine(folder, "fractional-deadline.json"));
        Must(store.TrySave(session.CaptureCity(World(session)), out _));
        Check(store.TryLoad(false, out var data, out var restored, out var message), "fractional accepted deadline survives JSON: " + message);
        Check(Math.Abs(session.ElapsedSeconds - restored.ElapsedSeconds) < 1e-9 &&
            Math.Abs(session.Order(DealOrderId.B).DeadlineSeconds - restored.Order(DealOrderId.B).DeadlineSeconds) < 1e-9,
            "fractional time precision");
        data.Orders[1].DeadlineSeconds += 0.001;
        Check(!WorkshopSession.TryRestoreCity(data, out _, out _), "meaningful deadline extension still rejected");
        restored.SetPaused(false);
        restored.AdvanceActiveSeconds(restored.Order(DealOrderId.B).DeadlineSeconds - restored.ElapsedSeconds);
        Check(restored.Order(DealOrderId.B).State == DealOrderState.Failed, "restored fractional deadline expires at exact boundary");
    }

    private static void VerifyTime()
    {
        var session = City();
        session.AdvanceActiveSeconds(2000);
        Check(session.Order(DealOrderId.A).State == DealOrderState.Available, "unaccepted offer does not expire");
        Must(session.AcceptOrder(DealOrderId.A, out _));
        Check(session.Order(DealOrderId.A).DeadlineSeconds == 2540, "relative deadline from acceptance");
        session.AdvanceActiveSeconds(539.999);
        var loaded = RoundTrip(session, "just before deadline");
        loaded.AdvanceActiveSeconds(0.001);
        Check(loaded.Order(DealOrderId.A).State == DealOrderState.Failed, "exact deadline fails");
        Check(loaded.Order(DealOrderId.B).State == DealOrderState.Available && !loaded.ShiftEnded, "other offer/world remain available");
        RoundTrip(loaded, "after failed deadline");
        Must(loaded.AcceptOrder(DealOrderId.B, out _)); Must(loaded.CloseOrder(DealOrderId.B, out _));
        RoundTrip(loaded, "cancelled accepted offer");
        session = City(); Must(session.CloseOrder(DealOrderId.A, out _));
        RoundTrip(session, "declined offer");
        session = City(); Must(session.PurchaseLot(out _)); Must(session.AcceptOrder(DealOrderId.A, out _));
        session.AdvanceActiveSeconds(301);
        Diagnose(session, 1, true);
        Check(session.Order(DealOrderId.A).State == DealOrderState.Failed && session.ElapsedSeconds == 541, "repair crosses deadline");
        RoundTrip(session, "repair deadline consequence");
    }

    private static void VerifyInvalidSnapshots()
    {
        var session = City(); Must(session.PurchaseLot(out _));
        Must(session.ToggleUnit(0, out _)); Must(session.PlaceHeld(out _));
        var original = session.CaptureCity(World(session));
        Action<CitySaveData>[] corruptions =
        {
            data => data.Version = 6,
            data => data.Lamps[1].InstanceId = data.Lamps[0].InstanceId,
            data => data.Lamps[0].Location = (FlashlightLocation)999,
            data => data.Lamps[0].LastTestPassed = true,
            data => data.Lamps[1].IsPowered = true,
            data => data.Lamps[0].Location = FlashlightLocation.BuyerB,
            data => data.Balance++, data => data.SpareBatteries++,
            data => data.ElapsedSeconds = double.NaN,
            data => data.World.PlayerPosition = new Vector3(900, 0, 0),
            data => data.World.CameraPitch = 90,
            data => data.World.CrateDock = (CityCrateDock)999,
            data => data.World.PlacedLamps = Array.Empty<CityPlacedLamp>(),
            data => data.World.PlacedLamps[0].InstanceId = "unknown",
            data => data.World.PlacedLamps[0].Position = new Vector3(0, float.PositiveInfinity, 0),
            data => data.Orders[0].Id = DealOrderId.B,
            data => data.Orders[0].State = DealOrderState.Completed,
            data => data.Orders[0].State = DealOrderState.Accepted,
            data => data.Lamps = null, data => data.World = null
        };
        foreach (var corrupt in corruptions)
        {
            var candidate = JsonUtility.FromJson<CitySaveData>(JsonUtility.ToJson(original));
            corrupt(candidate);
            Check(!WorkshopSession.TryRestoreCity(candidate, out var restored, out _) && restored == null, "invalid snapshot rejected wholly");
            Check(JsonUtility.ToJson(original) == JsonUtility.ToJson(session.CaptureCity(World(session))), "live session unchanged after rejection");
        }
        Check(!WorkshopSession.TryRestoreCity(null, out _, out _), "null snapshot rejected");
    }

    private static void VerifyFiles()
    {
        var store = new CitySaveStore(System.IO.Path.Combine(folder, "files.json"));
        Check(!store.TryLoad(false, out _, out _, out _), "missing file");
        var session = City();
        Must(store.TrySave(session.CaptureCity(World(session)), out _));
        var first = File.ReadAllText(store.Path);
        Must(session.PurchaseLot(out _));
        Must(store.TrySave(session.CaptureCity(World(session)), out _));
        Check(File.ReadAllText(store.Path + ".backup") == first, "previous good save retained");
        var good = File.ReadAllText(store.Path);
        File.WriteAllText(store.Path, "{truncated");
        Check(!store.TryLoad(false, out var invalid, out var missing, out _) && invalid == null && missing == null, "truncated file never partially loads");
        Must(store.TryLoad(true, out _, out var previous, out _));
        Check(!previous.LotPurchased && previous.Balance == 300, "explicit backup restores previous snapshot");
        Must(store.TrySave(session.CaptureCity(World(session)), out _));
        Check(File.ReadAllText(store.Path + ".backup") == first, "recovery preserves known-good backup");
        File.WriteAllText(store.Path + ".tmp", "interrupted write");
        Must(store.TryLoad(false, out _, out _, out _));
        using (new FileStream(store.Path, FileMode.Open, FileAccess.Read, FileShare.None))
            Check(!store.TrySave(session.CaptureCity(World(session)), out _), "locked destination reports write failure");
        Check(File.ReadAllText(store.Path) == good, "failed write preserves primary");
        File.WriteAllText(store.Path, good.Replace("Checksum", "BrokenChecksum"));
        Check(!store.TryLoad(false, out _, out _, out _), "checksum failure rejects file");
        File.WriteAllText(store.Path, new string('x', 256 * 1024 + 1));
        Check(!store.TryLoad(false, out _, out _, out _), "oversized file rejects");
        var blocked = System.IO.Path.Combine(folder, "not-a-directory");
        File.WriteAllText(blocked, "block");
        var blockedStore = new CitySaveStore(System.IO.Path.Combine(blocked, "save.json"));
        Check(!blockedStore.TrySave(session.CaptureCity(World(session)), out _), "directory failure reports error");
    }
}
