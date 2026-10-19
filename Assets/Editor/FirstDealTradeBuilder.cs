using System;
using Interactables;
using Player.Movement;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public static class FirstDealTradeBuilder
{
    public const string ScenePath = "Assets/Scenes/FirstDealTrade.unity";

    [MenuItem("Tools/T04/Create Trade Scene")]
    public static void Create()
    {
        if (EditorApplication.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Stop Play Mode and save the current scene first.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            throw new InvalidOperationException("Trade scene already exists; open it from Assets/Scenes.");
        var scene = EditorSceneManager.OpenScene(WorkshopSceneBuilder.ScenePath);
        EditorSceneManager.SaveScene(scene, ScenePath);
        var workshop = UnityEngine.Object.FindAnyObjectByType<WorkshopView>();
        var player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
        // The eye is inside the FPS capsule; downward rays must not hit its shell.
        player.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
        Set(player.GetComponent<Player.PlayerRaycast>(), "layerMask", Physics.DefaultRaycastLayers);
        player.transform.position = new Vector3(-4, 0.05f, 4.5f);
        player.transform.rotation = Quaternion.identity;
        var root = new GameObject("T04 Trade");
        var deal = root.AddComponent<FirstDealView>();
        Set(workshop, "firstDeal", true);
        Ref(deal, "workshop", workshop);
        Ref(deal, "player", player);
        var sellerDock = new GameObject("Seller crate dock").transform;
        sellerDock.SetParent(root.transform);
        sellerDock.position = new Vector3(-1.8f, 0.7f, -5.5f);
        Ref(workshop, "sellerDock", sellerDock);
        var crate = GameObject.Find("Carry crate").transform;
        crate.position = sellerDock.position;
        // A carried crate cannot occlude E. Its colliders are disabled by WorkshopView while carried.
        var visual = crate.Find("Crate visual").gameObject;
        visual.AddComponent<BoxCollider>();
        WorkshopControl(visual, workshop, WorkshopActionKind.Crate);
        for (var i = 0; i < 3; i++)
        {
            UnityEngine.Object.DestroyImmediate(GameObject.Find("Lamp " + (i + 1) + " take / return"));
            var selector = Block(crate, "Crate selector " + i, Vector3.zero, new Vector3(0.3f, 0.2f, 0.2f));
            selector.transform.localPosition = new Vector3((i - 1) * 0.45f, 0.45f, -0.4f);
            WorkshopControl(selector, workshop, WorkshopActionKind.Unit, i);
        }
        UnityEngine.Object.DestroyImmediate(GameObject.Find("BUY BATTERY $50"));
        foreach (var label in workshop.GetComponentsInChildren<TextMesh>())
            if (label.text == "BATTERY $50" || label.text.StartsWith("LAMP ")) UnityEngine.Object.DestroyImmediate(label.gameObject);
        UnityEngine.Object.DestroyImmediate(GameObject.Find("Test light - E"));
        UnityEngine.Object.DestroyImmediate(GameObject.Find("Counter A placeholder"));
        UnityEngine.Object.DestroyImmediate(GameObject.Find("Counter B placeholder"));
        Station(root.transform, deal, "OFFERS BOARD", new Vector3(-4, 1.5f, 6), FirstDealActionKind.Offers, false);
        Station(root.transform, deal, "SELLER | LOT $180 / BATTERY $50", new Vector3(0, 1, -6), FirstDealActionKind.Seller, true);
        Station(root.transform, deal, "A | 2 TESTED WORKING | $260 | BEFORE 09:00", new Vector3(-7, 1, -5), FirstDealActionKind.DeliverA, true);
        Station(root.transform, deal, "B | 1 TESTED WORKING | $125 | BEFORE 18:00", new Vector3(6, 1, -5), FirstDealActionKind.DeliverB, true);
        Station(root.transform, deal, "REPEAT B | $110 | BEFORE 18:00", new Vector3(8, 1, -5), FirstDealActionKind.DeliverRepeatB, true);

        var canvas = GameObject.Find("T03 HUD");
        canvas.name = "T04 HUD";
        var status = GameObject.Find("Workshop state").GetComponent<Text>();
        status.fontSize = 16;
        status.rectTransform.sizeDelta = new Vector2(940, 290);
        GameObject.Find("Status background").GetComponent<RectTransform>().sizeDelta = new Vector2(960, 310);
        GameObject.Find("Controls").GetComponent<Text>().text =
            "WASD / Mouse / Shift | E - interact | Esc - pause / resume\nRead board, buy seller's lot, carry crate, dock in workshop, test lamps, accept orders, deliver outside.";
        canvas.AddComponent<GraphicRaycaster>();
        var events = new GameObject("T04 EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        events.GetComponent<InputSystemUIInputModule>().deselectOnBackgroundClick = false;
        var menu = new GameObject("Trade menu", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        menu.transform.SetParent(canvas.transform, false);
        Stretch(menu.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
        menu.GetComponent<Image>().color = new Color(0.06f, 0.08f, 0.1f, 1);
        var text = Text(menu.transform, "Terms", "", new Vector2(0.05f, 0.35f), new Vector2(0.95f, 0.95f));
        text.alignment = TextAnchor.UpperLeft;
        text.fontSize = 22;
        var sellerButtons = Container(menu.transform, "Seller choices", new Vector2(0.05f, 0.15f), new Vector2(0.95f, 0.3f));
        var lot = Button(sellerButtons, "Confirm lot - $180", 0, 2);
        UnityEventTools.AddBoolPersistentListener(lot.onClick, deal.Purchase, false);
        var battery = Button(sellerButtons, "Buy battery - $50", 1, 2);
        UnityEventTools.AddBoolPersistentListener(battery.onClick, deal.Purchase, true);
        var orderButtons = Container(menu.transform, "Order choices", new Vector2(0.05f, 0.15f), new Vector2(0.95f, 0.3f));
        for (var i = 0; i < 3; i++)
        {
            var id = ((Inventory.DealOrderId)i).ToString();
            var accept = Button(orderButtons, "Accept " + id, i * 2, 6);
            var close = Button(orderButtons, "Decline / cancel " + id, i * 2 + 1, 6);
            // Persist concrete one-argument Unity callbacks; no runtime closure in the scene.
            UnityEventTools.AddIntPersistentListener(accept.onClick, deal.Accept, i);
            UnityEventTools.AddIntPersistentListener(close.onClick, deal.Decline, i);
        }
        var footer = Container(menu.transform, "Footer", new Vector2(0.35f, 0.04f), new Vector2(0.65f, 0.12f));
        var resume = Button(footer, "Resume / leave without buying", 0, 1);
        UnityEventTools.AddPersistentListener(resume.onClick, deal.CloseMenu);
        Ref(deal, "menu", menu);
        Ref(deal, "menuText", text);
        Ref(deal, "sellerButtons", sellerButtons.gameObject);
        Ref(deal, "orderButtons", orderButtons.gameObject);
        Ref(deal, "closeButton", resume);
        menu.SetActive(false);
        EditorSceneManager.SaveScene(scene);
        FirstDealPresentation.Apply();
        Debug.Log("T04 SCENE CREATED " + ScenePath);
    }

    private static void Station(Transform parent, FirstDealView deal, string name, Vector3 position, FirstDealActionKind kind, bool outside)
    {
        var target = Block(parent, name, position, new Vector3(1.3f, 0.6f, 0.35f));
        var action = target.AddComponent<FirstDealAction>();
        Ref(action, "deal", deal);
        Set(action, "action", (int)kind);
        var label = new GameObject("Sign").AddComponent<TextMesh>();
        label.transform.SetParent(parent, false);
        label.transform.position = position + new Vector3(0, 0.65f, outside ? 0.2f : -0.2f);
        label.transform.rotation = Quaternion.Euler(0, outside ? 180 : 0, 0);
        label.text = name;
        label.anchor = TextAnchor.MiddleCenter;
        label.characterSize = 0.04f;
        label.fontSize = 30;
    }

    private static GameObject Block(Transform parent, string name, Vector3 position, Vector3 size)
    {
        var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
        target.name = name;
        target.transform.SetParent(parent, false);
        target.transform.position = position;
        target.transform.localScale = size;
        return target;
    }

    private static void WorkshopControl(GameObject target, WorkshopView view, WorkshopActionKind kind, int index = 0)
    {
        var action = target.AddComponent<WorkshopAction>();
        Ref(action, "workshop", view); Set(action, "action", (int)kind); Set(action, "unitIndex", index);
    }

    private static Transform Container(Transform parent, string name, Vector2 min, Vector2 max)
    {
        var result = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        result.SetParent(parent, false); Stretch(result, min, max); return result;
    }

    private static Text Text(Transform parent, string name, string value, Vector2 min, Vector2 max)
    {
        var result = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)).GetComponent<Text>();
        result.transform.SetParent(parent, false); Stretch(result.rectTransform, min, max);
        result.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); result.fontSize = 18;
        result.text = value; result.alignment = TextAnchor.MiddleCenter; result.raycastTarget = false;
        return result;
    }

    private static Button Button(Transform parent, string label, int index, int count)
    {
        var target = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        target.transform.SetParent(parent, false);
        Stretch(target.GetComponent<RectTransform>(), new Vector2((float)index / count + 0.005f, 0), new Vector2((float)(index + 1) / count - 0.005f, 1));
        target.GetComponent<Image>().color = new Color(0.2f, 0.3f, 0.4f);
        Text(target.transform, "Label", label, Vector2.zero, Vector2.one);
        return target.GetComponent<Button>();
    }

    private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
    { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    private static void Ref(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        var serialized = new SerializedObject(target); serialized.FindProperty(field).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void Set(UnityEngine.Object target, string field, int value)
    {
        var serialized = new SerializedObject(target); serialized.FindProperty(field).intValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void Set(UnityEngine.Object target, string field, bool value)
    {
        var serialized = new SerializedObject(target); serialized.FindProperty(field).boolValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
