using System;
using System.IO;
using System.Text;
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
using System.Collections.Generic;
using System.Linq;

public static class CheatVerification
{
    private static bool inputRunning;
    private static int checks;
    private static readonly StringBuilder report = new StringBuilder();
    private static readonly CityWorldData World = new CityWorldData { PlacedLamps = Array.Empty<CityPlacedLamp>() };
    private static void Check(bool value, string context)
    {
        checks++; report.AppendLine((value ? "PASS " : "FAIL ") + context);
        if (!value) throw new InvalidOperationException(context);
    }
    private static WorkshopSession Round(WorkshopSession session)
    {
        var json = JsonUtility.ToJson(session.CaptureCity(World));
        Check(WorkshopSession.TryRestoreCity(JsonUtility.FromJson<CitySaveData>(json), out var restored, out var message), "restore: " + message);
        Check(JsonUtility.ToJson(restored.CaptureCity(World)) == json, "exact JSON round trip");
        return restored;
    }
    private static void Finish(string kind)
    {
        var result = "CHEATS " + kind + " PASS " + checks + " assertions; Unity " + Application.unityVersion;
        report.AppendLine(result); Debug.Log(result);
    }

    [MenuItem("Tools/Cheats/Verify Domain")]
    public static void Domain()
    {
        checks = 0; report.Clear();
        try
        {
            var fixture = WorkshopSession.CreateFirstDeal();
            Check(!fixture.CheatSetBalance(1000, out _), "FIRST DEAL remains separate");
            var s = WorkshopSession.CreateCity(19);
            foreach (var version in new[] { 1, 2, 3, 4, 5, 6, 7 })
            {
                var legacy = s.CaptureCity(World); legacy.Version = version;
                Check(WorkshopSession.TryRestoreCity(legacy, out var loaded, out _) && !loaded.CheatsUsed && loaded.CheatMoneyAdjustment == 0,
                    "legacy v" + version);
            }
            Check(!s.CheatSetBalance(-1, out _) && !s.CheatSetBalance(1000001, out _), "balance limits reject atomically");
            Check(s.Balance == 300 && !s.CheatsUsed, "rejections preserve state");
            Check(s.CheatSetBalance(10300, out _) && s.CheatMoneyAdjustment == 10000 && s.Revenue == 0, "money is separate from earnings");
            s = Round(s);
            Check(s.CheatSupplies(false, 10, out _) && s.SpareBatteries == 10 && s.Balance == 10300, "free batteries");
            Check(!s.CheatSupplies(true, 10, out _), "solder requires owned station");
            s.SetPaused(false);
            Check(!s.CheatSetBalance(1, out _) && !s.CheatSupplies(false, 10, out _) && !s.CheatAdvanceTime(60, out _), "commands require pause");
            Check(s.PurchaseLot(out _), "normal purchase after cheats");
            s.SetPaused(true); Check(s.CheatDiagnose(out _), "remote diagnosis");
            s = Round(s); s.SetPaused(false);
            for (var i = 0; i < 3; i++)
            {
                Check(s.ToggleUnit(i, out _), "take lamp");
                Check(s.SellBack(new[] { s.Units[i].InstanceId }, out _), "sell diagnosed lamp");
            }
            Check(s.PurchaseStation(true, out _), "normal station purchase");
            s.SetPaused(true);
            Check(s.CheatSupplies(true, 10, out _) && s.SolderDoses == 10 && s.PurchasedSolderDoses == 10, "free solder keeps purchase ledger");
            s = Round(s);
            Check(s.CheatSetBalance(0, out _), "zero balance");
            s = Round(s);
            var corrupt = s.CaptureCity(World); corrupt.Balance++;
            Check(!WorkshopSession.TryRestoreCity(corrupt, out _, out _), "cheats do not disable balance validation");
            corrupt = s.CaptureCity(World); corrupt.CheatsUsed = false;
            Check(!WorkshopSession.TryRestoreCity(corrupt, out _, out _), "adjustment requires cheat marker");
            corrupt = s.CaptureCity(World); corrupt.Version = 7;
            Check(!WorkshopSession.TryRestoreCity(corrupt, out _, out _), "legacy payload cannot contain cheat adjustment");
            corrupt = s.CaptureCity(World); corrupt.CheatMoneyAdjustment = long.MaxValue;
            Check(!WorkshopSession.TryRestoreCity(corrupt, out _, out _), "oversized ledger rejects without overflow");
            var clock = WorkshopSession.CreateCity(19); clock.SetPaused(false);
            Check(clock.AcceptOrder(DealOrderId.A, out _), "accept A"); clock.SetPaused(true);
            Check(!clock.CheatAdvanceTime(double.NaN, out _) && !clock.CheatAdvanceTime(double.PositiveInfinity, out _) &&
                !clock.CheatAdvanceTime(-1, out _) && !clock.CheatAdvanceTime(3601, out _), "invalid time rejects");
            Check(clock.CheatAdvanceTime(540, out _) && clock.Order(DealOrderId.A).State == DealOrderState.Failed, "advance fails order at exact deadline");
            Round(clock);
            Finish("DOMAIN");
        }
        finally { Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/Codex-cheats-domain.log", report.ToString()); }
    }

    [MenuItem("Tools/Cheats/Enter Isolated Play Mode")]
    public static void EnterPlay()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Leave Play Mode first.");
        Directory.CreateDirectory("Temp/CheatsVerification");
        SessionState.SetString("HustleScale.S2SaveOverride", Path.GetFullPath("Temp/CheatsVerification/city-save.json"));
        EditorApplication.isPlaying = true;
    }

