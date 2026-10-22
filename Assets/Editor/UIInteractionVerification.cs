using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Interactables;
using Player;
using Player.Movement;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

// Called by the UTF integration tests across the predefined assembly boundary.
public static class UIInteractionVerification
{
    public static IEnumerator RunChecks(string scenario, string output, Action<bool, string> check, Action<string> log)
    {
        var trade = UnityEngine.Object.FindAnyObjectByType<CityTradeView>();
        var walk = trade.GetComponent<CityGreyboxWalk>();
        var player = trade.GetComponent<PlayerController>();
        var save = trade.GetComponent<CitySaveController>();
        var keyboard = InputSystem.AddDevice<Keyboard>("UI audit keyboard");
        var mouse = InputSystem.AddDevice<Mouse>("UI audit mouse");
        player.GetComponent<Player.PlayerInput>().MouseInputEnabled = false;
        try
        {
            save.NewGame();
            walk.SetPaused(false);
            if (scenario == "Inventory")
            {
                var session = trade.Session;
                check(session.PurchaseCityLamp(out _), "buy first inventory instance");
                check(session.PurchaseCityLamp(out _), "buy second inventory instance");
                check(session.BuyBattery(out _), "buy inventory battery");
                var firstId = session.CityUnit(0).InstanceId;
                var canvas = GameObject.Find("City trading UI").transform;
                var bar = canvas.Find("Safe area/Inventory bar");
                Func<int, string> barCount = i => bar.Find("Quick slots/Quick slot " + i).GetComponentsInChildren<Text>(true).Last().text;
                check(barCount(0) == "×2" && barCount(1) == "×1" && barCount(2) == "" && barCount(5) == "",
                    "hotbar uses owned instances and actual battery quantity");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Tab));
                yield return null; yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null; yield return null;
                check(trade.MenuOpen && walk.IsPaused && session.IsPaused, "real Tab opens modal inventory");
                check(!bar.gameObject.activeInHierarchy, "modal inventory hides duplicate hotbar");
                check(GameObject.Find("Inventory grid") != null && GameObject.Find("Карта окрестности") != null,
                    "Tab defaults to item grid with map available separately");
                var grid = GameObject.Find("Inventory grid").transform;
                check(grid.childCount == 24 && grid.GetComponentsInChildren<Image>().Count(i => i.name == "Thumbnail" && i.sprite != null) == 2,
                    "24 cells contain only the two owned item types");
                check(grid.Cast<Transform>().Skip(2).All(t => t.childCount == 0), "empty cells have no text or placeholder icons");
                check(grid.GetChild(0).GetComponentsInChildren<Text>().Any(t => t.text == "×2") &&
                    grid.GetChild(1).GetComponentsInChildren<Text>().Any(t => t.text == "×1"), "menu quantities match hotbar");
                ScreenCapture.CaptureScreenshot(Path.Combine(output, "inventory.png"));
                yield return null; yield return null;
                var position = player.transform.position; var elapsed = session.ElapsedSeconds;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.Digit1));
                yield return null; yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null; yield return null;
                check(player.transform.position == position && session.ElapsedSeconds == elapsed && session.InHands == null,
                    "inventory blocks movement, shortcuts and active time");
                var button = grid.GetChild(0).GetComponent<Button>();
                EventSystem.current.SetSelectedGameObject(button.gameObject);
                var pointer = RectTransformUtility.WorldToScreenPoint(null, button.transform.position);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer });
                yield return null; yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer, buttons = 1 });
                yield return null; yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer });
                yield return null; yield return null;
                check(session.InHands == null && GameObject.Find("Взять · Фонарь 1 · не проверен") != null,
                    "stack selection exposes individual instances");
                button = GameObject.Find("Взять · Фонарь 1 · не проверен").GetComponent<Button>();
                EventSystem.current.SetSelectedGameObject(button.gameObject);
                yield return null; yield return null;
                pointer = RectTransformUtility.WorldToScreenPoint(null, button.transform.position);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer });
                yield return null; yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer, buttons = 1 });
                yield return null; yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer });
                yield return null; yield return null;
                check(session.InHands?.InstanceId == firstId && trade.MenuOpen && session.IsPaused,
                    "real instance click equips same ID and keeps menu paused");
                Layout(check, "populated inventory");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Tab));
                yield return null; yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null; yield return null;
                check(!trade.MenuOpen && !walk.IsPaused && bar.gameObject.activeInHierarchy, "second Tab restores game and hotbar");
                check(barCount(0) == "×2", "held instance remains counted once in stack");
                ScreenCapture.CaptureScreenshot(Path.Combine(output, "hotbar.png"));
                yield return null; yield return null;
                check(session.ToggleBench(out _), "move held item to bench");
                check(barCount(0) == "×1", "bench item no longer counts in hotbar stack");
                trade.SendMessage("Inventory"); yield return null; yield return null;
                grid = GameObject.Find("Inventory grid").transform;
                grid.GetChild(0).GetComponent<Button>().onClick.Invoke();
                yield return null; yield return null;
                check(GameObject.Find("Взять · Фонарь 1 · не проверен") == null && GameObject.Find("Взять · Фонарь 2 · не проверен") != null,
                    "bench item is excluded from inventory and cannot be equipped remotely");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                yield return null; yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null; yield return null;
                check(!trade.MenuOpen && !walk.IsPaused, "Esc closes inventory without opening pause");
            }
            else if (scenario == "Shortcuts")
            {
                trade.SendMessage("Inventory");
                var session = trade.Session;
                var originalFile = File.ReadAllText(Path.Combine(output, "city-save.json"));
                foreach (var key in new[] { Key.N, Key.F5, Key.F9, Key.B })
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
                    yield return null; yield return null;
                    check(!walk.ConfirmNewGame, "map must not arm a hidden new-game confirmation: " + key);
                    check(ReferenceEquals(session, trade.Session) && trade.MenuOpen, "map owns input over pause shortcut " + key);
                    check(File.ReadAllText(Path.Combine(output, "city-save.json")) == originalFile, "map shortcut does not write save " + key);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    yield return null; yield return null;
                }
                trade.CloseMenu(); walk.SetPaused(true);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.N));
                yield return null; yield return null;
                check(walk.ConfirmNewGame, "N still opens visible pause confirmation");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null; yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                yield return null; yield return null;
                check(!walk.ConfirmNewGame && walk.IsPaused, "Escape cancels confirmation without resuming");
            }
            else if (scenario == "Transitions")
            {
                var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
                target.name = "UI input regression target";
                target.transform.localScale = Vector3.one * 0.3f;
                player.RestorePose(new Vector3(-65, 0.08f, 25), 0, 0);
                target.transform.position = player.GetComponentInChildren<Camera>().transform.position + Vector3.forward * 2;
                var probe = target.AddComponent<GrayboxInteractable>();
                Physics.SyncTransforms();
                try
                {
                    foreach (var closeKey in new[] { Key.Tab, Key.Escape })
                    {
                        trade.SendMessage("Inventory");
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState(closeKey, Key.E));
                        yield return null; yield return null;
                        check(!trade.MenuOpen && !walk.IsPaused, closeKey + " closes only the current menu");
                        check(probe.InteractionCount == 0, closeKey + "+E must not interact through closing UI");
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                        yield return null; yield return null;
                    }
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
                    yield return null; yield return null;
                    check(probe.InteractionCount == 1, "fresh E after menu closure works exactly once");
                    for (var i = 0; i < 5; i++) yield return null;
                    check(probe.InteractionCount == 1, "held E does not repeat");
                }
                finally { UnityEngine.Object.Destroy(target); }
            }
            else if (scenario == "VehicleEntry" || scenario == "VehiclePause")
            {
                var car = UnityEngine.Object.FindAnyObjectByType<CityUtilityVehicle>();
                var feet = car.transform.TransformPoint(new Vector3(-2.5f, 0, 0.4f)); feet.y = 0.08f;
                var door = car.transform.TransformPoint(new Vector3(-1.1f, 1, 0.4f));
                var rotation = Quaternion.LookRotation(door - (feet + Vector3.up * 1.65f)).eulerAngles;
                player.RestorePose(feet, rotation.y, Mathf.DeltaAngle(0, rotation.x)); Physics.SyncTransforms();
                log("vehicle fixture before input: frame=" + Time.frameCount + " canInteract=" + player.CanInteract +
                    " speed=" + car.Speed + " keyboard=" + keyboard.name + " current=" + Keyboard.current.name);
                // Settle device/action-map initialization and establish a released
                // state before the first entry edge (the entry scenario also does
                // this between each of its menu transitions).
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null; yield return null;
                if (scenario == "VehicleEntry")
                    foreach (var closeKey in new[] { Key.Tab, Key.Escape })
                    {
                        trade.SendMessage("Inventory");
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState(closeKey, Key.E));
                        yield return null; yield return null;
                        check(!car.IsDriving, closeKey + "+E must not enter vehicle through closing map");
                        check(!trade.MenuOpen && !walk.IsPaused, "map closes without another action");
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                        yield return null; yield return null;
                    }
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
                yield return null; yield return null;
                check(car.IsDriving, "fresh E enters vehicle; speed=" + car.Speed + " canInteract=" + player.CanInteract +
                    " E=" + keyboard.eKey.isPressed + " Tab=" + keyboard.tabKey.wasPressedThisFrame +
                    " Escape=" + keyboard.escapeKey.wasPressedThisFrame);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null; yield return null;
                car.SetPaused(true);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape, Key.E));
                yield return null; yield return null;
                check(car.IsDriving && !car.IsPaused, "Escape+E resumes vehicle without exiting it");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null; yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
                yield return null; yield return null;
                check(!car.IsDriving && player.CanInteract, "fresh E exits vehicle and restores walking controls");
            }
            else if (scenario == "Screens")
            {
                var sizes = new[] { new Vector2Int(1280, 720), new Vector2Int(1024, 768), new Vector2Int(1920, 1080), new Vector2Int(2560, 1440),
                    new Vector2Int(1920, 1200), new Vector2Int(3440, 1440) };
                var group = SizeGroup();
                var game = EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView"));
                var property = game.GetType().GetProperty("selectedSizeIndex");
                var original = (int)property.GetValue(game);
                var added = new List<int>();
                try
                {
                    foreach (var size in sizes)
                    {
                        var index = AddSize(group, size); added.Add(index);
                        property.SetValue(game, index); game.Repaint();
                        yield return null; yield return null;
                        check(Screen.width == size.x && Screen.height == size.y, "actual Game View " + size);
                        foreach (var scale in new[] { 1f, 1.5f })
                        {
                            trade.SetTextScale(scale);
                            foreach (var kind in new[] { CityTradeKind.Seller, CityTradeKind.A, CityTradeKind.B, CityTradeKind.Buyback })
                            {
                                trade.CloseMenu(); walk.SetPaused(false);
                                var point = trade.Points.First(p => p.Kind == kind);
                                var feet = point.transform.position; feet.y = 0.08f; feet.z -= kind == CityTradeKind.A || kind == CityTradeKind.B ? 2.1f : 1.5f;
                                var rotation = Quaternion.LookRotation(point.GetComponent<Collider>().bounds.center - (feet + Vector3.up * 1.65f)).eulerAngles;
                                player.RestorePose(feet, rotation.y, Mathf.DeltaAngle(0, rotation.x)); Physics.SyncTransforms();
                                trade.Open(point);
                                check(trade.MenuOpen, "opens " + kind);
                                yield return null; yield return null;
                                Layout(check, size + " " + scale + " " + kind);
                                if (kind == CityTradeKind.Seller && scale == 1.5f)
                                { ScreenCapture.CaptureScreenshot(Path.Combine(output, "seller-" + size.x + "x" + size.y + ".png")); yield return null; }
                            }
                            trade.CloseMenu(); trade.SendMessage("Inventory");
                            yield return null; yield return null;
                            Layout(check, size + " map " + scale);
                            if (size == sizes[0] && scale == 1.5f)
                            { ScreenCapture.CaptureScreenshot(Path.Combine(output, "map-1280x720.png")); yield return null; }
                            var controls = GameObject.Find("Управление");
                            if (controls != null) controls.GetComponent<Button>().onClick.Invoke();
                            yield return null; yield return null;
                            Layout(check, size + " expanded controls " + scale);
                            var before = trade.Session.ElapsedSeconds;
                            var pose = player.transform.position;
                            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.E, Key.Q, Key.T, Key.R));
                            yield return null; yield return null;
                            check(player.transform.position == pose && trade.Session.ElapsedSeconds == before && !player.CanInteract,
                                "modal blocks movement, time and world actions");
                            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                            yield return null; yield return null;
                            trade.CloseMenu(); walk.SetPaused(true);
                            yield return null; yield return null;
                            Layout(check, size + " pause " + scale);
                            if (size == sizes[0] && scale == 1.5f)
                            { ScreenCapture.CaptureScreenshot(Path.Combine(output, "pause-1280x720.png")); yield return null; }
                        }
                    }
                    // Real pointer down/up goes through the UI input module.
                    var resume = GameObject.Find("Продолжить · Esc").GetComponent<Button>();
                    Canvas.ForceUpdateCanvases();
                    var position = RectTransformUtility.WorldToScreenPoint(null, resume.GetComponent<RectTransform>().position);
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = position }); yield return null; yield return null;
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = position, buttons = 1 }); yield return null; yield return null;
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = position }); yield return null; yield return null;
                    check(!walk.IsPaused && player.CanInteract, "pointer click resumes gameplay");
                    walk.SendMessage("OnApplicationFocus", false); yield return null; yield return null;
                    check(walk.IsPaused && trade.Session.IsPaused && !player.CanInteract && Cursor.visible, "focus loss pauses and frees cursor");
                    walk.SendMessage("OnApplicationFocus", true); yield return null;
                    check(walk.IsPaused, "focus regain needs explicit resume");
                    check(UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == 1, "one EventSystem");
                }
                finally
                {
                    property.SetValue(game, original);
                    for (var i = added.Count - 1; i >= 0; i--) group.GetType().GetMethod("RemoveCustomSize").Invoke(group, new object[] { added[i] });
                }
            }
            else throw new ArgumentException(scenario);
            log("PASS " + scenario);
        }
        finally
        {
            InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse);
        }
    }

    private static void Layout(Action<bool, string> check, string context)
    {
        Canvas.ForceUpdateCanvases();
        var canvas = GameObject.Find("City trading UI");
        foreach (var text in canvas.GetComponentsInChildren<Text>())
        {
            if (text.text.Length == 0) continue;
            check(text.preferredHeight <= text.rectTransform.rect.height + 0.1f,
                context + " text fits: " + text.text + " height " + text.rectTransform.rect.height + "/" + text.preferredHeight);
        }
        var selected = EventSystem.current.currentSelectedGameObject;
        check(selected != null && selected.activeInHierarchy, context + " live focus");
        check(Cursor.visible && Cursor.lockState == CursorLockMode.None, context + " menu cursor");
    }

    private static object SizeGroup()
    {
        var type = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizes");
        var instance = type.BaseType.GetProperty("instance").GetValue(null);
        return type.GetProperty("currentGroup").GetValue(instance);
    }

    private static int AddSize(object group, Vector2Int size)
    {
        var assembly = typeof(Editor).Assembly;
        var value = Activator.CreateInstance(assembly.GetType("UnityEditor.GameViewSize"),
            Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeType"), "FixedResolution"), size.x, size.y, "UI audit");
        var index = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null);
        group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { value });
        return index;
    }
}
