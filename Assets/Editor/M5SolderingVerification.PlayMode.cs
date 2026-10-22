using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Interactables;
using Inventory;
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
    [MenuItem("Tools/M5/Verify City Input In Play Mode")]
    public static void VerifyInput()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter CityGreybox Play Mode with isolated save override first.");
        if (!SessionState.GetString("HustleScale.S2SaveOverride", "").Contains("M5")) throw new InvalidOperationException("M5 isolated save required.");
        checks = 0; report.Clear();
        var save = UnityEngine.Object.FindAnyObjectByType<CitySaveController>();
        var trade = save.GetComponent<CityTradeView>(); var walk = save.GetComponent<CityGreyboxWalk>();
        var player = save.GetComponent<PlayerController>();
        var eye = save.GetComponentInChildren<Camera>();
        var funded = Funded();
        var world = World(); world.PlayerPosition = new Vector3(-65, .08f, 25);
        typeof(CitySaveController).GetMethod("Bind", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(save, new object[] { funded, world });
        walk.SetPaused(false);
        var t = trade.SolderingStation;
        Check(trade.StationPlaceAvailable(), "station footprint clears existing battery bench and routes");
        var keyboard = InputSystem.AddDevice<Keyboard>(); var mouse = InputSystem.AddDevice<Mouse>();
        keyboard.MakeCurrent(); mouse.MakeCurrent();
        var background = Application.runInBackground; Application.runInBackground = true;
        var oldInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        var oldFocus = InputSystem.settings.backgroundBehavior;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        var steps = new List<(Action action, double delay)>();
        Action<Action, double> add = (action, delay) => steps.Add((action, delay));
        Action<Key?> key = k => InputSystem.QueueStateEvent(keyboard, k.HasValue ? new KeyboardState(k.Value) : new KeyboardState());
        var mousePosition = Vector2.zero;
        Action<bool> click = down => InputSystem.QueueStateEvent(mouse, new MouseState { position = mousePosition, buttons = (ushort)(down ? 1 : 0) });
        Action<Vector2> guide = point => InputSystem.QueueDeltaStateEvent(mouse.delta, point - t.CursorScreenPosition);
        Action<SolderingTrial.RepairTarget> aim = target => guide(t.RepairScreenPosition(target));
        Action<string, string> uiClick = (name, parent) =>
        {
            Button button = null;
            add(() =>
            {
                button = UnityEngine.Object.FindObjectsByType<Button>().Single(b => b.name == name && (parent == null || b.transform.parent.name == parent));
                Check(button.interactable, "UI actionable " + (parent ?? name));
                EventSystem.current.SetSelectedGameObject(button.gameObject);
            }, .3);
            add(() =>
            {
                mousePosition = RectTransformUtility.WorldToScreenPoint(null, button.transform.position);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = mousePosition });
            }, .15);
            add(() => click(true), .15); add(() => click(false), .35);
        };
        Action<CityTradeKind> face = kind =>
        {
            var point = trade.Points.First(p => p.Kind == kind);
            var center = point.GetComponent<Collider>().bounds.center;
            player.RestorePose(new Vector3(center.x, .08f, center.z - 1.65f), 0, Mathf.Atan2(1.73f - center.y, 1.65f) * Mathf.Rad2Deg);
        };
        Action<CityTradeKind> openMenu = kind =>
        {
            add(() => face(kind), .2); add(() => key(Key.E), .2);
            add(() => { key(null); Check(trade.MenuOpen, "E opens " + kind + " · " + trade.GetComponent<PlayerRaycast>().FocusLabel + " · " + eye.transform.position); }, .2);
        };
        Action closeMenu = () => { add(() => key(Key.Escape), .2); add(() => { key(null); Check(!trade.MenuOpen, "Esc closes menu"); }, .2); };
        Action<SolderingTrial.RepairTarget> take = target =>
        {
            add(() => aim(target), .15);
            add(() => { Check(t.HoveredRepairTarget == target, "M3 cursor " + target); click(true); }, .1);
            add(() => click(false), .5);
        };
        Action<SolderingTrial.RepairTarget> turn = target =>
        {
            add(() => aim(target), .15); add(() => click(true), .7); add(() => click(false), .15);
        };
        Action<SolderingTrialState.Tool> tool = chosen =>
        {
            add(() => guide(t.ToolScreenPosition(chosen)), .15);
            add(() => { Check(t.HoveredTool == chosen, "M3 tool " + chosen); click(true); }, .1);
            add(() => { click(false); guide(t.JointScreenPosition); }, .45);
        };
        Action returnTool = () =>
        {
            add(() => guide(t.ToolDockScreenPosition(t.State.HeldTool)), .15);
            add(() => click(true), .1); add(() => click(false), .4);
        };
        openMenu(CityTradeKind.Seller);
        uiClick("Купить", "Паяльная станция");
        add(() => Check(save.Session.StationOwned && t.gameObject.activeInHierarchy, "real UI installs station once"), .1);
        uiClick("Купить", "Фонарик для пайки"); uiClick("Купить", "Припой");
        add(() => Check(save.Session.SolderingLamp.Location == FlashlightLocation.Hands && save.Session.SolderDoses == 1, "UI buys separate item and dose"), .1);
        closeMenu();
        add(() => face(CityTradeKind.Workshop), .2); add(() => key(Key.E), .2); add(() => key(null), .15);
        add(() => key(Key.T), .2); add(() => key(null), .15);
        add(() => { Check(save.Session.ConnectionDiagnosed && save.Session.OnBench == save.Session.SolderingLamp, "E/T diagnose same city ID"); key(Key.R); }, .15);
        add(() => { Check(save.Session.SolderingLamp.Condition == FlashlightCondition.ConnectionFault, "real R leaves connection fault"); key(null); }, .15);
        add(() => key(Key.E), .2); add(() => key(null), .15);
        float worldFov = 0;
        add(() => { player.RestorePose(t.transform.position + new Vector3(0, .08f, -1.4f), 0, 26); worldFov = eye.fieldOfView; }, .2);
        add(() => key(Key.E), .25);
        add(() => { key(null); Check(t.IsActive && Mathf.Approximately(eye.fieldOfView, worldFov * .8f), "world E enters compact M3 with working FOV"); }, .15);
        add(() => InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 2 }), .4);
        add(() => { Check(eye.fieldOfView < worldFov * .8f, "held RMB magnifies"); click(false); }, .4);
        take(SolderingTrial.RepairTarget.Flashlight); take(SolderingTrial.RepairTarget.WorkMat);
        add(() => Check(save.Session.AtSolderingStation && save.Session.OnBench.InstanceId == funded.SolderingLamp.InstanceId, "manual placement uses same city ID"), .1);
        turn(SolderingTrial.RepairTarget.Fastener0); turn(SolderingTrial.RepairTarget.Fastener1);
        take(SolderingTrial.RepairTarget.Cover);
        add(() => Check(t.Repair.Cover == FlashlightRepairState.CoverPlace.Held, "autosave leaves lid in working hand"), .5);
        add(() => Check(t.Repair.Cover == FlashlightRepairState.CoverPlace.Held, "deferred autosave never performs manual tray step"), .1);
        take(SolderingTrial.RepairTarget.Tray);
        tool(SolderingTrialState.Tool.Solder);
        add(() => click(true), .5);
        add(() => { click(false); Check(t.State.HasSolder && save.Session.SolderDoses == 0, "physical solder feed consumes exactly one dose"); }, .15);
        returnTool(); tool(SolderingTrialState.Tool.Iron);
        add(() => click(true), .3);
        add(() => { click(false); Check(t.State.Progress == 0, "cold physical contact no progress"); }, 4.3);
        add(() => { Check(t.State.Ready, "iron ready before hot contact, heat=" + t.State.Heat); click(true); }, .7);
        float partial = 0;
        add(() => { click(false); partial = t.State.Progress; Check(partial > 0 && partial < 1, "real held contact produces partial repair: " + partial + " contact=" + t.HasContact + " accessible=" + t.State.Accessible); key(Key.P); }, .2);
        add(() => { key(null); Check(walk.IsPaused && !t.State.Working, "P opens city pause and stops work"); }, .4);
        add(() => { Check(t.State.Progress == partial, "paused contact preserves progress"); key(Key.F5); }, .2);
        add(() => key(null), .2); add(() => key(Key.F9), .3);
        add(() =>
        {
            key(null); Check(walk.IsPaused && !t.IsActive && save.Session.SolderingState.Progress == partial &&
                save.Session.SolderingState.Heat == 0 && save.Session.SolderDoses == 0, "F5/F9 restores partial work, cold tool, exact dose");
        }, .2);
        add(() => key(Key.Escape), .2); add(() => key(null), .2);
        add(() => { player.RestorePose(t.transform.position + new Vector3(0, .08f, -1.4f), 0, 26); }, .2);
        add(() => key(Key.E), .2); add(() => { key(null); Check(t.IsActive, "return to loaded station"); }, .2);
        tool(SolderingTrialState.Tool.Iron);
        add(() => { }, 4.3); add(() => click(true), 3);
        add(() => { click(false); Check(t.State.Complete && !save.Session.SolderingReady && save.Session.SolderDoses == 0, "completed solder keeps assembly/test gate and no second charge"); }, .15);
        returnTool(); take(SolderingTrial.RepairTarget.Cover); take(SolderingTrial.RepairTarget.WorkMat);
        turn(SolderingTrial.RepairTarget.Fastener0); turn(SolderingTrial.RepairTarget.Fastener1);
        add(() => key(Key.Escape), .2);
        add(() => { key(null); Check(!t.IsActive && save.Session.AtSolderingStation && t.Repair.Assembled, "Esc leaves closed lamp placed on station"); }, .2);
        add(() => key(Key.E), .25);
        add(() => { key(null); Check(t.IsActive && t.Repair.Assembled && t.State.Complete, "real E reentry keeps closed lamp and completed solder"); }, .2);
        add(() => key(Key.P), .2); add(() => key(null), .2);
        add(() => key(Key.F5), .2); add(() => key(null), .2);
        add(() => key(Key.F9), .3);
        add(() =>
        {
            key(null); Check(walk.IsPaused && !t.IsActive && save.Session.AtSolderingStation && t.Repair.Assembled && t.State.Complete,
            "F5/F9 preserves closed lamp on station without manual replacement");
        }, .2);
        add(() => key(Key.Escape), .2); add(() => key(null), .2);
        add(() => key(Key.E), .25); add(() => key(null), .2);
        add(() => aim(SolderingTrial.RepairTarget.Power), .15); add(() => click(true), .2);
        add(() => { click(false); Check(t.BeamEnabled && save.Session.SolderingReady, "manual assembled switch gives real light and ready city item"); }, .15);
        take(SolderingTrial.RepairTarget.Flashlight);
        add(() => Check(!t.IsActive && save.Session.InHands == save.Session.SolderingLamp && Mathf.Approximately(eye.fieldOfView, worldFov), "LMB retrieves same lamp and restores world FOV"), .15);
        openMenu(CityTradeKind.B); uiClick("Принять пайку", null); uiClick("Передать восстановленный фонарик", null);
        add(() => Check(save.Session.Order(DealOrderId.Solder).State == DealOrderState.Completed && save.Session.SolderingLamp.Location == FlashlightLocation.SolderBuyer, "physical UI transfer pays craft order once"), .2);
        closeMenu();
        add(() => { Must(save.Save()); Must(save.Load(false)); Check(!save.Session.DeliverSoldering(new[] { save.Session.SolderingLamp.InstanceId }, out _), "post-load repeat payout refused"); }, .2);
        var index = 0; var frame = Time.frameCount; var next = Time.timeAsDouble;
        var deadline = EditorApplication.timeSinceStartup + 280;
        EditorApplication.CallbackFunction runner = null;
        Action cleanup = () =>
        {
            EditorApplication.update -= runner;
            InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse);
            InputSystem.settings.editorInputBehaviorInPlayMode = oldInput;
            InputSystem.settings.backgroundBehavior = oldFocus;
            Application.runInBackground = background; if (t != null) t.Exit();
        };
        runner = () =>
        {
            try
            {
                if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup > deadline) throw new InvalidOperationException("M5 input timeout step " + index);
                if (Time.frameCount < frame + 3 || Time.timeAsDouble < next) return;
                if (index == steps.Count)
                {
                    cleanup(); report.AppendLine("INPUT PASS " + checks); File.WriteAllText("Logs/Codex-M5-input.log", report.ToString());
                    Debug.Log("M5 INPUT PASS " + checks); return;
                }
                var step = steps[index++]; step.action(); frame = Time.frameCount; next = Time.timeAsDouble + step.delay;
            }
            catch (Exception e)
            {
                cleanup(); report.AppendLine("INPUT FAIL step " + index + " " + e); File.WriteAllText("Logs/Codex-M5-input.log", report.ToString()); Debug.LogException(e);
            }
        };
        EditorApplication.update += runner;
    }
}
