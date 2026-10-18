using System;
using System.IO;
using System.Linq;
using Interactables;
using Inventory;
using Player.Movement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

[InitializeOnLoad]
public static class T04Verification
{
    private const string LogPath = "Logs/Codex-T04-verification.log";
    private const string RunningKey = "T04.running";
    private static WorkshopView workshop;
    private static FirstDealView deal;
    private static PlayerController player;
    private static Transform[] lampVisuals;
    private static Keyboard keyboard;
    private static InputSettings previousSettings;
    private static InputSettings testSettings;
    private static bool previousBackground;
    private static bool failed;
    private static bool completed;
    private static int phase;
    private static int lastFrame;
    private static float phaseTime;
    private static double started;
    private static double pausedAt;
    private static int waypoint;
    private static float walkStarted;
    private static readonly Vector3[] WalkRoute =
    {
        new Vector3(6, 0, -1.5f), new Vector3(0, 0, -1.5f),
        new Vector3(0, 0, 2), new Vector3(-2.5f, 0, 2),
        new Vector3(0, 0, 2), new Vector3(0, 0, -1.5f), new Vector3(0, 0, -4),
        new Vector3(0, 0, -1.5f), new Vector3(0, 0, 2), new Vector3(-2.5f, 0, 2),
        new Vector3(0, 0, 2), new Vector3(0, 0, -4.4f), new Vector3(-7, 0, -4.4f),
        new Vector3(-7, 0, -3.2f), new Vector3(-7, 0, -4.4f), new Vector3(6, 0, -4.4f),
        new Vector3(6, 0, -3.2f)
    };

    static T04Verification() => EditorApplication.playModeStateChanged += PlayState;

