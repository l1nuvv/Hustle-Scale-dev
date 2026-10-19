using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Authoring only. Keep the existing item identities, light feedback and interaction colliders.
public static class FirstDealPropBuilder
{
    public const string Folder = "Assets/Prefabs/FirstDealProps";
    public const string VisualName = "V2 Model";
    private static Material steel, timber, paint, rubber, lens, copper;

    [MenuItem("Tools/V2/Apply Models and Materials")]
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
        // Validate all bindings before changing the scene or generating assets.
        var lamps = new[] { Find("Lamp 1"), Find("Lamp 2"), Find("Lamp 3") };
        var crate = Find("Carry crate");
        var crateSurface = Find("Crate visual");
        var bench = Find("Work bench");
        var test = Find("TEST");
        var repair = Find("REPLACE BATTERY");
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Prefabs", "FirstDealProps");
        steel = Palette("Steel", new Color(0.13f, 0.17f, 0.18f), 0.65f, 0.28f);
        timber = Palette("Timber", new Color(0.36f, 0.24f, 0.15f), 0, 0.12f);
        paint = Palette("BluePaint", new Color(0.18f, 0.34f, 0.43f), 0.15f, 0.25f);
        rubber = Palette("Rubber", new Color(0.045f, 0.055f, 0.05f), 0, 0.08f);
        lens = Palette("Ivory", new Color(0.82f, 0.81f, 0.68f), 0, 0.32f);
        copper = Palette("Contacts", new Color(0.62f, 0.38f, 0.17f), 0.7f, 0.3f);

        var lampModel = SaveModel("Flashlight", Flashlight);
        var crateModel = SaveModel("Crate", Crate);
        var batteryModel = SaveModel("Battery", Battery);
        var benchModel = SaveModel("Workbench", Workbench);
        var testModel = SaveModel("Tester", Tester);
        foreach (var lamp in lamps)
        {
            HidePrototype(lamp);
            Attach(lampModel, lamp, Vector3.zero);
        }
        HidePrototype(crateSurface);
        foreach (Transform child in crate)
            if (child.name.StartsWith("Readable.")) HidePrototype(child);
        // Keep numbered interaction labels and colliders; remove the bright blocks hiding the lamps.
        for (var i = 0; i < 3; i++) crate.Find("Crate selector " + i).GetComponent<Renderer>().enabled = false;
        Attach(crateModel, crate, Vector3.zero);
        HidePrototype(bench);
        Attach(benchModel, bench, Vector3.zero);
        // Rest both tools on the tabletop instead of leaving the old action cubes floating in front.
        test.position = new Vector3(-0.9f, 1.15f, 5.2f);
        repair.position = new Vector3(0, 1.08f, 5.2f);
        HidePrototype(test);
        Attach(testModel, test, Vector3.zero);
        HidePrototype(repair);
        Attach(batteryModel, repair, Vector3.zero);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    private static void Flashlight(Transform root)
    {
        Box(root, "Painted housing", new Vector3(0, 0, 0), new Vector3(0.24f, 0.28f, 0.18f), paint);
        Box(root, "Lens bezel", new Vector3(0, 0, -0.10f), new Vector3(0.235f, 0.255f, 0.035f), steel);
        Box(root, "Recessed lens", new Vector3(0, 0, -0.121f), new Vector3(0.18f, 0.19f, 0.012f), lens);
        Box(root, "Rubber foot", new Vector3(0, -0.17f, 0), new Vector3(0.29f, 0.06f, 0.23f), rubber);
        Box(root, "Grip", new Vector3(0, 0.26f, 0), new Vector3(0.21f, 0.035f, 0.055f), rubber);
        foreach (var side in new[] { -1, 1 })
            Box(root, "Handle upright", new Vector3(side * 0.09f, 0.20f, 0), new Vector3(0.025f, 0.12f, 0.04f), steel);
        Box(root, "Switch", new Vector3(0.07f, 0.145f, 0), new Vector3(0.04f, 0.016f, 0.04f), rubber);
        Box(root, "Battery cover", new Vector3(0, -0.03f, 0.096f), new Vector3(0.18f, 0.17f, 0.014f), steel);
        for (var i = 0; i < 3; i++)
            Box(root, "Cooling rib", new Vector3(0, -0.07f + i * 0.05f, 0.107f), new Vector3(0.14f, 0.009f, 0.012f), rubber);
    }

