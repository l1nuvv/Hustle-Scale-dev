using System;
using Interactables;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Metres, built for the existing 1.8m capsule, 0.6m diameter and 2.5m reach.
// Authoring only: no runtime generation or changes to the trading rules.
public static class FirstDealEnvironmentBuilder
{
    public const string RootName = "V1 Workshop and Yard";
    private const string Folder = "Assets/Prefabs/FirstDealEnvironment";
    private static Material steel, wood, cream, blue, orange, green, concrete, lettering;

    [MenuItem("Tools/V1/Apply Workshop and Yard")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Stop Play Mode and save the scene first.");
        EditorSceneManager.OpenScene(FirstDealTradeBuilder.ScenePath);
        ApplyCurrent();
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
    }

    public static void ApplyCurrent()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().path != FirstDealTradeBuilder.ScenePath)
            throw new InvalidOperationException("Open FirstDealTrade in Edit Mode first.");
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Prefabs", "FirstDealEnvironment");
        steel = Palette("Steel", new Color(0.13f, 0.17f, 0.18f));
        wood = Palette("Timber", new Color(0.36f, 0.24f, 0.15f));
        cream = Palette("Paint", new Color(0.77f, 0.73f, 0.62f));
        blue = Palette("SellerBlue", new Color(0.18f, 0.34f, 0.43f));
        orange = Palette("BuyerOrange", new Color(0.65f, 0.33f, 0.14f));
        green = Palette("BuyerGreen", new Color(0.28f, 0.43f, 0.30f));
        concrete = Palette("Concrete", new Color(0.39f, 0.38f, 0.35f));
        lettering = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/WorldSign.mat");
        if (lettering == null)
        {
            lettering = new Material(AssetDatabase.LoadAssetAtPath<Shader>(Folder + "/WorldSign.shader"));
            AssetDatabase.CreateAsset(lettering, Folder + "/WorldSign.mat");
        }
        var previous = GameObject.Find(RootName);
        if (previous != null) UnityEngine.Object.DestroyImmediate(previous);
        var root = new GameObject(RootName).transform;

        // Remove obsolete movement-test obstacles, not trading fixtures.
        foreach (var name in new[] { "Step 0.2m", "Step 0.4m", "Collision crate" })
        {
            var obstacle = GameObject.Find(name);
            if (obstacle != null) UnityEngine.Object.DestroyImmediate(obstacle);
        }
        // 2m opening; jambs are outside that width. No step or raised threshold.
        Resize("Partition left", new Vector3(-5.5f, 1.5f, 1), new Vector3(9, 3, 0.25f));
        Resize("Partition right", new Vector3(5.5f, 1.5f, 1), new Vector3(9, 3, 0.25f));
        Piece(root, "Workshop floor", new Vector3(0, 0.008f, 4.5f), new Vector3(19.6f, 0.012f, 6.7f), concrete);
        // Leading lines remain flush, non-colliding and outside the central walking strip.
        foreach (var x in new[] { -0.78f, 0.78f })
            Piece(root, "Passage guide", new Vector3(x, 0.018f, 0.1f), new Vector3(0.07f, 0.012f, 5), cream);
        Piece(root, "Yard route", new Vector3(0, 0.018f, -3.1f), new Vector3(15, 0.012f, 0.08f), cream);
        foreach (var x in new[] { -7f, 0f, 7f })
            Piece(root, "Trading approach", new Vector3(x, 0.018f, -3.8f), new Vector3(0.08f, 0.012f, 1.4f), cream);

        var portal = new GameObject("Workshop entrance model").transform;
        foreach (var x in new[] { -1.1f, 1.1f })
            Piece(portal, "Steel jamb", new Vector3(x, 1.4f, 0), new Vector3(0.18f, 2.8f, 0.32f), steel);
        Piece(portal, "Lintel", new Vector3(0, 2.9f, 0), new Vector3(2.4f, 0.2f, 0.4f), steel);
        Piece(portal, "Workshop fascia", new Vector3(0, 2.57f, -0.2f), new Vector3(2.1f, 0.42f, 0.10f), wood);
        Piece(portal, "Exit fascia", new Vector3(0, 2.57f, 0.2f), new Vector3(2.1f, 0.42f, 0.10f), steel);
        Sign(portal, "МАСТЕРСКАЯ", new Vector3(0, 2.57f, -0.26f), 0, 0.017f);
        Sign(portal, "ВЫХОД · ТОРГОВЫЙ ДВОР", new Vector3(0, 2.57f, 0.26f), 180, 0.011f);
        SaveModel(portal.gameObject, "WorkshopEntrance", root, new Vector3(0, 0, 1));

        // A roofed supply stall versus open buyer counters: silhouette reinforces colour.
        var seller = Counter("Seller stall model", 3.4f, blue);
        foreach (var x in new[] { -1.55f, 1.55f })
            Piece(seller, "Canopy upright", new Vector3(x, 1.4f, -0.3f), new Vector3(0.09f, 2.8f, 0.09f), steel);
        var canopy = Piece(seller, "Sloped sheet canopy", new Vector3(0, 2.8f, 0), new Vector3(3.65f, 0.09f, 1.8f), blue);
        canopy.localRotation = Quaternion.Euler(-7, 0, 0);
        Piece(seller, "Supply fascia", new Vector3(0, 2.55f, 0.83f), new Vector3(3.6f, 0.32f, 0.07f), steel);
        Sign(seller, "ПРОДАВЕЦ · ПАРТИЯ / ЗАПЧАСТИ", new Vector3(0, 2.55f, 0.88f), 180, 0.014f);
        SaveModel(seller.gameObject, "SellerStall", root, new Vector3(0, 0, -6));

        var buyerA = Counter("Buyer A counter model", 3, orange);
        Marker(buyerA, "А", -1.25f, orange, false);
        SaveModel(buyerA.gameObject, "BuyerACounter", root, new Vector3(-7, 0, -5));
        var buyerB = Counter("Buyer B counter model", 4.1f, green);
        Marker(buyerB, "Б", 0, green, true);
        SaveModel(buyerB.gameObject, "BuyerBCounter", root, new Vector3(7, 0, -5));

        // Keep the existing colliders/action references as the interaction surfaces.
        foreach (var action in UnityEngine.Object.FindObjectsByType<FirstDealAction>(FindObjectsSortMode.None))
            if (action.name != "OFFERS BOARD")
                foreach (var renderer in action.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
        foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            if (t.name.StartsWith("Readable.") && t.name.EndsWith(".Trader"))
                t.GetComponent<Renderer>().sharedMaterial = t.name.Contains("Orange") ? orange : t.name.Contains("Green") ? green : blue;

        // Give the work area its own large landmark, away from the interactive labels.
        Piece(root, "Workbench wall panel", new Vector3(0, 2.1f, 7.78f), new Vector3(4.6f, 1.3f, 0.08f), steel);
        Sign(root, "ПРОВЕРКА И РЕМОНТ", new Vector3(0, 2.4f, 7.72f), 0, 0.021f);
        Sign(root, "01  ПРОВЕРЬ     02  ЗАМЕНИ БАТАРЕЮ     03  ПРОВЕРЬ СНОВА", new Vector3(0, 1.92f, 7.72f), 0, 0.011f);
        // Existing labels also need depth, otherwise they float through the partition.
        foreach (var text in UnityEngine.Object.FindObjectsByType<TextMesh>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            text.GetComponent<Renderer>().sharedMaterial = lettering;
        root.gameObject.AddComponent<WorldSignFont>().Bind(Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"), lettering);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    private static Transform Counter(string name, float width, Material accent)
    {
        var root = new GameObject(name).transform;
        // Table top at 0.9m, 0.8m deep; dark steel frame and individual timber boards.
        foreach (var x in new[] { -width / 2 + 0.12f, width / 2 - 0.12f })
            foreach (var z in new[] { -0.30f, 0.30f })
                Piece(root, "Square tube leg", new Vector3(x, 0.43f, z), new Vector3(0.07f, 0.86f, 0.07f), steel);
        for (var i = 0; i < 4; i++)
            Piece(root, "Timber plank", new Vector3(0, 0.87f, -0.3f + i * 0.2f), new Vector3(width, 0.07f, 0.19f), wood);
        Piece(root, "Front rail", new Vector3(0, 0.7f, 0.31f), new Vector3(width, 0.08f, 0.07f), steel);
        Piece(root, "Foot rail", new Vector3(0, 0.18f, 0.31f), new Vector3(width, 0.06f, 0.07f), steel);
        Piece(root, "Painted front panel", new Vector3(0, 0.48f, 0.32f), new Vector3(width - 0.26f, 0.36f, 0.04f), accent);
        return root;
    }

    private static void Marker(Transform parent, string letter, float x, Material accent, bool round)
    {
        Piece(parent, "Marker post", new Vector3(x, 1.6f, -0.26f), new Vector3(0.06f, 2.6f, 0.06f), steel);
        var board = Piece(parent, "Buyer marker", new Vector3(x, 2.5f, -0.26f), new Vector3(0.75f, 0.75f, 0.08f), accent,
            round ? PrimitiveType.Cylinder : PrimitiveType.Cube);
        if (round)
        {
            board.localRotation = Quaternion.Euler(90, 0, 0);
            board.localScale = new Vector3(0.85f, 0.04f, 0.85f);
        }
        Sign(parent, letter, new Vector3(x, 2.5f, -0.20f), 180, 0.065f);
    }

    private static void SaveModel(GameObject model, string name, Transform parent, Vector3 position)
    {
        model.AddComponent<WorldSignFont>().Bind(Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"), lettering);
        var prefab = PrefabUtility.SaveAsPrefabAsset(model, Folder + "/" + name + ".prefab");
        UnityEngine.Object.DestroyImmediate(model);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        instance.transform.localPosition = position;
    }

    private static Material Palette(string name, Color color)
    {
        var path = Folder + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            var template = AssetDatabase.LoadAssetAtPath<Material>("Assets/Scenes/T02Materials/Fixtures.mat");
            material = new Material(template) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        material.color = color;
        material.SetFloat("_Smoothness", 0.18f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Transform Piece(Transform parent, string name, Vector3 position, Vector3 scale, Material material,
        PrimitiveType type = PrimitiveType.Cube)
    {
        var part = GameObject.CreatePrimitive(type);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        part.GetComponent<Renderer>().sharedMaterial = material;
        // Physical boundaries and interaction surfaces are the original validated colliders.
        UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
        return part.transform;
    }

    private static void Sign(Transform parent, string text, Vector3 position, float yaw, float size)
    {
        var sign = new GameObject("Sign " + text).AddComponent<TextMesh>();
        sign.transform.SetParent(parent, false);
        sign.transform.localPosition = position;
        sign.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        sign.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        sign.fontSize = 128;
        sign.characterSize = size;
        sign.anchor = TextAnchor.MiddleCenter;
        sign.alignment = TextAlignment.Center;
        sign.color = new Color(0.94f, 0.91f, 0.82f);
        sign.text = text;
        sign.GetComponent<Renderer>().sharedMaterial = lettering;
    }

    private static void Resize(string name, Vector3 position, Vector3 scale)
    {
        var target = GameObject.Find(name).transform;
        target.position = position;
        target.localScale = scale;
    }
}
