using System;
using System.Linq;
using Interactables;
using Player;
using Player.Movement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SharpeningTrialBuilder
{
    private const string Art = "Assets/Art/SharpeningTrial/";
    [MenuItem("Tools/Development Sandbox/Install M6 Once")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Stop Play Mode and preserve scene edits first.");
        var scene = EditorSceneManager.OpenScene(DevelopmentSandboxBuilder.ScenePath);
        if (UnityEngine.Object.FindAnyObjectByType<SharpeningTrial>() != null)
            throw new InvalidOperationException("M6 already exists; preserve authored work.");
        var root = new GameObject("M6 - Knife sharpening trial");
        var m3 = UnityEngine.Object.FindObjectsByType<SolderingTrial>().Single(t => t.IsFullOperation);
        root.transform.SetPositionAndRotation(m3.transform.position + m3.transform.right * 2.4f, m3.transform.rotation);
        var table = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/FirstDealProps/Workbench.prefab"), root.transform);
        table.transform.localPosition = new Vector3(0, .50f, 0);
        table.transform.localScale = new Vector3(.36f, 1, .70f);
        // Reuse the existing workbench, at its authored one-metre work surface.
        var machine = Model("M6_Grinder", root.transform);
        machine.transform.localPosition = Vector3.up;
        var knife = Model("M6_Knife", root.transform);
        var knifePivot = new GameObject("M6 edge pivot").transform;
        knifePivot.SetParent(root.transform, false);
        knife.transform.SetParent(knifePivot, false);
        var trial = root.AddComponent<SharpeningTrial>();
        var data = new SerializedObject(trial);
        var sandboxData = new SerializedObject(UnityEngine.Object.FindAnyObjectByType<DevelopmentSandbox>());
        data.FindProperty("player").objectReferenceValue = sandboxData.FindProperty("player").objectReferenceValue;
        data.FindProperty("view").objectReferenceValue = Camera.main;
        data.FindProperty("knife").objectReferenceValue = knifePivot;
        data.FindProperty("wheel").objectReferenceValue = machine.GetComponentsInChildren<Transform>().Single(t => t.name == "Wheel");
        data.FindProperty("wheelMark").objectReferenceValue = machine.GetComponentsInChildren<Transform>().Single(t => t.name == "WheelMark");
        data.ApplyModifiedPropertiesWithoutUndo();
        ConfigureKnife(trial);
        var collider = root.AddComponent<BoxCollider>();
        collider.center = new Vector3(0, 1.05f, 0);
        collider.size = new Vector3(1.05f, .30f, .62f);
        var label = new GameObject("M6 title").AddComponent<TextMesh>();
        label.transform.SetParent(root.transform, false);
        label.transform.localPosition = new Vector3(-.45f, 1.53f, .4f);
        label.text = "M6 / ЗАТОЧКА НОЖА\nE — начать пробу";
        label.characterSize = .008f; label.fontSize = 48; label.color = new Color(.8f, .85f, .8f);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene);
        Debug.Log("M6 installed beside M3 at " + root.transform.position);
    }

    [MenuItem("Tools/Development Sandbox/Apply M6 Kitchen Models")]
    public static void ApplyKitchenModels()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Stop Play Mode and preserve scene edits first.");
        var trial = UnityEngine.Object.FindAnyObjectByType<SharpeningTrial>();
        if (trial == null) throw new InvalidOperationException("Open the sandbox with M6 first.");
        // Rebind materials on the reimported FBX instances; preserve the station/camera/player setup.
        foreach (var renderer in trial.GetComponentsInChildren<Renderer>().Where(r => r.GetComponentInParent<TextMesh>() == null))
        {
            if (!renderer.transform.IsChildOf(trial.transform.Find("Workbench")))
            {
                var source = PrefabUtility.GetCorrespondingObjectFromSource(renderer);
                var materials = source != null ? source.sharedMaterials : renderer.sharedMaterials;
                renderer.sharedMaterials = materials.Select(m =>
                    Material(m.name, m.color, m.name.Contains("Steel") || m.name.Contains("Bevel") ? .7f : 0)).ToArray();
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
        }
        ConfigureKnife(trial);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(trial.gameObject.scene);
    }

    private static void ConfigureKnife(SharpeningTrial trial)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(Art + "M6PowderSpray.png");
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.wrapModeU = TextureWrapMode.Repeat; importer.wrapModeV = TextureWrapMode.Clamp;
        importer.maxTextureSize = 2048; importer.SaveAndReimport();
        var path = Art + "M6_PowderCoating.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("HustleScale/M6PowderCoating"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetTexture("_PowderMask", AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "M6PowderSpray.png"));
        material.SetColor("_Tint", new Color(.04f, .78f, 1));
        EditorUtility.SetDirty(material);
        var data = new SerializedObject(trial);
        var knife = (Transform)data.FindProperty("knife").objectReferenceValue;
        var coating = knife.GetComponentsInChildren<Renderer>().Single(r => r.name == "PowderCoating");
        coating.sharedMaterial = material;
        PrefabUtility.RecordPrefabInstancePropertyModifications(coating);
        data.FindProperty("coating").objectReferenceValue = coating;
        var samples = data.FindProperty("edgeSamples"); samples.arraySize = SharpeningTrialState.Sections;
        for (var i = 0; i < samples.arraySize; i++)
            samples.GetArrayElementAtIndex(i).objectReferenceValue = knife.GetComponentsInChildren<Transform>().Single(t => t.name == $"CuttingEdge_{i:00}");
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject Model(string name, Transform parent)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Art + name + ".fbx"), parent);
        foreach (var renderer in go.GetComponentsInChildren<Renderer>())
        {
            renderer.sharedMaterials = renderer.sharedMaterials.Select(m =>
                Material(m.name, m.color, m.name.Contains("Steel") ? .65f : 0)).ToArray();
        }
        return go;
    }

    private static Material Material(string name, Color color, float metal)
    {
        var path = Art + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.name = name; material.color = color;
        material.SetFloat("_Metallic", metal); material.SetFloat("_Smoothness", .28f);
        if (name == "M6_ClearShield")
        {
            material.color = new Color(.55f, .68f, .69f, .16f);
            material.SetFloat("_Surface", 1); material.SetFloat("_ZWrite", 0);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent"); material.renderQueue = 3000;
        }
        if (name == "M6_UnfinishedEdge")
        {
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 1.5f);
        }
        AssetDatabase.CreateAsset(material, path);
        return material;
    }
}
