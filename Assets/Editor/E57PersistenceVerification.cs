using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Interactables;
using Inventory;
using Player;
using Player.Movement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Uses the existing isolated city harness; no player save or authored scene is changed.
public static partial class S3Verification
{
    [Serializable]
    private sealed class PersistenceEnvelope
    {
        public string Format, Payload, Checksum;
    }

    [MenuItem("Tools/E5/Verify Slice Persistence")]
    public static void RunSlicePersistence()
    {
        SessionState.SetBool(Key + ".E57", true);
        try { RunCityVerification(false); }
        catch { SessionState.EraseBool(Key + ".E57"); throw; }
    }

    private static void PersistenceBindings(string context, bool afterPhysics = false)
    {
        Check(trade.Session == save.Session && walk.IsPaused && save.Session.IsPaused && !player.CanInteract,
            context + " paused shared session");
        Check(UnityEngine.Object.FindObjectsByType<CityTradeView>(FindObjectsSortMode.None).Length == 1 &&
            SceneManager.GetActiveScene().GetRootGameObjects().Count(g => g.name == "City trade runtime") == 1,
            context + " one runtime composition");
        Check(trade.Points.Count(p => p.Kind == CityTradeKind.Lamp && p.Index < 3) == 3 &&
            save.Session.Units.Select(u => u.InstanceId).Distinct().Count() == 3, context + " exactly three identities/targets");
        var vehicle = UnityEngine.Object.FindAnyObjectByType<CityUtilityVehicle>();
        Check(Vector3.Distance(player.transform.position, save.World.PlayerPosition) < 0.001f &&
            Mathf.Abs(Mathf.DeltaAngle(player.transform.eulerAngles.y, save.World.PlayerYaw)) < 0.001f &&
            Mathf.Abs(player.CameraPitch - save.World.CameraPitch) < 0.001f, context + " player/camera pose; actual " +
            player.transform.position.ToString("F6") + "/" + player.transform.eulerAngles.y + "/" + player.CameraPitch +
            ", saved " + save.World.PlayerPosition.ToString("F6") + "/" + save.World.PlayerYaw + "/" + save.World.CameraPitch);
        // Startup runs several physics frames. Suspension may settle vertically; the saved pose itself is compared exactly.
        var vehicleDelta = vehicle.transform.position - save.World.VehiclePosition;
        Check(new Vector2(vehicleDelta.x, vehicleDelta.z).magnitude < 0.001f &&
            Mathf.Abs(vehicleDelta.y) < (afterPhysics ? 0.15f : 0.001f) &&
            Quaternion.Angle(vehicle.transform.rotation, Quaternion.Euler(save.World.VehicleEuler)) < (afterPhysics ? 1 : 0.01f),
            context + " vehicle pose; actual " + vehicle.transform.position.ToString("F6") + "/" + vehicle.transform.eulerAngles.ToString("F6") +
            ", saved " + save.World.VehiclePosition.ToString("F6") + "/" + save.World.VehicleEuler.ToString("F6"));
        if (save.Session.IsCrateInVehicle || save.Session.IsCrateCarried)
            Check(trade.Crate.parent == (save.Session.IsCrateInVehicle ? vehicle.CargoAnchor : eye.transform), context + " carried/cargo crate parent");
        else
            Check(Vector3.Distance(trade.Crate.position,
                save.World.HasCratePose ? save.World.CratePosition :
                save.World.CrateDock == CityCrateDock.Seller ? UnityEngine.Object.FindAnyObjectByType<CityTradeView>().SellerDock : UnityEngine.Object.FindAnyObjectByType<CityTradeView>().WorkshopDock) < 0.001f,
                context + " crate dock");
        if (save.Session.IsCrateInVehicle)
            Check(Vector3.Distance(trade.Crate.position, vehicle.CargoAnchor.position) < 0.001f, context + " cargo local pose");
        for (var i = 0; i < 3; i++)
        {
            var unit = save.Session.Units[i];
            var lamp = trade.Lamp(i);
            var visible = unit.Location != FlashlightLocation.Pocket &&
                (WorkshopSession.IsOwned(unit) || unit.Location == FlashlightLocation.Seller);
            Check(lamp.gameObject.activeSelf == visible, context + " visibility " + i);
            Check(lamp.GetComponent<Collider>().enabled == (unit.Location == FlashlightLocation.World || unit.Location == FlashlightLocation.Bench), context + " collider " + i);
            Check(lamp.GetComponentInChildren<Light>(true).enabled == (visible && unit.IsPowered &&
                unit.Location != FlashlightLocation.Crate && unit.Location != FlashlightLocation.Seller), context + " light " + i);
            if (unit.Location == FlashlightLocation.World)
            {
                var pose = save.World.PlacedLamps.Single(p => p.InstanceId == unit.InstanceId);
                Check(lamp.parent.name == "City trade runtime" && Vector3.Distance(lamp.position, pose.Position) < 0.001f &&
                    Mathf.Abs(Mathf.DeltaAngle(lamp.eulerAngles.y, pose.Yaw)) < 0.001f, context + " placed ID/pose " + i);
            }
            else if (visible)
                Check(unit.Location == FlashlightLocation.Hands ? lamp.parent == eye.transform :
                    unit.Location == FlashlightLocation.Bench ? lamp.parent == UnityEngine.Object.FindAnyObjectByType<CitySceneBindings>().Workshop.BenchItem : lamp.parent == trade.Crate,
                    context + " item parent " + i);
        }
    }