    [MenuItem("Tools/T04/Verify Trade")]
    public static void Run()
    {
        if (EditorApplication.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Stop Play Mode and save the current scene first.");
        Directory.CreateDirectory("Logs");
        File.WriteAllText(LogPath, "T04 Unity " + Application.unityVersion + "\n");
        VerifyDomain();
        EditorSceneManager.OpenScene(FirstDealTradeBuilder.ScenePath);
        VerifyReferences();
        SessionState.SetBool(RunningKey, true);
        EditorApplication.isPlaying = true;
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
        File.AppendAllText(LogPath, "PASS " + message + "\n");
    }

    private static string Snapshot(WorkshopSession session) => session.Balance + ":" + session.SpareBatteries + ":" +
        session.RepairSeconds + ":" + session.ElapsedSeconds + ":" + session.IsCrateCarried + ":" +
        string.Join(";", session.Units.Select(u => u.InstanceId + u.Location + u.Condition + u.IsTested + u.LastTestPassed)) +
        string.Join(";", Enum.GetValues(typeof(DealOrderId)).Cast<DealOrderId>().Select(id => session.Order(id).State));

    private static void RejectUnchanged(WorkshopSession session, Func<bool> attempt, string reason)
    {
        var before = Snapshot(session);
        Check(!attempt() && before == Snapshot(session), reason);
    }

    private static void TestUnit(WorkshopSession session, int index, bool repair = false)
    {
        if (!session.ToggleUnit(index, out _) || !session.ToggleBench(out _) || !session.Test(out _))
            throw new Exception("Could not prepare lamp " + index);
        if (repair && (!session.BuyBattery(out _) || !session.Repair(out _) || !session.Test(out _)))
            throw new Exception("Could not repair lamp " + index);
        if (!session.ToggleBench(out _) || !session.ToggleUnit(index, out _)) throw new Exception("Could not store lamp");
    }

    private static WorkshopSession Purchased()
    {
        var session = WorkshopSession.CreateFirstDeal();
        session.StartShift();
        if (!session.PurchaseLot(out _)) throw new Exception("Purchase failed");
        return session;
    }

    private static string[] Ids(WorkshopSession session, params int[] indices) => indices.Select(i => session.Units[i].InstanceId).ToArray();

    private static void VerifyDomain()
    {
        var session = WorkshopSession.CreateFirstDeal();
        Check(session.Balance == 300 && session.Units.All(u => u.Location == FlashlightLocation.Seller), "start $300 and three seller-owned identities");
        Check(session.Units.Select(u => u.InstanceId).Distinct().Count() == 3 && session.Units.All(u => !u.IsTested), "three distinct untested lamps");
        RejectUnchanged(session, () => session.ToggleUnit(0, out _), "cannot take seller stock before purchase");
        Check(session.AcceptOrder(DealOrderId.A, out _) && session.AcceptOrder(DealOrderId.B, out _) && session.Balance == 300 &&
            session.Units.All(u => u.Location == FlashlightLocation.Seller), "simultaneous acceptance before purchase does not reserve or mutate stock");
        RejectUnchanged(session, () => session.AcceptOrder(DealOrderId.RepeatB, out _), "repeat B initially locked");
        session.AdvanceActiveSeconds(60);
        Check(session.ElapsedSeconds == 0, "clock does not advance before first exit");
        session.StartShift(); session.AdvanceActiveSeconds(10); session.StartShift();
        Check(session.ElapsedSeconds == 10, "one-time shift start does not reset clock");
        session.SetPaused(true); session.AdvanceActiveSeconds(500);
        Check(session.ElapsedSeconds == 10, "reading and pause freeze clock");
        RejectUnchanged(session, () => session.PurchaseLot(out _), "paused purchase rejected without mutation");
        session.SetPaused(false);
        Check(session.PurchaseLot(out _) && session.Balance == 120 && session.Units.All(u => u.Location == FlashlightLocation.Crate), "single lot atomically costs $180 and transfers all three");
        RejectUnchanged(session, () => session.PurchaseLot(out _), "repeat purchase cannot create stock");
        var poor = new WorkshopSession(179, true);
        RejectUnchanged(poor, () => poor.PurchaseLot(out _), "lot lower boundary $179");
        var exact = new WorkshopSession(180, true);
        Check(exact.PurchaseLot(out _) && exact.Balance == 0, "lot exact boundary $180");
        RejectUnchanged(exact, () => exact.BuyBattery(out _), "insufficient battery money cannot go negative");
        var batteryFirst = WorkshopSession.CreateFirstDeal();
        Check(batteryFirst.BuyBattery(out _) && batteryFirst.Balance == 250 && batteryFirst.SpareBatteries == 1 &&
            batteryFirst.PurchaseLot(out _) && batteryFirst.Balance == 70, "battery can be purchased before lot and stays in stock");
        session.ToggleCrate(out _);
        RejectUnchanged(session, () => session.DeliverOrder(DealOrderId.A, Ids(session, 0), out _), "partial A rejects all resources");
        RejectUnchanged(session, () => session.DeliverOrder(DealOrderId.A, Ids(session, 0, 0), out _), "duplicate identities rejected atomically");
        RejectUnchanged(session, () => session.DeliverOrder(DealOrderId.A, Ids(session, 0, 2), out _), "untested working lamps rejected atomically");
        session.ToggleCrate(out _);
        TestUnit(session, 0); TestUnit(session, 1); TestUnit(session, 2);
        RejectUnchanged(session, () => session.DeliverOrder(DealOrderId.A, Ids(session, 0, 2), out _), "remote stored crate cannot deliver");
        session.ToggleCrate(out _);
        RejectUnchanged(session, () => session.DeliverOrder(DealOrderId.A, Ids(session, 0, 1), out _), "faulty member rejects entire A transfer");
        RejectUnchanged(session, () => session.DeliverOrder(DealOrderId.A, new[] { "unknown", session.Units[0].InstanceId }, out _), "unknown identity rejected");
        Check(session.DeliverOrder(DealOrderId.A, Ids(session, 0, 2), out _) && session.Balance == 380 &&
            session.Units.Count(u => u.Location == FlashlightLocation.BuyerA) == 2, "A route ends $380 with two transferred identities");
        RejectUnchanged(session, () => session.DeliverOrder(DealOrderId.A, Ids(session, 0, 2), out _), "completed A cannot pay twice");
        RejectUnchanged(session, () => session.DeliverOrder(DealOrderId.B, Ids(session, 0), out _), "sold identity cannot be sold to B");

        var combined = Purchased();
        TestUnit(combined, 0); TestUnit(combined, 2);
        combined.ToggleUnit(1, out _); combined.ToggleBench(out _); combined.Test(out _); combined.BuyBattery(out _);
        combined.AcceptOrder(DealOrderId.A, out _); combined.AcceptOrder(DealOrderId.B, out _);
        Check(combined.Repair(out _) && combined.ElapsedSeconds == 240 && combined.Balance == 70 && combined.SpareBatteries == 0, "repair shares wallet and adds exactly 240 shift seconds");
        RejectUnchanged(combined, () => combined.Repair(out _), "repeat repair cannot consume money time or battery");
        combined.ToggleBench(out _);
        RejectUnchanged(combined, () => combined.DeliverOrder(DealOrderId.B, Ids(combined, 1), out _), "repaired but untested lamp rejected");
        combined.ToggleBench(out _); combined.Test(out _); combined.ToggleBench(out _); combined.ToggleUnit(1, out _);
        combined.ToggleCrate(out _);
        Check(combined.DeliverOrder(DealOrderId.A, Ids(combined, 0, 2), out _) && combined.DeliverOrder(DealOrderId.B, Ids(combined, 1), out _) &&
            combined.Balance == 455 && combined.Units.All(u => u.Location == FlashlightLocation.BuyerA || u.Location == FlashlightLocation.BuyerB), "A plus B with repair ends $455 and no owned stock");
        Check(combined.Order(DealOrderId.RepeatB).State == DealOrderState.Available, "B success opens exactly one repeat offer");
        combined.AcceptOrder(DealOrderId.RepeatB, out _);
        RejectUnchanged(combined, () => combined.DeliverOrder(DealOrderId.RepeatB, Ids(combined, 1), out _), "repeat offer cannot reuse sold B lamp");
        var b = Purchased(); TestUnit(b, 0); TestUnit(b, 2); b.ToggleCrate(out _);
        RejectUnchanged(b, () => b.DeliverOrder(DealOrderId.B, Ids(b, 0), out _), "unaccepted order cannot pay");
        b.AcceptOrder(DealOrderId.B, out _);
        Check(b.DeliverOrder(DealOrderId.B, Ids(b, 0), out _) && b.Balance == 245, "B initial route ends $245");
        b.AcceptOrder(DealOrderId.RepeatB, out _);
        Check(b.DeliverOrder(DealOrderId.RepeatB, Ids(b, 2), out _) && b.Balance == 355, "B repeat route ends $355");
        RejectUnchanged(b, () => b.AcceptOrder(DealOrderId.RepeatB, out _), "repeat B cannot reopen");
        RejectUnchanged(b, () => b.DeliverOrder(DealOrderId.B, Ids(b, 2), out _), "initial B cannot pay twice");
        Check(b.CloseOrder(DealOrderId.A, out _) && b.Order(DealOrderId.A).State == DealOrderState.Declined, "unaccepted A decline recorded");
        RejectUnchanged(b, () => b.AcceptOrder(DealOrderId.A, out _), "declined order stays closed");
        var cancelled = Purchased(); cancelled.AcceptOrder(DealOrderId.A, out _);
        Check(cancelled.CloseOrder(DealOrderId.A, out _) && cancelled.Order(DealOrderId.A).State == DealOrderState.Cancelled, "cancelled commitment recorded");

        foreach (var id in new[] { DealOrderId.A, DealOrderId.B, DealOrderId.RepeatB })
        {
            var boundary = Purchased(); TestUnit(boundary, 0); TestUnit(boundary, 2); boundary.ToggleCrate(out _);
            if (id == DealOrderId.RepeatB) { boundary.AcceptOrder(DealOrderId.B, out _); boundary.DeliverOrder(DealOrderId.B, Ids(boundary, 0), out _); }
            boundary.AcceptOrder(id, out _);
            boundary.AdvanceActiveSeconds(boundary.Order(id).DeadlineSeconds - 0.001);
            var presented = id == DealOrderId.A ? Ids(boundary, 0, 2) : Ids(boundary, 2);
            Check(boundary.DeliverOrder(id, presented, out _), id + " delivery strictly before boundary succeeds");
            var late = Purchased(); TestUnit(late, 0); TestUnit(late, 2); late.ToggleCrate(out _);
            if (id == DealOrderId.RepeatB) { late.AcceptOrder(DealOrderId.B, out _); late.DeliverOrder(DealOrderId.B, Ids(late, 0), out _); }
            late.AcceptOrder(id, out _); late.AdvanceActiveSeconds(late.Order(id).DeadlineSeconds);
            Check(late.Order(id).State == DealOrderState.Failed, id + " accepted order fails at exact deadline");
            RejectUnchanged(late, () => late.DeliverOrder(id, id == DealOrderId.A ? Ids(late, 0, 2) : Ids(late, 2), out _), id + " exact deadline cannot pay or remove stock");
            RejectUnchanged(late, () => late.AcceptOrder(id, out _), id + " expired acceptance rejected");
        }
        var expiry = Purchased(); expiry.AdvanceActiveSeconds(540);
        Check(expiry.Order(DealOrderId.A).State == DealOrderState.Expired && expiry.Order(DealOrderId.B).State == DealOrderState.Available, "unaccepted offers expire independently");
        expiry.AcceptOrder(DealOrderId.B, out _); expiry.AdvanceActiveSeconds(540);
        Check(expiry.Order(DealOrderId.B).State == DealOrderState.Failed && expiry.Order(DealOrderId.RepeatB).State == DealOrderState.Locked, "B failure never unlocks repeat B");
        var jump = Purchased(); jump.AcceptOrder(DealOrderId.A, out _); jump.AdvanceActiveSeconds(300);
        TestUnit(jump, 1, true);
        Check(jump.ElapsedSeconds == 540 && jump.Order(DealOrderId.A).State == DealOrderState.Failed, "repair jump expires accepted order at strict boundary");
        foreach (var invalid in new[] { -1d, double.NaN, double.PositiveInfinity })
        {
            var threw = false;
            try { jump.AdvanceActiveSeconds(invalid); } catch (ArgumentOutOfRangeException) { threw = true; }
            Check(threw && jump.ElapsedSeconds == 540, "invalid clock delta rejected " + invalid);
        }
    }

    private static void VerifyReferences()
    {
        var missing = 0;
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        foreach (var transform in root.GetComponentsInChildren<Transform>(true))
        {
            missing += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
            foreach (var component in transform.GetComponents<MonoBehaviour>())
            {
                if (component == null) continue;
                var serialized = new SerializedObject(component);
                var property = serialized.GetIterator();
                while (property.NextVisible(true))
                    if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == null &&
                        !property.objectReferenceEntityIdValue.Equals(default(EntityId)))
                        missing++;
            }
        }
        Check(missing == 0, "trade scene has no missing scripts or broken serialized references");
    }