    private static void Crate(Transform root)
    {
        Box(root, "Floor", new Vector3(0, -0.12f, 0), new Vector3(1.5f, 0.07f, 0.8f), timber);
        foreach (var x in new[] { -0.72f, 0.72f })
        {
            foreach (var z in new[] { -0.36f, 0.36f })
                Box(root, "Steel corner", new Vector3(x, 0.10f, z), new Vector3(0.055f, 0.5f, 0.055f), steel);
            for (var i = 0; i < 3; i++)
                Box(root, "End slat", new Vector3(x, -0.02f + i * 0.14f, 0), new Vector3(0.055f, 0.10f, 0.76f), timber);
            // Open centre in the top rim reads as a handhold without extra geometry.
            foreach (var z in new[] { -0.26f, 0.26f })
                Box(root, "Handhold rim", new Vector3(x, 0.34f, z), new Vector3(0.07f, 0.06f, 0.26f), steel);
        }
        foreach (var z in new[] { -0.37f, 0.37f })
            for (var i = 0; i < 3; i++)
                Box(root, "Long slat", new Vector3(0, -0.02f + i * 0.14f, z), new Vector3(1.45f, 0.10f, 0.055f), timber);
        foreach (var x in new[] { -0.225f, 0.225f })
            Box(root, "Lamp divider", new Vector3(x, 0.03f, 0), new Vector3(0.025f, 0.25f, 0.69f), timber);
    }

    private static void Battery(Transform root)
    {
        Box(root, "Battery case", Vector3.zero, new Vector3(0.3f, 0.16f, 0.16f), rubber);
        Box(root, "Ivory label", new Vector3(0, 0, -0.083f), new Vector3(0.22f, 0.11f, 0.01f), lens);
        Box(root, "Lid", new Vector3(0, 0.09f, 0), new Vector3(0.31f, 0.03f, 0.17f), steel);
        foreach (var x in new[] { -0.09f, 0.09f })
            Box(root, "Terminal", new Vector3(x, 0.12f, 0), new Vector3(0.035f, 0.035f, 0.04f), copper);
        Box(root, "Positive horizontal", new Vector3(-0.07f, 0, -0.091f), new Vector3(0.055f, 0.012f, 0.008f), rubber);
        Box(root, "Positive vertical", new Vector3(-0.07f, 0, -0.091f), new Vector3(0.012f, 0.055f, 0.008f), rubber);
        Box(root, "Negative", new Vector3(0.07f, 0, -0.091f), new Vector3(0.055f, 0.012f, 0.008f), rubber);
    }

    private static void Workbench(Transform root)
    {
        Box(root, "Thick timber top", new Vector3(0, 0.45f, 0), new Vector3(3, 0.1f, 1), timber);
        foreach (var x in new[] { -1.3f, 1.3f })
        {
            foreach (var z in new[] { -0.38f, 0.38f })
                Box(root, "Square steel leg", new Vector3(x, -0.04f, z), new Vector3(0.08f, 0.88f, 0.08f), steel);
            Box(root, "Side brace", new Vector3(x, -0.25f, 0), new Vector3(0.08f, 0.08f, 0.8f), steel);
        }
        Box(root, "Rear crossbar", new Vector3(0, -0.25f, 0.38f), new Vector3(2.6f, 0.08f, 0.06f), steel);
        Box(root, "Lower shelf", new Vector3(0, -0.22f, 0.10f), new Vector3(2.5f, 0.05f, 0.5f), timber);
        Box(root, "Service mat", new Vector3(0, 0.508f, 0), new Vector3(0.7f, 0.016f, 0.55f), rubber);
        Box(root, "Front edge", new Vector3(0, 0.44f, -0.5f), new Vector3(3, 0.12f, 0.035f), steel);
    }

