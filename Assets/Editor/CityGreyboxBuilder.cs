using System;
using Player.Movement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// An authored greybox, deliberately scoped to one scene and one owned root.
public static class CityGreyboxBuilder
{
    public const string ScenePath = "Assets/Scenes/CityGreybox.unity";
    private const string RootName = "CityGreybox - generated";
    private const string MaterialFolder = "Assets/Materials/CityGreybox";
    private static Material _road, _walk, _wall, _roof, _grass, _accent, _lettering;
    private static Transform _root;

    [MenuItem("Tools/City Greybox/Build Authored City")]
    public static void Build()
    {
        if (EditorApplication.isPlaying || SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Stop Play Mode and save the active scene first.");
        var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null
            ? EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single)
            : EditorSceneManager.OpenScene(ScenePath);
        foreach (var go in scene.GetRootGameObjects())
            if (go.name == RootName) UnityEngine.Object.DestroyImmediate(go);
        _root = new GameObject(RootName).transform;
        if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
        if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder("Assets/Materials", "CityGreybox");
        _road = Mat("Asphalt", new Color(0.16f, 0.19f, 0.22f));
        _walk = Mat("Pavement", new Color(0.52f, 0.53f, 0.5f));
        _wall = Mat("Neutral", new Color(0.65f, 0.62f, 0.55f));
        _roof = Mat("Roof", new Color(0.24f, 0.29f, 0.32f));
        _grass = Mat("Park", new Color(0.28f, 0.39f, 0.27f));
        _accent = Mat("Wayfinding", new Color(0.95f, 0.61f, 0.16f));
        _lettering = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/Signs.mat");
        if (_lettering == null)
        {
            _lettering = new Material(Shader.Find("HustleScale/WorldSign"));
            AssetDatabase.CreateAsset(_lettering, MaterialFolder + "/Signs.mat");
        }
        _root.gameObject.AddComponent<Interactables.WorldSignFont>().Bind(Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"), _lettering);
        Box(_root, "Continuous ground 1040m", new Vector3(0, -1, 0), new Vector3(1040, 2, 1040), _walk);
        var roads = Group("Streets and sidewalks");
        foreach (var x in new[] { -480, -320, -160, 0, 160, 320, 480 })
            Street(roads, x, true, Mathf.Abs(x) <= 160 ? 16 : 8);
        foreach (var z in new[] { -480, -288, -96, 96, 288, 480 })
            Street(roads, z, false, Mathf.Abs(z) == 96 ? 16 : 8);
        var blocks = Group("30 authored blocks");
        var n = 0;
        foreach (var z in new[] { -384, -192, 0, 192, 384 })
            foreach (var x in new[] { -400, -240, -80, 80, 240, 400 })
            {
                var district = z > 96 ? (x < 0 ? "Residential" : "Warehouses") :
                    z < -96 ? (x < 0 ? "Garages" : "Outskirts") : "Market";
                var block = new GameObject("Block " + (++n).ToString("00") + " " + district).transform;
                block.SetParent(blocks);
                if (z < -96 && x > 0 && (z == -384 || x == 400))
                {
                    Park(block, x, z, x == 240 && z == -384);
                    continue;
                }
                if (z == 0 && Mathf.Abs(x) == 80) continue;
                var color = district == "Residential" ? new Color(0.63f, 0.57f, 0.49f) :
                    district == "Warehouses" ? new Color(0.42f, 0.52f, 0.56f) :
                    district == "Garages" ? new Color(0.53f, 0.47f, 0.38f) : new Color(0.64f, 0.61f, 0.5f);
                var mat = Mat(district, color);
                // Four volumes frame a usable cross-shaped courtyard, with four exits.
                for (var i = 0; i < 4; i++)
                {
                    var bx = x + (i % 2 == 0 ? -39 : 39);
                    var bz = z + (i < 2 ? -48 : 48);
                    var height = district == "Residential" ? 12 + (n + i) % 4 * 6 :
                        district == "Warehouses" ? 9 + (n + i) % 3 * 3 :
                        district == "Garages" ? 4 + i % 2 : 7 + i % 3 * 3;
                    var width = district == "Warehouses" ? 64 : district == "Garages" ? 66 :
                        district == "Residential" ? (i < 2 ? 64 : 42) : 44 + i % 2 * 10;
                    var depth = district == "Warehouses" ? 62 : district == "Garages" ? (i < 2 ? 26 : 44) :
                        district == "Residential" ? (i < 2 ? 38 : 62) : 38 + i % 2 * 14;
                    Building(block, district + " volume " + i, bx, bz, width, depth, height, mat);
                }
                Box(block, "Courtyard cross east-west", new Vector3(x, 0.015f, z), new Vector3(142, 0.03f, 6), _walk, false);
                Box(block, "Courtyard cross north-south", new Vector3(x, 0.016f, z), new Vector3(6, 0.03f, 174), _walk, false);
            }
        var center = Group("Workshop and trading placeholders");
        Room(center, "WORKSHOP / BASE", -65, 30, 24, 20);
        Room(center, "BATTERY SHOP / FUTURE TRADE", 65, 30, 22, 20);
        Room(center, "BUYBACK / FUTURE TRADE", -65, -45, 22, 20);
        Box(center, "Workshop open yard", new Vector3(-65, 0.02f, 7), new Vector3(42, 0.04f, 22), _walk, false);
        Box(center, "Workshop bench", new Vector3(-72, 0.5f, 34), new Vector3(4, 1, 1), _roof);
        Market(center);
        Trade(center, "SUPPLIER", 240, 192);
        Trade(center, "RESIDENTIAL BUYER", -240, 192);
        Trade(center, "GARAGE BUYER", -240, -192);
        Trade(center, "OUTSKIRTS BUYER", 240, -192);
        Trade(center, "MARKET BUYER", -400, 0);
        Landmarks();
        Boundaries();
        Sign(_root, "CITY / NORTH +Z", new Vector3(-65, 4, 5), 0, 0.4f);
        var sun = new GameObject("City sun", typeof(Light));
        sun.transform.SetParent(_root);
        sun.transform.rotation = Quaternion.Euler(48, -32, 0);
        sun.GetComponent<Light>().type = LightType.Directional;
        sun.GetComponent<Light>().intensity = 1.5f;
        sun.GetComponent<Light>().shadows = LightShadows.Soft;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.64f, 0.69f, 0.76f);
        RenderSettings.fog = false;
        var player = new GameObject("City Player", typeof(CharacterController), typeof(Player.PlayerInput), typeof(PlayerController));
        player.transform.SetParent(_root);
        player.transform.position = new Vector3(-65, 0.08f, 25);
        player.transform.rotation = Quaternion.Euler(0, 180, 0);
        var body = player.GetComponent<CharacterController>();
        body.height = 1.8f;
        body.radius = 0.3f;
        body.center = Vector3.up * 0.9f;
        body.stepOffset = 0.3f;
        body.skinWidth = 0.03f;
        var camera = new GameObject("City Main Camera", typeof(Camera), typeof(AudioListener));
        camera.tag = "MainCamera";
        camera.transform.SetParent(player.transform);
        camera.transform.localPosition = Vector3.up * 1.65f;
        camera.transform.localRotation = Quaternion.identity;
        camera.GetComponent<Camera>().farClipPlane = 1600;
        camera.GetComponent<Camera>().fieldOfView = 70;
        camera.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
        camera.GetComponent<Camera>().backgroundColor = new Color(0.55f, 0.68f, 0.77f);
        var serialized = new SerializedObject(player.GetComponent<PlayerController>());
        serialized.FindProperty("cameraTransform").objectReferenceValue = camera.transform;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        player.AddComponent<Player.CityGreyboxWalk>();
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("CITY BUILD: 1000 x 1000m, 30 blocks, five districts; saved " + ScenePath);
    }