    private static void PlayState(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(RunningKey, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            failed = completed = false; phase = 0; lastFrame = -1; phaseTime = Time.time;
            previousBackground = Application.runInBackground; Application.runInBackground = true;
            previousSettings = InputSystem.settings;
            if (previousSettings.hideFlags == HideFlags.HideAndDontSave) previousSettings = UnityEngine.Object.Instantiate(previousSettings);
            testSettings = UnityEngine.Object.Instantiate(previousSettings);
            testSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            testSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = testSettings;
            started = EditorApplication.timeSinceStartup;
            Application.logMessageReceived += Error;
            EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            EditorApplication.update -= Tick; Application.runInBackground = previousBackground;
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            InputSystem.settings = previousSettings;
            if (testSettings != null) UnityEngine.Object.DestroyImmediate(testSettings);
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            Application.logMessageReceived -= Error; SessionState.SetBool(RunningKey, false);
            var result = completed && !failed ? "T04 RESULT PASS" : "T04 RESULT FAIL";
            File.AppendAllText(LogPath, result + "\n"); Debug.Log(result);
        }
    }

    private static void Error(string message, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        failed = true; File.AppendAllText(LogPath, "ERROR " + message + "\n" + stack + "\n");
    }

    private static void Next() { phase++; phaseTime = Time.time; }

