using System;
using Interactables;
using Player;
using Player.Movement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class FirstDealGrayboxBuilder
{
    public const string ScenePath = "Assets/Scenes/FirstDealGraybox.unity";

    [MenuItem("Tools/T02/Create Graybox Scene")]
    public static void Create()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            throw new InvalidOperationException("Graybox already exists. Open it from Assets/Scenes.");
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.55f, 0.58f, 0.62f);

        // Metres: capsule 1.8 tall / 0.6 wide, eye 1.65, walk 4m/s, reach 2.5.
        var floor = Material("Floor", new Color(0.28f, 0.3f, 0.32f));
        var wall = Material("Walls", new Color(0.65f, 0.67f, 0.69f));
        var fixture = Material("Fixtures", new Color(0.38f, 0.43f, 0.47f));
        var accent = Material("Interaction", new Color(0.9f, 0.62f, 0.15f));
        Block("Floor", new Vector3(0, -0.25f, 0), new Vector3(20, 0.5f, 16), floor);
        Block("North wall", new Vector3(0, 1.5f, 8), new Vector3(20, 3, 0.3f), wall);
        Block("South wall", new Vector3(0, 1.5f, -8), new Vector3(20, 3, 0.3f), wall);
        Block("West wall", new Vector3(-10, 1.5f, 0), new Vector3(0.3f, 3, 16), wall);
        Block("East wall", new Vector3(10, 1.5f, 0), new Vector3(0.3f, 3, 16), wall);
        // A 1.4m passage separates arrival from the work area; no required jumps.
        Block("Partition left", new Vector3(-5.35f, 1.5f, 1), new Vector3(9.3f, 3, 0.25f), wall);
        Block("Partition right", new Vector3(5.35f, 1.5f, 1), new Vector3(9.3f, 3, 0.25f), wall);
        Block("Work bench", new Vector3(0, 0.5f, 5.5f), new Vector3(3, 1, 1), fixture);
        Block("Counter A placeholder", new Vector3(-7, 0.5f, 5), new Vector3(2, 1, 1), fixture);
        Block("Counter B placeholder", new Vector3(7, 0.5f, 5), new Vector3(2, 1, 1), fixture);
        Block("Collision crate", new Vector3(4, 0.6f, -3), new Vector3(1.2f, 1.2f, 1.2f), fixture);
        Block("Step 0.2m", new Vector3(-4, 0.1f, -3), new Vector3(2, 0.2f, 2), fixture);
        Block("Step 0.4m", new Vector3(-4, 0.2f, -1), new Vector3(2, 0.4f, 2), fixture);
        var probe = Block("Test light - E", new Vector3(0, 1.5f, -3.5f), new Vector3(0.6f, 0.6f, 0.6f), accent);
        var lightObject = new GameObject("Test light indicator");
        lightObject.transform.SetParent(probe.transform, false);
        lightObject.transform.position = new Vector3(0, 2.3f, -3.5f);
        var indicator = lightObject.AddComponent<Light>();
        indicator.type = LightType.Point;
        indicator.range = 5;
        indicator.intensity = 3;
        indicator.color = new Color(1, 0.8f, 0.35f);
        indicator.enabled = false;
        var interactable = probe.AddComponent<GrayboxInteractable>();
        Reference(interactable, "indicator", indicator);

        var sun = new GameObject("Directional Light").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.5f;
        sun.transform.rotation = Quaternion.Euler(50, -30, 0);
        var player = new GameObject("Player");
        player.tag = "Player";
        player.transform.position = new Vector3(0, 0.05f, -6);
        var body = player.AddComponent<CharacterController>();
        body.height = 1.8f;
        body.radius = 0.3f;
        body.center = Vector3.up * 0.9f;
        body.stepOffset = 0.3f;
        body.slopeLimit = 45;
        body.skinWidth = 0.03f;
        var camera = new GameObject("Main Camera").AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.transform.SetParent(player.transform, false);
        camera.transform.localPosition = Vector3.up * 1.65f;
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 100;
        camera.fieldOfView = 75;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.12f, 0.15f, 0.18f);
        camera.gameObject.AddComponent<AudioListener>();
        player.AddComponent<Player.PlayerInput>();
        var controller = player.AddComponent<PlayerController>();
        Reference(controller, "cameraTransform", camera.transform);
        var raycast = player.AddComponent<PlayerRaycast>();
        Reference(raycast, "camera", camera);

        var canvas = new GameObject("T02 Controls", typeof(Canvas), typeof(CanvasScaler));
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 0.5f;
        Label(canvas.transform, "Controls", "T02 | WASD - move   Shift - sprint   Mouse - look   E - interact\nEsc - release cursor   Click - resume\nTest light ahead. Passage leads to the work area.", new Vector2(0.5f, 1), new Vector2(0, -65), new Vector2(1000, 90));
        Label(canvas.transform, "Crosshair", "+", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30, 30));
        var prompt = Label(canvas.transform, "Interaction", "", new Vector2(0.5f, 0.5f), new Vector2(0, -55), new Vector2(700, 45));
        var presenter = canvas.AddComponent<InteractionPrompt>();
        Reference(presenter, "interaction", raycast);
        Reference(presenter, "label", prompt);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("T02 SCENE CREATED " + ScenePath);
    }

    private static Material Material(string name, Color color)
    {
        const string folder = "Assets/Scenes/T02Materials";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Scenes", "T02Materials");
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, color = color };
        AssetDatabase.CreateAsset(material, folder + "/" + name + ".mat");
        return material;
    }
    private static GameObject Block(string name, Vector3 position, Vector3 size, Material material)
    {
        var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = name;
        block.transform.SetPositionAndRotation(position, Quaternion.identity);
        block.transform.localScale = size;
        block.GetComponent<Renderer>().sharedMaterial = material;
        return block;
    }
    private static Text Label(Transform parent, string name, string text, Vector2 anchor, Vector2 offset, Vector2 size)
    {
        var label = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)).GetComponent<Text>();
        label.transform.SetParent(parent, false);
        label.rectTransform.anchorMin = label.rectTransform.anchorMax = anchor;
        label.rectTransform.anchoredPosition = offset;
        label.rectTransform.sizeDelta = size;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 22;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
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
}