    private static string PersistenceRoundTrip(string context)
    {
        if (trade.MenuOpen) trade.CloseMenu();
        walk.SetPaused(true);
        Check(save.Save(), context + " save");
        var snapshot = JsonUtility.ToJson(save.Session.CaptureCity(save.World));
        for (var load = 0; load < 3; load++)
        {
            // Disturb scene objects independently of the model to prove rebinding restores them.
            foreach (var pose in save.World.PlacedLamps)
                trade.Lamp(Array.FindIndex(save.Session.Units.ToArray(), u => u.InstanceId == pose.InstanceId)).position += Vector3.right;
            Check(save.Load(false), context + " load " + load);
            Check(PersistenceMatches(snapshot), context + " entire snapshot unchanged");
            PersistenceBindings(context);
        }
        File.AppendAllText(Log, "PASS repeated restore: " + context + "\n");
        return snapshot;
    }

    private static bool PersistenceMatches(string snapshot)
    {
        var expected = JsonUtility.FromJson<CitySaveData>(snapshot);
        var actual = save.Session.CaptureCity(save.World);
        // JsonUtility may round the final double digit (e.g. 50.448617874853596 -> 50.4486178748536).
        // Allow at most one nanosecond in time fields; all money, IDs, states and poses remain exact.
        if (Math.Abs(expected.ElapsedSeconds - actual.ElapsedSeconds) > 1e-9) return false;
        actual.ElapsedSeconds = expected.ElapsedSeconds;
        for (var i = 0; i < expected.Orders.Length; i++)
        {
            if (Math.Abs(expected.Orders[i].DeadlineSeconds - actual.Orders[i].DeadlineSeconds) > 1e-9) return false;
            actual.Orders[i].DeadlineSeconds = expected.Orders[i].DeadlineSeconds;
        }
        return JsonUtility.ToJson(expected) == JsonUtility.ToJson(actual);
    }

    private static IEnumerator PersistenceRestart()
    {
        EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/CityGreybox.unity", new LoadSceneParameters(LoadSceneMode.Single));
        yield return null; yield return null; yield return null;
        trade = UnityEngine.Object.FindFirstObjectByType<CityTradeView>();
        save = trade.GetComponent<CitySaveController>(); walk = trade.GetComponent<CityGreyboxWalk>();
        player = trade.GetComponent<PlayerController>(); eye = player.GetComponentInChildren<Camera>();
    }

    private static Button PersistenceButton(string prefix) => GameObject.Find("City pause menu")
        .GetComponentsInChildren<Button>().Single(b => b.name.StartsWith(prefix, StringComparison.Ordinal));

