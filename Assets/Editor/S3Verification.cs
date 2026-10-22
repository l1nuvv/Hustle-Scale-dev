using System;
using System.Collections;
using System.IO;
using System.Linq;
using Interactables;
using Inventory;
using Player;
using Player.Movement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

[InitializeOnLoad]
public static partial class S3Verification
{
    private const string Key = "HustleScale.S3Runtime";
    private const string Override = "HustleScale.S2SaveOverride";
    private static bool IsS4 => SessionState.GetBool(Key + ".S4", false);
    private static bool IsE5 => SessionState.GetBool(Key + ".E5", false);
    private static bool IsE57 => SessionState.GetBool(Key + ".E57", false);
    private static bool IsE6 => SessionState.GetBool(Key + ".E6", false);
    private static bool IsPhysics => SessionState.GetBool(Key + ".Physics", false);
    private static string Stage => IsPhysics ? "Physics" : IsE6 ? "E6" : IsE57 ? "E5.7" : IsE5 ? "E5" : IsS4 ? "S4" : "S3";
    private static string Log => "Logs/Codex-" + Stage + "-playmode.log";
    private static IEnumerator routine;
    private static int checks, frame;
    private static Keyboard keyboard;
    private static bool oldBackground;
    private static InputSettings.BackgroundBehavior oldInputBackground;
    private static InputSettings.EditorInputBehaviorInPlayMode oldEditorInput;
    private static CityTradeView trade;
    private static CitySaveController save;
    private static CityGreyboxWalk walk;
    private static PlayerController player;
    private static Camera eye;
    private static double started;
    private static Exception runtimeFailure;
    static S3Verification() => EditorApplication.playModeStateChanged += State;

    [MenuItem("Tools/S3/Verify City Trading in Play Mode")]
    public static void Run()
    { RunCityVerification(false); }

    [MenuItem("Tools/Physics/Verify Collision and Interaction")]
    public static void RunPhysics()
    {
        SessionState.SetBool(Key + ".Physics", true);
        try { RunCityVerification(false); }
        catch { SessionState.EraseBool(Key + ".Physics"); throw; }
    }

