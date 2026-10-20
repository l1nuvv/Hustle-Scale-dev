using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// The editable character source lives in SourceArt/ArtDirectionTest/Worker.
// This only adapts the Blender FBX to the project's existing URP and prefab.
public static class ArtDirectionWorkerImporter
{
    private const string Folder = "Assets/ArtDirectionTest/Worker";
    private const string ModelPath = Folder + "/WorkshopWorker.fbx";
    private const string PrefabPath = "Assets/ArtDirectionTest/Prefabs/WorkshopWorker.prefab";

    [MenuItem("Tools/Art Direction/Import Blender Worker")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Stop Play Mode and save the current scene first.");
        AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceSynchronousImport);
        var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (importer == null) throw new InvalidOperationException("Export the worker FBX from Blender first.");
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importNormals = ModelImporterNormals.Import;
        importer.importAnimation = true;
        var clips = importer.defaultClipAnimations;
        if (clips.Length != 1) throw new InvalidOperationException("Expected one Blender idle take.");
        clips[0].name = "Worker_Idle";
        clips[0].loopTime = true;
        clips[0].lockRootPositionXZ = true;
        clips[0].lockRootHeightY = true;
        clips[0].lockRootRotation = true;
        importer.clipAnimations = clips;
        importer.SaveAndReimport();

        var materialNames = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Material>().Select(m => m.name)
            .Concat(importer.GetExternalObjectMap().Keys.Where(k => k.type == typeof(Material)).Select(k => k.name)).Distinct();
        foreach (var sourceName in materialNames)
        {
            var path = Folder + "/" + sourceName + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = sourceName };
                AssetDatabase.CreateAsset(mat, path);
            }
            var color = sourceName.Contains("Skin") ? new Color(.64f, .46f, .32f) :
                sourceName.Contains("Navy") ? new Color(.12f, .22f, .31f) :
                sourceName.Contains("Trousers") ? new Color(.13f, .18f, .24f) :
                sourceName.Contains("Leather") ? new Color(.13f, .095f, .07f) :
                sourceName.Contains("EyeWhite") ? new Color(.93f, .92f, .87f) :
                sourceName.Contains("Undershirt") ? new Color(.80f, .80f, .75f) :
                sourceName.Contains("Pupil") ? new Color(.025f, .025f, .022f) : new Color(.11f, .075f, .045f);
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Metallic", 0);
            mat.SetFloat("_Smoothness", sourceName.Contains("Eye") ? .28f : .12f);
            mat.SetTexture("_BaseMap", null);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", null);
            EditorUtility.SetDirty(mat);
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), sourceName), mat);
        }
        importer.SaveAndReimport();
        var clip = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>().Single(c => !c.name.StartsWith("__"));
        var controllerPath = Folder + "/WorkerIdle.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var state = controller.layers[0].stateMachine.AddState("Quiet standing idle");
            state.motion = clip;
            controller.layers[0].stateMachine.defaultState = state;
        }

        var guid = AssetDatabase.AssetPathToGUID(PrefabPath);
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            // Only the rejected art-sample character's contents are replaced.
            while (root.transform.childCount > 0) Object.DestroyImmediate(root.transform.GetChild(0).gameObject);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath), root.transform);
            model.name = "Blender worker";
            var animator = model.GetComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            clip.SampleAnimation(model, 0);
            model.transform.localRotation = Quaternion.identity;
            var bounds = PosedBounds(model);
            model.transform.localScale *= 1.75f / bounds.size.y;
            bounds = PosedBounds(model);
            model.transform.localPosition -= Vector3.up * bounds.min.y;
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        if (AssetDatabase.AssetPathToGUID(PrefabPath) != guid) throw new Exception("Worker prefab GUID changed.");
        if (SceneManager.GetActiveScene().path == ArtDirectionTestBuilder.ScenePath)
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log("BLENDER WORKER IMPORT PASS: rig, looping idle, URP materials; original prefab GUID preserved.");
    }

    private static Bounds PosedBounds(GameObject model)
    {
        var bounds = new Bounds();
        var initialized = false;
        foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            var mesh = new Mesh();
            // Account for FBX's centimetre transforms exactly once.
            renderer.BakeMesh(mesh, true);
            foreach (var vertex in mesh.vertices)
            {
                var point = model.transform.parent.InverseTransformPoint(renderer.transform.TransformPoint(vertex));
                if (!initialized) { bounds = new Bounds(point, Vector3.zero); initialized = true; }
                else bounds.Encapsulate(point);
            }
            Object.DestroyImmediate(mesh);
        }
        if (!initialized || bounds.size.y < .1f) throw new InvalidOperationException("Worker mesh has invalid bounds.");
        return bounds;
    }
}
