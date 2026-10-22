using System;
using System.IO;
using System.Linq;
using Interactables;
using Inventory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Real deferred Destroy calls require Play Mode. The empty scene and prefab instances
// isolate legacy pickup checks from the city session and the player's save files.
[InitializeOnLoad]
public static class GameStateReliabilityVerification
{
    private const string Key = "HustleScale.PickupReliability";
    private const string Log = "Logs/Codex-pickup-reliability.log";
    private static bool failed, completed;
    private static int phase, checks, frame;
    private static double started;
    private static GameObject fixture;
    private static InventoryManager manager;
    private static ItemPickup pickup;
    private static Player.PlayerRaycast raycast;
    private static InventorySlot source;

    static GameStateReliabilityVerification() => EditorApplication.playModeStateChanged += State;

    private static CitySaveData CitySnapshot(WorkshopSession session) => session.CaptureCity(new CityWorldData
    {
        HasVehiclePose = true,
        PlacedLamps = session.Units.Where(u => u.Location == FlashlightLocation.World)
            .Select(u => new CityPlacedLamp { InstanceId = u.InstanceId }).ToArray()
    });

    [MenuItem("Tools/Quality/Verify City State Sequences")]
    public static void RunCitySequences()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        const string path = "Logs/Codex-city-state-sequences.log";
        Directory.CreateDirectory("Logs");
        File.WriteAllText(path, "City sequences Unity " + Application.unityVersion + " UTC " + DateTime.UtcNow.ToString("O") + "\n");
        var assertions = 0;
        Action<bool, string> check = (ok, context) =>
        {
            assertions++;
            if (!ok) throw new InvalidOperationException(context);
        };
        try
        {
            for (var seed = 0; seed < 32; seed++)
            {
                var random = new System.Random(seed);
                var session = WorkshopSession.CreateCity(seed);
                session.SetPaused(false);
                check(session.PurchaseLot(out _), "initial purchase");
                for (var step = 0; step < 256; step++)
                {
                    var context = "seed " + seed + " step " + step;
                    var before = JsonUtility.ToJson(CitySnapshot(session));
                    var lots = session.PurchasedLots;
                    var locations = session.Units.Select(u => u.Location).ToArray();
                    var index = random.Next(-1, 4);
                    var id = (DealOrderId)random.Next(3);
                    var ids = session.Units.Select(u => u.InstanceId).ToArray();
                    var group = random.Next(6);
                    var presented = group == 0 ? null : group == 1 ? new[] { ids[0], ids[0] } :
                        group == 2 ? new[] { ids[0], "unknown" } : group == 3 ? ids :
                        group == 4 ? new[] { ids[0], ids[2] } : new[] { ids[random.Next(3)] };
                    Func<bool>[] actions =
                    {
                        () => session.PurchaseLot(out _), () => session.BuyBattery(out _),
                        () => session.ToggleUnit(index, out _), () => session.ToggleCrate(out _),
                        () => session.ToggleBench(out _), () => session.Test(out _), () => session.Repair(out _),
                        () => session.TakeCrateContents(out _), () => session.StowHeld(out _),
                        () => session.EquipSlot(index, out _), () => session.PlaceHeld(out _),
                        () => session.PickUpPlaced(index, out _), () => session.TogglePower(index, out _),
                        () => session.LoadVehicleCrate(out _), () => session.UnloadVehicleCrate(out _),
                        () => session.AcceptOrder(id, out _), () => session.CloseOrder(id, out _),
                        () => session.DeliverOrder(id, presented, out _), () => session.SellBack(presented, out _)
                    };
                    var action = random.Next(actions.Length + 3);
                    if (action < actions.Length)
                    {
                        var changed = 0; var completed = 0;
                        Action onChanged = () => changed++;
                        Action onCompleted = () => completed++;
                        session.Changed += onChanged; session.TransactionCompleted += onCompleted;
                        bool success;
                        try { success = actions[action](); }
                        finally { session.Changed -= onChanged; session.TransactionCompleted -= onCompleted; }
                        check(success ? changed == 1 && completed == 1 : changed == 0 && completed == 0 &&
                            before == JsonUtility.ToJson(CitySnapshot(session)), context + " action " + action + " atomicity/events");
                    }
                    else if (action == actions.Length) session.SetPaused(!session.IsPaused);
                    else if (action == actions.Length + 1)
                    {
                        var time = session.ElapsedSeconds;
                        session.AdvanceActiveSeconds(random.Next(301) + 0.25);
                        check(!session.IsPaused || session.ElapsedSeconds == time, context + " paused clock");
                    }
                    else
                    {
                        var paused = session.IsPaused;
                        check(WorkshopSession.TryRestoreCity(CitySnapshot(session), out var restored, out var error), context + " reload " + error);
                        session = restored; session.SetPaused(paused);
                        // Saves preserve bought goods, not the RNG for a future lot. Seed that
                        // future roll in the test so the action trace stays reproducible.
                        S2Verification.SeedCity(session);
                    }
                    var snapshot = CitySnapshot(session);
                    check(WorkshopSession.TryRestoreCity(snapshot, out var candidate, out var message), context + " reachable save " + message);
                    check(JsonUtility.ToJson(snapshot) == JsonUtility.ToJson(CitySnapshot(candidate)), context + " exact snapshot");
                    check((long)session.InitialBalance + session.Revenue - session.Expenses == session.Balance && session.Balance >= 0,
                        context + " money conservation");
                    if (lots == session.PurchasedLots)
                        for (var i = 0; i < 3; i++)
                            if (locations[i] == FlashlightLocation.BuyerA || locations[i] == FlashlightLocation.BuyerB ||
                                locations[i] == FlashlightLocation.BoughtBack)
                                check(session.Units[i].Location == locations[i], context + " sold goods never return");
                }
                File.AppendAllText(path, "PASS seed " + seed + " (256 actions with per-action validation)\n");
            }
            File.AppendAllText(path, "CITY SEQUENCES PASS: " + assertions + " assertions\n");
        }
        catch (Exception exception)
        {
            File.AppendAllText(path, "CITY SEQUENCES FAIL: " + exception + "\n"); throw;
        }
    }

    [MenuItem("Tools/Quality/Verify Pickup Integrity in Play Mode")]
    public static void Run()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty ||
            UnityEngine.SceneManagement.SceneManager.sceneCount != 1 || string.IsNullOrEmpty(scene.path))
            throw new InvalidOperationException("Stop Play Mode and save the single open scene first.");
        SessionState.SetString(Key + ".Scene", scene.path);
        SessionState.SetBool(Key, true);
        Directory.CreateDirectory("Logs");
        File.WriteAllText(Log, "Pickup integrity Unity " + Application.unityVersion + " UTC " + DateTime.UtcNow.ToString("O") + "\n");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorApplication.isPlaying = true;
    }

    private static void State(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            failed = completed = false; checks = phase = 0; frame = -1;
            started = EditorApplication.timeSinceStartup;
            Application.logMessageReceived += Error;
            EditorApplication.update += Tick;
        }
        if (state != PlayModeStateChange.EnteredEditMode) return;
        Application.logMessageReceived -= Error;
        EditorApplication.update -= Tick;
        File.AppendAllText(Log, "PICKUP RESULT " + (failed || !completed ? "FAIL" : "PASS") + ": " + checks + " assertions\n");
        var scene = SessionState.GetString(Key + ".Scene", "");
        SessionState.EraseBool(Key); SessionState.EraseString(Key + ".Scene");
        if (scene.Length > 0) EditorSceneManager.OpenScene(scene);
    }

    private static void Check(bool value, string context)
    {
        checks++;
        File.AppendAllText(Log, (value ? "PASS " : "FAIL ") + context + "\n");
        if (!value) failed = true;
    }

    private static void Error(string message, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        failed = true;
        File.AppendAllText(Log, "ERROR " + message + "\n" + stack + "\n");
    }

    private static void CreateFixture(int amount)
    {
        fixture = new GameObject("Isolated pickup fixture");
        manager = fixture.AddComponent<InventoryManager>();
        manager.enabled = false; // Supply prefab slots explicitly instead of running the old scene UI setup.
        var slot = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Slot.prefab"), fixture.transform)
            .GetComponent<InventorySlot>();
        manager.slots.Add(slot);
        pickup = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/AppleRed.prefab"), fixture.transform)
            .GetComponent<ItemPickup>();
        pickup.GetComponent<Rigidbody>().isKinematic = true;
        if (amount > 0)
        {
            slot.item = pickup.item; slot.amount = amount; slot.IsEmpty = false;
        }
    }

    private static int Amount => manager.slots.Sum(s => s.amount);

    private static void CreateRayFixture(int amount)
    {
        CreateFixture(amount);
        pickup.gameObject.SetActive(false);
        var cameraObject = new GameObject("Pickup test camera", typeof(Camera));
        cameraObject.transform.SetParent(fixture.transform, false);
        cameraObject.tag = "MainCamera";
        var playerObject = new GameObject("Pickup test player");
        playerObject.transform.SetParent(fixture.transform, false);
        playerObject.layer = 2;
        raycast = playerObject.AddComponent<Player.PlayerRaycast>();
        raycast.Configure(cameraObject.GetComponent<Camera>(), Physics.DefaultRaycastLayers);
        var controller = playerObject.GetComponent<Player.Movement.PlayerController>();
        controller.enabled = false;
        controller.SetCursorLocked(true);
        playerObject.GetComponent<Player.PlayerInput>().enabled = false;
        raycast.enabled = false;
        var serialized = new SerializedObject(raycast);
        serialized.FindProperty("inventoryManager").objectReferenceValue = manager;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        source = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Slot.prefab"), fixture.transform)
            .GetComponent<InventorySlot>();
        source.transform.position = new Vector3(0, 0, 1.5f);
        source.gameObject.layer = 0;
        source.item = pickup.item; source.amount = 1; source.IsEmpty = false;
        source.gameObject.AddComponent<BoxCollider>().size = Vector3.one * 0.4f;
        Physics.SyncTransforms();
    }

    private static void Tick()
    {
        EditorApplication.QueuePlayerLoopUpdate();
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (EditorApplication.timeSinceStartup - started > 30) throw new TimeoutException("Pickup verification timeout.");
            switch (phase++)
            {
                case 0:
                    CreateFixture(10);
                    Check(!pickup.PickupItem(fixture) && Amount == 10, "full inventory rejects pickup without changing stock");
                    break;
                case 1:
                    Check(pickup != null && pickup.gameObject.activeSelf, "rejected pickup survives deferred Destroy frame");
                    if (pickup != null)
                    {
                        manager.slots[0].amount = 9;
                        Check(pickup.PickupItem(fixture) && Amount == 10, "rejected pickup can retry after freeing capacity");
                        Check(!pickup.PickupItem(fixture) && Amount == 10, "cached destination cannot accept duplicate retry");
                    }
                    UnityEngine.Object.Destroy(fixture);
                    break;
                case 2:
                    CreateFixture(0);
                    Check(pickup.PickupItem(fixture) && Amount == 1, "empty slot accepts exactly one item");
                    Check(!pickup.PickupItem(fixture) && Amount == 1, "same-frame second pickup cannot duplicate stock");
                    break;
                case 3:
                    Check(pickup == null && Amount == 1, "successful pickup destroys world instance once");
                    UnityEngine.Object.Destroy(fixture);
                    break;
                case 4:
                    CreateFixture(9);
                    Check(pickup.PickupItem(fixture) && Amount == 10, "exact stack capacity accepts item");
                    break;
                case 5:
                    UnityEngine.Object.Destroy(fixture);
                    break;
                case 6:
                    CreateFixture(0);
                    pickup.amount = -1;
                    Check(!pickup.PickupItem(fixture) && Amount == 0 && manager.slots[0].IsEmpty,
                        "negative pickup cannot mutate empty slot");
                    pickup.amount = 0;
                    Check(!pickup.PickupItem(fixture) && Amount == 0, "zero pickup rejected atomically");
                    pickup.amount = 11;
                    Check(!pickup.PickupItem(fixture) && Amount == 0, "oversized stack rejected atomically");
                    break;
                case 7:
                    Check(pickup != null && pickup.gameObject.activeSelf, "invalid quantities leave world instance available");
                    UnityEngine.Object.Destroy(fixture);
                    break;
                case 8:
                    CreateRayFixture(10);
                    raycast.SendMessage("HandleInteraction");
                    Check(Amount == 10 && source.gameObject.activeSelf && source.GetComponent<Collider>().enabled,
                        "raycast slot transfer rejects full destination without consuming source");
                    break;
                case 9:
                    Check(source != null && source.gameObject.activeSelf, "rejected raycast source survives next frame");
                    UnityEngine.Object.Destroy(fixture);
                    break;
                case 10:
                    CreateRayFixture(9);
                    raycast.SendMessage("HandleInteraction");
                    Check(Amount == 10 && !source.GetComponent<Collider>().enabled, "raycast slot transfer accepts once at capacity");
                    raycast.SendMessage("HandleInteraction");
                    Check(Amount == 10, "same-frame raycast repeat cannot duplicate stock");
                    break;
                case 11:
                    Check(source == null && Amount == 10, "successful raycast transfer destroys only source");
                    UnityEngine.Object.Destroy(fixture);
                    break;
                default:
                    completed = true;
                    EditorApplication.update -= Tick;
                    EditorApplication.isPlaying = false;
                    break;
            }
        }
        catch (Exception exception)
        {
            failed = true; File.AppendAllText(Log, "FAIL " + exception + "\n");
            if (fixture != null) UnityEngine.Object.Destroy(fixture);
            EditorApplication.update -= Tick; EditorApplication.isPlaying = false;
        }
    }
}
