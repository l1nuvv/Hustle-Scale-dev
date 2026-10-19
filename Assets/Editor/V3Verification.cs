using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class V3Verification
{
    private const string LogPath = "Logs/Codex-V3-verification.log";
    private static int checks;

    [MenuItem("Tools/V3/Verify Light and Surroundings")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Stop Play Mode and save the scene first.");
        Directory.CreateDirectory("Logs");
        File.WriteAllText(LogPath, "V3 Unity " + Application.unityVersion + "\n");
        checks = 0;
        EditorSceneManager.OpenScene(FirstDealTradeBuilder.ScenePath);
        var colliders = UnityEngine.Object.FindObjectsByType<Collider>();
        var assets = AssetDatabase.FindAssets("", new[] { FirstDealLightingBuilder.Folder }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p).ToArray();
        var guids = assets.Select(AssetDatabase.AssetPathToGUID).ToArray();
        FirstDealLightingBuilder.ApplyCurrent();
        FirstDealLightingBuilder.ApplyCurrent();
        var reappliedColliders = UnityEngine.Object.FindObjectsByType<Collider>();
        Check(colliders.Length == reappliedColliders.Length && colliders.All(c => reappliedColliders.Contains(c)), "original collision and interaction surfaces preserved");
        Check(assets.SequenceEqual(AssetDatabase.FindAssets("", new[] { FirstDealLightingBuilder.Folder }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p)), "reapply creates no extra assets");
        Check(guids.SequenceEqual(assets.Select(AssetDatabase.AssetPathToGUID)), "material GUIDs stable");
        Check(UnityEngine.Object.FindObjectsByType<Transform>().Count(t => t.name == FirstDealLightingBuilder.RootName) == 1, "one presentation root after reapply");
        var root = GameObject.Find(FirstDealLightingBuilder.RootName);
        Check(root.GetComponentsInChildren<Collider>().Length == 0, "decoration introduces no blockers");
        Check(root.GetComponentsInChildren<Renderer>().Length <= 40, "bounded decoration renderer count");
        foreach (var renderer in root.GetComponentsInChildren<Renderer>())
            Check(renderer.sharedMaterial != null && renderer.sharedMaterial.shader.isSupported && !ShaderUtil.ShaderHasError(renderer.sharedMaterial.shader), "supported material: " + renderer.name);
        var roof = GameObject.Find("Workshop roof").GetComponent<Renderer>();
        Check(roof.bounds.min.y >= 2.99f && roof.shadowCastingMode == ShadowCastingMode.On, "roof meets walls above player and casts shadow");
        Check(RenderSettings.sun != null && RenderSettings.sun.shadows == LightShadows.Soft, "sun owns soft shadows");
        Check(RenderSettings.ambientMode == AmbientMode.Trilight && !RenderSettings.fog, "readable ambient fill without indoor fog");
        var lights = root.GetComponentsInChildren<Light>();
        Check(lights.Length == 2 && lights.All(l => l.type == LightType.Spot && l.shadows == LightShadows.None && l.range <= 5.5f), "two bounded task lights without extra shadow maps");
        foreach (var name in new[] { "Bench task light", "Entry fill light" })
            Check(GameObject.Find(name).transform.forward.y < -0.99f, name + " aims downward");
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Capture("workbench", new Vector3(-0.3f, 2.1f, 3.9f), new Vector3(0, 1.15f, 5.3f));
        Capture("workshop", new Vector3(-4, 1.7f, 2.4f), new Vector3(0, 1.65f, 6));
        Capture("yard", new Vector3(0, 1.7f, 0), new Vector3(0, 1.4f, -5));
        Capture("entrance", new Vector3(0, 1.7f, -3.5f), new Vector3(0, 1.7f, 4));
        File.AppendAllText(LogPath, "V3 RESULT PASS " + checks + " checks; four captures\n");
        Debug.Log("V3 RESULT PASS " + checks + " checks");
    }

    private static void Capture(string name, Vector3 position, Vector3 target)
    {
        var go = new GameObject("V3 temporary camera");
        var camera = go.AddComponent<Camera>();
        var texture = new RenderTexture(1200, 800, 24);
        var pixels = new Texture2D(1200, 800, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            camera.CopyFrom(Camera.main);
            camera.enabled = false;
            camera.fieldOfView = 60;
            camera.transform.position = position;
            camera.transform.LookAt(target);
            camera.targetTexture = texture;
            camera.Render();
            RenderTexture.active = texture;
            pixels.ReadPixels(new Rect(0, 0, 1200, 800), 0, 0);
            pixels.Apply();
            File.WriteAllBytes("Logs/V3-" + name + ".png", pixels.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            camera.targetTexture = null;
            texture.Release();
            UnityEngine.Object.DestroyImmediate(pixels);
            UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("V3 FAIL: " + message);
        checks++;
        File.AppendAllText(LogPath, "PASS " + message + "\n");
    }
}
