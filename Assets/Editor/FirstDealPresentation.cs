using System;
using Interactables;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class FirstDealPresentation
{
    [MenuItem("Tools/T04/Apply Playable Presentation")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Stop Play Mode and save the current scene first.");
        var scene = EditorSceneManager.OpenScene(FirstDealTradeBuilder.ScenePath);
        var view = UnityEngine.Object.FindAnyObjectByType<FirstDealView>();
        var workshop = UnityEngine.Object.FindAnyObjectByType<WorkshopView>();
        var canvas = GameObject.Find("T04 HUD");
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.referenceResolution = new Vector2(1600, 900);
        scaler.matchWidthOrHeight = 1;
        var status = GameObject.Find("Workshop state").GetComponent<Text>();
        Layout(status.rectTransform, new Vector2(0.025f, 0.77f), new Vector2(0.48f, 0.975f));
        status.fontSize = 22;
        Layout(GameObject.Find("Status background").GetComponent<RectTransform>(), new Vector2(0.015f, 0.75f), new Vector2(0.49f, 0.985f));
        GameObject.Find("Status background").GetComponent<Image>().color = new Color(0.025f, 0.035f, 0.045f, 0.97f);
        var prompt = GameObject.Find("Interaction").GetComponent<Text>();
        prompt.fontSize = 26;
        Layout(prompt.rectTransform, new Vector2(0.06f, 0.32f), new Vector2(0.94f, 0.42f));
        var controls = GameObject.Find("Controls").GetComponent<Text>();
        controls.fontSize = 22;
        controls.text = "WASD — ходить · Мышь — смотреть · Shift — быстрее · E — действие\nНаведи прицел на предмет и подойди ближе. Esc — пауза.";
        Layout(controls.rectTransform, new Vector2(0.03f, 0.02f), new Vector2(0.97f, 0.11f));
        var terms = FindAll("Terms").GetComponent<Text>();
        Layout(terms.rectTransform, new Vector2(0.035f, 0.30f), new Vector2(0.965f, 0.97f));
        terms.fontSize = 24;
        foreach (var text in canvas.GetComponentsInChildren<Text>(true))
        {
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
        }
        ButtonLabel(canvas, "Confirm lot - $180", "Купить 3 фонаря · $180");
        ButtonLabel(canvas, "Buy battery - $50", "Купить батарею · $50");
        ButtonLabel(canvas, "Resume / leave without buying", "Начать / продолжить");
        for (var i = 0; i < 3; i++)
        {
            var id = ((Inventory.DealOrderId)i).ToString();
            var title = i == 0 ? "A" : i == 1 ? "Б" : "повтор Б";
            ButtonLabel(canvas, "Accept " + id, "Принять " + title);
            ButtonLabel(canvas, "Decline / cancel " + id, "Отказаться: " + title);
        }

        if (view.GetComponent<FirstDealGuide>() == null)
        {
            var guide = view.gameObject.AddComponent<FirstDealGuide>();
            var panel = new GameObject("Next action panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(canvas.transform, false);
            Layout(panel.GetComponent<RectTransform>(), new Vector2(0.51f, 0.68f), new Vector2(0.985f, 0.985f));
            panel.GetComponent<Image>().color = new Color(0.025f, 0.07f, 0.1f, 0.94f);
            panel.GetComponent<Image>().raycastTarget = false;
            var help = Label(panel.transform, "Next action", new Vector2(0.035f, 0.23f), new Vector2(0.965f, 0.95f), 24);
            var direction = Label(panel.transform, "Next action direction", new Vector2(0.035f, 0.03f), new Vector2(0.965f, 0.22f), 22);
            direction.color = new Color(1, 0.82f, 0.3f);
            // Keep fullscreen terms on top of the new HUD panel.
            FindAll("Trade menu").transform.SetAsLastSibling();
            Ref(guide, "workshop", workshop); Ref(guide, "deal", view); Ref(guide, "label", help); Ref(guide, "direction", direction);
            Ref(guide, "board", GameObject.Find("OFFERS BOARD").transform);
            Ref(guide, "seller", GameObject.Find("SELLER | LOT $180 / BATTERY $50").transform);
            Ref(guide, "buyerA", GameObject.Find("A | 2 TESTED WORKING | $260 | BEFORE 09:00").transform);
            Ref(guide, "buyerB", GameObject.Find("B | 1 TESTED WORKING | $125 | BEFORE 18:00").transform);
            Ref(guide, "dock", GameObject.Find("Carry / dock crate").transform);
            Ref(guide, "bench", GameObject.Find("Work bench").transform);
            Ref(guide, "crate", GameObject.Find("Carry crate").transform);
            Fixtures();
        }
        foreach (var sign in UnityEngine.Object.FindObjectsByType<TextMesh>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            sign.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            sign.GetComponent<Renderer>().sharedMaterial = sign.font.material;
            sign.fontSize = 128;
            sign.characterSize = 0.012f;
            sign.color = Color.white;
            var parentName = sign.transform.parent.name;
            if (parentName == "T04 Trade")
            {
                if (sign.text.StartsWith("OFFERS")) sign.text = "ЗАКАЗЫ · E";
                else if (sign.text.StartsWith("SELLER")) sign.text = "ПРОДАВЕЦ · E\nПартия $180 / Батарея $50";
                else if (sign.text.StartsWith("A |")) sign.text = "ПОКУПАТЕЛЬ A\n2 исправных · $260 · до 09:00";
                else if (sign.text.StartsWith("B |")) sign.text = "ПОКУПАТЕЛЬ Б\n1 исправный · $125 · до 18:00";
                else if (sign.text.StartsWith("REPEAT")) sign.text = "ПОВТОР Б · E\nПосле первого заказа Б";
            }
            else if (sign.text == "TEST") sign.text = "ПРОВЕРИТЬ · E";
            else if (sign.text == "REPAIR" || sign.text == "ЗАМЕНИТЬ БАТАРЕЮ · E") sign.text = "ЗАМЕНИТЬ\nБАТАРЕЮ · E";
            else if (sign.text == "CRATE") sign.text = "ПОСТАВИТЬ / ВЗЯТЬ ЯЩИК · E";
            if (parentName.StartsWith("Crate selector")) sign.characterSize = 0.009f;
            else if (parentName == "T03 Workshop")
            {
                sign.characterSize = 0.008f;
                if (sign.text.StartsWith("ПОСТАВИТЬ /")) sign.text = "ПОСТАВИТЬ ЯЩИК\nE · ВЗЯТЬ ОБРАТНО";
            }
            sign.font.RequestCharactersInTexture(sign.text, sign.fontSize, FontStyle.Normal);
            var bounds = sign.GetComponent<Renderer>().bounds.size;
            var backing = sign.transform.Find("Readable.Dark.Sign backing");
            if (backing == null)
            {
                Piece(sign.transform, "Readable.Dark.Sign backing", new Vector3(0, 0, 0.04f), Vector3.one);
                backing = sign.transform.Find("Readable.Dark.Sign backing");
            }
            backing.localScale = new Vector3(bounds.x / Mathf.Abs(sign.transform.lossyScale.x) + 0.05f,
                bounds.y / Mathf.Abs(sign.transform.lossyScale.y) + 0.05f, 0.02f);
        }
        // Explicitly select the playable scenario; preserve unrelated config objects.
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(FirstDealTradeBuilder.ScenePath, true) };
        PlayerSettings.defaultScreenWidth = 1600;
        PlayerSettings.defaultScreenHeight = 900;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        FirstDealOutcomeBuilder.ApplyCurrent();
        ApplyCompactHud();
        ApplyOfferCards();
        FirstDealEnvironmentBuilder.ApplyCurrent();
        EditorSceneManager.SaveScene(scene);
        Debug.Log("T04 PRESENTATION APPLIED: Russian guidance, readable props, T04 build scene, 1600x900 resizable window.");
    }

    // Also called independently when updating an existing scene, preserving its fixtures.
    public static void ApplyCompactHud()
    {
        var workshop = UnityEngine.Object.FindAnyObjectByType<WorkshopView>();
        var status = FindAll("Workshop state").GetComponent<Text>();
        Layout(status.rectTransform, new Vector2(0.025f, 0.89f), new Vector2(0.48f, 0.975f));
        Layout(FindAll("Status background").GetComponent<RectTransform>(), new Vector2(0.015f, 0.88f), new Vector2(0.49f, 0.985f));
        Layout(FindAll("Next action panel").GetComponent<RectTransform>(), new Vector2(0.51f, 0.78f), new Vector2(0.985f, 0.985f));
        var goal = FindAll("Next action").GetComponent<Text>();
        Layout(goal.rectTransform, new Vector2(0.035f, 0.27f), new Vector2(0.965f, 0.96f));
        goal.fontSize = 22;
        var direction = FindAll("Next action direction").GetComponent<Text>();
        Layout(direction.rectTransform, new Vector2(0.035f, 0.03f), new Vector2(0.965f, 0.24f));
        direction.fontSize = 20;
        var feedback = FindAll("Controls").GetComponent<Text>();
        feedback.text = "";
        feedback.alignment = TextAnchor.LowerLeft;
        Ref(workshop, "feedbackLabel", feedback);
        EditorSceneManager.MarkSceneDirty(workshop.gameObject.scene);
    }

    public static void ApplyOfferCards()
    {
        // Empty menu space must not strand keyboard navigation after using the mouse.
        var input = UnityEngine.Object.FindAnyObjectByType<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        input.deselectOnBackgroundClick = false;
        EditorUtility.SetDirty(input);
        var deal = UnityEngine.Object.FindAnyObjectByType<FirstDealView>();
        var menu = FindAll("Trade menu").transform;
        var orders = FindAll("Order choices").GetComponent<RectTransform>();
        var seller = FindAll("Seller choices").GetComponent<RectTransform>();
        Layout(orders, new Vector2(0.035f, 0.26f), new Vector2(0.965f, 0.84f));
        Layout(seller, new Vector2(0.035f, 0.26f), new Vector2(0.965f, 0.84f));
        var so = new SerializedObject(deal);
        foreach (var field in new[] { "orderCards", "sellerCards" }) so.FindProperty(field).arraySize = 3;
        for (var i = 0; i < 3; i++)
        {
            var orderCard = OfferCard(orders, "Order card " + i, i);
            var sellerCard = OfferCard(seller, "Seller card " + i, i);
            so.FindProperty("orderCards").GetArrayElementAtIndex(i).objectReferenceValue = orderCard;
            so.FindProperty("sellerCards").GetArrayElementAtIndex(i).objectReferenceValue = sellerCard;
            MoveButton("Accept " + ((Inventory.DealOrderId)i), orderCard.transform.parent, 0.17f, 0.28f);
            MoveButton("Decline / cancel " + ((Inventory.DealOrderId)i), orderCard.transform.parent, 0.03f, 0.14f);
            MoveButton(new[] { "Confirm lot - $180", "Buy battery - $50", "Sell brought lamps" }[i], sellerCard.transform.parent, 0.03f, 0.17f);
        }
        var feedback = menu.Find("Trade feedback")?.GetComponent<Text>();
        if (feedback == null) feedback = Label(menu, "Trade feedback", new Vector2(0.035f, 0.14f), new Vector2(0.965f, 0.24f), 22);
        feedback.text = "";
        feedback.color = new Color(1, 0.85f, 0.45f);
        so.FindProperty("menuFeedback").objectReferenceValue = feedback;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(deal.gameObject.scene);
    }

    private static Text OfferCard(Transform parent, string name, int index)
    {
        var panel = parent.Find(name);
        if (panel == null)
        {
            panel = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).transform;
            panel.SetParent(parent, false);
            panel.GetComponent<Image>().color = new Color(0.09f, 0.15f, 0.2f, 1);
            panel.GetComponent<Image>().raycastTarget = false;
        }
        Layout((RectTransform)panel, new Vector2(index / 3f + 0.005f, 0), new Vector2((index + 1) / 3f - 0.005f, 1));
        var text = panel.Find("Details")?.GetComponent<Text>();
        if (text == null) text = Label(panel, "Details", new Vector2(0.05f, 0.33f), new Vector2(0.95f, 0.96f), 22);
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    private static void MoveButton(string name, Transform parent, float minY, float maxY)
    {
        var button = FindAll(name).GetComponent<Button>();
        button.transform.SetParent(parent, false);
        Layout((RectTransform)button.transform, new Vector2(0.05f, minY), new Vector2(0.95f, maxY));
        button.GetComponentInChildren<Text>(true).fontSize = 22;
    }

    private static void Fixtures()
    {
        var root = new GameObject("T04 Readable Fixtures").transform;
        foreach (var lampName in new[] { "Lamp 1", "Lamp 2", "Lamp 3" })
        {
            var lamp = GameObject.Find(lampName).transform;
            Piece(lamp, "Readable.White.Lens", new Vector3(0, 0, -0.55f), new Vector3(0.82f, 0.75f, 0.12f));
            Piece(lamp, "Readable.Dark.Base", new Vector3(0, -0.62f, 0), new Vector3(1.4f, 0.14f, 1.4f));
            Piece(lamp, "Readable.Dark.Handle top", new Vector3(0, 0.9f, 0), new Vector3(0.9f, 0.12f, 0.22f));
            foreach (var side in new[] { -1, 1 }) Piece(lamp, "Readable.Dark.Handle side", new Vector3(side * 0.4f, 0.65f, 0), new Vector3(0.12f, 0.5f, 0.22f));
        }
        var crate = GameObject.Find("Carry crate").transform;
        Piece(crate, "Readable.Orange.Crate left", new Vector3(-0.73f, 0.15f, 0), new Vector3(0.06f, 0.5f, 0.75f));
        Piece(crate, "Readable.Orange.Crate right", new Vector3(0.73f, 0.15f, 0), new Vector3(0.06f, 0.5f, 0.75f));
        Piece(crate, "Readable.Orange.Crate back", new Vector3(0, 0.15f, 0.38f), new Vector3(1.5f, 0.5f, 0.06f));
        for (var i = 0; i < 3; i++)
        {
            var selector = GameObject.Find("Crate selector " + i).transform;
            var text = new GameObject("Selector number").AddComponent<TextMesh>();
            text.transform.SetParent(selector, false); text.transform.localPosition = new Vector3(0, 0, -0.6f);
            text.transform.localScale = new Vector3(3.3f, 5, 5);
            text.text = (i + 1) + " · E"; text.anchor = TextAnchor.MiddleCenter;
        }
        var bench = GameObject.Find("Work bench").transform;
        bench.GetComponent<Renderer>().enabled = false;
        Piece(bench, "Readable.Wood.Tabletop", new Vector3(0, 0.45f, 0), new Vector3(1, 0.1f, 1));
        foreach (var x in new[] { -0.43f, 0.43f }) foreach (var z in new[] { -0.4f, 0.4f })
            Piece(bench, "Readable.Wood.Table leg", new Vector3(x, -0.05f, z), new Vector3(0.06f, 0.85f, 0.12f));
        foreach (var station in UnityEngine.Object.FindObjectsByType<FirstDealAction>(FindObjectsSortMode.None))
        {
            var prefix = station.name.StartsWith("A |") ? "Orange" : station.name.Contains("B |") ? "Green" : "Blue";
            station.GetComponent<Renderer>().enabled = false;
            Piece(station.transform, "Readable." + prefix + ".Counter", Vector3.zero, Vector3.one);
            if (station.name == "OFFERS BOARD")
                Piece(station.transform, "Readable.Dark.Board post", new Vector3(0, -1, 0), new Vector3(0.08f, 2, 0.5f));
        }
        Mannequin(root, "Blue", new Vector3(1.8f, 0.85f, -6.5f));
        Mannequin(root, "Orange", new Vector3(-8.5f, 0.85f, -5.4f));
        Mannequin(root, "Green", new Vector3(7.0f, 0.85f, -6.4f));
        Piece(GameObject.Find("TEST").transform, "Readable.Blue.Test", Vector3.zero, Vector3.one);
        Piece(GameObject.Find("REPLACE BATTERY").transform, "Readable.Orange.Repair", Vector3.zero, Vector3.one);
        Piece(GameObject.Find("Carry / dock crate").transform, "Readable.Orange.Dock", Vector3.zero, Vector3.one);
    }

    private static void Mannequin(Transform parent, string color, Vector3 position)
    {
        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Readable." + color + ".Trader";
        body.transform.SetParent(parent, false); body.transform.position = position; body.transform.localScale = new Vector3(0.4f, 0.85f, 0.4f);
        UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());
    }

    private static void Piece(Transform parent, string name, Vector3 position, Vector3 scale)
    {
        var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name; part.transform.SetParent(parent, false); part.transform.localPosition = position; part.transform.localScale = scale;
        UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
    }

    private static GameObject FindAll(string name)
    {
        foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name == name) return t.gameObject;
        throw new InvalidOperationException("Missing T04 object: " + name);
    }
    private static void ButtonLabel(GameObject canvas, string name, string value)
    {
        foreach (var button in canvas.GetComponentsInChildren<Button>(true))
            if (button.name == name) { var text = button.GetComponentInChildren<Text>(true); text.text = value; text.fontSize = 22; }
    }
    private static Text Label(Transform parent, string name, Vector2 min, Vector2 max, int size)
    {
        var text = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)).GetComponent<Text>();
        text.transform.SetParent(parent, false); Layout(text.rectTransform, min, max);
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = size; text.alignment = TextAnchor.UpperLeft; text.raycastTarget = false;
        return text;
    }
    private static void Layout(RectTransform rect, Vector2 min, Vector2 max)
    { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    private static void Ref(UnityEngine.Object target, string name, UnityEngine.Object value)
    {
        var serialized = new SerializedObject(target); serialized.FindProperty(name).objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
