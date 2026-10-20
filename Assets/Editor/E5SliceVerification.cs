using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Interactables;
using Inventory;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

public static partial class S3Verification
{
    private const string SliceOutput = "docs/ArtDirection/UnityTest/E5-owner-fixes";
    private static int sliceOriginalSize;
    private static readonly List<int> sliceSizes = new List<int>();

    [MenuItem("Tools/E5/Verify Integrated Slice")]
    public static void RunSlice()
    {
        SessionState.SetBool(Key + ".E5", true);
        try { RunCityVerification(false); }
        catch { SessionState.EraseBool(Key + ".E5"); throw; }
    }

    private static void SliceResolution(int width, int height)
    {
        var assembly = typeof(Editor).Assembly;
        var type = assembly.GetType("UnityEditor.GameViewSizes");
        var instance = type.BaseType.GetProperty("instance").GetValue(null, null);
        var group = type.GetProperty("currentGroup").GetValue(instance, null);
        var size = Activator.CreateInstance(assembly.GetType("UnityEditor.GameViewSize"),
            Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeType"), "FixedResolution"), width, height, "E5 " + width + "x" + height);
        var index = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null);
        group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
        var game = EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView"));
        if (sliceSizes.Count == 0) sliceOriginalSize = (int)game.GetType().GetProperty("selectedSizeIndex").GetValue(game, null);
        sliceSizes.Add(index);
        game.GetType().GetProperty("selectedSizeIndex").SetValue(game, index, null); game.Repaint();
    }

    private static void RestoreSliceResolution()
    {
        if (sliceSizes.Count == 0) return;
        var assembly = typeof(Editor).Assembly;
        var game = EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView"));
        game.GetType().GetProperty("selectedSizeIndex").SetValue(game, sliceOriginalSize, null);
        var type = assembly.GetType("UnityEditor.GameViewSizes");
        var instance = type.BaseType.GetProperty("instance").GetValue(null, null);
        var group = type.GetProperty("currentGroup").GetValue(instance, null);
        for (var i = sliceSizes.Count - 1; i >= 0; i--)
            group.GetType().GetMethod("RemoveCustomSize").Invoke(group, new object[] { sliceSizes[i] });
        sliceSizes.Clear();
    }

    private static void SliceLayout(string context)
    {
        Canvas.ForceUpdateCanvases();
        foreach (var text in GameObject.Find("City trading UI").GetComponentsInChildren<Text>())
        {
            if (text.text.Length == 0) continue;
            Check(text.preferredHeight <= text.rectTransform.rect.height + 1,
                context + " text height " + text.text + " preferred " + text.preferredHeight + " actual " + text.rectTransform.rect.height);
            Check(text.rectTransform.rect.width >= text.fontSize, context + " nonzero text width");
        }
        var focused = EventSystem.current.currentSelectedGameObject;
        Check(focused != null && focused.activeInHierarchy, context + " live keyboard focus");
    }

