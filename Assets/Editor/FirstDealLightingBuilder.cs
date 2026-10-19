using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// V3 authoring only. Keeps the validated collision and interaction geometry intact.
public static class FirstDealLightingBuilder
{
    public const string RootName = "V3 Light and Surroundings";
    public const string Folder = "Assets/Prefabs/FirstDealLighting";

    [MenuItem("Tools/V3/Apply Light and Surroundings")]
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
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Prefabs", "FirstDealLighting");
        var plaster = Material("Warm plaster", new Color(0.64f, 0.61f, 0.53f));
        var steel = Material("Roof steel", new Color(0.16f, 0.20f, 0.22f));
        var timber = Material("Shelf timber", new Color(0.34f, 0.24f, 0.15f));
        var yard = Material("Yard masonry", new Color(0.45f, 0.48f, 0.47f));
        var lamp = Material("Warm diffuser", new Color(0.95f, 0.86f, 0.63f));
        // The visible tube stays bright independently of the real spot light.
        lamp.shader = Shader.Find("Universal Render Pipeline/Unlit");
        lamp.color = new Color(1, 0.88f, 0.65f);
        lamp.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        EditorUtility.SetDirty(lamp);

        var previous = GameObject.Find(RootName);
        if (previous != null) UnityEngine.Object.DestroyImmediate(previous);
        var root = new GameObject(RootName).transform;
        foreach (var name in new[] { "North wall", "Partition left", "Partition right" })
            GameObject.Find(name).GetComponent<Renderer>().sharedMaterial = plaster;
        foreach (var name in new[] { "South wall", "West wall", "East wall" })
            GameObject.Find(name).GetComponent<Renderer>().sharedMaterial = yard;

        // Roof casts the sun's shadow; ambient fill keeps labels readable underneath.
        Piece(root, "Workshop roof", new Vector3(0, 3.08f, 4.5f), new Vector3(20, 0.16f, 7.2f), steel);
        foreach (var x in new[] { -6f, 0f, 6f })
            Piece(root, "Roof beam", new Vector3(x, 3.04f, 4.5f), new Vector3(0.12f, 0.18f, 7), timber);
        Fixture(root, "Bench task light", new Vector3(0, 2.88f, 5.4f), 6f, 5.3f, lamp, steel);
        Fixture(root, "Entry fill light", new Vector3(-3.8f, 2.88f, 3.7f), 3f, 5.5f, lamp, steel);

        // Wall details stay behind the stations and away from all walking routes.
        foreach (var x in new[] { -5.8f, 5.8f })
        {
            Piece(root, "Wall shelf", new Vector3(x, 1.35f, 7.65f), new Vector3(2.6f, 0.09f, 0.32f), timber);
            foreach (var offset in new[] { -0.9f, 0.9f })
                Piece(root, "Shelf bracket", new Vector3(x + offset, 1.16f, 7.72f), new Vector3(0.06f, 0.3f, 0.16f), steel);
            Piece(root, "Closed storage box", new Vector3(x - 0.5f, 1.58f, 7.65f), new Vector3(0.65f, 0.38f, 0.28f), timber);
        }
        Piece(root, "Yard drain border", new Vector3(0, 0.02f, -7.35f), new Vector3(17, 0.015f, 0.22f), steel);
        for (var i = -8; i <= 8; i++)
            Piece(root, "Drain crossbar", new Vector3(i, 0.031f, -7.35f), new Vector3(0.035f, 0.01f, 0.24f), yard);

        var sun = GameObject.Find("Directional Light").GetComponent<Light>();
        sun.transform.rotation = Quaternion.Euler(48, -145, 0);
        sun.color = new Color(1, 0.94f, 0.82f);
        sun.intensity = 1.3f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.8f;
        sun.shadowBias = 0.035f;
        RenderSettings.sun = sun;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.52f, 0.60f, 0.72f);
        RenderSettings.ambientEquatorColor = new Color(0.36f, 0.38f, 0.40f);
        RenderSettings.ambientGroundColor = new Color(0.22f, 0.21f, 0.19f);
        RenderSettings.fog = false;
        Camera.main.clearFlags = CameraClearFlags.Skybox;
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    private static void Fixture(Transform parent, string name, Vector3 position, float intensity, float range, Material diffuser, Material steel)
    {
        Piece(parent, name + " housing", position, new Vector3(1.6f, 0.10f, 0.32f), steel);
        Piece(parent, name + " diffuser", position + Vector3.down * 0.06f, new Vector3(1.4f, 0.025f, 0.24f), diffuser);
        var light = new GameObject(name).AddComponent<Light>();
        light.transform.SetParent(parent, false);
        light.transform.position = position + Vector3.down * 0.15f;
        light.transform.rotation = Quaternion.Euler(90, 0, 0);
        light.type = LightType.Spot;
        light.color = new Color(1, 0.85f, 0.62f);
        light.intensity = intensity;
        light.range = range;
        light.spotAngle = 115;
        light.innerSpotAngle = 65;
        // Only the sun owns a shadow map; task lights are short range and inexpensive.
        light.shadows = LightShadows.None;
        light.renderMode = LightRenderMode.ForcePixel;
    }

    private static Material Material(string name, Color color)
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

    private static void Piece(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
    {
        var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        part.GetComponent<Renderer>().sharedMaterial = material;
        UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
    }
}
