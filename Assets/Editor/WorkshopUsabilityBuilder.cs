using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Interactables;
using Player.Movement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class WorkshopUsabilityBuilder
{
    private const string Folder = "Assets/Prefabs/WorkshopSurfaces";

    [MenuItem("Tools/Workshop/Apply Inventory and Surface Materials")]
    public static void Apply()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty || scene.path != FirstDealTradeBuilder.ScenePath)
            throw new InvalidOperationException("Open and save FirstDealTrade in Edit Mode first.");
        var workshop = UnityEngine.Object.FindAnyObjectByType<WorkshopView>();
        var carry = workshop.GetComponent<WorkshopCarryController>();
        if (carry == null) carry = workshop.gameObject.AddComponent<WorkshopCarryController>();
        var serialized = new SerializedObject(carry);
        serialized.FindProperty("deal").objectReferenceValue = UnityEngine.Object.FindAnyObjectByType<FirstDealView>();
        serialized.FindProperty("player").objectReferenceValue = UnityEngine.Object.FindObjectsByType<PlayerController>().First(p => p.isActiveAndEnabled);
        serialized.FindProperty("eye").objectReferenceValue = Camera.main;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Prefabs", "WorkshopSurfaces");
        var textures = new Dictionary<string, Texture2D[]>();
        foreach (var type in new[] { "Timber", "Concrete", "Metal", "Paint" }) textures[type] = TexturePair(type);
        var replacements = new Dictionary<Material, Material>();
        foreach (var renderer in UnityEngine.Object.FindObjectsByType<MeshRenderer>())
        {
            if (!renderer.enabled || renderer.GetComponent<TextMesh>() != null) continue;
            var source = renderer.sharedMaterial;
            if (source == null || source.name.StartsWith("Workshop_")) continue;
            var path = AssetDatabase.GetAssetPath(source);
            if (!path.Contains("FirstDealProps/") && !path.Contains("FirstDealEnvironment/") &&
                !path.Contains("FirstDealLighting/") && source.name != "Floor" && !renderer.name.StartsWith("Readable.")) continue;
            // Preserve the user's diffuser and the existing sign shaders.
            if (source.name == "Warm diffuser" || source.shader.name != "Universal Render Pipeline/Lit") continue;
            if (!replacements.TryGetValue(source, out var material))
            {
                var type = source.name.Contains("Timber") || source.name.Contains("timber") ? "Timber" :
                    source.name.Contains("Steel") || source.name.Contains("steel") || source.name == "Contacts" ? "Metal" :
                    source.name == "Concrete" || source.name == "Floor" || source.name.Contains("masonry") || source.name.Contains("plaster") ? "Concrete" : "Paint";
                var name = "Workshop_" + source.name.Replace(' ', '_');
                material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/" + name + ".mat");
                if (material == null)
                {
                    material = new Material(source) { name = name };
                    AssetDatabase.CreateAsset(material, Folder + "/" + name + ".mat");
                }
                material.SetTexture("_BaseMap", textures[type][0]);
                material.SetTexture("_BumpMap", textures[type][1]);
                material.SetFloat("_BumpScale", type == "Concrete" ? 0.5f : 0.25f);
                material.EnableKeyword("_NORMALMAP");
                material.SetFloat("_Smoothness", type == "Metal" ? 0.38f : type == "Paint" ? 0.22f : 0.1f);
                material.SetFloat("_Metallic", type == "Metal" ? 0.75f : 0);
                if (source.name == "Concrete" || source.name == "Floor") material.SetTextureScale("_BaseMap", new Vector2(12, 8));
                else if (source.name.Contains("plaster") || source.name.Contains("masonry")) material.SetTextureScale("_BaseMap", new Vector2(7, 3));
                if (source.name == "Ivory") { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", Color.black); }
                EditorUtility.SetDirty(material);
                replacements[source] = material;
            }
            renderer.sharedMaterial = material;
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        }
        var body = RoundedHousing();
        var bindings = new SerializedObject(workshop);
        for (var i = 0; i < 3; i++)
        {
            var lamp = (Transform)bindings.FindProperty("lamps").GetArrayElementAtIndex(i).objectReferenceValue;
            var housing = lamp.Find(FirstDealPropBuilder.VisualName + "/BluePaint");
            housing.GetComponent<MeshFilter>().sharedMesh = body;
            PrefabUtility.RecordPrefabInstancePropertyModifications(housing.GetComponent<MeshFilter>());
            var light = (Light)bindings.FindProperty("indicators").GetArrayElementAtIndex(i).objectReferenceValue;
            var scale = lamp.lossyScale;
            light.transform.localPosition = new Vector3(0, 0, -0.145f / scale.z);
            light.transform.localRotation = Quaternion.Euler(0, 180, 0);
            light.type = LightType.Spot; light.range = 12; light.spotAngle = 48; light.innerSpotAngle = 26;
            light.intensity = 8; light.color = new Color(1, 0.93f, 0.78f);
            light.shadows = LightShadows.Soft; light.shadowBias = 0.015f; light.shadowNormalBias = 0.1f;
            light.enabled = false;
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Workshop updated: four inventory slots, placement, switchable spot beams and surface materials.");
    }

    private static Texture2D[] TexturePair(string type)
    {
        const int size = 256;
        var heights = new float[size * size];
        var colors = new Color[heights.Length];
        var normals = new Color[heights.Length];
        var random = new System.Random(413);
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var fine = (float)random.NextDouble();
                var broad = TileNoise(x / (float)size, y / (float)size, 8);
                var h = type == "Timber" ? 0.5f + 0.2f * Mathf.Sin(y * Mathf.PI / 8 + broad * 9) + fine * 0.06f :
                    type == "Metal" ? 0.65f + fine * 0.04f - (y % 23 == 0 ? 0.08f : 0) :
                    type == "Concrete" ? broad * 0.5f + fine * 0.25f : 0.65f + broad * 0.08f + fine * 0.04f;
                heights[y * size + x] = h;
                var shade = type == "Timber" ? 0.58f + h * 0.45f : 0.72f + h * 0.3f;
                colors[y * size + x] = new Color(shade, shade, shade, 1);
            }
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var dx = heights[y * size + (x + 1) % size] - heights[y * size + (x + size - 1) % size];
                var dy = heights[((y + 1) % size) * size + x] - heights[((y + size - 1) % size) * size + x];
                var n = new Vector3(-dx * 1.4f, -dy * 1.4f, 1).normalized;
                normals[y * size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1);
            }
        return new[] { SaveTexture(type, colors, false), SaveTexture(type + "_Normal", normals, true) };
    }

    private static float TileNoise(float x, float y, float scale)
    {
        var a = Mathf.Lerp(Mathf.PerlinNoise(x * scale, y * scale), Mathf.PerlinNoise((x - 1) * scale, y * scale), x);
        var b = Mathf.Lerp(Mathf.PerlinNoise(x * scale, (y - 1) * scale), Mathf.PerlinNoise((x - 1) * scale, (y - 1) * scale), x);
        return Mathf.Lerp(a, b, y);
    }

    private static Texture2D SaveTexture(string name, Color[] pixels, bool normal)
    {
        var texture = new Texture2D(256, 256, TextureFormat.RGB24, true, normal);
        texture.SetPixels(pixels); texture.Apply();
        var path = Folder + "/" + name + ".png";
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = !normal; importer.wrapMode = TextureWrapMode.Repeat;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static Mesh RoundedHousing()
    {
        var vertices = new List<Vector3>(); var normals = new List<Vector3>();
        var uv = new List<Vector2>(); var triangles = new List<int>();
        var half = new Vector3(0.12f, 0.14f, 0.09f); const float radius = 0.012f;
        var inside = half - Vector3.one * radius;
        foreach (var normal in new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back })
        {
            var u = Mathf.Abs(normal.y) > 0.5f ? Vector3.right : Vector3.Cross(Vector3.up, normal).normalized;
            var v = Vector3.Cross(normal, u);
            var start = vertices.Count;
            for (var y = 0; y < 4; y++)
                for (var x = 0; x < 4; x++)
                {
                    var p = Vector3.Scale(normal, half) + Vector3.Scale(u, half) * Grid(x, Vector3.Dot(Abs(u), half), radius) +
                        Vector3.Scale(v, half) * Grid(y, Vector3.Dot(Abs(v), half), radius);
                    var centre = new Vector3(Mathf.Clamp(p.x, -inside.x, inside.x), Mathf.Clamp(p.y, -inside.y, inside.y), Mathf.Clamp(p.z, -inside.z, inside.z));
                    var n = (p - centre).normalized;
                    vertices.Add(centre + n * radius); normals.Add(n); uv.Add(new Vector2(x / 3f, y / 3f));
                }
            for (var y = 0; y < 3; y++)
                for (var x = 0; x < 3; x++)
                {
                    var a = start + y * 4 + x;
                    triangles.AddRange(new[] { a, a + 1, a + 4, a + 1, a + 5, a + 4 });
                }
        }
        var mesh = new Mesh { name = "Bevelled workshop lamp housing" };
        mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds(); mesh.RecalculateTangents();
        var path = Folder + "/LampHousing.asset";
        var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (saved == null) { AssetDatabase.CreateAsset(mesh, path); saved = mesh; }
        else { EditorUtility.CopySerialized(mesh, saved); UnityEngine.Object.DestroyImmediate(mesh); }
        return saved;
    }

    private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    private static float Grid(int i, float half, float radius) => i == 0 ? -1 : i == 3 ? 1 : (i == 1 ? -1 : 1) * (1 - radius / half);
}