    private static IEnumerator SliceScenario()
    {
        Directory.CreateDirectory(SliceOutput);
        save.NewGame(); walk.SetPaused(false);
        foreach (var point in trade.Points.Where(p => p.Kind != CityTradeKind.Cargo))
            Check(ReferenceEquals(point.Prompt, point.Prompt), "static interaction prompt is reused");
        var actors = UnityEngine.Object.FindObjectsByType<CityTradeReaction>(FindObjectsSortMode.None);
        Check(actors.Length == 2, "only necessary seller and A character set");
        var sceneRoot = GameObject.Find("CityGreybox - generated");
        var oldDirection = sceneRoot.GetComponentsInChildren<MeshRenderer>(true).Single(r => r.name == "Local board | < BASE MARKET >");
        Check(!oldDirection.gameObject.activeInHierarchy, "old orange backing is disabled, not only its lettering");
        var solidShop = sceneRoot.GetComponentsInChildren<BoxCollider>().Single(c => c.name == "Building | Market corner shop");
        Check(!solidShop.bounds.Contains(actors.Single(a => a.name == "Покупатель A").transform.position + Vector3.up), "buyer A stands outside solid shop");
        Check(Vector3.Dot(-actors.Single(a => a.name == "Продавец").transform.forward, Vector3.forward) > 0.9f, "seller faces north approach from crate and street");
        foreach (var kind in new[] { CityTradeKind.A, CityTradeKind.B })
        {
            var sign = GameObject.Find("Вывеска | ЗАКАЗ " + (kind == CityTradeKind.A ? "A · 2 исправных · $260" : "Б · 1 исправный · $125"));
            Check(sign != null, "visible named order sign " + kind);
            var counter = sceneRoot.GetComponentsInChildren<Renderer>().Single(r => r.name == "Recessed market counter" && Mathf.Abs(r.transform.position.x - Point(kind).transform.position.x) < 0.1f);
            Check(!counter.bounds.Contains(sign.transform.position), "order sign outside counter " + kind);
            Check(Physics.Linecast(Point(kind).transform.position + new Vector3(0, 0.35f, -2.1f), sign.transform.position,
                out var signHit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && signHit.collider.gameObject == sign,
                "visible order sign is the first street interaction surface " + kind);
        }
        foreach (var kind in new[] { CityTradeKind.A, CityTradeKind.B, CityTradeKind.Seller })
        {
            var card = trade.Points.Single(p => p.Kind == kind && p.name.StartsWith("Вывеска |"));
            var approach = kind == CityTradeKind.Seller ? Vector3.forward : Vector3.back;
            var feet = card.transform.position + approach * 2.1f; feet.y = 0.08f;
            Aim(card, feet); yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.E)); yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            Check(trade.MenuOpen, "E aimed at visible storefront card opens " + kind);
            trade.CloseMenu();
        }
        Check(GameObject.Find("Площадка двора мастерской") != null &&
            GameObject.Find("City trade runtime").GetComponentsInChildren<Collider>().Count(c => c.name == "Низкое ограждение двора" && c.enabled) == 2,
            "recognisable paved courtyard with physical side boundaries and open central approach");
        foreach (var actor in actors)
        {
            var animator = actor.GetComponentInChildren<Animator>();
            Check(actor.GetComponentsInChildren<Transform>().Any(t => t.name == "Head"), "reaction head exists");
            Check(animator != null && animator.avatar != null && animator.avatar.isValid && !animator.applyRootMotion, "rig and idle without root motion");
            Check(!actor.GetComponentsInChildren<Collider>().Any(c => c.enabled), "NPC does not obstruct routes");
        }
        walk.SetPaused(true);
        // Pause autosave captures the newly aimed player pose; compare reactions after that snapshot.
        var before = JsonUtility.ToJson(save.Session.CaptureCity(save.World));
        foreach (var actor in actors) actor.React(true);
        yield return null; yield return null;
        Check(actors.All(a => a.Reacting && a.GetComponentInChildren<Animator>().speed == 0), "NPC reaction and idle freeze in pause");
        foreach (var actor in actors) { actor.enabled = false; Check(!actor.Reacting, "disable clears transient reaction"); actor.enabled = true; }
        Check(before == JsonUtility.ToJson(save.Session.CaptureCity(save.World)), "presentation leaves economy untouched");
        walk.SetPaused(false);
        var reactingActor = actors.Single(a => a.name == "Продавец");
        var reactionHead = reactingActor.GetComponentsInChildren<Transform>().First(t => t.name == "Head");
        var neutralHead = reactionHead.localRotation;
        reactingActor.React(true);
        var nodTime = Time.time + 0.2f;
        while (Time.time < nodTime) yield return null;
        Check(Quaternion.Angle(neutralHead, reactionHead.localRotation) > 1, "acknowledgement visibly moves head bone");
        walk.SetPaused(true); yield return null;
        var pausedHead = reactionHead.localRotation; yield return null; yield return null;
        Check(Quaternion.Angle(pausedHead, reactionHead.localRotation) < 0.01f, "head pose remains fixed while paused");
        reactingActor.enabled = false; reactingActor.enabled = true; walk.SetPaused(false);
        SliceResolution(1600, 900); yield return null; yield return null;
        foreach (var pose in new[]
        {
            ("01-workshop", new Vector3(-69, 0.08f, 30), new Vector3(-72, 1.2f, 34)),
            ("02-yard", new Vector3(-62, 0.08f, 4), new Vector3(-65, 1.2f, 18)),
            ("03-seller", new Vector3(87, 0.08f, -30), new Vector3(88, 1.8f, -36.9f)),
            ("04-buyer", new Vector3(-28, 0.08f, 5), new Vector3(-27.5f, 1.7f, 11)),
            ("06-wayfinding", new Vector3(-38, 0.08f, 0), new Vector3(-42, 2.4f, 3)),
            ("07-buyers", new Vector3(-18, 0.08f, -10), new Vector3(-18, 1.8f, 11))
        })
        {
            var angles = Quaternion.LookRotation(pose.Item3 - (pose.Item2 + Vector3.up * 1.65f)).eulerAngles;
            player.RestorePose(pose.Item2, angles.y, Mathf.DeltaAngle(0, angles.x));
            yield return null; yield return null;
            ScreenCapture.CaptureScreenshot(SliceOutput + "/" + pose.Item1 + ".png"); yield return null;
        }
        foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1600, 900), new Vector2Int(1600, 1000), new Vector2Int(2100, 900) })
        {
            SliceResolution(size.x, size.y); yield return null; yield return null;
            Check(Screen.width == size.x && Screen.height == size.y, "actual Game View resolution " + size);
            foreach (var scale in new[] { 1f, 1.5f })
            {
                trade.SetTextScale(scale);
                foreach (var kind in new[] { CityTradeKind.Seller, CityTradeKind.Workshop, CityTradeKind.A, CityTradeKind.Crate })
                {
                    Open(kind); yield return null; yield return null;
                    SliceLayout(size + " scale " + scale + " " + kind);
                    if (kind == CityTradeKind.Seller && size == new Vector2Int(1600, 900))
                    {
                        ScreenCapture.CaptureScreenshot(SliceOutput + "/shop-" + Mathf.RoundToInt(scale * 100) + ".png"); yield return null;
                    }
                    var buttons = GameObject.Find("City trading UI").GetComponentsInChildren<Button>().Where(b => b.interactable).ToArray();
                    EventSystem.current.SetSelectedGameObject(buttons.Last().gameObject);
                    yield return null; yield return null;
                    var viewport = GameObject.Find("Viewport").GetComponent<RectTransform>();
                    var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, buttons.Last().transform);
                    Check(bounds.min.y >= viewport.rect.yMin - 1 && bounds.max.y <= viewport.rect.yMax + 1, "keyboard selection scrolls into view");
                    if (kind == CityTradeKind.A && scale == 1.5f)
                    {
                        EventSystem.current.SetSelectedGameObject(GameObject.Find("Text size"));
                        GameObject.Find("Trade menu").GetComponent<ScrollRect>().verticalNormalizedPosition = 1;
                        yield return null; yield return null;
                        ScreenCapture.CaptureScreenshot(SliceOutput + "/menu-" + size.x + "x" + size.y + "-150.png"); yield return null;
                    }
                    trade.CloseMenu();
                }
                // The map is a separate long flow with world-relative labels.
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.Tab)); yield return null; yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
                Check(trade.MenuOpen, "map opens at each scale"); SliceLayout("map " + size + " " + scale);
                trade.CloseMenu();
                walk.SetPaused(true); yield return null; yield return null;
                Check(GameObject.Find("City pause menu") != null && !trade.MenuOpen, "Esc pause uses uGUI");
                SliceLayout("pause " + size + " " + scale);
                if (scale == 1.5f)
                {
                    ScreenCapture.CaptureScreenshot(SliceOutput + "/pause-" + size.x + "x" + size.y + "-150.png"); yield return null;
                }
                var oldBalance = save.Session.Balance;
                walk.PauseAction("new"); yield return null; yield return null;
                Check(walk.ConfirmNewGame, "new game has explicit confirmation");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.Escape)); yield return null; yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
                Check(!walk.ConfirmNewGame && walk.IsPaused && save.Session.Balance == oldBalance, "Esc cancels new game without resuming or resetting economy");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.Escape)); yield return null; yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
                Check(!walk.IsPaused && GameObject.Find("City pause menu") == null, "Esc resumes and hides pause card");
            }
        }
        SliceResolution(1600, 900); yield return null; yield return null;
        trade.SetTextScale(1.5f); Open(CityTradeKind.Seller); Command("purchase");
        Check(GameObject.Find("City trade runtime").GetComponent<AudioSource>().isPlaying, "successful purchase emits local audio cue");
        Check(GameObject.Find("City trading UI").GetComponentsInChildren<Text>().Any(t => t.text.Contains("Партия куплена")), "purchase result visible");
        Command("battery");
        Check(save.Session.SpareBatteries == 1 && save.Session.Balance == 70, "seller also supplies batteries at unchanged price");
        Check(actors.Single(a => a.name == "Продавец").Reacting, "seller acknowledges purchase");
        var pausedTime = save.Session.ElapsedSeconds; yield return null; yield return null;
        Check(save.Session.ElapsedSeconds == pausedTime, "result remains paused");
        trade.CloseMenu();
        var reactionEnd = Time.time + 1.1f;
        while (Time.time < reactionEnd) yield return null;
        Check(!actors.Single(a => a.name == "Продавец").Reacting, "acknowledgement finishes on resume");
        Open(CityTradeKind.A);
        var firstButton = EventSystem.current.currentSelectedGameObject;
        Check(firstButton.GetComponentInChildren<Text>().text == "Принять заказ", "new interaction focuses first action, not previous close button");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.Enter)); yield return null; yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
        Check(save.Session.Order(DealOrderId.A).State == DealOrderState.Accepted, "keyboard Enter accepts focused order");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.Escape)); yield return null; yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
        Check(!trade.MenuOpen && !walk.IsPaused && player.CanInteract, "Esc returns controls after enlarged menu");
        // Reproduce the no-session state reached after a failed startup load.
        typeof(Player.CitySaveController).GetProperty("Session").SetValue(save, null);
        typeof(CityTradeView).GetMethod("Bind", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(trade, null);
        walk.SetPaused(true); yield return null; yield return null;
        Check(GameObject.Find("City pause menu") != null, "recovery pause visible without a session");
        var recoveryButtons = GameObject.Find("City pause menu").GetComponentsInChildren<Button>();
        Check(!recoveryButtons.First(b => b.name.StartsWith("Продолжить")).interactable &&
            recoveryButtons.First(b => b.name.StartsWith("Новая игра")).interactable, "recovery disables resume and retains new game");
        recoveryButtons.First(b => b.name.StartsWith("Новая игра")).onClick.Invoke(); yield return null; yield return null;
        GameObject.Find("City pause menu").GetComponentsInChildren<Button>().First(b => b.name.StartsWith("Начать новую игру")).onClick.Invoke();
        yield return null; yield return null;
        Check(save.Session != null && save.Session.Balance == 300, "recovery confirmation creates a usable session");
        File.AppendAllText(Log, "PASS E5 layouts: 1280x720, 1600x900, 1600x1000, 2100x900 at 100/150%; NPC pause/disable, keyboard, result. Captures: " + SliceOutput + "\n");
    }
}
