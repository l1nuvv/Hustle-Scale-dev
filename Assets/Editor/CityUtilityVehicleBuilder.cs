using System;
using Player;
using Player.Movement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CityUtilityVehicleBuilder
{
    private static Material paint, trim, rubber, signal, lettering;

    [MenuItem("Tools/City Greybox/Add Workshop Utility Vehicle")]
    public static void EnsureVehicle()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.path != CityGreyboxBuilder.ScenePath)
            throw new InvalidOperationException("Open CityGreybox in Edit Mode.");
        var root = GameObject.Find("CityGreybox - generated").transform;
        if (root.Find("Workshop electric utility") != null) return;
        const string folder = "Assets/Materials/CityGreybox/";
        paint = AssetDatabase.LoadAssetAtPath<Material>(folder + "Wayfinding.mat");
        trim = AssetDatabase.LoadAssetAtPath<Material>(folder + "Neutral.mat");
        lettering = AssetDatabase.LoadAssetAtPath<Material>(folder + "Signs.mat");
        rubber = Material(folder + "VehicleRubber.mat", new Color(0.055f, 0.065f, 0.075f));
        signal = Material(folder + "VehicleTailLight.mat", new Color(0.7f, 0.08f, 0.035f));
        var car = new GameObject("Workshop electric utility", typeof(Rigidbody));
        car.transform.SetParent(root);
        car.transform.SetPositionAndRotation(CityUtilityVehicle.WorkshopParkingPosition, Quaternion.Euler(0, 270, 0));
        var body = car.GetComponent<Rigidbody>();
        body.mass = 650;
        body.centerOfMass = new Vector3(0, 0.05f, -0.1f);
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.angularDamping = 1.5f;
        body.linearDamping = 0.03f;
        body.solverIterations = 10;
        var t = car.transform;
        Part(t, "Steel chassis", new Vector3(0, 0.12f, 0), new Vector3(1.5f, 0.25f, 3.4f), rubber, true);
        Part(t, "Sculpted short bonnet", new Vector3(0, 0.48f, 1.08f), new Vector3(1.5f, 0.46f, 1.2f), paint, true);
        Part(t, "Front bumper", new Vector3(0, 0.18f, 1.76f), new Vector3(1.65f, 0.19f, 0.14f), rubber);
        Part(t, "Rear bumper", new Vector3(0, 0.18f, -1.76f), new Vector3(1.65f, 0.19f, 0.14f), rubber);
        Part(t, "Front grille", new Vector3(0, 0.45f, 1.69f), new Vector3(0.65f, 0.16f, 0.035f), rubber);
        Part(t, "Cab roof", new Vector3(0, 1.75f, -0.15f), new Vector3(1.62f, 0.12f, 1.5f), paint, true);
        Part(t, "Cab rear bulkhead", new Vector3(0, 0.9f, -0.78f), new Vector3(1.5f, 1.35f, 0.09f), paint, true);
        Part(t, "Rear window inset", new Vector3(0, 1.32f, -0.725f), new Vector3(1.1f, 0.35f, 0.025f), rubber);
        Part(t, "Dashboard", new Vector3(0, 0.84f, 0.53f), new Vector3(1.42f, 0.18f, 0.45f), rubber);
        foreach (var side in new[] { -1, 1 })
        {
            Part(t, "Windscreen pillar", new Vector3(side * 0.73f, 1.2f, 0.49f), new Vector3(0.07f, 1.05f, 0.07f), trim);
            Part(t, "Door sill", new Vector3(side * 0.76f, 0.48f, -0.1f), new Vector3(0.1f, 0.43f, 1.13f), paint);
            Part(t, "Door handle", new Vector3(side * 0.82f, 0.77f, -0.22f), new Vector3(0.035f, 0.05f, 0.2f), rubber);
            Part(t, "Bed side", new Vector3(side * 0.73f, 0.52f, -1.24f), new Vector3(0.09f, 0.55f, 0.95f), paint, true);
            Part(t, "Headlamp", new Vector3(side * 0.55f, 0.5f, 1.7f), new Vector3(0.25f, 0.2f, 0.04f), trim);
            Part(t, "Tail light", new Vector3(side * 0.62f, 0.45f, -1.72f), new Vector3(0.18f, 0.15f, 0.05f), signal);
            Part(t, "Seat cushion", new Vector3(side * 0.38f, 0.48f, -0.32f), new Vector3(0.55f, 0.16f, 0.6f), rubber);
            Part(t, "Seat back", new Vector3(side * 0.38f, 0.82f, -0.59f), new Vector3(0.55f, 0.6f, 0.12f), rubber);
            Part(t, "Mirror arm", new Vector3(side * 0.89f, 1.05f, 0.38f), new Vector3(0.25f, 0.035f, 0.035f), rubber);
            Part(t, "Mirror housing", new Vector3(side * 1.01f, 1.09f, 0.38f), new Vector3(0.08f, 0.18f, 0.15f), rubber);
        }
        Part(t, "Bed floor", new Vector3(0, 0.27f, -1.23f), new Vector3(1.4f, 0.08f, 0.95f), rubber);
        Part(t, "Tailgate", new Vector3(0, 0.52f, -1.68f), new Vector3(1.5f, 0.55f, 0.09f), paint, true);
        var wheelColliders = new WheelCollider[4];
        var visuals = new Transform[4];
        for (var i = 0; i < 4; i++)
        {
            var position = new Vector3(i % 2 == 0 ? -0.81f : 0.81f, 0.05f, i < 2 ? 1.1f : -1.1f);
            var go = new GameObject(i < 2 ? "Front wheel suspension" : "Rear wheel suspension", typeof(WheelCollider));
            go.transform.SetParent(t, false);
            go.transform.localPosition = position;
            var wheel = go.GetComponent<WheelCollider>();
            wheel.radius = 0.31f;
            wheel.mass = 18;
            wheel.suspensionDistance = 0.18f;
            wheel.forceAppPointDistance = 0.15f;
            wheel.suspensionSpring = new JointSpring { spring = 32000, damper = 4200, targetPosition = 0.5f };
            var sideways = wheel.sidewaysFriction;
            sideways.stiffness = 1.15f;
            wheel.sidewaysFriction = sideways;
            var tyre = Part(t, "Tyre", position, new Vector3(0.62f, 0.12f, 0.62f), rubber, false, PrimitiveType.Cylinder);
            tyre.localRotation = Quaternion.Euler(0, 0, 90);
            var hub = Part(tyre, "Wheel hub", Vector3.zero, new Vector3(0.6f, 1.04f, 0.6f), trim, false, PrimitiveType.Cylinder);
            Part(hub, "Hub spoke", Vector3.zero, new Vector3(0.14f, 1.03f, 1.3f), rubber);
            wheelColliders[i] = wheel;
            visuals[i] = tyre;
        }
        var steering = new GameObject("Steering wheel").transform;
        steering.SetParent(t, false);
        steering.localPosition = new Vector3(-0.38f, 0.98f, 0.3f);
        for (var i = 0; i < 20; i++)
        {
            var angle = i * Mathf.PI * 2 / 20;
            var segment = Part(steering, "Steering rim", new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * 0.19f,
                new Vector3(0.068f, 0.03f, 0.035f), rubber);
            segment.localRotation = Quaternion.Euler(0, 0, i * 18 + 90);
        }
        Part(steering, "Steering spokes", Vector3.zero, new Vector3(0.32f, 0.035f, 0.035f), trim);
        Part(steering, "Steering centre", Vector3.zero, new Vector3(0.09f, 0.09f, 0.06f), rubber);
        var speed = Label(t, "00 km/h", new Vector3(-0.38f, 0.96f, 0.61f), 0.08f);
        speed.transform.localScale = Vector3.one * 0.12f;
        Part(t, "Number plate backing", new Vector3(0, 0.5f, -1.747f), new Vector3(0.95f, 0.22f, 0.025f), rubber);
        var plate = Label(t, "WORKSHOP 01", new Vector3(0, 0.5f, -1.77f), 0.07f).transform;
        plate.localScale = Vector3.one * 0.25f;
        var camera = new GameObject("Utility driver camera", typeof(Camera), typeof(AudioListener));
        camera.transform.SetParent(t, false);
        camera.transform.localPosition = new Vector3(-0.38f, 1.25f, -0.38f);
        camera.GetComponent<Camera>().fieldOfView = 68;
        camera.GetComponent<Camera>().farClipPlane = 1600;
        camera.SetActive(false);
        var player = root.Find("City Player").GetComponent<PlayerController>();
        var vehicle = car.AddComponent<CityUtilityVehicle>();
        var serialized = new SerializedObject(vehicle);
        serialized.FindProperty("walker").objectReferenceValue = player;
        serialized.FindProperty("walkOverlay").objectReferenceValue = player.GetComponent<CityGreyboxWalk>();
        serialized.FindProperty("walkingCamera").objectReferenceValue = player.GetComponentInChildren<Camera>();
        serialized.FindProperty("drivingCamera").objectReferenceValue = camera.GetComponent<Camera>();
        serialized.FindProperty("steeringWheel").objectReferenceValue = steering;
        serialized.FindProperty("speedDisplay").objectReferenceValue = speed;
        serialized.FindProperty("wheels").arraySize = 4;
        serialized.FindProperty("wheelVisuals").arraySize = 4;
        for (var i = 0; i < 4; i++)
        {
            serialized.FindProperty("wheels").GetArrayElementAtIndex(i).objectReferenceValue = wheelColliders[i];
            serialized.FindProperty("wheelVisuals").GetArrayElementAtIndex(i).objectReferenceValue = visuals[i];
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static Material Material(string path, Color color)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color };
        material.SetFloat("_Smoothness", 0.2f);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static Transform Part(Transform parent, string name, Vector3 position, Vector3 size, Material material,
        bool solid = false, PrimitiveType primitive = PrimitiveType.Cube)
    {
        var go = GameObject.CreatePrimitive(primitive);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = material;
        if (!solid) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        return go.transform;
    }

    private static TextMesh Label(Transform parent, string text, Vector3 position, float size)
    {
        var go = new GameObject(text, typeof(TextMesh));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        var mesh = go.GetComponent<TextMesh>();
        mesh.text = text;
        mesh.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        mesh.fontSize = 64;
        mesh.characterSize = size;
        mesh.anchor = TextAnchor.MiddleCenter;
        mesh.color = Color.white;
        go.GetComponent<Renderer>().sharedMaterial = lettering;
        return mesh;
    }
}