    private static void RunCityVerification(bool s4)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || UnityEngine.SceneManagement.SceneManager.sceneCount != 1 ||
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Stop Play Mode and save the single open scene first.");
        SessionState.SetString(Key + ".Scene", UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
        SessionState.SetBool(Key + ".S4", s4);
        var directory = Path.GetFullPath("Temp/" + Stage + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        SessionState.SetString(Override, Path.Combine(directory, "city-save.json"));
        SessionState.SetBool(Key, true);
        Directory.CreateDirectory("Logs"); File.WriteAllText(Log, Stage + " isolated Play Mode verification\n");
        EditorSceneManager.OpenScene("Assets/Scenes/CityGreybox.unity");
        EditorApplication.isPlaying = true;
    }
    private static void State(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            oldBackground = Application.runInBackground; Application.runInBackground = true;
            oldInputBackground = InputSystem.settings.backgroundBehavior;
            oldEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>(); checks = 0; frame = -1;
            runtimeFailure = null; Application.logMessageReceived += RuntimeError;
            started = EditorApplication.timeSinceStartup;
            routine = Scenario(); EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            if (IsE5) RestoreSliceResolution();
            Application.logMessageReceived -= RuntimeError;
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.backgroundBehavior = oldInputBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorInput;
            Application.runInBackground = oldBackground;
        }
        if (state != PlayModeStateChange.EnteredEditMode) return;
        EditorApplication.update -= Tick;
        var scene = SessionState.GetString(Key + ".Scene", "");
        SessionState.EraseBool(Key); SessionState.EraseString(Key + ".Scene"); SessionState.EraseString(Override);
        SessionState.EraseBool(Key + ".S4");
        SessionState.EraseBool(Key + ".E5");
        SessionState.EraseBool(Key + ".E57");
        SessionState.EraseBool(Key + ".E6");
        SessionState.EraseBool(Key + ".Physics");
        if (scene.Length > 0) EditorSceneManager.OpenScene(scene);
        // Keep the isolated fixture under Temp for failure diagnosis; never touch the user's save.
    }
    private static void Tick()
    {
        EditorApplication.QueuePlayerLoopUpdate();
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (runtimeFailure != null) throw runtimeFailure;
            Check(EditorApplication.timeSinceStartup - started < 120, "timeout");
            var running = routine.MoveNext();
            if (runtimeFailure != null) throw runtimeFailure;
            if (running) return;
            File.AppendAllText(Log, Stage + " RESULT PASS: " + checks + " assertions; Unity " + Application.unityVersion + "\n");
            Debug.Log(Stage + " RESULT PASS: " + checks);
            EditorApplication.update -= Tick; EditorApplication.isPlaying = false;
        }
        catch (Exception exception)
        {
            File.AppendAllText(Log, Stage + " RESULT FAIL: " + exception + "\n"); Debug.LogException(exception);
            EditorApplication.update -= Tick; EditorApplication.isPlaying = false;
        }
    }
    private static void RuntimeError(string message, string stack, LogType type)
    {
        if (runtimeFailure == null && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
            runtimeFailure = new InvalidOperationException("Unexpected Unity " + type + ": " + message + "\n" + stack);
    }
    private static void Check(bool value, string context)
    { checks++; if (!value) throw new InvalidOperationException(Stage + ": " + context); }
    private static CityTradePoint Point(CityTradeKind kind) => trade.Points.First(p => p.Kind == kind);
    private static void Aim(CityTradePoint point, Vector3 feet)
    {
        var target = point.GetComponent<Collider>().bounds.center;
        var delta = target - (feet + Vector3.up * 1.65f);
        var rotation = Quaternion.LookRotation(delta).eulerAngles;
        var pitch = rotation.x > 180 ? rotation.x - 360 : rotation.x;
        player.RestorePose(feet, rotation.y, pitch); Physics.SyncTransforms();
    }
    private static void Approach(CityTradeKind kind)
    {
        var point = Point(kind);
        var position = point.transform.position;
        var standOff = kind == CityTradeKind.A || kind == CityTradeKind.B ? 2.1f : 1.5f;
        Aim(point, new Vector3(position.x, 0.08f, position.z - standOff));
        if (!trade.Reachable(point))
        {
            var target = point.GetComponent<Collider>().bounds.center;
            Physics.Linecast(eye.transform.position, target, out var obstruction, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            File.AppendAllText(Log, "Reachability: " + kind + " eye " + eye.transform.position + " target " + target +
                " hit " + (obstruction.collider != null ? obstruction.collider.name : "none") + " distance " +
                Vector3.Distance(eye.transform.position, point.GetComponent<Collider>().ClosestPoint(eye.transform.position)) + "\n");
        }
        Check(trade.Reachable(point), "reachable " + kind);
        Check(Physics.Raycast(eye.transform.position, eye.transform.forward, out var hit, 2.5f, Physics.DefaultRaycastLayers) &&
            hit.collider.GetComponent<CityTradePoint>() == point, "nearest E hit " + kind);
    }
    private static void Open(CityTradeKind kind)
    {
        if (trade.MenuOpen) trade.CloseMenu();
        if (kind == CityTradeKind.Workshop)
        { walk.SetPaused(false); Approach(kind); return; }
        walk.SetPaused(false); Approach(kind); Point(kind).Interact(); Check(trade.MenuOpen, "open " + kind);
    }
    private static void Command(string command, int index = -1) => Check(
        command == "bench" || command == "test" || command == "repair" ?
        trade.WorkshopAction(Point(CityTradeKind.Workshop), command) : trade.Execute(command, index), "command " + command);

    private static void ToggleFixtureCrate(Vector3 position)
    {
        if (trade.MenuOpen) trade.CloseMenu();
        walk.SetPaused(false);
        if (!save.Session.IsCrateCarried)
        { Open(CityTradeKind.Crate); Command("crate"); trade.CloseMenu(); return; }
        player.RestorePose(new Vector3(position.x, 0.08f, position.z - 1.4f), 0, 45);
        Physics.SyncTransforms();
        Check(trade.DropHeld(), "drop carried crate on free surface");
    }
    private static void Reject(Func<bool> operation, string context)
    {
        save.Session.SetPaused(false);
        var before = JsonUtility.ToJson(save.Session.CaptureCity(save.World));
        var visible = Enumerable.Range(0, 3).Select(i => trade.Lamp(i).gameObject.activeSelf).ToArray();
        Check(!operation(), context + " rejected");
        Check(before == JsonUtility.ToJson(save.Session.CaptureCity(save.World)) &&
            visible.SequenceEqual(Enumerable.Range(0, 3).Select(i => trade.Lamp(i).gameObject.activeSelf)), context + " atomic model and visuals");
        save.Session.SetPaused(true);
    }
    private static void SelectOnly(params int[] indices)
    {
        for (var i = 0; i < 3; i++)
        {
            var index = i;
            var button = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).First(b =>
                b.gameObject.activeInHierarchy && b.GetComponentInChildren<Text>().text.StartsWith("[") &&
                b.GetComponentInChildren<Text>().text.Contains("Фонарь " + (index + 1)));
            var text = button.GetComponentInChildren<Text>().text;
            if (text.StartsWith("[x]") != indices.Contains(i)) button.onClick.Invoke();
        }
    }
    private static void WalkRoute(string name, params float[] coordinates)
    {
        if (trade.MenuOpen) trade.CloseMenu(); walk.SetPaused(false);
        player.RestorePose(new Vector3(coordinates[0], 0.08f, coordinates[1]), 0, 0);
        var capsule = player.GetComponent<CharacterController>();
        float length = 0;
        for (var i = 2; i < coordinates.Length; i += 2)
        {
            var start = new Vector3(coordinates[i - 2], 0, coordinates[i - 1]);
            var end = new Vector3(coordinates[i], 0, coordinates[i + 1]);
            var delta = end - start; length += delta.magnitude;
            for (var distance = 0f; distance < delta.magnitude; distance += 0.1f)
            {
                var yaw = Quaternion.LookRotation(delta).eulerAngles.y;
                player.transform.rotation = Quaternion.Euler(0, yaw, 0);
                capsule.Move(delta.normalized * Mathf.Min(0.1f, delta.magnitude - distance) + Vector3.down * 0.03f);
                // The rendered carried crate has no collision; validate its actual clearance too.
                if (save.Session.IsCrateCarried)
                {
                    Physics.SyncTransforms();
                    var center = player.transform.position + Vector3.up * 0.8f + delta.normalized * 1.1f;
                    Check(!Physics.CheckBox(center, new Vector3(0.625f, 0.175f, 0.325f),
                        Quaternion.Euler(0, yaw, 0), Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore), name + " crate clearance " + center);
                }
            }
            Check(Vector2.Distance(new Vector2(player.transform.position.x, player.transform.position.z),
                new Vector2(end.x, end.z)) < 0.2f, name + " capsule reaches " + end + " actual " + player.transform.position);
        }
        File.AppendAllText(Log, "PASS route " + name + ": " + length.ToString("F1") + "m; model " + (length / 4).ToString("F1") + "s at 4m/s (collision simulation, not human timing)\n");
    }
    private static void CheckLampPresentation(string context)
    {
        for (var i = 0; i < 3; i++)
        {
            var lamp = trade.Lamp(i);
            var unit = save.Session.Units[i];
            var collider = lamp.GetComponent<BoxCollider>();
            Check(lamp.GetComponentsInChildren<Collider>(true).Length == 1 &&
                collider.center == Vector3.zero && collider.size == new Vector3(0.3f, 0.48f, 0.26f),
                context + " one unchanged interaction collider " + i);
            var renderers = lamp.GetComponentsInChildren<MeshRenderer>(true);
            Check(renderers.Length > 1 && lamp.GetComponent<MeshRenderer>() == null,
                context + " detailed sample replaces cube " + i);
            // Check the actual mesh vertices, including handle/feet, against the physical envelope.
            foreach (var renderer in renderers)
                foreach (var vertex in renderer.GetComponent<MeshFilter>().sharedMesh.vertices)
                {
                    var local = lamp.InverseTransformPoint(renderer.transform.TransformPoint(vertex));
                    Check(Mathf.Abs(local.x) <= 0.151f && Mathf.Abs(local.y) <= 0.241f && Mathf.Abs(local.z) <= 0.131f,
                        context + " mesh fits interaction envelope " + i);
                }
            var beams = lamp.GetComponentsInChildren<Light>(true);
            var visible = unit.Location != FlashlightLocation.Pocket &&
                (WorkshopSession.IsOwned(unit) || unit.Location == FlashlightLocation.Seller);
            var powered = visible && unit.IsPowered && unit.Location != FlashlightLocation.Crate && unit.Location != FlashlightLocation.Seller;
            Check(lamp.gameObject.activeSelf == visible && beams.Length == 1 && beams[0].enabled == powered,
                context + " one beam follows item state " + i);
            Check(Vector3.Dot(beams[0].transform.forward, lamp.forward) > 0.999f,
                context + " beam points out of city lamp front " + i);
            var diffuser = renderers.Single(r => r.name == "Inset diffuser");
            var properties = new MaterialPropertyBlock(); diffuser.GetPropertyBlock(properties);
            Check((properties.GetColor("_EmissionColor").maxColorComponent > 0) == powered,
                context + " diffuser follows actual light without revealing test state " + i);
        }
    }

    private static void NewFixture()
    {
        save.NewGame();
        S2Verification.SeedCity(save.Session);
    }

    private static IEnumerator Scenario()
    {
        yield return null; yield return null; yield return null;
        trade = UnityEngine.Object.FindFirstObjectByType<CityTradeView>();
        save = trade.GetComponent<CitySaveController>(); walk = trade.GetComponent<CityGreyboxWalk>();
        player = trade.GetComponent<PlayerController>(); eye = player.GetComponentInChildren<Camera>();
        Check(save.Session != null && trade.Session == save.Session, "one shared session");
        if (IsPhysics)
        {
            var physics = CityPlayerVerification.PhysicsChecks(trade, keyboard, Check, text => File.AppendAllText(Log, text + "\n"));
            while (physics.MoveNext()) yield return physics.Current;
            yield break;
        }
        if (IsE6)
        {
            var expansion = ExpansionScenario();
            while (expansion.MoveNext()) yield return expansion.Current;
            yield break;
        }
        if (IsE57)
        {
            var persistence = SlicePersistence();
            while (persistence.MoveNext()) yield return persistence.Current;
            yield break;
        }
        CheckLampPresentation("seller");
        foreach (var kind in new[] { CityTradeKind.Seller, CityTradeKind.Battery, CityTradeKind.Buyback,
            CityTradeKind.A, CityTradeKind.B, CityTradeKind.Workshop }) Approach(kind);

        // E goes through the real Input System and PlayerRaycast; Escape has one owner.
        walk.SetPaused(false); Approach(CityTradeKind.A);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.E));
        yield return null; yield return null;
        Check(trade.MenuOpen && walk.IsPaused, "E opens modal");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
        var time = save.Session.ElapsedSeconds; yield return null;
        Check(time == save.Session.ElapsedSeconds, "modal freezes clock");
        Command("close"); trade.CloseMenu(); Open(CityTradeKind.B); Command("close"); trade.CloseMenu();
        var identities = save.Session.Units.Select(u => u.InstanceId).ToArray();
        player.RestorePose(new Vector3(-240, 0.08f, 192), 0, 0); yield return null;
        Check(save.Session.Balance == 300 && identities.SequenceEqual(save.Session.Units.Select(u => u.InstanceId)) &&
            !save.Session.ShiftEnded && player.CanInteract, "decline both and leave freely");

        NewFixture(); walk.SetPaused(false);
        Open(CityTradeKind.B); Command("accept"); trade.CloseMenu(); Open(CityTradeKind.A); Command("accept"); trade.CloseMenu();
        Open(CityTradeKind.Seller); Command("purchase");
        Check(!trade.Execute("purchase") && save.Session.Balance == 120, "purchase once"); trade.CloseMenu();
        Open(CityTradeKind.Crate); Command("crate"); trade.CloseMenu();
        Check(trade.Crate.parent == eye.transform, "physical crate carried");
        Check(save.Save(), "save carrying"); Check(save.Load(false), "load carrying"); walk.SetPaused(false);
        Check(trade.Session == save.Session && save.Session.IsCrateCarried && trade.Crate.parent == eye.transform, "restore carrying and rebind");
        WalkRoute("seller to workshop carrying", -58, 12, -60, 12, -60, 14, -65, 14, -65, 25, -69, 30);
        ToggleFixtureCrate(CityTradeView.WorkshopDock);
        Check(save.World.HasCratePose && Vector3.Distance(trade.Crate.position, save.World.CratePosition) < 0.01f, "free crate position persists without teleport");
        player.RestorePose(CityTradeView.SellerDock, 0, 0); Physics.SyncTransforms(); Point(CityTradeKind.Crate).Interact();
        Check(!trade.MenuOpen && !save.Session.IsCrateCarried, "remote crate cannot be taken");
        Open(CityTradeKind.Crate); Command("unit", 0); trade.CloseMenu();
        CheckLampPresentation("held");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.F)); yield return null; yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
        Check(save.Session.InHands.IsPowered && !save.Session.InHands.IsTested, "F lights held sample without diagnosing it");
        CheckLampPresentation("held powered");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.F)); yield return null; yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
        Check(!save.Session.InHands.IsPowered, "F switches sample off");
        Open(CityTradeKind.Workshop); Command("bench"); CheckLampPresentation("bench before test");
        Command("test"); CheckLampPresentation("bench after test"); Command("bench"); trade.CloseMenu();
        Open(CityTradeKind.Crate); Command("unit", 0); Command("unit", 1); trade.CloseMenu();
        Open(CityTradeKind.Workshop); Command("bench"); Command("test");
        Check(!save.Session.OnBench.LastTestPassed, "fault diagnosed"); trade.CloseMenu();
        WalkRoute("workshop to battery", -65, 25, -65, 0, -42, 0, -15, -7, 35, -7, 65, 0, 65, 24);
        Open(CityTradeKind.Battery); Command("battery"); trade.CloseMenu();
        WalkRoute("battery to workshop", 65, 24, 65, 0, 35, -7, -15, -7, -42, 0, -65, 0, -65, 25);
        Open(CityTradeKind.Workshop); Command("repair");
        Check(!save.Session.OnBench.IsTested && save.Session.RepairSeconds == 240, "repair requires retest");
        Command("test"); Command("bench"); trade.CloseMenu();
        Open(CityTradeKind.Crate); Command("unit", 1); Command("unit", 2); trade.CloseMenu();
        Open(CityTradeKind.Workshop); Command("bench"); Command("test"); Command("bench"); trade.CloseMenu();

        // Q places a real world lamp; save while placed, disturb it, then reload its pose and ID.
        player.RestorePose(new Vector3(-65, 0.08f, 25), 180, 60);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.Q)); yield return null; yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
        Check(save.Session.Units[2].Location == FlashlightLocation.World, "Q physical placement");
        CheckLampPresentation("placed");
        var pose = trade.Lamp(2).position; var identity = save.Session.Units[2].InstanceId;
        Check(save.Save() && save.World.PlacedLamps.Length == 1, "capture placed pose before write");
        trade.Lamp(2).position += Vector3.right * 5;
        Check(save.Load(false), "load placed lamp"); walk.SetPaused(false);
        Check(save.Session.Units[2].InstanceId == identity && Vector3.Distance(trade.Lamp(2).position, pose) < 0.001f &&
            trade.Lamp(2).parent.name == "City trade runtime", "restore world pose identity and parent");
        CheckLampPresentation("restored world pose");
        var placed = trade.Points.First(p => p.Kind == CityTradeKind.Lamp && p.Index == 2);
        Aim(placed, pose + new Vector3(0, -pose.y + 0.08f, -1)); placed.Interact();
        Check(save.Session.Units[2].Location == FlashlightLocation.Hands, "pick placed via world interaction");
        Open(CityTradeKind.Crate); Command("unit", 2); trade.CloseMenu();
        Open(CityTradeKind.B);
        Reject(() => save.Session.DeliverOrder(DealOrderId.B, new[] { save.Session.Units[0].InstanceId }, out _), "remote stored goods");
        trade.CloseMenu();
        Open(CityTradeKind.Crate); Command("crate"); trade.CloseMenu();
        WalkRoute("workshop to B carrying", -69, 30, -65, 25, -65, 0, -42, 0, -15, -7, -7, -7, -7, 8.4f);
        Open(CityTradeKind.B);
        Check(!trade.Execute("deliver") && save.Session.Balance == 70, "wrong selected quantity leaves money and goods");
        Reject(() => save.Session.DeliverOrder(DealOrderId.A, new[] { "unknown", save.Session.Units[0].InstanceId }, out _), "invalid identity");
        Reject(() => save.Session.DeliverOrder(DealOrderId.A, new[] { save.Session.Units[0].InstanceId, save.Session.Units[0].InstanceId }, out _), "duplicate identity");
        Reject(() => save.Session.DeliverOrder(DealOrderId.A, new[] { save.Session.Units[0].InstanceId }, out _), "partial delivery");
        player.RestorePose(new Vector3(-65, 0.08f, 25), 0, 0); Physics.SyncTransforms();
        Check(!trade.Execute("deliver") && save.Session.Balance == 70, "distant UI cannot settle trade");
        Approach(CityTradeKind.B); SelectOnly(0); Command("deliver");
        Check(save.Session.Balance == 195 && !trade.Lamp(0).gameObject.activeSelf, "B money and world removal");
        Check(!trade.Execute("deliver"), "B cannot pay twice"); trade.CloseMenu();
        WalkRoute("B to A carrying", -7, 8.4f, -7, 5, -29, 5, -29, 8.4f);
        Open(CityTradeKind.A); SelectOnly(1, 2); Command("deliver"); trade.CloseMenu();
        Check(save.Session.Balance == 455 && save.Session.OwnedLampCount == 0 && !save.Session.ShiftEnded && player.CanInteract, "B then A returns to free world");
        CheckLampPresentation("sold");
        Check(save.Save() && save.Load(false) && save.Session.Balance == 455, "paid state reload has no duplicate payout");

        // Independent route: only B, then checked faulty/working leftovers to buyback.
        NewFixture(); walk.SetPaused(false); Open(CityTradeKind.Seller); Command("purchase"); trade.CloseMenu();
        Open(CityTradeKind.Crate); Command("crate"); trade.CloseMenu();
        ToggleFixtureCrate(CityTradeView.WorkshopDock);
        for (var i = 0; i < 3; i++)
        {
            Open(CityTradeKind.Crate); Command("unit", i); trade.CloseMenu();
            Open(CityTradeKind.Workshop); Command("bench"); Command("test"); Command("bench"); trade.CloseMenu();
            Open(CityTradeKind.Crate); Command("unit", i); trade.CloseMenu();
        }
        Open(CityTradeKind.Crate); Command("crate"); trade.CloseMenu();
        Open(CityTradeKind.B); Command("accept"); SelectOnly(0); Command("deliver"); trade.CloseMenu();
        WalkRoute("workshop to buyback carrying", -65, 25, -65, 0, -80, 0, -80, -65, -65, -65, -65, -49);
        Open(CityTradeKind.Buyback); SelectOnly(1, 2); Command("sell");
        Check(save.Session.Balance == 340 && !trade.Lamp(1).gameObject.activeSelf && !trade.Lamp(2).gameObject.activeSelf, "buyback atomic visual removal");
        Check(!trade.Execute("sell") && save.Session.Balance == 340, "no repeated buyback"); trade.CloseMenu();

        NewFixture(); walk.SetPaused(false); Open(CityTradeKind.A); Command("accept"); trade.CloseMenu();
        save.Session.AdvanceActiveSeconds(400); Open(CityTradeKind.B); Command("accept"); trade.CloseMenu();
        save.Session.AdvanceActiveSeconds(140);
        Check(save.Session.Order(DealOrderId.A).State == DealOrderState.Failed && save.Session.Order(DealOrderId.B).State == DealOrderState.Accepted &&
            !save.Session.ShiftEnded, "one deadline expires without ending city or other order");
        Open(CityTradeKind.Battery); for (var i = 0; i < 6; i++) Command("battery");
        Check(!trade.Execute("battery") && save.Session.Balance == 0, "insufficient battery funds leaves balance"); trade.CloseMenu();
        Open(CityTradeKind.Seller); Check(!trade.Execute("purchase") && !save.Session.LotPurchased, "insufficient lot funds leaves stock");
        trade.CloseMenu();
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.Tab)); yield return null; yield return null;
        Check(trade.MenuOpen && walk.IsPaused, "Tab inventory modal");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.Escape)); yield return null; yield return null;
        Check(!trade.MenuOpen && !walk.IsPaused, "Esc closes only city modal");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return null;
        if (IsS4)
        {
            var freeRoutes = FreeRoutes();
            while (freeRoutes.MoveNext()) yield return freeRoutes.Current;
        }
        if (IsE5)
        {
            var slice = SliceScenario();
            while (slice.MoveNext()) yield return slice.Current;
        }
    }
}
