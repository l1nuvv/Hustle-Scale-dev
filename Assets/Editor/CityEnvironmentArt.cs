using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// DCC assets replace the greybox's renderers; existing gameplay and collision remain authored in the scene.
public static class CityEnvironmentArt
{
    private const string Folder = "Assets/Art/CityEnvironment";
    private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();

    [MenuItem("Tools/City/Apply Blender Environment Art")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before authoring.");
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/CityGreybox.unity") throw new InvalidOperationException("Open CityGreybox first.");
        Materials.Clear();
        EnsureFolder(Folder + "/Materials");
        ConfigureTexture(Folder + "/Textures/Masonry.png");
        ConfigureTexture(Folder + "/Textures/Asphalt.png");
        ConfigureTexture(Folder + "/Textures/Timber.png");
        var generated = GameObject.Find("CityGreybox - generated").transform;
        var local = generated.Find("Workshop neighbourhood");
        // The room's literal name contains '/', so Transform.Find cannot address it as a path.
        var room = generated.Find("Workshop and trading placeholders").Cast<Transform>()
            .Single(t => t.name.StartsWith("Interior | WORKSHOP", StringComparison.Ordinal));
        foreach (var renderer in local.GetComponentsInChildren<MeshRenderer>(true))
        {
            var name = renderer.name;
            if (name.StartsWith("Building |", StringComparison.Ordinal) || name.Contains(" roof") ||
                name.Contains(" closed door") || name.Contains(" window band") || name.StartsWith("Terrace door", StringComparison.Ordinal) ||
                name == "Shop awning" || name == "Recessed market counter" || name.StartsWith("Base beacon", StringComparison.Ordinal) || name == "Base roof monitor")
                renderer.enabled = false;
        }
        foreach (var renderer in room.GetComponentsInChildren<MeshRenderer>(true))
            if (renderer.GetComponent<TextMesh>() == null && !renderer.name.StartsWith("Sign", StringComparison.Ordinal)) renderer.enabled = false;

        Replace("NeighbourhoodArchitecture", generated, Vector3.zero);
        Replace("WorkshopShell", generated, new Vector3(-65, 0, 30));
        var seller = GameObject.Find("CitySeller").transform;
        // Keep the seller's item, signs, NPC, reaction, collider and anchors.
        foreach (var renderer in seller.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (renderer.name != "Visual") continue;
            var size = renderer.bounds.size;
            if (size.y > 1.8f || size.x > 4 && size.z > .5f) renderer.enabled = false;
        }
        Replace("SellerStall", seller, new Vector3(0, -seller.position.y, 0));
        var sellerNpc = seller.GetComponentInChildren<Interactables.CityTradeReaction>(true).transform;
        sellerNpc.localPosition = new Vector3(1.1f, .08f - seller.position.y, 1.05f);
        // The Blender worker's face points along local -Z.
        sellerNpc.localRotation = Quaternion.identity;
        foreach (var renderer in GameObject.Find("CityGreybox authored surroundings").GetComponentsInChildren<MeshRenderer>(true))
        {
            var size = renderer.bounds.size;
            if (size.y < .1f && size.x > 12 && size.z > 8) renderer.sharedMaterial = Material("Stone");
            if (renderer.transform.parent.name == "Площадка двора мастерской")
                renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/CityGreybox/Asphalt.mat");
            if (renderer.transform.parent.name == "Шов покрытия двора" || renderer.transform.parent.name == "Поперечный шов двора")
                renderer.enabled = false;
            if (Vector3.Distance(renderer.bounds.center, new Vector3(-65, 3.85f, 19.3f)) < .02f && size.x > 3 && size.x < 3.5f)
                renderer.enabled = false; // Replaced by the Blender entrance canopy.
        }
        foreach (var renderer in GameObject.Find("CityWorkshop").GetComponentsInChildren<MeshRenderer>(true))
        {
            if (renderer.name == "Timber") renderer.sharedMaterial = Material("Timber");
            if (renderer.name == "Steel") renderer.sharedMaterial = Material("Steel");
        }

        // World projection gives a consistent metre scale on the large original street meshes.
        Surface("Asphalt", "Asphalt", Color.white, 5);
        Surface("Pavement", "Concrete", new Color(.5f, .52f, .49f), 3);
        Surface("Neutral", "Masonry", new Color(.94f, .94f, .9f), 3);
        Surface("Residential", "Masonry", new Color(.9f, .86f, .77f), 3);
        Surface("Market", "Masonry", new Color(.88f, .83f, .72f), 3);
        Surface("Warehouses", "Metal", new Color(.55f, .61f, .63f), 4);
        Surface("Garages", "Metal", new Color(.62f, .65f, .61f), 4);
        Surface("Outskirts", "Masonry", new Color(.8f, .79f, .71f), 3);
        Surface("Roof", "Metal", new Color(.36f, .43f, .47f), 4);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Blender environment applied: 13 neighbourhood buildings, workshop shell and seller stall. Collision and gameplay anchors preserved.");
    }

