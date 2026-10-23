using System;
using System.Collections;
using System.IO;
using System.Linq;
using Inventory;
using Interactables;
using Player;
using Player.Movement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

public static partial class UIInteractionVerification
{
    private static IEnumerator Frames(int count = 4) { for (var i = 0; i < count; i++) yield return null; }
    private static IEnumerator FeedbackSeconds(float seconds)
    {
        var until = Time.timeAsDouble + seconds;
        while (Time.timeAsDouble < until) yield return null;
    }
    private static IEnumerator ClickFeedbackButton(Button button, Mouse mouse)
    {
        EventSystem.current.SetSelectedGameObject(button.gameObject);
        // The focus helper scrolls the chosen control into view before the actual pointer click.
        yield return Frames();
        var p = RectTransformUtility.WorldToScreenPoint(null, button.transform.position);
        InputSystem.QueueStateEvent(mouse, new MouseState { position = p }); yield return Frames();
        InputSystem.QueueStateEvent(mouse, new MouseState { position = p, buttons = 1 }); yield return Frames();
        InputSystem.QueueStateEvent(mouse, new MouseState { position = p }); yield return Frames();
    }
    private static Button FeedbackButton(string prefix) => UnityEngine.Object.FindObjectsByType<Button>().Single(b => b.name.StartsWith(prefix, StringComparison.Ordinal));
    private static IEnumerator RunFeedback2(string scenario, string output, CityTradeView trade, Keyboard keyboard, Mouse mouse, Action<bool, string> check)
    {
        var save = trade.GetComponent<CitySaveController>(); var walk = trade.GetComponent<CityGreyboxWalk>();
        var player = trade.GetComponent<PlayerController>();
        if (scenario == "InventoryStacks")
        {
            var s = save.Session;
            for (var i = 0; i < 3; i++) check(s.PurchaseCityLamp(out _), "purchase one ordinary lamp");
            var ids = s.Units.Select(u => u.InstanceId).ToArray();
            check(s.EquipSlot(0, out _) && s.ToggleBench(out _) && s.Test(out _) && s.ToggleBench(out _) && s.StowHeld(out _), "test exactly first of three lamps");
            var money = s.Balance; var diagnosis = s.CityUnit(0).KnownCondition;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Tab)); yield return Frames();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return Frames();
            check(trade.MenuOpen && GameObject.Find("Trade menu").GetComponent<Image>().enabled, "Tab has opaque readable menu surface");
            yield return ClickFeedbackButton(FeedbackButton("Инвентарь"), mouse);
            check(GameObject.Find("Inventory grid").GetComponentsInChildren<Text>().Count(t => t.text == "×2") == 1 &&
                GameObject.Find("Inventory grid").GetComponentsInChildren<Text>().Count(t => t.text == "×1") == 1, "diagnosed first lamp automatically separates from two unknown lamps");
            // Inspect the group containing the two unknown IDs, regardless of the selected sort order.
            var unknownCell = GameObject.Find("Inventory grid").transform.Cast<Transform>().First(c => c.GetComponentsInChildren<Text>().Any(t => t.text == "×2"));
            yield return ClickFeedbackButton(unknownCell.GetComponent<Button>(), mouse);
            var viewport = GameObject.Find("Trade menu").GetComponent<ScrollRect>().viewport;
            foreach (var b in UnityEngine.Object.FindObjectsByType<Button>().Where(b => b.name.StartsWith("Взять · Фонарь")))
            {
                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, b.transform);
                check(bounds.min.y >= viewport.rect.yMin - 1 && bounds.max.y <= viewport.rect.yMax + 1, "all three item actions visible: " + b.name);
            }
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "diagnosis-stacks.png")); yield return Frames();
            yield return ClickFeedbackButton(FeedbackButton("Взять · Фонарь 2"), mouse);
            check(s.InHands?.InstanceId == ids[1] && !s.InHands.IsTested, "real click selects second untested ID after first diagnosis");
            yield return ClickFeedbackButton(FeedbackButton("Взять · Фонарь 3"), mouse);
            check(s.InHands?.InstanceId == ids[2] && !s.InHands.IsTested, "real click selects third untested ID");
            yield return ClickFeedbackButton(FeedbackButton("Отделить один фонарь"), mouse);
            check(GameObject.Find("Inventory grid").GetComponentsInChildren<Text>().Count(t => t.text == "×1") == 3, "split unknown pair preserves separate diagnosed stack");
            check(GameObject.Find("Inventory grid").GetComponentsInChildren<Text>().Count(t => t.text == "×1") == 3, "split remainder into individual slots");
            yield return ClickFeedbackButton(FeedbackButton("Сортировка: тип"), mouse);
            check(s.Units.Select(u => u.InstanceId).SequenceEqual(ids) && s.CityUnit(0).KnownCondition == diagnosis && s.Balance == money, "sorting preserves IDs, known diagnoses and money");
            check(save.Save() && save.Load(false), "actual save/load of stack partition"); walk.SetPaused(false);
            check(save.World.InventoryLampStacks.Length == 2 && save.World.InventorySort == 1, "stack partition and sort restored");
            var snapshot = save.Session.CaptureCity(save.World);
            snapshot.World.InventoryLampStacks = new[] { 3, 6 };
            check(!WorkshopSession.TryRestoreCity(snapshot, out _, out _), "overlapping saved stack partition rejected");
            // The captured world is the live DTO; restore only the test's deliberately corrupted presentation field.
            save.World.InventoryLampStacks = new[] { 5, 2 };
            trade.SendMessage("Inventory"); yield return Frames();
            yield return ClickFeedbackButton(FeedbackButton("Инвентарь"), mouse);
            var grid = GameObject.Find("Inventory grid").transform;
            yield return ClickFeedbackButton(grid.GetChild(0).GetComponent<Button>(), mouse);
            yield return ClickFeedbackButton(FeedbackButton("Объединить одинаковые стопки"), mouse);
            check(GameObject.Find("Inventory grid").GetComponentsInChildren<Text>().Count(t => t.text == "×2") == 1, "merge combines only equal known conditions without cloning goods");
            check(save.Session.Units.Select(u => u.InstanceId).SequenceEqual(ids) && save.Session.Balance == money, "load and merge preserve all owned IDs and money");
            trade.CloseMenu();
            var seed = 0;
            for (; ; seed++) { var r = new System.Random(seed); if (r.Next(2) == 0 && r.Next(2) == 1) break; }
            var candidate = WorkshopSession.CreateCity(seed); candidate.SetPaused(false);
            for (var i = 0; i < 3; i++) check(candidate.PurchaseCityLamp(out _), "buy deterministic diagnosis fixture");
            for (var i = 0; i < 2; i++) check(candidate.EquipSlot(i, out _) && candidate.ToggleBench(out _) && candidate.Test(out _) && candidate.ToggleBench(out _) && candidate.StowHeld(out _), "diagnose working and faulty IDs");
            typeof(CitySaveController).GetMethod("Bind", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(save, new object[] { candidate, new CityWorldData { PlacedLamps = Array.Empty<CityPlacedLamp>() } });
            trade.SendMessage("Inventory"); yield return Frames();
            yield return ClickFeedbackButton(FeedbackButton("Инвентарь"), mouse);
            var categoryCells = GameObject.Find("Inventory grid").transform.Cast<Transform>().Where(c => c.childCount > 0).ToArray();
            check(categoryCells.Length == 3 && categoryCells.Select(c => c.GetComponent<Image>().color).Distinct().Count() == 3, "three known-state stacks have three different colors");
            check(categoryCells.SelectMany(c => c.GetComponentsInChildren<Text>()).Any(t => t.text == "Не пров.") &&
                categoryCells.SelectMany(c => c.GetComponentsInChildren<Text>()).Any(t => t.text == "Ремонт") &&
                categoryCells.SelectMany(c => c.GetComponentsInChildren<Text>()).Any(t => t.text == "Исправен"), "color labels distinguish unknown, faulty and working without hidden RNG leakage");
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "inventory-stacks.png")); yield return Frames(); trade.CloseMenu();
            check(candidate.EquipSlot(1, out _) && candidate.ToggleBench(out _) && candidate.BuyBattery(out _) && candidate.Repair(out _) && candidate.ToggleBench(out _) && candidate.StowHeld(out _), "repair faulty battery without final test");
            trade.SendMessage("Inventory"); yield return Frames(); yield return ClickFeedbackButton(FeedbackButton("Инвентарь"), mouse);
            var unknown = GameObject.Find("Inventory grid").transform.Cast<Transform>().Single(c => c.GetComponentsInChildren<Text>().Any(t => t.text == "Не пров."));
            check(unknown.GetComponentsInChildren<Text>().Any(t => t.text == "×2"), "repaired but untested item joins unknown stack until retest"); trade.CloseMenu();
            check(candidate.EquipSlot(1, out _) && candidate.ToggleBench(out _) && candidate.Test(out _) && candidate.ToggleBench(out _) && candidate.StowHeld(out _), "retest repaired ID");
            trade.SendMessage("Inventory"); yield return Frames(); yield return ClickFeedbackButton(FeedbackButton("Инвентарь"), mouse);
            var working = GameObject.Find("Inventory grid").transform.Cast<Transform>().Single(c => c.GetComponentsInChildren<Text>().Any(t => t.text == "Исправен"));
            check(working.GetComponentsInChildren<Text>().Any(t => t.text == "×2"), "retested repaired item joins existing working stack");
            trade.CloseMenu();
        }
        else if (scenario == "WorkshopPresentation")
        {
            var b = save.SceneBindings;
            var solder = b.Station;
            var sharp = trade.SharpeningSite;
            var data = new UnityEditor.SerializedObject(solder);
            var preview = (GameObject)data.FindProperty("preview").objectReferenceValue;
            var plaque = (GameObject)data.FindProperty("plaque").objectReferenceValue;
            Action<WorkshopSession> bind = candidate => typeof(CitySaveController).GetMethod("Bind", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(save, new object[] { candidate, new CityWorldData { PlacedLamps = Array.Empty<CityPlacedLamp>() } });
            bind(M7SharpeningVerification.Funded()); walk.SetPaused(false); yield return Frames();
            check(!preview.activeInHierarchy && !sharp.Preview.activeInHierarchy && !plaque.activeInHierarchy && !sharp.Plaque.activeInHierarchy, "unlocked but unpurchased crafts have no silhouette or floating labels");
            check(!solder.Station.gameObject.activeInHierarchy && !sharp.Station.gameObject.activeInHierarchy, "unowned station models remain absent");
            player.RestorePose(new Vector3(-69, .08f, 32), 0, 0);
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "workshop-unpurchased.png")); yield return Frames();
            check(save.Session.PurchaseStation(true, out _), "buy soldering station"); yield return Frames();
            check(plaque.activeInHierarchy && plaque.transform.position.z > 39.5f && !preview.activeInHierarchy, "purchased soldering has small wall plaque, no preview");
            check(solder.Station.GetComponentsInChildren<TextMesh>(true).All(t => t.text != "ПАЙКА"), "city trial has no duplicate floating craft title");
            bind(M7SharpeningVerification.Funded()); walk.SetPaused(false);
            check(save.Session.PurchaseSharpeningStation(true, out _), "buy sharpening station"); yield return Frames();
            check(sharp.Plaque.activeInHierarchy && sharp.Plaque.transform.position.z > 39.5f && !sharp.Preview.activeInHierarchy, "purchased sharpening has wall label, no floating place marker");
            var shell = GameObject.Find("WorkshopShell");
            check(shell.GetComponentsInChildren<MeshFilter>().All(f => f.sharedMesh != null), "reimported workshop preserves every mesh reference");
            var glass = shell.GetComponentsInChildren<MeshFilter>().Single(f => f.name.StartsWith("Building surface Glass"));
            check(glass.sharedMesh.triangles.Length == 6 * 12 * 3, "six authored glass window boxes replace twenty openings");
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "workshop-purchased.png")); yield return Frames();
        }
        else
        {
            player.RestorePose(new Vector3(0, .08f, 24), 0, 0); player.SetCursorLocked(true); yield return Frames(15);
            var start = player.transform.position;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W)); yield return FeedbackSeconds(.6f);
            var walking = Vector3.Distance(start, player.transform.position);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return Frames();
            player.RestorePose(start, 0, 0); yield return Frames();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.LeftShift)); yield return FeedbackSeconds(.6f);
            var running = Vector3.Distance(start, player.transform.position);
            check(running > walking * 1.65f && running < walking * 2.35f, "real Shift sprint doubles walking distance: " + walking + "/" + running);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return Frames();
            player.RestorePose(start, 0, 0); yield return Frames(10); var ground = player.transform.position.y;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space)); yield return FeedbackSeconds(.2f);
            check(player.transform.position.y > ground + .25f, "real Space jumps above ground");
            yield return FeedbackSeconds(.9f);
            check(Mathf.Abs(player.transform.position.y - ground) < .12f, "holding Space lands without auto jumping");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return Frames();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Tab)); yield return Frames();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return Frames();
            var before = player.transform.position; var time = save.Session.ElapsedSeconds;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.LeftShift, Key.Space)); yield return FeedbackSeconds(.25f);
            check(Vector3.Distance(player.transform.position, before) < .01f && save.Session.ElapsedSeconds == time, "Tab blocks jump/sprint/movement and active clock");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return Frames(); trade.CloseMenu();
        }
    }
}
