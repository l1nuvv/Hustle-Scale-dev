using System;
using System.IO;
using Interactables;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SolderingTrialArtBuilder
{
    private const string Root = "Assets/Art/SolderingTrial";

    [MenuItem("Tools/Development Sandbox/Apply M2 Blender Visuals")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Stop Play Mode and save scene work first.");
        EnsureFolder(Root + "/Materials");
        EnsureFolder(Root + "/Prefabs");
        ConfigureTexture("M2_BaseColor", true, false);
        ConfigureTexture("M2_MetallicSmoothness", false, false);
        ConfigureTexture("M2_SoftSmoke", true, true);
        var materialPath = Root + "/Materials/M2_SurfaceAtlas.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, materialPath);
        }
        material.color = Color.white;
        material.SetTexture("_BaseMap", Texture("M2_BaseColor"));
        material.SetTexture("_MetallicGlossMap", Texture("M2_MetallicSmoothness"));
        material.SetFloat("_Metallic", 1);
        material.SetFloat("_Smoothness", 1);
        material.EnableKeyword("_METALLICSPECGLOSSMAP");
        EditorUtility.SetDirty(material);
        var iron = Prefab("M2_SolderingIron", material);
        var solder = Prefab("M2_SolderSpool", material);
        var flashlight = Prefab("M2_OpenFlashlight", material);
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != DevelopmentSandboxBuilder.ScenePath)
            scene = EditorSceneManager.OpenScene(DevelopmentSandboxBuilder.ScenePath);
        var trial = UnityEngine.Object.FindAnyObjectByType<SolderingTrial>();
        if (trial == null) throw new InvalidOperationException("M2 station missing from sandbox.");
        var data = new SerializedObject(trial);
        data.FindProperty("ironVisualPrefab").objectReferenceValue = iron;
        data.FindProperty("solderVisualPrefab").objectReferenceValue = solder;
        data.FindProperty("flashlightVisualPrefab").objectReferenceValue = flashlight;
        data.FindProperty("smokeTexture").objectReferenceValue = Texture("M2_SoftSmoke");
        data.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene);
        Debug.Log("M2 Blender visuals applied: three FBX props, shared textured URP material, soft smoke. Gameplay anchors unchanged.");
    }

    private static GameObject Prefab(string name, Material material)
    {
        var modelPath = Root + "/Models/" + name + ".fbx";
        var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
        if (importer == null) throw new FileNotFoundException("Export Blender models first.", modelPath);
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.addCollider = false;
        importer.globalScale = 1;
        importer.SaveAndReimport();
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        var wrapper = new GameObject(name + " Visual");
        try
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.transform.SetParent(wrapper.transform, false);
            foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>())
                renderer.sharedMaterial = material;
            return PrefabUtility.SaveAsPrefabAsset(wrapper, Root + "/Prefabs/" + name + ".prefab");
        }
        finally { UnityEngine.Object.DestroyImmediate(wrapper); }
    }

    private static Texture2D Texture(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/" + name + ".png");

    private static void ConfigureTexture(string name, bool srgb, bool transparency)
    {
        var importer = AssetImporter.GetAtPath(Root + "/Textures/" + name + ".png") as TextureImporter;
        if (importer == null) throw new FileNotFoundException("Missing Blender texture " + name);
        importer.sRGBTexture = srgb;
        importer.alphaIsTransparency = transparency;
        importer.maxTextureSize = 512;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
    }
}