    private static void AimAt(string name, Vector3 from)
    {
        player.SetMovementEnabled(true); player.SetCameraControlEnabled(true); player.SetCursorLocked(true);
        var body = player.GetComponent<CharacterController>(); body.enabled = false;
        player.transform.position = from; body.enabled = true;
        var direction = GameObject.Find(name).transform.position - (from + Vector3.up * 1.65f);
        var rotation = Quaternion.LookRotation(direction).eulerAngles;
        player.transform.rotation = Quaternion.Euler(0, rotation.y, 0);
        typeof(PlayerController).GetField("_pitch", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .SetValue(player, Mathf.DeltaAngle(0, rotation.x));
        Camera.main.transform.SetPositionAndRotation(from + Vector3.up * 1.65f, Quaternion.LookRotation(direction));
        Physics.SyncTransforms();
    }

    private static void Tick()
    {
        EditorApplication.QueuePlayerLoopUpdate();
        if (EditorApplication.timeSinceStartup - started > 90)
        { failed = true; File.AppendAllText(LogPath, "FAIL Play Mode timeout\n"); EditorApplication.isPlaying = false; return; }
        if (!EditorApplication.isPlaying || lastFrame == Time.frameCount || Time.time - phaseTime < 0.25f) return;
        lastFrame = Time.frameCount;
        try
        {
            switch (phase)
            {
                case 0:
                    workshop = UnityEngine.Object.FindAnyObjectByType<WorkshopView>();
                    deal = UnityEngine.Object.FindAnyObjectByType<FirstDealView>();
                    player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
                    lampVisuals = Enumerable.Range(1, 3).Select(i => GameObject.Find("Lamp " + i).transform).ToArray();
                    player.GetComponent<Player.PlayerInput>().MouseInputEnabled = false;
                    keyboard = InputSystem.AddDevice<Keyboard>("T04Keyboard");
                    deal.SendMessage("OnApplicationFocus", true);
                    Check(workshop.Session.Balance == 300 && !workshop.Session.ShiftStarted, "scene initializes at $300 inside workshop before clock starts");
                    AimAt("OFFERS BOARD", new Vector3(-4, 0.05f, 4.5f));
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E)); Next(); break;
                case 1:
                    var boardRay = new Ray(Camera.main.transform.position, Camera.main.transform.forward);
                    File.AppendAllText(LogPath, "INFO board E enabled=" + keyboard.enabled + " added=" + keyboard.added +
                        " key=" + keyboard.eKey.isPressed + " focus=" + Application.isFocused + " canInteract=" + player.CanInteract +
                        " menu=" + deal.MenuOpen + " paused=" + workshop.Session.IsPaused +
                        " prompt=" + player.GetComponent<Player.PlayerRaycast>().FocusLabel + " ray=" + boardRay +
                        " hit=" + (Physics.Raycast(boardRay, out var boardHit, 2.5f, Physics.DefaultRaycastLayers) ? boardHit.collider.name : "none") +
                        " inputMode=" + InputSystem.settings.updateMode + "\n");
                    Check(deal.MenuOpen && workshop.Session.IsPaused && !player.CanInteract, "E opens fullscreen offers and blocks gameplay");
                    GameObject.Find("Accept RepeatB").GetComponent<Button>().onClick.Invoke();
                    Check(GameObject.Find("Terms").GetComponent<Text>().text.Contains("This offer is not available"), "locked-offer refusal appears inside the paused terms screen");
                    ScreenCapture.CaptureScreenshot("Logs/T04-offers.png");
                    pausedAt = workshop.Session.ElapsedSeconds;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState()); Next(); break;
                case 2:
                    Check(workshop.Session.ElapsedSeconds == pausedAt, "fullscreen offer clock remains frozen across frames");
                    GameObject.Find("Accept A").GetComponent<Button>().onClick.Invoke();
                    GameObject.Find("Accept B").GetComponent<Button>().onClick.Invoke();
                    Check(workshop.Session.Order(DealOrderId.A).State == DealOrderState.Accepted && workshop.Session.Order(DealOrderId.B).State == DealOrderState.Accepted,
                        "serialized UI accepts concurrent A and B before purchase");
                    deal.CloseMenu();
                    AimAt("SELLER | LOT $180 / BATTERY $50", new Vector3(0, 0.05f, -4));
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E)); Next(); break;
                case 3:
                    Check(deal.MenuOpen && workshop.Session.ShiftStarted, "first workshop exit starts shift and seller E opens confirmation");
                    GameObject.Find("Confirm lot - $180").GetComponent<Button>().onClick.Invoke();
                    Check(workshop.Session.Balance == 120 && workshop.Session.LotPurchased && !deal.MenuOpen, "seller confirmation buys one lot using shared wallet");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    AimAt("Crate visual", new Vector3(-1.8f, 0.05f, -3.7f)); Next(); break;
                case 4:
                    var ray = new Ray(Camera.main.transform.position, Camera.main.transform.forward);
                    File.AppendAllText(LogPath, "INFO crate E ray " + ray + " hit " +
                        (Physics.Raycast(ray, out var crateHit, 2.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide) ? crateHit.collider.name : "nothing") +
                        " canInteract=" + player.CanInteract + " menu=" + deal.MenuOpen + " paused=" + workshop.Session.IsPaused +
                        " crate=" + GameObject.Find("Carry crate").transform.position + " player=" + player.transform.position + "\n");
                    ScreenCapture.CaptureScreenshot("Logs/T04-crate.png");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E)); Next(); break;
                case 5:
                    File.AppendAllText(LogPath, "INFO crate E prompt=" + player.GetComponent<Player.PlayerRaycast>().FocusLabel +
                        " keyboard=" + keyboard.eKey.isPressed + " hud=" + GameObject.Find("Workshop state").GetComponent<Text>().text + "\n");
                    Check(workshop.Session.IsCrateCarried && GameObject.Find("Carry crate").transform.parent == Camera.main.transform,
                        "E physically picks seller crate up with all lamps");
                    Check(workshop.Session.ElapsedSeconds > 0, "active real frame time advances the shared shift clock");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    AimAt("Carry / dock crate", new Vector3(-2.5f, 0.05f, 1.3f)); Next(); break;
                case 6:
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E)); Next(); break;
                case 7:
                    Check(!workshop.Session.IsCrateCarried && Vector3.Distance(GameObject.Find("Carry crate").transform.position, new Vector3(-2.5f, 0.7f, 3.5f)) < 0.01f,
                        "E docks carried crate at workshop without replacing identities");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    TestUnit(workshop.Session, 0); TestUnit(workshop.Session, 2); TestUnit(workshop.Session, 1, true);
                    workshop.Session.ToggleCrate(out _);
                    AimAt("A | 2 TESTED WORKING | $260 | BEFORE 09:00", new Vector3(-7, 0.05f, -3.2f)); Next(); break;
                case 8:
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E)); Next(); break;
                case 9:
                    Check(workshop.Session.Balance == 330 && workshop.Session.Order(DealOrderId.A).State == DealOrderState.Completed,
                        "A E transfers two carried tested lamps once after $50 repair");
                    Check(workshop.Session.Units.Count(u => u.Location == FlashlightLocation.BuyerA) == 2,
                        "A removes exactly two physical owned lamps");
                    Check(Enumerable.Range(0, 3).Where(i => workshop.Session.Units[i].Location == FlashlightLocation.BuyerA)
                        .All(i => !lampVisuals[i].gameObject.activeSelf), "sold lamp visuals disappear");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    AimAt("B | 1 TESTED WORKING | $125 | BEFORE 18:00", new Vector3(6, 0.05f, -3.2f)); Next(); break;
                case 10:
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E)); Next(); break;
                case 11:
                    Check(workshop.Session.Balance == 455 && workshop.Session.Order(DealOrderId.B).State == DealOrderState.Completed &&
                        workshop.Session.Order(DealOrderId.RepeatB).State == DealOrderState.Available, "B E completes combined route at $455 and opens repeat offer");
                    Check(GameObject.Find("Workshop state").GetComponent<Text>().text.Contains("$455"), "HUD receives updated trade wallet");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState()); Next(); break;
                case 12:
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E)); Next(); break;
                case 13:
                    Check(workshop.Session.Balance == 455, "repeat E at completed buyer cannot duplicate payout");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); Next(); break;
                case 14:
                    Check(deal.MenuOpen && workshop.Session.IsPaused && !player.CanInteract, "Escape opens pause and locks world actions");
                    pausedAt = workshop.Session.ElapsedSeconds;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState()); Next(); break;
                case 15:
                    Check(workshop.Session.ElapsedSeconds == pausedAt, "pause freezes running shift");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); Next(); break;
                case 16:
                    Check(!deal.MenuOpen && !workshop.Session.IsPaused && player.CanInteract, "Escape resumes from pause with cursor and world interaction restored");
                    Check(workshop.Session.ElapsedSeconds > pausedAt, "clock advances again after pause closes");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    waypoint = 0; walkStarted = Time.time; Next(); break;
                case 17:
                    var offset = WalkRoute[waypoint] - player.transform.position; offset.y = 0;
                    if (offset.magnitude < 0.35f)
                    {
                        waypoint++;
                        if (waypoint == WalkRoute.Length)
                        {
                            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                            Check(Time.time - walkStarted < 60 && workshop.Session.ElapsedSeconds < 540,
                                "walk at 4m/s traverses passage, seller, workshop, A and B with collision; route plus repair stays below 09:00");
                            File.AppendAllText(LogPath, "INFO physical route seconds " + (Time.time - walkStarted).ToString("F2") + "\n");
                            completed = true; EditorApplication.isPlaying = false; break;
                        }
                        offset = WalkRoute[waypoint] - player.transform.position; offset.y = 0;
                    }
                    player.transform.rotation = Quaternion.LookRotation(offset);
                    player.SetCursorLocked(true);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                    if (Time.time - walkStarted > 60) throw new Exception("Walking route blocked near waypoint " + waypoint + " at " + player.transform.position);
                    break;
            }
        }
        catch (Exception exception)
        { failed = true; File.AppendAllText(LogPath, "FAIL " + exception + "\n"); EditorApplication.isPlaying = false; }
    }
}
