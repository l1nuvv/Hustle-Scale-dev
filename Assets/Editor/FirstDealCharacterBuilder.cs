using System;
using System.Linq;
using Interactables;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FirstDealCharacterBuilder
{
    public const string Folder = "Assets/Prefabs/FirstDealCharacters";
    public const string RootName = "V4 Traders";
    private static Material skin, dark, shirt, paper;

    [MenuItem("Tools/V4/Apply Characters")]
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
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Prefabs", "FirstDealCharacters");
        skin = Palette("Skin", new Color(0.61f, 0.42f, 0.29f));
        dark = Palette("BootsAndHair", new Color(0.09f, 0.11f, 0.12f));
        shirt = Palette("WorkShirt", new Color(0.65f, 0.62f, 0.52f));
        paper = Palette("Paper", new Color(0.83f, 0.80f, 0.66f));
        var previous = GameObject.Find(RootName);
        if (previous != null) UnityEngine.Object.DestroyImmediate(previous);
        var root = new GameObject(RootName).transform;
        // Remove only the obsolete collider-free capsules, preserving all action surfaces.
        foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            if (t.name.StartsWith("Readable.") && t.name.EndsWith(".Trader")) UnityEngine.Object.DestroyImmediate(t.gameObject);
        Make(root, FirstDealCharacter.Role.Seller, new Vector3(0, 0.02f, -6.65f), "SellerBlue");
        Make(root, FirstDealCharacter.Role.BuyerA, new Vector3(-7, 0.02f, -5.65f), "BuyerOrange");
        // Offset from B's central circular sign and repeat-order interaction surface.
        Make(root, FirstDealCharacter.Role.BuyerB, new Vector3(7.65f, 0.02f, -5.65f), "BuyerGreen");
        // The former floating labels occupied face height. Mount them on the counter fronts.
        foreach (var sign in UnityEngine.Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None))
        {
            var seller = sign.text.StartsWith("ПРОДАВЕЦ · E");
            if (!seller && !sign.text.StartsWith("ПОКУПАТЕЛЬ") && !sign.text.StartsWith("ПОВТОР Б · E")) continue;
            sign.transform.position = new Vector3(sign.transform.position.x, 0.5f, seller ? -5.60f : -4.60f);
            sign.transform.localScale = Vector3.one * 0.85f;
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    private static void Make(Transform parent, FirstDealCharacter.Role role, Vector3 position, string color)
    {
        var accent = AssetDatabase.LoadAssetAtPath<Material>("Assets/Prefabs/FirstDealEnvironment/" + color + ".mat");
        if (accent == null) throw new InvalidOperationException("Apply V1 materials first.");
        var model = new GameObject(role.ToString());
        try
        {
            var body = new GameObject("Body").transform;
            body.SetParent(model.transform, false);
            foreach (var x in new[] { -0.12f, 0.12f })
            {
                Box(body, "Boot", new Vector3(x, 0.07f, 0.045f), new Vector3(0.19f, 0.14f, 0.32f), dark);
                Box(body, "Trouser leg", new Vector3(x, 0.44f, 0), new Vector3(0.18f, 0.63f, 0.21f), dark);
            }
            Box(body, "Hips", new Vector3(0, 0.78f, 0), new Vector3(0.43f, 0.18f, 0.25f), dark);
            Box(body, "Jacket", new Vector3(0, 1.08f, 0), new Vector3(0.49f, 0.47f, 0.28f), accent);
            Box(body, "Shirt front", new Vector3(0, 1.15f, 0.147f), new Vector3(0.13f, 0.34f, 0.018f), shirt);
            Box(body, "Pocket", new Vector3(-0.15f, 1.15f, 0.152f), new Vector3(0.10f, 0.12f, 0.022f), shirt);
            Box(body, "Neck", new Vector3(0, 1.37f, 0), new Vector3(0.13f, 0.13f, 0.13f), skin);
            var head = new GameObject("Head pivot").transform;
            head.SetParent(model.transform, false);
            head.localPosition = new Vector3(0, 1.48f, 0);
            Box(head, "Face", new Vector3(0, 0.045f, 0), new Vector3(0.26f, 0.29f, 0.25f), skin);
            Box(head, "Hair", new Vector3(0, 0.19f, -0.01f), new Vector3(0.27f, 0.085f, 0.26f), dark);
            Box(head, "Nose", new Vector3(0, 0.035f, 0.142f), new Vector3(0.055f, 0.065f, 0.06f), skin);
            foreach (var x in new[] { -0.065f, 0.065f })
                Box(head, "Eye", new Vector3(x, 0.08f, 0.128f), new Vector3(0.032f, 0.025f, 0.012f), dark);
            if (role == FirstDealCharacter.Role.Seller)
            {
                Box(head, "Work cap", new Vector3(0, 0.235f, 0), new Vector3(0.29f, 0.075f, 0.28f), accent);
                Box(head, "Cap peak", new Vector3(0, 0.21f, 0.15f), new Vector3(0.30f, 0.025f, 0.17f), accent);
                Box(body, "Apron", new Vector3(0, 0.86f, 0.16f), new Vector3(0.38f, 0.50f, 0.028f), shirt);
            }
            if (role == FirstDealCharacter.Role.BuyerA)
            {
                Box(body, "Order clipboard", new Vector3(-0.19f, 1.04f, 0.30f), new Vector3(0.24f, 0.30f, 0.035f), dark);
                Box(body, "Order paper", new Vector3(-0.19f, 1.05f, 0.325f), new Vector3(0.20f, 0.24f, 0.008f), paper);
            }
            // Left arm is part of the fixed body; only head and right arm need movable pivots.
            Limb(body, -1, role == FirstDealCharacter.Role.BuyerA, accent);
            var arm = new GameObject("Arm pivot").transform;
            arm.SetParent(model.transform, false);
            arm.localPosition = new Vector3(0.31f, 1.27f, 0);
            Limb(arm, 1, true, accent, true);
            if (role == FirstDealCharacter.Role.BuyerB) arm.localRotation = Quaternion.Euler(0, -20, -12);
            foreach (var group in new[] { body, head, arm }) Combine(group, role.ToString());
            var character = model.AddComponent<FirstDealCharacter>();
            var so = new SerializedObject(character);
            so.FindProperty("role").enumValueIndex = (int)role;
            so.FindProperty("head").objectReferenceValue = head;
            so.FindProperty("arm").objectReferenceValue = arm;
            so.ApplyModifiedPropertiesWithoutUndo();
            var prefab = PrefabUtility.SaveAsPrefabAsset(model, Folder + "/" + role + ".prefab");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.localPosition = position;
            var binding = new SerializedObject(instance.GetComponent<FirstDealCharacter>());
            binding.FindProperty("workshop").objectReferenceValue = UnityEngine.Object.FindAnyObjectByType<WorkshopView>();
            binding.ApplyModifiedPropertiesWithoutUndo();
        }
        finally { UnityEngine.Object.DestroyImmediate(model); }
    }

    private static void Limb(Transform parent, int side, bool bent, Material accent, bool atPivot = false)
    {
        var origin = atPivot ? Vector3.zero : new Vector3(side * 0.31f, 1.27f, 0);
        Box(parent, "Sleeve", origin + new Vector3(0, -0.13f, 0), new Vector3(0.15f, 0.30f, 0.18f), accent);
        Box(parent, "Forearm", origin + new Vector3(0, bent ? -0.28f : -0.36f, bent ? 0.12f : 0),
            bent ? new Vector3(0.13f, 0.13f, 0.30f) : new Vector3(0.13f, 0.24f, 0.15f), shirt);
        Box(parent, "Hand", origin + new Vector3(0, bent ? -0.28f : -0.51f, bent ? 0.30f : 0), new Vector3(0.13f, 0.12f, 0.12f), skin);
    }

    private static void Combine(Transform pivot, string role)
    {
        var parts = pivot.GetComponentsInChildren<MeshFilter>();
        foreach (var group in parts.GroupBy(p => p.GetComponent<Renderer>().sharedMaterial))
        {
            var mesh = new Mesh { name = role + "_" + pivot.name.Replace(" ", "") + "_" + group.Key.name };
            mesh.CombineMeshes(group.Select(p => new CombineInstance
            {
                mesh = p.sharedMesh,
                transform = pivot.worldToLocalMatrix * p.transform.localToWorldMatrix
            }).ToArray(), true, true);
            var path = Folder + "/" + mesh.name + ".asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved == null) { AssetDatabase.CreateAsset(mesh, path); saved = mesh; }
            else { EditorUtility.CopySerialized(mesh, saved); UnityEngine.Object.DestroyImmediate(mesh); }
            var batch = new GameObject(group.Key.name, typeof(MeshFilter), typeof(MeshRenderer));
            batch.transform.SetParent(pivot, false);
            batch.GetComponent<MeshFilter>().sharedMesh = saved;
            batch.GetComponent<Renderer>().sharedMaterial = group.Key;
        }
        foreach (var part in parts) UnityEngine.Object.DestroyImmediate(part.gameObject);
    }

    private static void Box(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
    {
        var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        part.GetComponent<Renderer>().sharedMaterial = material;
        UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
    }

    private static Material Palette(string name, Color color)
    {
        var path = Folder + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Scenes/T02Materials/Fixtures.mat")) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        material.color = color;
        material.SetFloat("_Smoothness", 0.15f);
        EditorUtility.SetDirty(material);
        return material;
    }
}