    [MenuItem("Tools/Cheats/Verify Integration In Play Mode")]
    public static void Integration()
    {
        if (!EditorApplication.isPlaying || !SessionState.GetString("HustleScale.S2SaveOverride", "").Contains("CheatsVerification"))
            throw new InvalidOperationException("Isolated cheats Play Mode required.");
        checks = 0; report.Clear();
        try
        {
            var save = UnityEngine.Object.FindAnyObjectByType<CitySaveController>();
            var view = save.GetComponent<CityTradeView>();
            var walk = save.GetComponent<CityGreyboxWalk>();
            var player = save.GetComponent<PlayerController>();
            save.NewGame();
            walk.SetPaused(false); view.ToggleCheatsMenu();
            Check(walk.IsPaused && save.Session.IsPaused && view.CheatsMenuOpen && Cursor.visible, "menu pauses gameplay and releases cursor");
            Check(view.CheatAction("money1000") && save.Session.Balance == 1300, "money updates live session");
            Check(view.CheatAction("batteries") && save.Session.SpareBatteries == 10, "live supplies");
            Check(view.CheatAction("speed5") && player.CheatSpeedMultiplier == 5, "movement multiplier");
            Check(view.CheatAction("clock0") && save.CheatClockMultiplier == 0, "freeze clock");
            Check(view.CheatAction("clock5") && save.CheatClockMultiplier == 5, "accelerate clock");
            Check(view.CheatAction("reset") && player.CheatSpeedMultiplier == 1 && save.CheatClockMultiplier == 1, "reset modifiers");
            Check(view.CheatAction("speed2") && view.CheatAction("clock0"), "set modifiers before load");
            Check(view.CheatAction("advance60") && save.Session.ElapsedSeconds >= 60, "live advance");
            Check(view.CheatAction("spawn") && Vector3.Distance(player.transform.position, save.SceneBindings.PlayerSpawn.position) < .01f, "spawn uses scene binding");
            Check(view.CheatAction("status"), "diagnostic command");
            Check(!view.CheatAction("unknown"), "unknown command rejects");
            Check(save.Save() && save.Load(false), "actual store save/load");
            Check(save.Session.Balance == 1300 && save.Session.SpareBatteries == 10 && save.Session.CheatsUsed, "cheats survive file round trip");
            Check(!view.CheatsMenuOpen && player.CheatSpeedMultiplier == 1 && save.CheatClockMultiplier == 1, "load closes menu and resets transient modifiers");
            Check(!view.CheatAction("money1000"), "closed menu rejects commands");
            save.NewGame();
            Check(save.Session.Balance == 300 && !save.Session.CheatsUsed && save.Session.CheatMoneyAdjustment == 0, "new game clears cheat ledger");
            view.ToggleCheatsMenu();
            Finish("INTEGRATION");
        }
        finally { Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/Codex-cheats-playmode.log", report.ToString()); }
    }

    [MenuItem("Tools/Cheats/Verify Input In Play Mode")]
    public static void Input()
    {
        if (inputRunning) return;
        if (!EditorApplication.isPlaying || !SessionState.GetString("HustleScale.S2SaveOverride", "").Contains("CheatsVerification"))
            throw new InvalidOperationException("Isolated cheats Play Mode required.");
        inputRunning = true; checks = 0; report.Clear();
        var save = UnityEngine.Object.FindAnyObjectByType<CitySaveController>();
        var view = save.GetComponent<CityTradeView>(); var walk = save.GetComponent<CityGreyboxWalk>();
        var player = save.GetComponent<PlayerController>();
        save.NewGame(); walk.SetPaused(false);
        var keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
        var background = Application.runInBackground; Application.runInBackground = true;
        var oldInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        var oldFocus = InputSystem.settings.backgroundBehavior;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        Action<Key?> key = k => InputSystem.QueueStateEvent(keyboard, k.HasValue ? new KeyboardState(k.Value) : new KeyboardState());
        var steps = new List<Action>();
        steps.Add(() => key(Key.F8)); steps.Add(() => key(null));
        steps.Add(() =>
        {
            Check(view.CheatsMenuOpen && walk.IsPaused && !player.CanInteract, "F8 opens modal cheat menu");
            Check(UnityEngine.Object.FindObjectsByType<Button>().Count(b => b.transform.parent.name == "Pause content") == CheatMenu.Entries.Count + 1,
                "all commands have real buttons");
            var speed = UnityEngine.Object.FindObjectsByType<Button>().Single(b => b.name == "Ходьба / спринт: ×5");
            EventSystem.current.SetSelectedGameObject(speed.gameObject);
        });
        steps.Add(() => key(Key.Enter)); steps.Add(() => key(null));
        steps.Add(() =>
        {
            Check(player.CheatSpeedMultiplier == 5, "Enter activates focused command");
            var focus = EventSystem.current.currentSelectedGameObject;
            Check(focus != null && focus.name == "Ходьба / спринт: ×5", "focus survives command refresh");
            var scroll = focus.GetComponentInParent<ScrollRect>();
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, focus.transform);
            Check(bounds.min.y >= scroll.viewport.rect.yMin - 1 && bounds.max.y <= scroll.viewport.rect.yMax + 1,
                "keyboard focus scrolls offscreen command into view");
            key(Key.Escape);
        });
        steps.Add(() => key(null));
        steps.Add(() => { Check(!view.CheatsMenuOpen && walk.IsPaused, "Esc returns to pause"); key(Key.Escape); });
        steps.Add(() => key(null));
        double frozen = 0;
        steps.Add(() => { Check(!walk.IsPaused, "second Esc resumes"); key(Key.F8); });
        steps.Add(() => key(null));
        steps.Add(() => { Check(view.CheatAction("clock0"), "freeze from menu"); view.ToggleCheatsMenu(); walk.SetPaused(false); frozen = save.Session.ElapsedSeconds; });
        steps.Add(() => Check(save.Session.ElapsedSeconds == frozen, "frozen clock stays still while gameplay runs"));
        steps.Add(() => { key(Key.F8); }); steps.Add(() => key(null));
        steps.Add(() =>
        {
            Check(view.CheatsMenuOpen, "F8 reopens after resume");
            view.CheatAction("reset");
            ScreenCapture.CaptureScreenshot("Temp/CheatsVerification/input-menu.png");
        });
        var index = 0; var frame = Time.frameCount; var next = EditorApplication.timeSinceStartup;
        var deadline = next + 45;
        EditorApplication.CallbackFunction runner = null;
        Action cleanup = () =>
        {
            EditorApplication.update -= runner; inputRunning = false;
            InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.editorInputBehaviorInPlayMode = oldInput;
            InputSystem.settings.backgroundBehavior = oldFocus; Application.runInBackground = background;
        };
        runner = () =>
        {
            try
            {
                if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup > deadline) throw new InvalidOperationException("Input timeout step " + index);
                if (Time.frameCount < frame + 3 || EditorApplication.timeSinceStartup < next) return;
                if (index == steps.Count) { cleanup(); Finish("INPUT"); File.WriteAllText("Logs/Codex-cheats-input.log", report.ToString()); return; }
                steps[index++](); frame = Time.frameCount; next = EditorApplication.timeSinceStartup + .2;
            }
            catch (Exception e)
            {
                cleanup(); report.AppendLine("FAIL step " + index + " " + e);
                Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/Codex-cheats-input.log", report.ToString()); Debug.LogException(e);
            }
        };
        EditorApplication.update += runner;
    }
}
