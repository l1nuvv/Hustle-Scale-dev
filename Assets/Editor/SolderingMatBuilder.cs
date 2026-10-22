using System;
using System.IO;
using Interactables;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SolderingMatBuilder
{
    private const string Root = "Assets/Art/SolderingTrial/";
    [Serializable] private sealed class Anchor { public string name; public Vector3 position; public Quaternion rotation; }
    [Serializable] private sealed class Layout { public Anchor[] anchors; }

    [MenuItem("Tools/Development Sandbox/Apply Silicone Soldering Mat")]
    public static void Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty || scene.path != DevelopmentSandboxBuilder.ScenePath)
            throw new InvalidOperationException("Open saved DevelopmentSandbox outside Play Mode first.");
        Texture("BaseColor", true, false);
        Texture("Normal", false, true);
        Texture("MetallicSmoothness", false, false);
        Texture("Roughness", false, false);
        var materialPath = Root + "Materials/SolderingMat_Silicone.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, materialPath);
        }
        material.color = Color.white;
        material.SetTexture("_BaseMap", Map("BaseColor"));
        material.SetTexture("_BumpMap", Map("Normal"));
        material.SetFloat("_BumpScale", 0.45f);
        material.EnableKeyword("_NORMALMAP");
        material.SetTexture("_MetallicGlossMap", Map("MetallicSmoothness"));
        material.SetFloat("_Metallic", 0);
        material.SetFloat("_Smoothness", 1);
        material.EnableKeyword("_METALLICSPECGLOSSMAP");
        EditorUtility.SetDirty(material);
        var modelPath = Root + "Models/SolderingMat.fbx";
        var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
        if (importer == null) throw new FileNotFoundException("Export the Blender mat first.", modelPath);
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.addCollider = false;
        importer.globalScale = 1;
        importer.SaveAndReimport();
        var wrapper = new GameObject("Silicone Soldering Mat");
        GameObject prefab;
        try
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));
            model.transform.SetParent(wrapper.transform, false);
            foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>()) renderer.sharedMaterial = material;
            var layout = JsonUtility.FromJson<Layout>(File.ReadAllText(Root + "Models/SolderingMat.layout.json"));
            foreach (var anchor in layout.anchors)
            {
                var marker = new GameObject(anchor.name).transform;
                marker.SetParent(wrapper.transform, false);
                marker.localPosition = anchor.position;
                var q = anchor.rotation;
                marker.localRotation = q.x == 0 && q.y == 0 && q.z == 0 && q.w == 0 ? Quaternion.identity : q;
            }
            prefab = PrefabUtility.SaveAsPrefabAsset(wrapper, Root + "Prefabs/SolderingMat.prefab");
        }
        finally { UnityEngine.Object.DestroyImmediate(wrapper); }
        foreach (var trial in UnityEngine.Object.FindObjectsByType<SolderingTrial>())
        {
            var data = new SerializedObject(trial);
            data.FindProperty("solderingMatPrefab").objectReferenceValue = prefab;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Silicone mat applied to M2/M3: repair centre, solder upper left, iron right, lid tray behind repair. No economy or save changes.");
    }

    private static Texture2D Map(string suffix) => AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "Textures/SolderingMat_" + suffix + ".png");

    [MenuItem("Tools/Development Sandbox/Verify Silicone Mat In Play Mode")]
    public static void Verify()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter DevelopmentSandbox Play Mode first.");
        var report = new System.Text.StringBuilder();
        var count = 0;
        Action<bool, string> check = (ok, message) =>
        {
            report.AppendLine((ok ? "PASS " : "FAIL ") + message);
            if (!ok) { File.AppendAllText("Logs/Codex-soldering-mat-verification.log", report.ToString()); throw new InvalidOperationException(message); }
            count++;
        };
        foreach (var trial in UnityEngine.Object.FindObjectsByType<SolderingTrial>())
        {
            trial.Exit(); trial.ResetTrial();
            var mat = trial.WorkMat;
            check(mat != null, "M2/M3 uses authored silicone mat");
            var mesh = mat.GetComponentInChildren<MeshFilter>();
            check(mesh.sharedMesh.uv.Length == mesh.sharedMesh.vertexCount, "UVs cover the complete silicone mesh");
            var material = mesh.GetComponent<MeshRenderer>().sharedMaterial;
            check(material.GetTexture("_BaseMap") != null && material.GetTexture("_BumpMap") != null &&
                material.GetTexture("_MetallicGlossMap") != null && material.IsKeywordEnabled("_NORMALMAP"), "URP uses colour, normal and packed smoothness maps");
            var bounds = mesh.GetComponent<MeshRenderer>().bounds;
            check(bounds.size.x > 1.7f && bounds.size.x < 1.8f && bounds.size.z <= (trial.IsFullOperation ? 1.23f : 0.92f), "mat fits existing table without expanding it");
            var iron = trial.transform.Find("Soldering iron - tip origin");
            var solder = trial.transform.Find("Solder spool - wire end origin");
            check(Vector3.Distance(iron.position, mat.Find("IronDock").position) < .001f &&
                Quaternion.Angle(iron.rotation, mat.Find("IronDock").rotation) < .01f, "iron returns exactly to authored position and resting rotation");
            foreach (var renderer in iron.GetComponentsInChildren<MeshRenderer>())
                if (renderer.name == "Ribbed rubber hand grip")
                {
                    var minimum = float.MaxValue;
                    foreach (var vertex in renderer.GetComponent<MeshFilter>().sharedMesh.vertices)
                        minimum = Mathf.Min(minimum, renderer.transform.TransformPoint(vertex).y);
                    check(renderer.bounds.center.y > 1.005f && minimum > .993f, "actual iron grip vertices remain above silicone surface");
                }
            check(Vector3.Distance(solder.position, mat.Find("SolderDock").position) < .001f, "solder returns exactly to upper-left anchor");
            var feedVisible = false;
            foreach (var part in solder.GetComponentsInChildren<Transform>(true))
                if (part.name == "Solder feed wire") feedVisible = part.gameObject.activeSelf;
            check(!feedVisible && solder.GetComponent<BoxCollider>().bounds.min.y >= .993f, "resting spool sits on silicone without wire protruding through table");
            check(mat.Find("RepairOrigin").position.x > mat.Find("SolderDock").position.x &&
                mat.Find("IronDock").position.x > mat.Find("RepairOrigin").position.x, "centre device leaves separate left/right tool zones");
            if (trial.IsFullOperation)
            {
                var flashlight = trial.transform.Find("M3_Flashlight(Clone)");
                check(flashlight != null && Vector3.Distance(flashlight.position, mat.Find("Supply").position) < .001f, "reset flashlight uses mat supply anchor");
                check(mat.Find("CoverTray").position.z > mat.Find("RepairOrigin").position.z + .3f, "lid tray is behind repair, clear of solder and iron");
            }
        }
        report.AppendLine("SILICONE MAT PLAY MODE: PASS " + count + "; Unity " + Application.unityVersion);
        File.AppendAllText("Logs/Codex-soldering-mat-verification.log", report.ToString());
        Debug.Log("SILICONE MAT PLAY MODE: PASS " + count + "; Logs/Codex-soldering-mat-verification.log");
    }
    private static void Texture(string suffix, bool srgb, bool normal)
    {
        var path = Root + "Textures/SolderingMat_" + suffix + ".png";
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new FileNotFoundException(path);
        importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = srgb;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.maxTextureSize = 1024;
        importer.SaveAndReimport();
    }
}
