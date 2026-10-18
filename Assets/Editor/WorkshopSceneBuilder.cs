using System;
using Interactables;
using Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class WorkshopSceneBuilder
{
    public const string ScenePath = "Assets/Scenes/FirstDealWorkshop.unity";

    [MenuItem("Tools/T03/Create Workshop Scene")]
    public static void Create()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            throw new InvalidOperationException("Workshop already exists; open it from Assets/Scenes.");
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Save your current scene before creating the workshop.");
        var scene = EditorSceneManager.OpenScene(FirstDealGrayboxBuilder.ScenePath);
        // Save as a new scene before changing objects; preserve the T02 baseline and its GUID.
        EditorSceneManager.SaveScene(scene, ScenePath);
        UnityEngine.Object.DestroyImmediate(GameObject.Find("T02 Controls"));
        var root = new GameObject("T03 Workshop");
        var view = root.AddComponent<WorkshopView>();
        var camera = Camera.main;
        var dock = Anchor(root.transform, "Crate dock", new Vector3(-2.5f, 0.7f, 3.5f));
        var bench = Anchor(root.transform, "Bench lamp", new Vector3(0, 1.22f, 5.5f));
        var crate = Anchor(root.transform, "Carry crate", dock.position);
        Block(crate, "Crate visual", dock.position, new Vector3(1.4f, 0.3f, 0.7f), false);
        var slots = new Transform[3];
        var lamps = new Transform[3];
        var lights = new Light[3];
        var benchObject = GameObject.Find("Work bench");
        Action(benchObject, view, WorkshopActionKind.Bench);
        Button(root.transform, "Carry / dock crate", new Vector3(-2.5f, 0.8f, 2.8f), view, WorkshopActionKind.Crate);
        for (var i = 0; i < 3; i++)
        {
            slots[i] = Anchor(crate, "Stored lamp " + (i + 1), Vector3.zero);
            slots[i].localPosition = new Vector3((i - 1) * 0.45f, 0.32f, 0);
            lamps[i] = Block(root.transform, "Lamp " + (i + 1), slots[i].position,
                new Vector3(0.24f, 0.32f, 0.18f), false);
            var light = new GameObject("Test indicator").AddComponent<Light>();
            light.transform.SetParent(lamps[i], false);
            light.transform.localPosition = new Vector3(0, 0, -0.6f);
            light.type = LightType.Point;
            light.color = new Color(1, 0.85f, 0.5f);
            light.range = 3;
            light.intensity = 4;
            light.enabled = false;
            lights[i] = light;
            Button(root.transform, "Lamp " + (i + 1) + " take / return",
                new Vector3(-3.2f + i * 0.7f, 1.1f, 3), view, WorkshopActionKind.Unit, i);
        }
        Button(root.transform, "TEST", new Vector3(-0.9f, 1.15f, 4.75f), view, WorkshopActionKind.Test);
        Button(root.transform, "REPLACE BATTERY", new Vector3(0, 1.15f, 4.75f), view, WorkshopActionKind.Repair);
        Button(root.transform, "BUY BATTERY $50", new Vector3(0.9f, 1.15f, 4.75f), view, WorkshopActionKind.BuyBattery);

        var canvas = new GameObject("T03 HUD", typeof(Canvas), typeof(CanvasScaler));
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 0.5f;
        var backdrop = new GameObject("Status background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        backdrop.transform.SetParent(canvas.transform, false);
        backdrop.color = new Color(0, 0, 0, 0.7f);
        backdrop.raycastTarget = false;
        var backdropRect = backdrop.rectTransform;
        backdropRect.anchorMin = backdropRect.anchorMax = backdropRect.pivot = new Vector2(0, 1);
        backdropRect.anchoredPosition = new Vector2(10, -10);
        backdropRect.sizeDelta = new Vector2(920, 210);
        var status = Label(canvas.transform, "Workshop state", "", new Vector2(0, 1), new Vector2(20, -20), new Vector2(900, 210));
        status.alignment = TextAnchor.UpperLeft;
        status.rectTransform.pivot = new Vector2(0, 1);
        Label(canvas.transform, "Crosshair", "+", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30, 30));
        var prompt = Label(canvas.transform, "Interaction", "", new Vector2(0.5f, 0.5f), new Vector2(0, -55), new Vector2(1100, 60));
        Label(canvas.transform, "Controls", "WASD / Mouse / Shift | E - interact | Esc - release cursor | Click - resume\nWorkshop through the passage. Carry crate, choose lamp, place on bench, TEST.",
            new Vector2(0.5f, 0), new Vector2(0, 55), new Vector2(1100, 80));
        var presenter = canvas.AddComponent<InteractionPrompt>();
        Reference(presenter, "interaction", camera.GetComponentInParent<PlayerRaycast>());
        Reference(presenter, "label", prompt);
        Reference(view, "hand", camera.transform);
        Reference(view, "crate", crate);
        Reference(view, "dock", dock);
        Reference(view, "bench", bench);
        Reference(view, "status", status);
        References(view, "lamps", lamps);
        References(view, "storageSlots", slots);
        References(view, "indicators", lights);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }

    private static Transform Anchor(Transform parent, string name, Vector3 position)
    {
        var result = new GameObject(name).transform;
        result.SetParent(parent, false);
        result.position = position;
        return result;
    }

    private static Transform Block(Transform parent, string name, Vector3 position, Vector3 size, bool collider)
    {
        var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = name;
        block.transform.SetParent(parent, false);
        block.transform.position = position;
        block.transform.localScale = size;
        if (!collider) UnityEngine.Object.DestroyImmediate(block.GetComponent<Collider>());
        return block.transform;
    }

    private static void Button(Transform parent, string name, Vector3 position, WorkshopView view, WorkshopActionKind kind, int index = 0)
    {
        var target = Block(parent, name, position, new Vector3(0.55f, 0.2f, 0.2f), true);
        Action(target.gameObject, view, kind, index);
        var label = new GameObject("Label").AddComponent<TextMesh>();
        label.transform.SetParent(parent, false);
        label.transform.position = position + new Vector3(0, 0.25f, -0.15f);
        label.text = kind == WorkshopActionKind.Unit ? "LAMP " + (index + 1) :
            kind == WorkshopActionKind.Crate ? "CRATE" :
            kind == WorkshopActionKind.Repair ? "REPAIR" :
            kind == WorkshopActionKind.BuyBattery ? "BATTERY $50" : name;
        label.anchor = TextAnchor.MiddleCenter;
        label.characterSize = 0.012f;
        label.fontSize = 36;
        label.color = Color.black;
    }

    private static void Action(GameObject target, WorkshopView view, WorkshopActionKind kind, int index = 0)
    {
        var action = target.AddComponent<WorkshopAction>();
        Reference(action, "workshop", view);
        var serialized = new SerializedObject(action);
        serialized.FindProperty("action").enumValueIndex = (int)kind;
        serialized.FindProperty("unitIndex").intValue = index;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Text Label(Transform parent, string name, string text, Vector2 anchor, Vector2 offset, Vector2 size)
    {
        var label = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)).GetComponent<Text>();
        label.transform.SetParent(parent, false);
        label.rectTransform.anchorMin = label.rectTransform.anchorMax = anchor;
        label.rectTransform.anchoredPosition = offset;
        label.rectTransform.sizeDelta = size;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 20;
        label.alignment = TextAnchor.MiddleCenter;
        label.text = text;
        label.raycastTarget = false;
        return label;
    }

    private static void Reference(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(field).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void References(UnityEngine.Object target, string field, UnityEngine.Object[] values)
    {
        var serialized = new SerializedObject(target);
        var property = serialized.FindProperty(field);
        property.arraySize = values.Length;
        for (var i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