    private static void Replace(string name, Transform parent, Vector3 position)
    {
        var previous = parent.Find(name);
        if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
        var path = Folder + "/Models/" + name + ".fbx";
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null) throw new InvalidOperationException("Export Blender asset first: " + path);
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.addCollider = false;
        importer.globalScale = 1;
        importer.SaveAndReimport();
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
        instance.name = name;
        instance.transform.localPosition = position;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;
        foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>())
        {
            // Blender adds .001 suffixes to identical mesh names in separate exports.
            var key = renderer.name.Replace("Building surface ", "").Replace("Roof ", "").Split('.')[0];
            if (name == "WorkshopShell" && key == "Glass")
            {
                renderer.sharedMaterial = ClearGlass();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            else renderer.sharedMaterial = Material(key);
        }
    }

    private static Material ClearGlass()
    {
        var path = Folder + "/Materials/WorkshopClearGlass.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetColor("_BaseColor", new Color(.76f, .88f, .91f, .12f));
        material.SetFloat("_Surface", 1);
        material.SetFloat("_Blend", 0);
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0);
        material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        material.SetFloat("_Smoothness", .2f);
        material.SetFloat("_SpecularHighlights", 0);
        material.SetFloat("_EnvironmentReflections", 0);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material Material(string name)
    {
        if (Materials.TryGetValue(name, out var found)) return found;
        var texture = name == "Plaster" || name == "Brick" ? "Masonry" : name == "Roof" || name == "Steel" ? "Metal" :
            name == "Stone" ? "Concrete" : name == "Timber" ? "Timber" : "Paint";
        var color = name == "Plaster" ? new Color(.8f, .8f, .75f) : name == "Brick" ? new Color(.72f, .64f, .54f) :
            name == "Roof" ? new Color(.37f, .46f, .5f) : name == "Steel" ? new Color(.38f, .42f, .42f) :
            name == "Stone" ? new Color(.62f, .64f, .59f) : name == "Blue" || name == "CanvasBlue" ? new Color(.22f, .38f, .42f) :
            name == "Glass" ? new Color(.22f, .34f, .38f) : name == "Timber" ? new Color(.9f, .9f, .86f) :
            name == "Cardboard" ? new Color(.64f, .51f, .33f) : new Color(.91f, .9f, .82f);
        var path = Folder + "/Materials/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("HustleScale/CityMatteSurface"));
            AssetDatabase.CreateAsset(material, path);
        }
        Configure(material, texture, color, name == "Plaster" || name == "Brick" ? 3 : 2);
        Materials.Add(name, material);
        return material;
    }

    private static void Surface(string name, string texture, Color color, float metres)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/CityGreybox/" + name + ".mat");
        if (material == null) throw new InvalidOperationException("Missing city material " + name);
        Configure(material, texture, color, metres);
    }

    private static void Configure(Material material, string texture, Color color, float metres)
    {
        material.shader = Shader.Find("HustleScale/CityMatteSurface");
        var path = texture == "Masonry" || texture == "Asphalt" || texture == "Timber" ? Folder + "/Textures/" + texture + ".png" :
            "Assets/Prefabs/WorkshopSurfaces/" + texture + ".png";
        var asset = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (asset == null) throw new InvalidOperationException("Missing texture " + path);
        material.SetTexture("_BaseMap", asset);
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Metres", metres);
        EditorUtility.SetDirty(material);
    }

    private static void ConfigureTexture(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Missing generated texture " + path);
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.mipmapEnabled = true;
        importer.maxTextureSize = 1024;
        importer.textureCompression = TextureImporterCompression.Compressed;
        importer.SaveAndReimport();
    }

    private static void EnsureFolder(string path)
    {
        var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
}