    private static Transform Group(string name)
    {
        var t = new GameObject(name).transform;
        t.SetParent(_root);
        return t;
    }

    private static Material Mat(string name, Color color)
    {
        var path = MaterialFolder + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material; // Preserve manual material edits.
        material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.color = color;
        material.SetFloat("_Smoothness", 0.12f);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static GameObject Box(Transform parent, string name, Vector3 position, Vector3 size, Material mat, bool solid = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.position = position;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        if (!solid) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        go.isStatic = true;
        return go;
    }

    private static void Street(Transform parent, int coordinate, bool vertical, float width)
    {
        Box(parent, (vertical ? "N-S " : "E-W ") + coordinate, vertical ? new Vector3(coordinate, 0.025f, 0) : new Vector3(0, 0.026f, coordinate),
            vertical ? new Vector3(width, 0.05f, 976) : new Vector3(976, 0.05f, width), _road, false);
        foreach (var side in new[] { -1, 1 })
        {
            var offset = coordinate + side * (width / 2 + 1.5f);
            Box(parent, "Flush sidewalk", vertical ? new Vector3(offset, 0.03f, 0) : new Vector3(0, 0.031f, offset),
                vertical ? new Vector3(3, 0.06f, 976) : new Vector3(976, 0.06f, 3), _walk, false);
        }
        for (var s = -450; s <= 450; s += 40)
            Box(parent, "Street dashed line", vertical ? new Vector3(coordinate, 0.055f, s) : new Vector3(s, 0.056f, coordinate),
                vertical ? new Vector3(0.18f, 0.01f, 8) : new Vector3(8, 0.01f, 0.18f), _accent, false);
    }

    private static void Building(Transform parent, string name, float x, float z, float w, float d, float h, Material mat)
    {
        var go = Box(parent, name, new Vector3(x, h / 2, z), new Vector3(w, h, d), mat);
        Box(parent, name + " roof", new Vector3(x, h + 0.2f, z), new Vector3(w + 1, 0.4f, d + 1), _roof);
        // Closed door recesses make the street-facing scale readable.
        Box(parent, name + " closed door", new Vector3(x, 1.4f, z - d / 2 - 0.025f), new Vector3(2.4f, 2.8f, 0.04f), _roof, false);
        if (h > 8)
            for (var floor = 5; floor < h - 1; floor += 5)
                Box(parent, name + " window band", new Vector3(x, floor, z - d / 2 - 0.03f), new Vector3(w - 6, 1.1f, 0.04f), _roof, false);
        go.name = "Building | " + name;
    }

    private static void Room(Transform parent, string name, float x, float z, float w, float d)
    {
        var room = new GameObject("Interior | " + name).transform;
        room.SetParent(parent);
        Box(room, "Back wall", new Vector3(x, 2, z + d / 2), new Vector3(w, 4, 0.4f), _wall);
        foreach (var side in new[] { -1, 1 })
        {
            Box(room, "Side wall", new Vector3(x + side * w / 2, 2, z), new Vector3(0.4f, 4, d), _wall);
            var wing = (w - 2.4f) / 2;
            Box(room, "Door side", new Vector3(x + side * (1.2f + wing / 2), 2, z - d / 2), new Vector3(wing, 4, 0.4f), _wall);
        }
        Box(room, "Door lintel 2.8m clear", new Vector3(x, 3.4f, z - d / 2), new Vector3(2.4f, 1.2f, 0.4f), _wall);
        Box(room, "Roof", new Vector3(x, 4.2f, z), new Vector3(w + 0.4f, 0.4f, d + 0.4f), _roof);
        Sign(room, name, new Vector3(x, 3.35f, z - d / 2 - 0.24f), 0, 0.23f);
        Sign(room, name == "WORKSHOP / BASE" ? "BASE / NO ACTIVE SESSION" : "PLACEHOLDER / NO TRADING", new Vector3(x, 2.5f, z + d / 2 - 0.25f), 180, 0.18f);
    }

    private static void Trade(Transform parent, string name, float x, float z)
    {
        Box(parent, name + " future counter", new Vector3(x + 8, 0.6f, z + 8), new Vector3(5, 1.2f, 2), _accent);
        Sign(parent, name + "\nFUTURE TRADE", new Vector3(x + 8, 3, z + 6.8f), 0, 0.25f);
    }

    private static void Market(Transform parent)
    {
        Box(parent, "Market canopy landmark", new Vector3(80, 8, -45), new Vector3(55, 1, 36), _accent);
        foreach (var x in new[] { 55, 105 })
            foreach (var z in new[] { -60, -30 })
                Box(parent, "Market canopy support", new Vector3(x, 4, z), new Vector3(0.7f, 8, 0.7f), _roof);
        Trade(parent, "MARKET", 80, -45);
    }

    private static void Park(Transform parent, int x, int z, bool landmark)
    {
        Box(parent, landmark ? "Park landmark" : "Open recreation ground", new Vector3(x, 0.01f, z), new Vector3(138, 0.02f, 168), _grass, false);
        Box(parent, "Park walk E-W", new Vector3(x, 0.035f, z), new Vector3(142, 0.03f, 8), _walk, false);
        Box(parent, "Park walk N-S", new Vector3(x, 0.036f, z), new Vector3(8, 0.03f, 174), _walk, false);
        for (var i = 0; i < 8; i++)
        {
            var tx = x + (i % 2 == 0 ? -42 : 42);
            var tz = z - 65 + i / 2 * 42;
            Box(parent, "Tree trunk greybox", new Vector3(tx, 2, tz), new Vector3(1.4f, 4, 1.4f), _wall);
            var crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            crown.name = "Tree crown greybox";
            crown.transform.SetParent(parent);
            crown.transform.position = new Vector3(tx, 6, tz);
            crown.transform.localScale = new Vector3(9, 7, 9);
            crown.GetComponent<Renderer>().sharedMaterial = _grass;
            crown.isStatic = true;
        }
        var diagonal = Box(parent, "Diagonal park shortcut", new Vector3(x, 0.04f, z), new Vector3(4, 0.02f, 160), _walk, false);
        diagonal.transform.rotation = Quaternion.Euler(0, 32, 0);
        if (landmark) Sign(parent, "PARK / OPEN COMMON", new Vector3(x, 4, z + 15), 0, 0.6f);
    }

    private static void Landmarks()
    {
        var t = Group("Landmarks");
        foreach (var dx in new[] { -5, 5 })
            foreach (var dz in new[] { -5, 5 })
                Box(t, "Water tower leg", new Vector3(-240 + dx, 17, 384 + dz), new Vector3(1, 34, 1), _roof);
        var tank = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        tank.name = "Water tower tank landmark";
        tank.transform.SetParent(t);
        tank.transform.position = new Vector3(-240, 36, 384);
        tank.transform.localScale = new Vector3(20, 5, 20);
        tank.GetComponent<Renderer>().sharedMaterial = _accent;
        var chimney = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        chimney.name = "Chimney landmark 62m";
        chimney.transform.SetParent(t);
        chimney.transform.position = new Vector3(400, 31, 384);
        chimney.transform.localScale = new Vector3(8, 31, 8);
        chimney.GetComponent<Renderer>().sharedMaterial = _roof;
        // This crosses the courtyard only east of its unobstructed north-south route.
        Building(t, "Long supplier warehouse landmark", 278, 192, 48, 126, 16, _wall);
        foreach (var data in new[] { new Vector3(-320, 0, 96), new Vector3(0, 0, 96), new Vector3(160, 0, 288), new Vector3(-160, 0, -288), new Vector3(160, 0, -288) })
            Sign(t, "N: HOMES / WAREHOUSES\nS: GARAGES / PARK\nCENTRE: MARKET / BASE", data + new Vector3(13, 4, 13), 0, 0.28f);
    }

    private static void Boundaries()
    {
        var t = Group("Visible continuous boundary");
        foreach (var side in new[] { -1, 1 })
        {
            Box(t, "Boundary embankment", new Vector3(side * 498, 2.5f, 0), new Vector3(4, 5, 1000), _roof);
            Box(t, "Boundary industrial wall", new Vector3(0, 2.5f, side * 498), new Vector3(1000, 5, 4), _roof);
        }
    }

    private static void Sign(Transform parent, string text, Vector3 position, float yaw, float size)
    {
        var go = new GameObject("Sign | " + text.Replace('\n', ' '), typeof(TextMesh));
        go.transform.SetParent(parent);
        go.transform.position = position;
        go.transform.rotation = Quaternion.Euler(0, yaw, 0);
        var tm = go.GetComponent<TextMesh>();
        tm.text = text;
        tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        tm.fontSize = 64;
        tm.characterSize = size;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = new Color(0.08f, 0.11f, 0.14f);
        go.GetComponent<Renderer>().sharedMaterial = _lettering;
    }
}