    private static void PersistenceWriteInvalid(string path, string good)
    {
        var envelope = JsonUtility.FromJson<PersistenceEnvelope>(good);
        var data = JsonUtility.FromJson<CitySaveData>(envelope.Payload);
        data.Lamps[1].InstanceId = data.Lamps[0].InstanceId;
        envelope.Payload = JsonUtility.ToJson(data);
        using (var hash = SHA256.Create())
            envelope.Checksum = Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(envelope.Payload)));
        File.WriteAllText(path, JsonUtility.ToJson(envelope));
    }

    private static IEnumerator SlicePersistence()
    {
        var usability = CityUsability();
        while (usability.MoveNext()) yield return usability.Current;
        PersistenceRoundTrip("fresh city");
        S2Verification.SeedCity(save.Session);
        Open(CityTradeKind.Seller); Command("purchase"); Command("battery"); trade.CloseMenu();
        Open(CityTradeKind.A); Command("accept"); trade.CloseMenu();
        Open(CityTradeKind.Crate); Command("crate"); trade.CloseMenu();
        PersistenceRoundTrip("accepted order, purchase and carried crate");
        ToggleFixtureCrate(UnityEngine.Object.FindAnyObjectByType<CityTradeView>().WorkshopDock);
        Open(CityTradeKind.Crate); Command("unit", 0); trade.CloseMenu();
        walk.SetPaused(false); Check(save.Session.StowHeld(out _), "pocket lamp");
        Open(CityTradeKind.Crate); Command("unit", 2); trade.CloseMenu();
        walk.SetPaused(false); Check(save.Session.TogglePower(2, out _), "power held lamp");
        player.RestorePose(new Vector3(-65, 0.08f, 25), 180, 60);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.Q)); yield return null; yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
        Check(save.Session.Units[2].Location == FlashlightLocation.World, "real Q placement");
        Open(CityTradeKind.Crate); Command("unit", 1); trade.CloseMenu();
        Open(CityTradeKind.Workshop); Command("bench"); Command("test"); Command("repair"); trade.CloseMenu();
        var mixed = PersistenceRoundTrip("pocket + powered world lamp + repaired untested bench + accepted A");
        var restart = PersistenceRestart(); while (restart.MoveNext()) yield return restart.Current;
        Check(PersistenceMatches(mixed), "startup restores entire mixed snapshot without offline ticking");
        PersistenceBindings("mixed startup", true);
        var elapsed = save.Session.ElapsedSeconds; yield return null; yield return null;
        Check(elapsed == save.Session.ElapsedSeconds, "paused startup clock frozen");
        // Continue the restored repaired item and finish both orders through the integrated trade flow.
        Open(CityTradeKind.Workshop); Command("test"); Command("bench"); trade.CloseMenu();
        Open(CityTradeKind.Crate); Command("unit", 1); trade.CloseMenu();
        walk.SetPaused(false); Check(save.Session.EquipSlot(0, out _), "equip restored pocket lamp");
        Open(CityTradeKind.Workshop); Command("bench"); Command("test"); Command("bench"); trade.CloseMenu();
        Open(CityTradeKind.Crate); Command("unit", 0); trade.CloseMenu();
        walk.SetPaused(false); Check(save.Session.PickUpPlaced(2, out _), "retrieve restored world lamp");
        Open(CityTradeKind.Workshop); Command("bench"); Command("test"); Command("bench"); trade.CloseMenu();
        Open(CityTradeKind.Crate); Command("unit", 2); Command("crate"); trade.CloseMenu();
        Open(CityTradeKind.B); Command("accept"); SelectOnly(0); Command("deliver"); trade.CloseMenu();
        Open(CityTradeKind.A); SelectOnly(1, 2); Command("deliver"); trade.CloseMenu();
        var paid = PersistenceRoundTrip("A and B completed, repaired stock exhausted");
        Check(save.Session.Balance == 455 && save.Session.Revenue == 385 && save.Session.Expenses == 230,
            "restored exact accounting");
        Open(CityTradeKind.A); Reject(() => trade.Execute("deliver"), "restored A cannot pay twice"); trade.CloseMenu();
        Open(CityTradeKind.B); Reject(() => trade.Execute("deliver"), "restored B cannot pay twice"); trade.CloseMenu();
        Open(CityTradeKind.Seller); Check(save.Session.CanPurchaseLot && save.Session.PurchasedLots == 1,
            "restored first lot offers optional restock without charging"); trade.CloseMenu();
        // Backup/corruption tests run against the current integrated state and its physical objects.
        walk.SetPaused(true); Check(save.Save(), "save paid state after UI moves");
        paid = JsonUtility.ToJson(save.Session.CaptureCity(save.World));
        var path = SessionState.GetString(Override, "");
        var good = File.ReadAllText(path);
        File.WriteAllText(path + ".backup", good);
        var corruptions = new[] { "{truncated", good.Replace("Checksum", "BrokenChecksum"), "semantic" };
        foreach (var corruption in corruptions)
        {
            if (corruption == "semantic") PersistenceWriteInvalid(path, good);
            else File.WriteAllText(path, corruption);
            var broken = File.ReadAllText(path);
            var live = save.Session; var world = save.World;
            Check(!save.Load(false) && ReferenceEquals(live, save.Session) && ReferenceEquals(world, save.World), "corrupt load retains live references");
            Check(PersistenceMatches(paid) && File.ReadAllText(path) == broken &&
                File.ReadAllText(path + ".backup") == good, "corrupt load retains live values and files");
            PersistenceBindings("rejected " + corruption.Substring(0, Math.Min(12, corruption.Length)));
        }
        File.AppendAllText(Log, "PASS live rejection: truncated JSON, checksum mismatch, duplicate ID with valid checksum\n");
        restart = PersistenceRestart(); while (restart.MoveNext()) yield return restart.Current;
        Check(save.Session == null && trade.Session == null && walk.IsPaused && !player.CanInteract,
            "real corrupt startup preserves file and pauses without inventing a session");
        Check(!PersistenceButton("Продолжить").interactable && !PersistenceButton("Сохранить").interactable &&
            PersistenceButton("Резервная копия").interactable, "corrupt startup recovery buttons");
        Check(File.ReadAllText(path + ".backup") == good, "startup preserves good backup");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.F9)); yield return null; yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
        Check(save.Session == null && walk.IsPaused, "F9 corrupt retry stays in recovery");
        PersistenceButton("Резервная копия").onClick.Invoke(); yield return null; yield return null;
        Check(PersistenceMatches(paid), "recovery button restores paid backup exactly");
        PersistenceBindings("backup recovery");
        Check(save.Save() && File.ReadAllText(path + ".backup") == good, "saving recovered state preserves good backup");
        var liveRecovered = save.Session;
        var recoveredSnapshot = JsonUtility.ToJson(save.Session.CaptureCity(save.World));
        File.WriteAllText(path + ".backup", "broken backup");
        Check(!save.Load(true) && ReferenceEquals(liveRecovered, save.Session) &&
            PersistenceMatches(recoveredSnapshot), "bad backup also retains live state");
        File.WriteAllText(path + ".backup", good);
        File.AppendAllText(Log, "PASS real corrupt startup, F9 retry, explicit uGUI backup recovery and resave\n");
        // Missing primary and unusable backup must also offer recovery rather than silently overwrite data.
        File.Move(path, path + ".removed");
        restart = PersistenceRestart(); while (restart.MoveNext()) yield return restart.Current;
        Check(save.Session == null && walk.IsPaused && PersistenceButton("Резервная копия").interactable && !File.Exists(path),
            "backup-only startup requires explicit recovery");
        File.WriteAllText(path + ".backup", "broken backup");
        PersistenceButton("Резервная копия").onClick.Invoke(); yield return null; yield return null;
        Check(save.Session == null && walk.IsPaused && !File.Exists(path) && File.ReadAllText(path + ".backup") == "broken backup",
            "failed backup recovery retains files and no session");
        PersistenceButton("Новая игра").onClick.Invoke(); yield return null; yield return null;
        PersistenceButton("Назад").onClick.Invoke(); yield return null; yield return null;
        Check(save.Session == null && !File.Exists(path), "new game cancellation preserves failed startup");
        PersistenceButton("Новая игра").onClick.Invoke(); yield return null; yield return null;
        PersistenceButton("Начать новую игру").onClick.Invoke(); yield return null; yield return null;
        Check(save.Session != null && save.Session.Balance == 300 && !save.Session.LotPurchased && walk.IsPaused,
            "confirmed new game after failed startup");
        PersistenceRoundTrip("new game after recovery failure");
        // Cargo is also a persisted place; it must bind to the existing vehicle after repeated loads.
        walk.SetPaused(false); Check(save.Session.PurchaseLot(out _) && save.Session.ToggleCrate(out _) &&
            save.Session.LoadVehicleCrate(out _), "prepare vehicle cargo");
        var cargo = PersistenceRoundTrip("crate in vehicle");
        restart = PersistenceRestart(); while (restart.MoveNext()) yield return restart.Current;
        Check(PersistenceMatches(cargo), "cargo startup exact saved snapshot");
        PersistenceBindings("cargo startup", true);
        Check(save.Session.IsCrateInVehicle && trade.Crate.parent == UnityEngine.Object.FindAnyObjectByType<CityUtilityVehicle>().CargoAnchor,
            "cargo startup attaches to vehicle");
        var vehicle = UnityEngine.Object.FindAnyObjectByType<CityUtilityVehicle>();
        var body = vehicle.GetComponent<Rigidbody>();
        vehicle.transform.SetPositionAndRotation(new Vector3(0, 0.5f, 0), Quaternion.identity);
        body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero;
        Physics.SyncTransforms();
        var cargoPoint = Point(CityTradeKind.Cargo);
        Check(Vector3.Distance(vehicle.CargoGate.position, UnityEngine.Object.FindAnyObjectByType<CityTradeView>().SellerDock) > 2.5f &&
            Vector3.Distance(vehicle.CargoGate.position, UnityEngine.Object.FindAnyObjectByType<CityTradeView>().WorkshopDock) > 2.5f, "cargo test is away from both docks");
        walk.SetPaused(false);
        var rear = vehicle.CargoGate.position;
        Aim(cargoPoint, new Vector3(rear.x, 0.08f, rear.z - 5));
        cargoPoint.Interact();
        Check(save.Session.IsCrateInVehicle && !save.Session.IsCrateCarried, "remote cargo interaction rejected");
        Approach(CityTradeKind.Cargo);
        body.linearVelocity = Vector3.forward;
        cargoPoint.Interact();
        Check(save.Session.IsCrateInVehicle && !save.Session.IsCrateCarried, "moving vehicle unload rejected");
        body.linearVelocity = Vector3.zero;
        cargoPoint.Interact();
        Check(save.Session.IsCrateCarried && !save.Session.IsCrateInVehicle && trade.Crate.parent == eye.transform &&
            !trade.Crate.GetComponent<Collider>().enabled, "take crate directly from stopped vehicle away from docks");
        PersistenceRoundTrip("crate carried after vehicle unload away from docks");
        walk.SetPaused(false); Approach(CityTradeKind.Cargo); cargoPoint.Interact();
        Check(save.Session.IsCrateInVehicle && !save.Session.IsCrateCarried && trade.Crate.parent == vehicle.CargoAnchor,
            "reload carried crate into vehicle away from docks");
        File.AppendAllText(Log, "PASS backup-only startup, failed backup, new-game cancel/confirm, vehicle cargo startup\n");
    }

    private static IEnumerator CityUsability()
    {
        NewFixture(); walk.SetPaused(false);
        Check(!trade.Points.Any(point => point.Kind == CityTradeKind.SellerDock || point.Kind == CityTradeKind.WorkshopDock) &&
            GameObject.Find("Место для ящика") == null && GameObject.Find("Опора дока") == null, "no dock objects or interactions");
        foreach (var name in new[] { "Коробка запчастей", "Полка хранения мастерской", "Стойка хранения мастерской",
            "Складской контейнер двора", "Полка двора", "Запас материалов двора", "Прилавок фонарей", "Верстак", "Нагрузочный тестер" })
        {
            var objects = trade.Crate.parent.GetComponentsInChildren<Collider>().Where(collider => collider.name == name).ToArray();
            Check(objects.Length > 0 && objects.All(collider => collider.enabled && !collider.isTrigger), "solid world prop " + name);
        }
        player.RestorePose(new Vector3(-58.8f, 0.08f, 37), 0, 0); Physics.SyncTransforms();
        player.GetComponent<CharacterController>().Move(Vector3.forward * 3);
        Check(player.transform.position.z < 38.5f, "storage physically blocks player movement");
        Open(CityTradeKind.Seller); Command("purchase"); trade.CloseMenu();
        Open(CityTradeKind.Crate); Command("crate"); trade.CloseMenu();
        player.RestorePose(new Vector3(-65, 0.08f, 14), 90, 0); Physics.SyncTransforms();
        var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        blocker.transform.position = new Vector3(-64, 1, 14); blocker.transform.localScale = new Vector3(0.5f, 2, 3);
        Physics.SyncTransforms();
        Check(!trade.DropHeld() && save.Session.IsCrateCarried && !save.World.HasCratePose, "blocked crate drop preserves held state and pose");
        UnityEngine.Object.Destroy(blocker); yield return null; Physics.SyncTransforms();
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.Q)); yield return null; yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
        Check(!save.Session.IsCrateCarried && save.World.HasCratePose && !trade.MenuOpen && trade.Crate.GetComponent<Collider>().enabled,
            "real Q drops crate in front of player without aiming at floor");
        var pose = trade.Crate.position;
        PersistenceRoundTrip("free crate in courtyard");
        Check(Vector3.Distance(pose, trade.Crate.position) < 0.001f, "free crate pose restored exactly");
        var restart = PersistenceRestart(); while (restart.MoveNext()) yield return restart.Current;
        Check(Vector3.Distance(pose, trade.Crate.position) < 0.001f, "free crate pose survives scene restart");
        walk.SetPaused(false); Open(CityTradeKind.Crate); Command("contents"); trade.CloseMenu();
        Check(save.Session.Units.All(unit => unit.Location == FlashlightLocation.Pocket) && !save.Session.IsCrateCarried,
            "leave box and transfer contents to inventory");
        walk.SetPaused(false); Check(save.Session.EquipSlot(1, out _), "equip faulty lamp from inventory");
        Approach(CityTradeKind.Workshop);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.E)); yield return null; yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
        Check(!trade.MenuOpen && player.CanInteract && save.Session.OnBench == save.Session.Units[1], "E places lamp on actual workbench without modal");
        Approach(CityTradeKind.Workshop);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.T)); yield return null; yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
        Check(!trade.MenuOpen && save.Session.OnBench.IsTested && !save.Session.OnBench.LastTestPassed, "T tests without modal");
        ScreenCapture.CaptureScreenshot("Temp/CityUsability-workbench.png"); yield return null; yield return null;
        var before = save.Session.RepairSeconds;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.R)); yield return null; yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
        Check(save.Session.RepairSeconds == before && save.Session.OnBench.Condition == FlashlightCondition.BatteryFault, "R without spare battery is atomic");
        Open(CityTradeKind.Seller); Command("battery"); trade.CloseMenu(); Approach(CityTradeKind.Workshop);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.R)); yield return null; yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
        Check(!trade.MenuOpen && save.Session.RepairSeconds == before + 240 && save.Session.SpareBatteries == 0 &&
            !save.Session.OnBench.IsTested && save.Session.OnBench.Condition == FlashlightCondition.Working, "R replaces one battery and requires retest");
        Approach(CityTradeKind.Workshop);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.T)); yield return null; yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
        Check(save.Session.OnBench.LastTestPassed, "T retests repaired lamp");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.E)); yield return null; yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
        Check(!trade.MenuOpen && save.Session.InHands == save.Session.Units[1], "E takes lamp without modal");
        player.RestorePose(new Vector3(-65, 0.08f, 14), 180, 0); Physics.SyncTransforms();
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.Q)); yield return null; yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
        Check(save.Session.Units[1].Location == FlashlightLocation.World && save.Session.InHands == null, "Q drops lamp without aiming down");
        PersistenceRoundTrip("free crate and dropped repaired lamp with pocket goods");
        File.AppendAllText(Log, "PASS free crate, inventory transfer, actual E/T/R/Q and solid storage\n");
        NewFixture(); walk.SetPaused(false);
    }
}
