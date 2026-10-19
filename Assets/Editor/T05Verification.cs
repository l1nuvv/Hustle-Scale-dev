using System;
using System.IO;
using System.Linq;
using Interactables;
using Inventory;
using Player.Movement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

[InitializeOnLoad]
public static class T05Verification
{
    private const string LogPath = "Logs/Codex-T05-verification.log";
    private const string RunningKey = "T05.running";
    private static WorkshopView workshop;
    private static FirstDealView deal;
    private static PlayerController player;
    private static Keyboard keyboard;
    private static InputSettings settings;
    private static InputSettings.BackgroundBehavior background;
    private static InputSettings.EditorInputBehaviorInPlayMode inputBehavior;
    private static bool runInBackground, failed, completed;
    private static int phase, frame;
    private static double started;
    private static float phaseTime;
    private static Transform[] visuals;
    private static Light[] indicators;
    private static double frozen;

    static T05Verification() => EditorApplication.playModeStateChanged += PlayState;

    [MenuItem("Tools/T05/Verify Outcomes")]
    public static void Run()
    {
        if (EditorApplication.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Stop Play Mode and save the scene first.");
        Directory.CreateDirectory("Logs");
        File.WriteAllText(LogPath, "T05 Unity " + Application.unityVersion + "\n");
        VerifyDomain();
        EditorSceneManager.OpenScene(FirstDealTradeBuilder.ScenePath);
        var missing = 0;
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                missing += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
        Check(missing == 0, "trade scene has no missing scripts");
        var view = UnityEngine.Object.FindAnyObjectByType<FirstDealView>();
        var so = new SerializedObject(view);
        foreach (var field in new[] { "outcomeButtons", "finishButton", "restartButton", "summaryButton" })
            Check(so.FindProperty(field).objectReferenceValue != null, "serialized outcome reference: " + field);
        SessionState.SetBool(RunningKey, true);
        EditorApplication.isPlaying = true;
    }

    private static void Check(bool ok, string name)
    {
        if (!ok) throw new Exception(name);
        File.AppendAllText(LogPath, "PASS " + name + "\n");
    }

    private static string Snapshot(WorkshopSession s) => s.Balance + ":" + s.Revenue + ":" + s.Expenses + ":" +
        s.ElapsedSeconds + ":" + s.RepairSeconds + ":" + s.SpareBatteries + ":" + s.IsPaused + ":" + s.IsCrateCarried + ":" + s.ShiftEnded +
        string.Join(";", s.Units.Select(u => u.InstanceId + u.Location + u.IsTested + u.LastTestPassed + u.Condition)) +
        string.Join(";", Enum.GetValues(typeof(DealOrderId)).Cast<DealOrderId>().Select(id => s.Order(id).State));
    private static void Reject(WorkshopSession s, Func<bool> action, string name)
    { var before = Snapshot(s); Check(!action() && before == Snapshot(s), name); }
    private static string[] Ids(WorkshopSession s, params int[] indices) => indices.Select(i => s.Units[i].InstanceId).ToArray();
    private static WorkshopSession Purchased()
    {
        var s = WorkshopSession.CreateFirstDeal(); s.StartShift();
        if (!s.PurchaseLot(out _)) throw new Exception("fixture purchase");
        return s;
    }
    private static void Test(WorkshopSession s, int index, bool repair = false)
    {
        if (!s.ToggleUnit(index, out _) || !s.ToggleBench(out _) || !s.Test(out _)) throw new Exception("fixture test");
        if (repair && (!s.BuyBattery(out _) || !s.Repair(out _) || !s.Test(out _))) throw new Exception("fixture repair");
        if (!s.ToggleBench(out _) || !s.ToggleUnit(index, out _)) throw new Exception("fixture return");
    }

    private static void VerifyDomain()
    {
        var s = Purchased();
        Check(s.InitialBalance == 300 && s.Expenses == 180 && s.Revenue == 0 && s.OwnedLampCount == 3, "purchase ledger and owned stock");
        s.ToggleCrate(out _);
        Reject(s, () => s.SellBack(Ids(s, 0), out _), "buyback locked before primary sale and 18:00");
        s.AdvanceActiveSeconds(1079.999);
        Check(!s.BuybackAvailable, "buyback closed immediately before 18:00");
        s.AdvanceActiveSeconds(0.001);
        Check(s.BuybackAvailable && s.Order(DealOrderId.A).State == DealOrderState.Expired && s.Order(DealOrderId.B).State == DealOrderState.Expired, "18:00 opens reserve with both main offers expired");
        Reject(s, () => s.SellBack(Ids(s, 0, 1, 2), out _), "untested batch rejects without partial payout");
        s.ToggleCrate(out _); Test(s, 0); Test(s, 1);
        Reject(s, () => s.SellBack(Ids(s, 0, 1), out _), "stored stock cannot sell remotely");
        s.ToggleCrate(out _);
        Reject(s, () => s.SellBack(null, out _), "null presentation rejects");
        Reject(s, () => s.SellBack(new string[0], out _), "empty presentation rejects");
        Reject(s, () => s.SellBack(Ids(s, 0, 0), out _), "duplicate identity rejects");
        Reject(s, () => s.SellBack(new[] { s.Units[0].InstanceId, "unknown" }, out _), "unknown identity rejects entire batch");
        Reject(s, () => s.SellBack(new[] { s.Units[0].InstanceId, null }, out _), "null identity rejects entire batch");
        Reject(s, () => s.SellBack(Ids(s, 0, 2), out _), "tested plus untested rejects entire batch");
        s.SetPaused(true);
        Reject(s, () => s.SellBack(Ids(s, 0, 1), out _), "paused buyback rejects");
        s.SetPaused(false); s.ToggleCrate(out _); Test(s, 2); s.ToggleCrate(out _);
        Check(s.SellBack(Ids(s, 0, 1, 2), out _) && s.Balance == 275 && s.Revenue == 155 && s.Expenses == 180 && s.OwnedLampCount == 0 &&
            s.Units.All(u => u.Location == FlashlightLocation.BoughtBack), "reserve route $275 = $300 - $180 + $155, all ownership transfers once");
        Reject(s, () => s.SellBack(Ids(s, 0, 1, 2), out _), "buyback cannot pay twice");
        Check(!s.HasPrimarySale && !s.ContactBOpened, "reserve is neither primary sale nor B contact");
        Reject(s, () => s.EndShift(false, out _), "no primary sale requires explicit abandonment");
        Check(s.EndShift(true, out _) && s.ShiftEnded && s.IsPaused, "explicit reserve abandonment can finish");
        var end = Snapshot(s); s.SetPaused(false); s.AdvanceActiveSeconds(100); s.StartShift();
        Check(end == Snapshot(s), "ended shift cannot resume or tick");
        Reject(s, () => s.EndShift(true, out _), "finish is one time");
        Reject(s, () => s.BuyBattery(out _), "finished battery purchase blocked");
        Reject(s, () => s.PurchaseLot(out _), "finished lot purchase blocked");
        Reject(s, () => s.AcceptOrder(DealOrderId.B, out _), "finished acceptance blocked");
        Reject(s, () => s.CloseOrder(DealOrderId.B, out _), "finished cancellation blocked");
        Reject(s, () => s.ToggleCrate(out _), "finished item movement blocked");
        Reject(s, () => s.Test(out _), "finished test blocked");
        Reject(s, () => s.Repair(out _), "finished repair blocked");
        Reject(s, () => s.SellBack(Ids(s, 0), out _), "finished buyback blocked");

        var a = Purchased(); Test(a, 0); Test(a, 1); Test(a, 2);
        a.AcceptOrder(DealOrderId.A, out _); a.ToggleCrate(out _);
        Check(a.DeliverOrder(DealOrderId.A, Ids(a, 0, 2), out _) && a.BuybackAvailable && a.ElapsedSeconds < 540,
            "A primary sale immediately opens buyback before deadlines");
        Reject(a, () => a.SellBack(Ids(a, 0, 1), out _), "sold-to-A identity rejects mixed buyback");
        Check(a.SellBack(Ids(a, 1), out _) && a.Balance == 415 && a.Revenue == 295 && a.Expenses == 180 && a.OwnedLampCount == 0,
            "A plus faulty buyback route $415");
        Check(a.EndShift(false, out _), "primary route finishes voluntarily");

        var b = Purchased(); Test(b, 0); Test(b, 2); b.AcceptOrder(DealOrderId.B, out _); b.ToggleCrate(out _);
        b.DeliverOrder(DealOrderId.B, Ids(b, 0), out _);
        Check(b.BuybackAvailable && b.ContactBOpened && b.Balance == 245 && b.Revenue == 125 && b.OwnedLampCount == 2 &&
            b.Order(DealOrderId.RepeatB).State == DealOrderState.Available, "B shows $245, two remaining, contact and repeat opportunity");
        b.AcceptOrder(DealOrderId.RepeatB, out _); b.DeliverOrder(DealOrderId.RepeatB, Ids(b, 2), out _);
        Check(b.Balance == 355 && b.Revenue == 235 && b.Expenses == 180 && b.OwnedLampCount == 1, "repeat B ledger $355 with faulty remainder");
        var repair = Purchased(); Test(repair, 1, true); repair.AdvanceActiveSeconds(1080); repair.ToggleUnit(1, out _);
        Check(repair.SellBack(Ids(repair, 1), out _) && repair.Balance == 130 && repair.Revenue == 60 && repair.Expenses == 230 && repair.SpareBatteries == 0,
            "repaired retested loose lamp sells at $60; $50 battery remains expense");
        var untestedRepair = Purchased(); untestedRepair.ToggleUnit(1, out _); untestedRepair.ToggleBench(out _);
        untestedRepair.Test(out _); untestedRepair.BuyBattery(out _); untestedRepair.Repair(out _);
        untestedRepair.ToggleBench(out _); untestedRepair.AdvanceActiveSeconds(1080);
        Reject(untestedRepair, () => untestedRepair.SellBack(Ids(untestedRepair, 1), out _), "repair invalidates buyback diagnosis until retest");
        var spare = Purchased(); spare.BuyBattery(out _); spare.EndShift(true, out _);
        Check(spare.SpareBatteries == 1 && spare.Expenses == 230 && spare.Revenue == 0 && spare.OwnedLampCount == 3, "unused battery and lamps remain in final inventory");
        var full = new WorkshopSession(int.MaxValue, true); full.StartShift(); full.PurchaseLot(out _);
        Test(full, 0); Test(full, 2); full.ToggleCrate(out _); full.AcceptOrder(DealOrderId.B, out _);
        full.DeliverOrder(DealOrderId.B, Ids(full, 0), out _);
        Reject(full, () => full.SellBack(Ids(full, 2), out _), "wallet overflow rejects without partial transfer");
    }

    private static void PlayState(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(RunningKey, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            phase = 0; frame = -1; failed = completed = false; started = EditorApplication.timeSinceStartup; phaseTime = Time.time;
            runInBackground = Application.runInBackground; Application.runInBackground = true;
            settings = InputSystem.settings; background = settings.backgroundBehavior; inputBehavior = settings.editorInputBehaviorInPlayMode;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            Application.logMessageReceived += Error; EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            EditorApplication.update -= Tick;
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            settings.backgroundBehavior = background; settings.editorInputBehaviorInPlayMode = inputBehavior;
            Application.runInBackground = runInBackground;
        }
        if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.update += Finish;
    }
    private static void Error(string message, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        failed = true; File.AppendAllText(LogPath, "ERROR " + message + "\n" + stack + "\n");
    }
    private static void Finish()
    {
        EditorApplication.update -= Finish; Application.logMessageReceived -= Error; SessionState.SetBool(RunningKey, false);
        var result = completed && !failed ? "T05 RESULT PASS" : "T05 RESULT FAIL";
        File.AppendAllText(LogPath, result + "\n"); Debug.Log(result);
        if (Application.isBatchMode) EditorApplication.Exit(completed && !failed ? 0 : 1);
    }
    private static void Bind()
    {
        workshop = UnityEngine.Object.FindAnyObjectByType<WorkshopView>(); deal = UnityEngine.Object.FindAnyObjectByType<FirstDealView>();
        player = UnityEngine.Object.FindAnyObjectByType<PlayerController>(); player.GetComponent<Player.PlayerInput>().MouseInputEnabled = false;
        deal.SendMessage("OnApplicationFocus", true);
    }
    private static void Click(string name) => GameObject.Find(name).GetComponent<Button>().onClick.Invoke();
    private static Text Terms => GameObject.Find("Terms").GetComponent<Text>();
    private static void Fits(string description)
    {
        Canvas.ForceUpdateCanvases();
        File.AppendAllText(LogPath, "INFO " + Screen.width + "x" + Screen.height + " terms " + Terms.preferredHeight + "/" + Terms.rectTransform.rect.height + "\n");
        Check(Terms.preferredHeight <= Terms.rectTransform.rect.height + 1, description);
    }
    private static void Next() { phase++; phaseTime = Time.time; }
    private static void Tick()
    {
        EditorApplication.QueuePlayerLoopUpdate();
        if (EditorApplication.timeSinceStartup - started > 60) { failed = true; File.AppendAllText(LogPath, "FAIL timeout\n"); EditorApplication.isPlaying = false; return; }
        if (!EditorApplication.isPlaying || frame == Time.frameCount || Time.time - phaseTime < 0.25f) return;
        frame = Time.frameCount;
        try
        {
            switch (phase)
            {
                case 0:
                    Bind(); keyboard = InputSystem.AddDevice<Keyboard>("T05Keyboard");
                    visuals = Enumerable.Range(1, 3).Select(i => GameObject.Find("Lamp " + i).transform).ToArray();
                    Fits("intro with buyback terms fits");
                    Click("Shift summary"); Check(deal.MenuOpen && workshop.Session.IsPaused && !player.CanInteract, "summary pauses and locks world");
                    Check(Terms.text.Contains("$300") && Terms.text.Contains("Осталось фонарей: 0"), "initial summary excludes seller-owned stock");
                    Check(FirstDealText.Summary(workshop.Session).Contains("Можно продолжить или явно отказаться"),
                        "unbought introduction still explains abandonment choice");
                    Click("Finish shift"); Check(!workshop.Session.ShiftEnded && Terms.text.Contains("Завершить неудачную попытку?"), "no-sale finish asks explicit abandonment");
                    Fits("abandonment confirmation fits"); Click("Resume / leave without buying");
                    Check(!deal.MenuOpen && !workshop.Session.ShiftEnded && player.CanInteract, "cancel abandonment continues without blocking controls");
                    deal.OpenMenu(true);
                    Check(GameObject.Find("Confirm lot - $180").GetComponent<Button>().interactable, "first lot purchase button is enabled");
                    Click("Confirm lot - $180");
                    Check(!deal.MenuOpen && workshop.Session.LotPurchased && workshop.Session.Balance == 120, "successful purchase closes seller and debits once");
                    Test(workshop.Session, 0); Test(workshop.Session, 1); Test(workshop.Session, 2);
                    deal.OpenMenu(true);
                    var purchased = Snapshot(workshop.Session);
                    Check(!GameObject.Find("Confirm lot - $180").GetComponent<Button>().interactable &&
                        GameObject.Find("Confirm lot - $180").GetComponentInChildren<Text>().text.Contains("уже куплена"), "purchased lot button disabled and explicitly labelled");
                    Click("Confirm lot - $180"); // Invoke a stale callback even though normal UI blocks it.
                    Check(deal.MenuOpen && workshop.Session.IsPaused && !player.CanInteract && Snapshot(workshop.Session) == purchased &&
                        Terms.text.Contains("Эта единственная партия уже куплена"), "rejected duplicate purchase keeps paused seller visible and preserves diagnostics");
                    Fits("purchased lot refusal fits");
                    deal.CloseMenu();
                    workshop.Session.StartShift(); workshop.Session.AdvanceActiveSeconds(1080); workshop.Session.ToggleCrate(out _);
                    var body = player.GetComponent<CharacterController>(); body.enabled = false;
                    var from = new Vector3(0, 0.05f, -4); player.transform.position = from; body.enabled = true;
                    var direction = GameObject.Find("SELLER | LOT $180 / BATTERY $50").transform.position - (from + Vector3.up * 1.65f);
                    var rotation = Quaternion.LookRotation(direction).eulerAngles; player.transform.rotation = Quaternion.Euler(0, rotation.y, 0);
                    typeof(PlayerController).GetField("_pitch", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(player, Mathf.DeltaAngle(0, rotation.x));
                    Camera.main.transform.SetPositionAndRotation(from + Vector3.up * 1.65f, Quaternion.LookRotation(direction)); Physics.SyncTransforms();
                    Next(); break;
                case 1: InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E)); Next(); break;
                case 2:
                    Check(deal.MenuOpen && GameObject.Find("Sell brought lamps") != null, "physical seller E opens serialized buyback action");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState()); Click("Sell brought lamps");
                    Check(workshop.Session.Balance == 275 && workshop.Session.OwnedLampCount == 0 && visuals.All(t => !t.gameObject.activeSelf), "seller button pays $155 and removes all three bought-back visuals");
                    Check(Terms.text.Contains("+$155") && Terms.text.Contains("Остаток: 0"), "seller feedback displays payment and remaining stock");
                    Fits("seller success terms fit"); Click("Sell brought lamps");
                    Check(workshop.Session.Balance == 275 && Terms.text.Contains("Принеси остатки"), "repeat seller action rejects visibly without payout");
                    Check(deal.GetPrompt(FirstDealActionKind.Seller).Contains("уже куплена") &&
                        workshop.GetPrompt(WorkshopActionKind.Unit, 0).Contains("уже продан"), "seller and sold selectors do not imply new or previously diagnosed stock");
                    Click("Shift summary");
                    Check(Terms.text.Contains("$275") && Terms.text.Contains("Выручка: $155") && Terms.text.Contains("Расходы: $180") && Terms.text.Contains("−$25") && Terms.text.Contains("срок истёк"), "reserve summary reconciles cash, revenue, expenses, loss and expired offers");
                    Fits("reserve summary fits"); ScreenCapture.CaptureScreenshot("Logs/T05-reserve-summary.png"); frozen = workshop.Session.ElapsedSeconds;
                    Next(); break;
                case 3:
                    Check(workshop.Session.ElapsedSeconds == frozen, "summary freezes shift across frames");
                    Check(EventSystem.current.currentSelectedGameObject != null, "summary has keyboard focus");
                    Click("Finish shift"); Click("Finish shift");
                    Check(workshop.Session.ShiftEnded && Terms.text.Contains("ЗАВЕРШЁННОЙ"), "explicit abandonment finishes reserve route");
                    Fits("final reserve summary fits"); deal.CloseMenu();
                    Check(deal.MenuOpen && workshop.Session.IsPaused && !player.CanInteract, "finished screen cannot unlock world through resume");
                    Click("Restart shift"); Next(); break;
                case 4:
                    Bind(); Check(workshop.Session.Balance == 300 && !workshop.Session.LotPurchased && !workshop.Session.ShiftStarted && deal.MenuOpen, "restart loads fresh $300 session and paused introduction");
                    Click("Resume / leave without buying"); workshop.Session.PurchaseLot(out _); Test(workshop.Session, 0);
                    workshop.Session.StartShift(); workshop.Session.ToggleCrate(out _); workshop.Session.AcceptOrder(DealOrderId.B, out _);
                    workshop.Session.DeliverOrder(DealOrderId.B, Ids(workshop.Session, 0), out _);
                    Check(!deal.MenuOpen && !workshop.Session.IsPaused && player.CanInteract, "primary sale does not automatically interrupt play");
                    Check(GameObject.Find("Next action").GetComponent<Text>().text.Contains("$245"), "post-sale consequences appear without forcing summary");
                    var guide = UnityEngine.Object.FindAnyObjectByType<FirstDealGuide>();
                    Check((Transform)typeof(FirstDealGuide).GetField("target", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(guide) ==
                        GameObject.Find("OFFERS BOARD").transform, "repeat offer guidance points to the board rather than buyback seller");
                    deal.OpenMenu(false, true);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftArrow)); Next(); break;
                case 5:
                    Check(EventSystem.current.currentSelectedGameObject == GameObject.Find("Shift summary"), "left arrow navigates from continue to summary");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter)); Next(); break;
                case 6:
                    Check(Terms.text.Contains("Контакт Б: открыт") && Terms.text.Contains("Повтор Б: ещё") && Terms.text.Contains("Осталось фонарей: 2"), "B summary shows contact, physical remainder and repeat opportunity");
                    Fits("B summary with unknown remaining stock fits"); ScreenCapture.CaptureScreenshot("Logs/T05-B-summary.png");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState()); Next(); break;
                case 7:
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); Next(); break;
                case 8:
                    Check(!deal.MenuOpen && !workshop.Session.ShiftEnded, "Escape continues primary route from summary");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    // Spend the remainder at another buyer after the repeat has been offered.
                    workshop.Session.ToggleCrate(out _); // dock before workbench operations
                    Test(workshop.Session, 1, true); Test(workshop.Session, 2);
                    workshop.Session.AcceptOrder(DealOrderId.RepeatB, out _);
                    workshop.Session.AcceptOrder(DealOrderId.A, out _); workshop.Session.ToggleCrate(out _);
                    Check(workshop.Session.DeliverOrder(DealOrderId.A, Ids(workshop.Session, 1, 2), out _), "B then A consumes remainder in running scene");
                    deal.OpenMenu(false);
                    Check(!GameObject.Find("Accept RepeatB").GetComponent<Button>().interactable &&
                        !GameObject.Find("Decline / cancel RepeatB").GetComponent<Button>().interactable &&
                        Terms.text.Contains("Пополнения партии нет") && Terms.text.Contains("Осталось фонарей: 0"), "board disables exhausted repeat and explains no replenishment");
                    Check(FirstDealText.Summary(workshop.Session).Contains("Все фонари проданы") &&
                        !FirstDealText.Summary(workshop.Session).Contains("Можно продолжить торговлю"),
                        "exhausted summary shows one clear next step");
                    Check(deal.GetPrompt(FirstDealActionKind.DeliverRepeatB).Contains("Недостаточно товара") &&
                        GameObject.Find("Next action").GetComponent<Text>().text.Contains("Все фонари проданы"), "world prompt and guide explain exhausted stock");
                    Fits("exhausted board fits"); ScreenCapture.CaptureScreenshot("Logs/T06-exhausted-board.png");
                    deal.CloseMenu();
                    deal.OpenSummary(); Click("Finish shift");
                    Check(workshop.Session.ShiftEnded && GameObject.Find("Restart shift") != null, "primary sale finishes voluntarily with restart available");
                    Click("Restart shift"); Next(); break;
                case 9:
                    Bind(); deal.CloseMenu(); workshop.Session.PurchaseLot(out _);
                    indicators = (Light[])typeof(WorkshopView).GetField("indicators", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(workshop);
                    workshop.Session.ToggleUnit(0, out _); workshop.Session.ToggleBench(out _);
                    workshop.Perform(WorkshopActionKind.Test, 0);
                    Check(indicators[0].enabled && workshop.Session.OnBench.LastTestPassed, "workbench interaction lights tested working lamp");
                    workshop.Session.ToggleBench(out _); workshop.Session.ToggleUnit(0, out _);
                    workshop.Session.ToggleUnit(1, out _); workshop.Session.ToggleBench(out _);
                    workshop.Perform(WorkshopActionKind.Test, 0);
                    Check(indicators[1].enabled && !workshop.Session.OnBench.LastTestPassed, "faulty lamp starts visible blink through workshop interaction");
                    Next(); break;
                case 10:
                    if (Time.time - phaseTime < 1) return;
                    Check(!indicators[1].enabled && workshop.Session.OnBench.IsTested, "faulty lamp blink ends with light off and diagnosis retained");
                    workshop.Session.BuyBattery(out _); workshop.Perform(WorkshopActionKind.Repair, 0);
                    Check(!workshop.Session.OnBench.IsTested && !indicators[1].enabled, "repair clears diagnosis and requires retest");
                    workshop.Perform(WorkshopActionKind.Test, 0);
                    Check(workshop.Session.OnBench.LastTestPassed && indicators[1].enabled, "repaired lamp lights steadily after retest");
                    completed = true; EditorApplication.isPlaying = false; break;
            }
        }
        catch (Exception e) { failed = true; File.AppendAllText(LogPath, "FAIL " + e + "\n"); EditorApplication.isPlaying = false; }
    }
}
