using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Inventory;
using Interactables;
using Player;
using Player.Movement;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

public static partial class M5SolderingVerification
{
    private static bool feedbackInputRunning;
    [MenuItem("Tools/Feedback/Verify UI and Input In Play Mode")]
    public static void VerifyFeedbackInput()
    {
        if (feedbackInputRunning) return;
        if (!EditorApplication.isPlaying || !SessionState.GetString("HustleScale.S2SaveOverride", "").Contains("M5Feedback"))
            throw new InvalidOperationException("Isolated feedback Play Mode required.");
        feedbackInputRunning = true;
        checks = 0; report.Clear();
        var save = UnityEngine.Object.FindAnyObjectByType<CitySaveController>();
        var trade = save.GetComponent<CityTradeView>(); var walk = save.GetComponent<CityGreyboxWalk>();
        var player = save.GetComponent<PlayerController>(); var eye = save.GetComponentInChildren<Camera>();
        var s = WorkshopSession.CreateCity(19);
        var world = World(); world.PlayerPosition = new Vector3(-65, .08f, 25);
        typeof(CitySaveController).GetMethod("Bind", Private).Invoke(save, new object[] { s, world });
        walk.SetPaused(false);
        var keyboard = InputSystem.AddDevice<Keyboard>(); var mouse = InputSystem.AddDevice<Mouse>();
        keyboard.MakeCurrent(); mouse.MakeCurrent();
        var background = Application.runInBackground; Application.runInBackground = true;
        var oldInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        var oldFocus = InputSystem.settings.backgroundBehavior;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        var input = player.GetComponent<Player.PlayerInput>(); var oldMouse = input.MouseInputEnabled; input.MouseInputEnabled = false;
        var steps = new List<(Action action, double delay)>();
        Action<Action, double> add = (a, d) => steps.Add((a, d));
        Action<Key?> key = k => InputSystem.QueueStateEvent(keyboard, k.HasValue ? new KeyboardState(k.Value) : new KeyboardState());
        var pointer = Vector2.zero;
        Action<bool> click = down => InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer, buttons = (ushort)(down ? 1 : 0) });
        Action<CityTradePoint> face = point =>
        {
            var collider = point.GetComponent<Collider>(); var center = collider.bounds.center;
            foreach (var direction in new[] { point.transform.forward, -point.transform.forward, point.transform.right, -point.transform.right })
            {
                var origin = center + direction * 1.8f; origin.y = 1.73f;
                if (!Physics.Linecast(origin, center, out var hit) || hit.collider != collider) continue;
                var delta = center - origin; var rotation = Quaternion.LookRotation(delta).eulerAngles;
                player.RestorePose(new Vector3(origin.x, .08f, origin.z), rotation.y, rotation.x > 180 ? rotation.x - 360 : rotation.x);
                Physics.SyncTransforms(); return;
            }
            throw new InvalidOperationException("No clear view of " + point.name);
        };
        Action<CityTradeKind> openNpc = kind =>
        {
            add(() => face(trade.Points.Single(p => p.Kind == kind && p.GetComponent<CityTradeReaction>() != null)), .25);
            add(() => key(Key.E), .2);
            add(() => { key(null); Check(trade.MenuOpen, "real E aimed at NPC opens " + kind); }, .2);
        };
        Action closeMenu = () => { add(() => key(Key.Escape), .2); add(() => { key(null); Check(!trade.MenuOpen, "Esc closes menu once"); }, .2); };
        Action<string, string> uiClick = (name, parent) =>
        {
            Button button = null;
            add(() =>
            {
                button = UnityEngine.Object.FindObjectsByType<Button>().Single(b => b.name == name && (parent == null || b.transform.parent.name == parent));
                Check(button.interactable, "UI enabled " + name); EventSystem.current.SetSelectedGameObject(button.gameObject);
            }, .2);
            add(() => { pointer = RectTransformUtility.WorldToScreenPoint(null, button.transform.position); click(false); }, .2);
            add(() => click(true), .15); add(() => click(false), .3);
        };
        openNpc(CityTradeKind.Seller);
        for (var i = 0; i < 3; i++) uiClick("Купить", "Фонарь");
        add(() => Check(s.Balance == 120 && s.Units.All(l => l.Location == FlashlightLocation.Pocket) && !s.IsCrateCarried,
            "three real UI clicks buy exactly three individual lamps into stack without crate"), .2);
        closeMenu();
        var batterySlot = -1;
        add(() =>
        {
            batterySlot = s.Units.ToList().FindIndex(l => l.Condition == FlashlightCondition.BatteryFault);
            Check(batterySlot >= 0, "seeded fixture includes ordinary battery fault");
            Must(s.BuyBattery(out _));
            key((Key)((int)Key.Digit1 + batterySlot));
        }, .2);
        add(() => { key(null); Check(s.InHands == s.Units[batterySlot], "real 1-3 equips one stack instance"); face(trade.Points.First(p => p.Kind == CityTradeKind.Workshop)); }, .2);
        add(() => key(Key.E), .2); add(() => key(null), .2);
        add(() => key(Key.T), .2);
        add(() => { key(null); Check(s.OnBench == s.Units[batterySlot] && s.OnBench.IsTested && !s.OnBench.LastTestPassed, "real E/T diagnoses battery on ordinary bench"); }, .2);
        add(() => key(Key.R), .2);
        add(() =>
        {
            key(null); Check(s.OnBench.Condition == FlashlightCondition.Working && !s.OnBench.IsTested && s.SpareBatteries == 0 && s.RepairSeconds == 240,
            "real R consumes one battery, adds 240 seconds and requires retest");
        }, .2);
        add(() => key(Key.T), .2); add(() => key(null), .2);
        add(() => key(Key.E), .2); add(() => key(null), .2);
        add(() => key(Key.G), .2);
        add(() =>
        {
            key(null); Check(s.Units[batterySlot].Location == FlashlightLocation.Pocket && s.Units[batterySlot].LastTestPassed,
            "real E/G returns repaired and retested same instance to stack");
        }, .2);
        openNpc(CityTradeKind.Seller);
        var game = EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView"));
        var sizeProperty = game.GetType().GetProperty("selectedSizeIndex"); var oldSize = sizeProperty.GetValue(game);
        var group = typeof(UIInteractionVerification).GetMethod("SizeGroup", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        var sizes = new List<int>();
        foreach (var size in new[] { new Vector2Int(1024, 768), new Vector2Int(1600, 900) })
            foreach (var scale in new[] { 1f, 1.5f })
            {
                add(() =>
                {
                    var index = (int)typeof(UIInteractionVerification).GetMethod("AddSize", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { group, size });
                    sizes.Add(index); sizeProperty.SetValue(game, index); game.Repaint(); trade.SetTextScale(scale);
                }, .5);
                add(() =>
                {
                    foreach (var b in UnityEngine.Object.FindObjectsByType<Button>().Where(b => b.transform.parent.name == "Фонарь" || b.transform.parent.name == "Паяльная станция"))
                    {
                        var label = b.GetComponentInChildren<Text>();
                        Check(label.text == "Недоступно" && label.preferredHeight <= label.rectTransform.rect.height + .1f,
                            "short disabled status fits at " + size + " Aa=" + scale);
                    }
                    var scroll = UnityEngine.Object.FindObjectsByType<ScrollRect>().Single(r => r.name == "Trade menu");
                    scroll.verticalNormalizedPosition = 0;
                }, .3);
                add(() =>
                {
                    var b = UnityEngine.Object.FindObjectsByType<Button>().Single(b => b.transform.parent.name == "Паяльная станция");
                    pointer = RectTransformUtility.WorldToScreenPoint(null, b.transform.position); click(false);
                }, .4);
                add(() =>
                {
                    var tooltip = GameObject.Find("Product tooltip").GetComponent<RectTransform>(); var label = tooltip.GetComponentInChildren<Text>();
                    Check(label.text.Contains("Продай всю первую партию") && label.preferredHeight <= label.rectTransform.rect.height + .1f,
                        "real hover on disabled button shows full Russian reason " + size + " Aa=" + scale);
                    var corners = new Vector3[4]; tooltip.GetWorldCorners(corners);
                    Check(corners.All(c => Screen.safeArea.Contains(RectTransformUtility.WorldToScreenPoint(null, c))), "tooltip entirely inside safe screen");
                    ScreenCapture.CaptureScreenshot("Temp/M5Feedback/shop-" + size.x + "-" + scale + ".png");
                }, .3);
            }
        closeMenu();
        add(() =>
        {
            trade.SetTextScale(1);
            for (var i = 0; i < 3; i++)
            {
                Must(s.EquipSlot(i, out _)); Must(s.ToggleBench(out _)); Must(s.Test(out _));
                if (!s.OnBench.LastTestPassed) { Must(s.BuyBattery(out _)); Must(s.Repair(out _)); Must(s.Test(out _)); }
                Must(s.ToggleBench(out _)); Must(s.StowHeld(out _));
            }
        }, .2);
        openNpc(CityTradeKind.A); uiClick("Принять заказ", null);
        add(() => Check(UnityEngine.Object.FindObjectsByType<Button>().Any(b => b.name == "Передать · 2/2" && b.interactable), "order UI selects exactly 2 of carried 3"), .2);
        uiClick("Передать · 2/2", null);
        add(() =>
        {
            Check(s.Order(DealOrderId.A).State == DealOrderState.Completed && s.Units[2].Location == FlashlightLocation.Pocket, "real UI transfers two and preserves third");
            var before = Snapshot(s); Check(!trade.Execute("deliver") && Snapshot(s) == before, "repeat UI delivery cannot pay");
        }, .2);
        closeMenu(); openNpc(CityTradeKind.B); closeMenu();
        add(() => face(trade.Points.First(p => p.Kind == CityTradeKind.Crate)), .2);
        add(() => key(Key.E), .2);
        add(() => { key(null); Check(s.IsCrateCarried && !trade.MenuOpen, "real E takes optional crate directly without menu"); }, .2);
        add(() =>
        {
            var funded = Funded(); Must(funded.PurchaseStation(true, out _)); Must(funded.PurchaseSolderingLamp(out _));
            typeof(CitySaveController).GetMethod("Bind", Private).Invoke(save, new object[] { funded, world });
            walk.SetPaused(false); trade.SetTextScale(1.5f);
        }, .2);
        openNpc(CityTradeKind.Seller);
        add(() =>
        {
            var bought = UnityEngine.Object.FindObjectsByType<Button>().Where(b => b.name == "КУПЛЕНО").ToArray();
            Check(bought.Length == 2 && bought.All(b => !b.interactable), "station and soldering lamp keep explicit bought status");
            foreach (var b in bought)
            {
                var label = b.GetComponentInChildren<Text>();
                Check(label.preferredHeight <= label.rectTransform.rect.height + .1f, "bought status fits with enlarged text");
            }
        }, .2);
        closeMenu();
        var stepIndex = 0; var frame = Time.frameCount; var next = Time.timeAsDouble;
        var deadline = EditorApplication.timeSinceStartup + 160;
        EditorApplication.CallbackFunction runner = null;
        Action cleanup = () =>
        {
            EditorApplication.update -= runner;
            feedbackInputRunning = false;
            InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse);
            InputSystem.settings.editorInputBehaviorInPlayMode = oldInput; InputSystem.settings.backgroundBehavior = oldFocus;
            Application.runInBackground = background; input.MouseInputEnabled = oldMouse;
            sizeProperty.SetValue(game, oldSize);
            foreach (var index in sizes.OrderByDescending(x => x)) group.GetType().GetMethod("RemoveCustomSize").Invoke(group, new object[] { index });
        };
        runner = () =>
        {
            try
            {
                if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup > deadline) throw new InvalidOperationException("Feedback input timeout step " + stepIndex);
                if (Time.frameCount < frame + 3 || Time.timeAsDouble < next) return;
                if (stepIndex == steps.Count)
                {
                    cleanup(); report.AppendLine("INPUT PASS " + checks); File.WriteAllText("Logs/Codex-feedback-input.log", report.ToString());
                    Debug.Log("Feedback INPUT PASS " + checks); return;
                }
                var step = steps[stepIndex++]; step.action(); frame = Time.frameCount; next = Time.timeAsDouble + step.delay;
            }
            catch (Exception e)
            {
                cleanup(); report.AppendLine("INPUT FAIL step " + stepIndex + " " + e); File.WriteAllText("Logs/Codex-feedback-input.log", report.ToString()); Debug.LogException(e);
            }
        };
        EditorApplication.update += runner;
    }

    [MenuItem("Tools/Feedback/Verify Inventory and Partial Purchases")]
    public static void VerifyFeedbackDomain()
    {
        checks = 0; report.Clear();
        try
        {
            var s = WorkshopSession.CreateCity(19);
            var before = Snapshot(s);
            Check(!s.PurchaseCityLamp(out _) && Snapshot(s) == before, "paused single purchase atomic");
            s.SetPaused(false);
            for (var i = 0; i < 3; i++)
            {
                Must(s.PurchaseCityLamp(out _));
                Check(s.Balance == 300 - 60 * (i + 1) && s.SellerLampCount == 2 - i &&
                    s.Units[i].Location == FlashlightLocation.Pocket && !s.IsCrateCarried, "one purchase costs 60 and adds one instance without crate");
                Check(s.Order(DealOrderId.A).State == DealOrderState.Available, "unpurchased seller stock can still supply A");
                s = Round(s);
                var data = s.CaptureCity(World());
                data.Expenses += 60; data.Balance -= 60;
                Check(!WorkshopSession.TryRestoreCity(data, out _, out _), "partial purchase ledger rejects forged expense");
            }
            before = Snapshot(s);
            Check(!s.PurchaseCityLamp(out _) && Snapshot(s) == before, "cannot replace owned batch");
            for (var i = 0; i < 3; i++)
            {
                Must(s.EquipSlot(i, out _)); Must(s.ToggleBench(out _)); Must(s.Test(out _));
                if (!s.OnBench.LastTestPassed)
                {
                    Must(s.BuyBattery(out _)); Must(s.Repair(out _));
                    Check(!s.OnBench.IsTested, "battery repair still clears diagnosis"); Must(s.Test(out _));
                }
                Must(s.ToggleBench(out _)); Must(s.StowHeld(out _));
            }
            var ids = s.Units.Select(x => x.InstanceId).ToArray();
            Must(s.AcceptOrder(DealOrderId.A, out _));
            before = Snapshot(s);
            Check(!s.DeliverOrder(DealOrderId.A, ids, out _) && Snapshot(s) == before, "model rejects oversized selection atomically");
            Check(!s.DeliverOrder(DealOrderId.A, new[] { ids[0], ids[0] }, out _) && Snapshot(s) == before, "duplicate identities rejected atomically");
            var balance = s.Balance;
            Must(s.DeliverOrder(DealOrderId.A, ids.Take(2).ToArray(), out _));
            Check(s.Balance == balance + 260 && s.Units[2].InstanceId == ids[2] && s.Units[2].Location == FlashlightLocation.Pocket,
                "deliver 2 of 3 from stack preserves remaining instance and pays once");
            s = Round(s); before = Snapshot(s);
            Check(!s.DeliverOrder(DealOrderId.A, ids.Take(2).ToArray(), out _) && Snapshot(s) == before, "repeat delivery after reload never pays");
            Must(s.AcceptOrder(DealOrderId.B, out _)); Must(s.DeliverOrder(DealOrderId.B, new[] { ids[2] }, out _));
            Check(s.CanBuyStation, "station unlocks after all three sold");
            Must(s.PurchaseCityLamp(out _));
            Check(s.PurchasedLots == 2 && s.SellerLampCount == 2 && !ids.Contains(s.Units[0].InstanceId) &&
                s.PreviousOrder(DealOrderId.A).State == DealOrderState.Completed, "second partial batch has new IDs and retains paid history");
            s = Round(s);
            Must(s.PurchaseCityLamp(out _)); Must(s.PurchaseCityLamp(out _));
            s = Round(s);
            Check(!s.CanPurchaseCityLamp, "six ordinary purchases maximum");
            var legacy = Funded();
            foreach (var version in new[] { 3, 4, 5 })
            {
                var data = legacy.CaptureCity(World()); data.Version = version;
                Must(WorkshopSession.TryRestoreCity(data, out _, out _));
            }
            Debug.Log("Feedback domain PASS " + checks);
        }
        finally { Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/Codex-feedback-domain.log", report.ToString()); }
    }

    [MenuItem("Tools/Feedback/Enter Isolated Play Mode")]
    public static void EnterFeedbackPlay()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Leave Play Mode first.");
        SessionState.SetString("HustleScale.S2SaveOverride", Path.GetFullPath("Temp/M5Feedback/city-save.json"));
        Directory.CreateDirectory("Temp/M5Feedback");
        EditorApplication.isPlaying = true;
    }

    [MenuItem("Tools/Feedback/Verify Closed Lamp Exit")]
    public static void VerifyClosedLampExit()
    {
        if (!EditorApplication.isPlaying || !SessionState.GetString("HustleScale.S2SaveOverride", "").Contains("M5Feedback"))
            throw new InvalidOperationException("Isolated feedback Play Mode required.");
        checks = 0; report.Clear();
        var save = UnityEngine.Object.FindAnyObjectByType<CitySaveController>();
        var trade = save.GetComponent<CityTradeView>();
        var walk = save.GetComponent<CityGreyboxWalk>();
        var player = save.GetComponent<PlayerController>();
        var trial = trade.SolderingStation;
        try
        {
            foreach (var repaired in new[] { false, true })
            {
                var s = Prepared();
                var world = World(); world.PlayerPosition = UnityEngine.Object.FindAnyObjectByType<CityTradeView>().StationPosition + new Vector3(0, .08f, -1.4f);
                typeof(CitySaveController).GetMethod("Bind", Private).Invoke(save, new object[] { s, world });
                walk.SetPaused(false); player.RestorePose(world.PlayerPosition, 0, 26); trial.Interact();
                Open(s);
                if (repaired) Solder(s, 3);
                Close(s);
                var id = s.SolderingLamp.InstanceId;
                trial.Exit();
                Check(s.AtSolderingStation && s.SolderingLamp.Location == FlashlightLocation.Bench &&
                    s.RepairState.Flashlight == FlashlightRepairState.ItemPlace.Bench, "closed lamp stays at station after Exit");
                trial.Interact();
                Check(trial.IsActive && trial.Repair.Flashlight == FlashlightRepairState.ItemPlace.Bench &&
                    trial.Repair.Assembled, "closed lamp remains placed on reentry");
                trial.Exit(); Must(save.Save()); Must(save.Load(false));
                Check(save.Session.AtSolderingStation && trial.Repair.Flashlight == FlashlightRepairState.ItemPlace.Bench &&
                    trial.Repair.Assembled && save.Session.SolderingLamp.InstanceId == id && trial.State.Complete == repaired,
                    "closed lamp placement, identity and repair survive actual save/load");
            }
            Debug.Log("Feedback closed lamp PASS " + checks);
        }
        finally
        {
            trial.Exit(); Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/Codex-feedback-closed.log", report.ToString());
        }
    }
}