    private static void Tester(Transform root)
    {
        Box(root, "Meter housing", Vector3.zero, new Vector3(0.48f, 0.2f, 0.23f), paint);
        Box(root, "Scale surround", new Vector3(-0.06f, 0.02f, -0.126f), new Vector3(0.28f, 0.14f, 0.025f), steel);
        Box(root, "Analogue face", new Vector3(-0.06f, 0.02f, -0.142f), new Vector3(0.24f, 0.11f, 0.01f), lens);
        for (var i = 0; i < 5; i++)
            Box(root, "Scale tick", new Vector3(-0.15f + i * 0.045f, 0.045f, -0.15f), new Vector3(0.009f, 0.025f, 0.008f), rubber);
        var needle = Box(root, "Resting needle", new Vector3(-0.09f, 0, -0.156f), new Vector3(0.008f, 0.075f, 0.008f), copper);
        needle.localRotation = Quaternion.Euler(0, 0, -30);
        Box(root, "Selector knob", new Vector3(0.15f, 0, -0.14f), new Vector3(0.06f, 0.06f, 0.04f), rubber);
        foreach (var x in new[] { -0.15f, 0.15f })
        {
            Box(root, "Test lead", new Vector3(x, -0.12f, -0.08f), new Vector3(0.018f, 0.06f, 0.018f), rubber);
            Box(root, "Probe grip", new Vector3(x, -0.13f, 0.06f), new Vector3(0.035f, 0.025f, 0.20f), rubber);
            Box(root, "Probe contact", new Vector3(x, -0.13f, 0.18f), new Vector3(0.012f, 0.012f, 0.065f), copper);
        }
    }

    private static GameObject SaveModel(string name, Action<Transform> build)
    {
        var model = new GameObject(name);
        try
        {
            build(model.transform);
            // One mesh per material, rather than one renderer per small modelling detail.
            var parts = model.GetComponentsInChildren<MeshFilter>();
            foreach (var group in parts.GroupBy(p => p.GetComponent<Renderer>().sharedMaterial))
            {
                var combined = new Mesh { name = name + "_" + group.Key.name };
                combined.CombineMeshes(group.Select(p => new CombineInstance
                {
                    mesh = p.sharedMesh, transform = model.transform.worldToLocalMatrix * p.transform.localToWorldMatrix
                }).ToArray(), true, true);
                var path = Folder + "/" + combined.name + ".asset";
                var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (saved == null) { AssetDatabase.CreateAsset(combined, path); saved = combined; }
                else { EditorUtility.CopySerialized(combined, saved); UnityEngine.Object.DestroyImmediate(combined); }
                var batch = new GameObject(group.Key.name, typeof(MeshFilter), typeof(MeshRenderer));
                batch.transform.SetParent(model.transform, false);
                batch.GetComponent<MeshFilter>().sharedMesh = saved;
                batch.GetComponent<Renderer>().sharedMaterial = group.Key;
            }
            foreach (var part in parts) UnityEngine.Object.DestroyImmediate(part.gameObject);
            return PrefabUtility.SaveAsPrefabAsset(model, Folder + "/" + name + ".prefab");
        }
        finally { UnityEngine.Object.DestroyImmediate(model); }
    }

    private static void Attach(GameObject prefab, Transform parent, Vector3 offset)
    {
        var previous = parent.Find(VisualName);
        if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
        var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        model.name = VisualName;
        model.transform.localPosition = offset;
        model.transform.localRotation = Quaternion.identity;
        var scale = parent.lossyScale;
        model.transform.localScale = new Vector3(1 / scale.x, 1 / scale.y, 1 / scale.z);
    }

    private static void HidePrototype(Transform target)
    {
        var renderer = target.GetComponent<MeshRenderer>();
        if (renderer != null && target.GetComponent<TextMesh>() == null) renderer.enabled = false;
        foreach (Transform child in target)
            if (child.name.StartsWith("Readable.")) HidePrototype(child);
    }

    private static Transform Find(string name)
    {
        var target = GameObject.Find(name);
        if (target == null) throw new InvalidOperationException("Missing FIRST DEAL binding: " + name);
        return target.transform;
    }

    private static Material Palette(string name, Color color, float metallic, float smoothness)
    {
        var path = Folder + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            var template = AssetDatabase.LoadAssetAtPath<Material>("Assets/Scenes/T02Materials/Fixtures.mat");
            if (template == null) throw new InvalidOperationException("Missing project URP material template.");
            material = new Material(template) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        material.color = color;
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", smoothness);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Transform Box(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
    {
        var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        part.GetComponent<Renderer>().sharedMaterial = material;
        UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
        return part.transform;
    }
}
