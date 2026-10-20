using System;
using System.Linq;
using Interactables;
using Player;
using Player.Movement;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class DevelopmentSandboxBuilder
{
    public const string ScenePath = "Assets/Scenes/DevelopmentSandbox.unity";
    private const string VehiclePath = "Assets/Prefabs/DevelopmentSandbox/UtilityVehicle.prefab";
    private static Material floor, dark, accent, lettering;
    private static Transform root;

    [MenuItem("Tools/Development Sandbox/Open Playground")]
    public static void Open()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(ScenePath);
    }

    [MenuItem("Tools/Development Sandbox/Create Playground Once")]
    public static void Create()
    {
        if (EditorApplication.isPlaying || SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Stop Play Mode and save the current scene first.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            throw new InvalidOperationException("Playground exists. Open and edit it; creation never overwrites authored work.");

        // Copy the complete working scenario with Unity serialization, preserving its internal references.
        var scene = EditorSceneManager.OpenScene(FirstDealTradeBuilder.ScenePath);
        if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Cannot save playground.");
        var trade = new GameObject("01 - LIVE FIRST DEAL - workshop, seller and buyers").transform;
        foreach (var go in scene.GetRootGameObjects())
            if (go != trade.gameObject) go.transform.SetParent(trade, true);
        // Open the existing yard to the playground without changing the source scenario.
        UnityEngine.Object.DestroyImmediate(trade.Find("South wall").gameObject);

        floor = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/CityGreybox/Pavement.mat");
        dark = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/CityGreybox/Asphalt.mat");
        accent = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/CityGreybox/Wayfinding.mat");
        lettering = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/CityGreybox/Signs.mat");
        root = new GameObject("Development Sandbox - authored zones").transform;
        root.gameObject.AddComponent<WorldSignFont>().Bind(Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"), lettering);
        var ground = Group("00 - Site, grid and wayfinding");
        Box(ground, "Continuous ground 260 x 220m", new Vector3(0, -0.5f, 0), new Vector3(260, 1, 220), floor);
        for (var x = -120; x <= 120; x += 10)
            Box(ground, "10m grid X " + x, new Vector3(x, 0.008f, 0), new Vector3(0.025f, 0.01f, 218), dark, false);
        for (var z = -100; z <= 100; z += 10)
            Box(ground, "10m grid Z " + z, new Vector3(0, 0.008f, z), new Vector3(258, 0.01f, 0.025f), dark, false);
        Box(ground, "West boundary", new Vector3(-130, 1, 0), new Vector3(0.5f, 2, 220), dark);
        Box(ground, "East boundary", new Vector3(130, 1, 0), new Vector3(0.5f, 2, 220), dark);
        Box(ground, "North boundary", new Vector3(0, 1, 110), new Vector3(260, 2, 0.5f), dark);
        Box(ground, "South boundary", new Vector3(0, 1, -110), new Vector3(260, 2, 0.5f), dark);
        Sign(ground, "ПОЛИГОН РАЗРАБОТКИ\n260 × 220 м / сетка 10 м\nF2 — переходы, часы, сброс", new Vector3(0, 3, -15), 0.23f);
        Sign(ground, "01  ТОРГОВЛЯ И МАСТЕРСКАЯ\nРабочий FIRST DEAL / E — взаимодействие", new Vector3(0, 4, 0), 0.18f);
        Track();
        Movement();
        Lighting();
        Gallery();
        var staging = Group("06 - Free staging and placement surfaces");
        Pad(staging, new Vector3(0, 0, -67), new Vector2(48, 38));
        Sign(staging, "06  СВОБОДНАЯ СБОРКА\nПереноска, размещение, новые прототипы", new Vector3(0, 3, -48), 0.2f);
        for (var i = 0; i < 3; i++)
        {
            var height = 0.45f + i * 0.3f;
            Box(staging, "Placement surface " + height.ToString("F2") + "m", new Vector3(-12 + i * 12, height / 2, -65), new Vector3(4, height, 2), floor);
        }
        var environment = Group("07 - Streetscape and scale reference");
        Pad(environment, new Vector3(68, 0, 67), new Vector2(68, 48));
        Sign(environment, "07  ОКРУЖЕНИЕ / МАСШТАБ\nУлица, бордюры, фасады и проходы", new Vector3(68, 3, 43), 0.2f);
        for (var i = 0; i < 4; i++)
        {
            Box(environment, "Facade " + i, new Vector3(44 + i * 16, 4 + i * 2, 83), new Vector3(12, 8 + i * 4, 8), floor);
            Box(environment, "Sidewalk " + i, new Vector3(44 + i * 16, 0.09f, 74), new Vector3(14, 0.18f, 4), floor);
            Box(environment, "Streetlight post " + i, new Vector3(44 + i * 16, 2.5f, 69), new Vector3(0.15f, 5, 0.15f), dark);
            Box(environment, "Streetlight head " + i, new Vector3(44 + i * 16, 5, 68.6f), new Vector3(0.7f, 0.12f, 1), accent);
            var lamp = new GameObject("Streetlight beam " + i).AddComponent<Light>();
            lamp.transform.SetParent(environment);
            lamp.transform.SetPositionAndRotation(new Vector3(44 + i * 16, 4.8f, 68.6f), Quaternion.Euler(90, 0, 0));
            lamp.type = LightType.Spot;
            lamp.intensity = 3;
            lamp.range = 12;
            lamp.spotAngle = 95;
            lamp.color = new Color(1, 0.82f, 0.6f);
        }

        var player = trade.GetComponentInChildren<PlayerController>(true);
        var vehicle = ImportVehicle(scene, player);
        var controller = root.gameObject.AddComponent<DevelopmentSandbox>();
        Ref(controller, "player", player);
        Ref(controller, "vehicle", vehicle);
        Ref(controller, "deal", trade.GetComponentInChildren<FirstDealView>(true));
        Ref(controller, "workshop", trade.GetComponentInChildren<WorkshopView>(true));
        var destinations = Group("Development teleport destinations");
        var names = new[] { "01  Мастерская и торговля", "02  Машина и автодром", "03  Движение и коллизии", "04  Световой коридор", "05  Каталог объектов", "06  Свободная сборка", "07  Улица и масштаб" };
        var positions = new[] { new Vector3(-4, 0.05f, 4.5f), new Vector3(39, 0.05f, -36), new Vector3(-65, 0.05f, -35), new Vector3(-65, 0.05f, 41), new Vector3(-16, 0.05f, 42), new Vector3(0, 0.05f, -51), new Vector3(68, 0.05f, 47) };
        var serialized = new SerializedObject(controller);
        var array = serialized.FindProperty("destinations");
        array.arraySize = names.Length;
        for (var i = 0; i < names.Length; i++)
        {
            var point = new GameObject(names[i]).transform;
            point.SetParent(destinations);
            point.position = positions[i];
            array.GetArrayElementAtIndex(i).objectReferenceValue = point;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
        // This editor-first scene reloads directly and needs no changes to the owner's build list.
        var restart = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include)
            .First(b => b.name == "Restart shift");
        for (var i = restart.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            UnityEventTools.RemovePersistentListener(restart.onClick, i);
        UnityEventTools.AddPersistentListener(restart.onClick, controller.RestartScene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Development Sandbox created: 260 x 220m, seven zones, live FIRST DEAL, linked prefab catalogue and utility vehicle.");
    }

    private static void Track()
    {
        var zone = Group("02 - Vehicle circuit, braking and slalom");
        Pad(zone, new Vector3(74, 0, -31), new Vector2(92, 126));
        Sign(zone, "02  АВТОДРОМ\nE у двери / WASD / Space — тормоз / V — камера", new Vector3(70, 3, -92), 0.2f);
        // A 12m-wide rectangular circuit; the centre stays available for manoeuvres.
        foreach (var x in new[] { 37f, 111f })
            Box(zone, "Circuit edge", new Vector3(x, 0.035f, -31), new Vector3(0.15f, 0.02f, 106), accent, false);
        foreach (var z in new[] { -84f, 22f })
            Box(zone, "Circuit edge", new Vector3(74, 0.035f, z), new Vector3(74, 0.02f, 0.15f), accent, false);
        for (var i = 0; i < 6; i++)
        {
            var cone = Box(zone, "Slalom marker " + i, new Vector3(67 + (i % 2) * 10, 0.4f, -65 + i * 14), new Vector3(0.45f, 0.8f, 0.45f), accent);
            cone.transform.rotation = Quaternion.Euler(0, 45, 0);
        }
        for (var i = 0; i < 6; i++)
        {
            Box(zone, "Braking ruler " + i * 10 + "m", new Vector3(103, 0.035f, -70 + i * 10), new Vector3(10, 0.02f, 0.12f), floor, false);
            Sign(zone, i * 10 + " м", new Vector3(96, 0.5f, -70 + i * 10), 0.1f);
        }
        Box(zone, "Loading dock", new Vector3(50, 0.4f, -5), new Vector3(10, 0.8f, 6), floor);
        var ramp = Box(zone, "Loading ramp", new Vector3(50, 0.2f, -11), new Vector3(4, 0.2f, 6), floor);
        ramp.transform.rotation = Quaternion.Euler(-7.5f, 0, 0);
    }

    private static void Movement()
    {
        var zone = Group("03 - Walking, slopes, steps and narrow gaps");
        Pad(zone, new Vector3(-71, 0, -30), new Vector2(82, 90));
        Sign(zone, "03  ДВИЖЕНИЕ И КОЛЛИЗИИ\nСтупени / уклоны / узкие проходы / ящик", new Vector3(-70, 3, -74), 0.2f);
        var heights = new[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.6f };
        for (var i = 0; i < heights.Length; i++)
        {
            Box(zone, "Step " + heights[i] + "m", new Vector3(-100 + i * 9, heights[i] / 2, -48), new Vector3(6, heights[i], 5), floor);
            Sign(zone, heights[i].ToString("F1") + " м", new Vector3(-100 + i * 9, 1, -52), 0.12f);
        }
        for (var i = 0; i < 3; i++)
        {
            var angle = 15 + i * 15;
            var ramp = Box(zone, "Slope " + angle + " degrees", new Vector3(-98 + i * 16, Mathf.Sin(angle * Mathf.Deg2Rad) * 5, -20), new Vector3(6, 0.3f, 10), floor);
            ramp.transform.rotation = Quaternion.Euler(-angle, 0, 0);
            Sign(zone, angle + "°", new Vector3(-98 + i * 16, 1, -27), 0.14f);
        }
        var gaps = new[] { 0.7f, 1f, 1.5f, 2f };
        for (var i = 0; i < gaps.Length; i++)
        {
            var x = -101 + i * 18;
            foreach (var side in new[] { -1, 1 })
                Box(zone, "Gap " + gaps[i] + "m wall", new Vector3(x + side * (gaps[i] / 2 + 0.15f), 1.25f, 3), new Vector3(0.3f, 2.5f, 6), dark);
            Sign(zone, gaps[i].ToString("F1") + " м", new Vector3(x, 2.9f, -0.2f), 0.12f);
        }
    }

    private static void Lighting()
    {
        var zone = Group("04 - Covered lighting and flashlight corridor");
        Pad(zone, new Vector3(-70, 0, 65), new Vector2(80, 56));
        Sign(zone, "04  СВЕТ И ФОНАРИ\nВозьми рабочий фонарь в мастерской / F — свет", new Vector3(-70, 3, 38), 0.18f);
        Box(zone, "Dark corridor roof", new Vector3(-70, 3.2f, 70), new Vector3(16, 0.3f, 36), dark);
        Box(zone, "Dark corridor west", new Vector3(-78, 1.6f, 70), new Vector3(0.3f, 3.2f, 36), dark);
        Box(zone, "Dark corridor east", new Vector3(-62, 1.6f, 70), new Vector3(0.3f, 3.2f, 36), dark);
        Box(zone, "Dark corridor back", new Vector3(-70, 1.6f, 88), new Vector3(16, 3.2f, 0.3f), dark);
        for (var i = 0; i < 3; i++)
        {
            Box(zone, "Light baffle " + i, new Vector3(-70 + (i % 2 == 0 ? -3 : 3), 1.5f, 58 + i * 9), new Vector3(9, 3, 0.3f), dark);
            Sign(zone, "Дистанция " + (i + 1) * 9 + " м", new Vector3(-70, 2, 57.8f + i * 9), 0.12f);
        }
        var light = new GameObject("Warm reference light").AddComponent<Light>();
        light.transform.SetParent(zone);
        light.transform.position = new Vector3(-90, 3, 65);
        light.type = LightType.Point;
        light.color = new Color(1, 0.72f, 0.4f);
        light.intensity = 4;
        light.range = 12;
        Box(zone, "Neutral light target", new Vector3(-90, 1.5f, 69), new Vector3(6, 3, 0.2f), floor);
    }

    private static void Gallery()
    {
        var zone = Group("05 - Linked prefab catalogue - display only");
        Pad(zone, new Vector3(0, 0, 70), new Vector2(54, 58));
        Sign(zone, "05  КАТАЛОГ ПРЕФАБОВ\nОбразцы: исходные prefab-связи сохранены\nРабочие действия — в зоне 01", new Vector3(0, 3, 40), 0.18f);
        var paths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" })
            .Select(AssetDatabase.GUIDToAssetPath).Where(p => !p.Contains("/DevelopmentSandbox/")).OrderBy(p => p).ToArray();
        var worldIndex = 0;
        foreach (var path in paths)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var stand = new GameObject("Source | " + path).transform;
            stand.SetParent(zone);
            var index = worldIndex++;
            stand.position = new Vector3(-18 + index % 4 * 12, 0, 50 + index / 4 * 13);
            Box(stand, "Display pad", stand.position + Vector3.up * 0.1f, new Vector3(10, 0.2f, 10), floor);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source, stand);
            instance.transform.localPosition = Vector3.up * 0.25f;
            instance.transform.localRotation = Quaternion.identity;
            // Legacy inventory/UI samples are visible references, not a second live economy.
            foreach (var behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
            foreach (var camera in instance.GetComponentsInChildren<Camera>(true))
            {
                camera.enabled = false;
                camera.gameObject.tag = "Untagged";
                camera.gameObject.name = "Display camera - disabled";
            }
            foreach (var listener in instance.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
            foreach (var audio in instance.GetComponentsInChildren<AudioSource>(true)) audio.enabled = false;
            foreach (var body in instance.GetComponentsInChildren<Rigidbody>(true)) body.isKinematic = true;
            foreach (var part in instance.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = 2;
            if (instance.transform is RectTransform)
            {
                var canvas = new GameObject("UI sample canvas", typeof(Canvas)).GetComponent<Canvas>();
                canvas.transform.SetParent(stand, false);
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.GetComponent<RectTransform>().sizeDelta = new Vector2(300, 300);
                canvas.transform.localPosition = new Vector3(0, 2, 0);
                canvas.transform.localScale = Vector3.one * 0.01f;
                instance.transform.SetParent(canvas.transform, false);
            }
            Sign(stand, source.name + "\nОБРАЗЕЦ", stand.position + new Vector3(0, 1.2f, -4.8f), 0.13f);
        }
    }

    private static CityUtilityVehicle ImportVehicle(Scene target, PlayerController player)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs/DevelopmentSandbox")) AssetDatabase.CreateFolder("Assets/Prefabs", "DevelopmentSandbox");
        if (AssetDatabase.LoadAssetAtPath<GameObject>(VehiclePath) == null)
        {
            var city = EditorSceneManager.OpenScene(CityGreyboxBuilder.ScenePath, OpenSceneMode.Additive);
            try
            {
                var source = city.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<CityUtilityVehicle>(true)).Single();
                var clone = UnityEngine.Object.Instantiate(source.gameObject);
                SceneManager.MoveGameObjectToScene(clone, target);
                Ref(clone.GetComponent<CityUtilityVehicle>(), "walker", null);
                Ref(clone.GetComponent<CityUtilityVehicle>(), "walkOverlay", null);
                Ref(clone.GetComponent<CityUtilityVehicle>(), "walkingCamera", null);
                PrefabUtility.SaveAsPrefabAsset(clone, VehiclePath);
                UnityEngine.Object.DestroyImmediate(clone);
            }
            finally { EditorSceneManager.CloseScene(city, true); }
        }
        SceneManager.SetActiveScene(target);
        var car = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(VehiclePath), root);
        car.transform.SetPositionAndRotation(new Vector3(42, 0.4f, -36), Quaternion.identity);
        var vehicle = car.GetComponent<CityUtilityVehicle>();
        Ref(vehicle, "walker", player);
        Ref(vehicle, "walkingCamera", player.GetComponentInChildren<Camera>());
        return vehicle;
    }

    private static Transform Group(string name)
    {
        var group = new GameObject(name).transform;
        group.SetParent(root, false);
        return group;
    }

    private static void Pad(Transform parent, Vector3 centre, Vector2 size) =>
        Box(parent, "Zone paving", centre + Vector3.up * 0.02f, new Vector3(size.x, 0.02f, size.y), dark, false);

    private static GameObject Box(Transform parent, string name, Vector3 position, Vector3 size, Material material, bool solid = true)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.position = position;
        box.transform.localScale = size;
        box.GetComponent<Renderer>().sharedMaterial = material;
        if (!solid) UnityEngine.Object.DestroyImmediate(box.GetComponent<Collider>());
        return box;
    }

    private static void Sign(Transform parent, string text, Vector3 position, float size)
    {
        var label = new GameObject("Sign | " + text.Split('\n')[0]).AddComponent<TextMesh>();
        label.transform.SetParent(parent, false);
        label.transform.position = position;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 64;
        label.characterSize = size;
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.color = new Color(1, 0.85f, 0.5f);
        label.text = text;
        label.GetComponent<MeshRenderer>().sharedMaterial = lettering;
    }

    private static void Ref(UnityEngine.Object target, string name, UnityEngine.Object value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(name).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
