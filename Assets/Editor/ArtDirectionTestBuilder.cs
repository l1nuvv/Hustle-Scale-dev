using System;
using System.Collections.Generic;
using Interactables;
using Player.Movement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Original, editable geometry for this sample. No downloaded models or generated images.
public static class ArtDirectionTestBuilder
{
    public const string ScenePath = "Assets/Scenes/ArtDirectionTest.unity";
    public const string Folder = "Assets/ArtDirectionTest";
    private static readonly Dictionary<string, Mesh> Meshes = new Dictionary<string, Mesh>();
    private static Material blue, rubber, steel, ivory, timber, plaster, concrete, cardboard, skin, hair, cloth, trousers, paper;
    private static Transform room;

    [MenuItem("Tools/Art Direction/Create Isolated Workshop")]
    public static void Create()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        if (SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the current scene first.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null) throw new InvalidOperationException("The art scene already exists. Edit it directly.");
        EnsureFolder(Folder);
        EnsureFolder(Folder + "/Meshes");
        EnsureFolder(Folder + "/Materials");
        EnsureFolder(Folder + "/Prefabs");
        Meshes.Clear();
        blue = Mat("Equipment blue", new Color(0.055f, 0.27f, 0.46f), 0, 0.25f);
        rubber = Mat("Graphite rubber", new Color(0.055f, 0.065f, 0.07f), 0, 0.08f);
        steel = Mat("Bare steel", new Color(0.29f, 0.33f, 0.34f), 0.78f, 0.38f);
        ivory = Mat("Recessed lens", new Color(0.83f, 0.82f, 0.73f), 0, 0.28f);
        ivory.EnableKeyword("_EMISSION");
        ivory.SetColor("_EmissionColor", Color.white);
        ivory.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        EditorUtility.SetDirty(ivory);
        timber = Mat("Plywood", new Color(0.54f, 0.36f, 0.18f), 0, 0.18f);
        plaster = Mat("Warm plaster", new Color(0.64f, 0.62f, 0.56f), 0, 0.05f);
        concrete = Mat("Concrete", new Color(0.36f, 0.38f, 0.37f), 0, 0.09f);
        cardboard = Mat("Cardboard", new Color(0.47f, 0.33f, 0.18f), 0, 0.04f);
        skin = Mat("Skin", new Color(0.57f, 0.37f, 0.21f), 0, 0.12f);
        hair = Mat("Hair", new Color(0.095f, 0.065f, 0.042f), 0, 0.12f);
        cloth = Mat("Work jacket", new Color(0.09f, 0.15f, 0.21f), 0, 0.08f);
        trousers = Mat("Work trousers", new Color(0.14f, 0.17f, 0.18f), 0, 0.08f);
        paper = Mat("Offwhite", new Color(0.83f, 0.81f, 0.73f), 0, 0.12f);
        Grain(timber, "Wood grain", true);
        Grain(plaster, "Plaster variation", false);

        // Manufacture the hero first, then use its exact same mesh and materials three times.
        var lampSource = new GameObject("Worklight").transform;
        Lamp(lampSource);
        var lampPrefab = PrefabUtility.SaveAsPrefabAsset(lampSource.gameObject, Folder + "/Prefabs/Worklight.prefab");
        Object.DestroyImmediate(lampSource.gameObject);
        var npcSource = new GameObject("Workshop worker").transform;
        Worker(npcSource);
        var npcPrefab = PrefabUtility.SaveAsPrefabAsset(npcSource.gameObject, Folder + "/Prefabs/WorkshopWorker.prefab");
        Object.DestroyImmediate(npcSource.gameObject);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.47f, 0.54f, 0.63f);
        RenderSettings.ambientEquatorColor = new Color(0.38f, 0.37f, 0.33f);
        RenderSettings.ambientGroundColor = new Color(0.20f, 0.19f, 0.17f);
        RenderSettings.fog = false;
        room = new GameObject("Workshop · art sample").transform;
        Environment();
        var worker = (GameObject)PrefabUtility.InstantiatePrefab(npcPrefab);
        worker.transform.position = new Vector3(1.95f, 0, 2.65f);
        worker.transform.rotation = Quaternion.Euler(0, 25, 0);
        var npcCollider = worker.AddComponent<CapsuleCollider>();
        npcCollider.center = new Vector3(0, 0.9f, 0); npcCollider.height = 1.8f; npcCollider.radius = 0.24f;

        var player = new GameObject("Player");
        player.tag = "Player";
        player.layer = 2; // Built-in Ignore Raycast: the eye must not hit its own capsule when looking down.
        player.transform.position = new Vector3(0.1f, 0.04f, -0.7f);
        var body = player.AddComponent<CharacterController>();
        body.height = 1.8f; body.radius = 0.3f; body.center = Vector3.up * 0.9f;
        body.skinWidth = 0.03f; body.stepOffset = 0.3f;
        var camera = new GameObject("Main Camera").AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.transform.SetParent(player.transform, false);
        camera.transform.localPosition = Vector3.up * 1.65f;
        camera.fieldOfView = 70; camera.nearClipPlane = 0.05f; camera.farClipPlane = 60;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.49f, 0.65f, 0.79f);
        camera.gameObject.AddComponent<AudioListener>();
        var input = player.AddComponent<Player.PlayerInput>();
        var controller = player.AddComponent<PlayerController>();
        Ref(controller, "cameraTransform", camera.transform);
        var raycast = player.AddComponent<Player.PlayerRaycast>();
        raycast.Configure(camera, Physics.DefaultRaycastLayers);
        var view = new GameObject("Art sample · existing WorkshopSession").AddComponent<ArtDirectionTestView>();
        Ref(view, "input", input); Ref(view, "controller", controller); Ref(view, "raycast", raycast); Ref(view, "eye", camera);
        var bench = Anchor(view.transform, "Diagnostic lamp anchor", new Vector3(0.25f, 1.165f, 1.96f));
        Ref(view, "bench", bench);
        var pad = Part(room, "Single-lamp test position", new Vector3(0.25f, 0.965f, 1.96f), new Vector3(0.66f, 0.014f, 0.56f), rubber, true, 0.004f);
        Interaction(pad.gameObject, view, ArtTestInteraction.ActionKind.Bench);
        var tester = new GameObject("Battery load tester").transform;
        tester.SetParent(room); tester.position = new Vector3(1.02f, 0.96f, 1.92f);
        Tester(tester);
        var testCollider = tester.gameObject.AddComponent<BoxCollider>();
        testCollider.center = new Vector3(0, 0.06f, 0); testCollider.size = new Vector3(0.25f, 0.15f, 0.32f);
        Interaction(tester.gameObject, view, ArtTestInteraction.ActionKind.Test);
        Battery(Anchor(room, "Spare battery · display prop", new Vector3(1.39f, 1.01f, 2.14f)));
        var crate = Anchor(room, "Transport crate", new Vector3(-1.35f, 0.96f, 2.05f));
        Crate(crate);
        var crateCollider = crate.gameObject.AddComponent<BoxCollider>();
        crateCollider.center = new Vector3(0, 0.17f, -0.255f); crateCollider.size = new Vector3(1.3f, 0.3f, 0.035f);
        Interaction(crate.gameObject, view, ArtTestInteraction.ActionKind.Crate);
        var lamps = new Transform[3]; var slots = new Transform[3];
        var lenses = new Renderer[3]; var beams = new Light[3];
        for (var i = 0; i < 3; i++)
        {
            slots[i] = Anchor(crate, "Lamp slot " + (i + 1), crate.position + new Vector3((i - 1) * 0.40f, 0.245f, 0));
            lamps[i] = ((GameObject)PrefabUtility.InstantiatePrefab(lampPrefab)).transform;
            lamps[i].name = "Worklight " + (i + 1).ToString("00"); lamps[i].position = slots[i].position;
            var collider = lamps[i].gameObject.AddComponent<BoxCollider>();
            collider.center = new Vector3(0, 0.04f, 0); collider.size = new Vector3(0.30f, 0.48f, 0.26f);
            Interaction(lamps[i].gameObject, view, ArtTestInteraction.ActionKind.Lamp, i);
            lenses[i] = lamps[i].Find("Inset diffuser").GetComponent<Renderer>();
            beams[i] = lamps[i].GetComponentInChildren<Light>(true);
        }
        Refs(view, "lamps", lamps); Refs(view, "slots", slots); Refs(view, "lenses", lenses); Refs(view, "beams", beams);
        HUD(view);
        var sun = new GameObject("Daylight").AddComponent<Light>();
        sun.type = LightType.Directional; sun.color = new Color(1, 0.94f, 0.82f); sun.intensity = 1.25f;
        sun.transform.rotation = Quaternion.Euler(48, 205, 0); sun.shadows = LightShadows.Soft;
        var fill = new GameObject("Workshop ceiling light").AddComponent<Light>();
        fill.type = LightType.Point; fill.transform.position = new Vector3(-0.5f, 2.8f, 0.8f);
        fill.range = 7; fill.intensity = 3; fill.color = new Color(1, 0.93f, 0.81f); fill.shadows = LightShadows.Soft;
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene, ScenePath);
        Selection.activeObject = view;
    }

    private static void Lamp(Transform root)
    {
        Part(root, "Blue rear housing", new Vector3(0, 0, 0.025f), new Vector3(0.28f, 0.29f, 0.15f), blue, false, 0.009f);
        // Four frame rails form a real recess; the diffuser is behind their front face.
        foreach (var side in new[] { -1, 1 })
        {
            Part(root, "Blue side bezel", new Vector3(side * 0.127f, 0, -0.077f), new Vector3(0.026f, 0.29f, 0.055f), blue, false, 0.004f);
            Part(root, "Blue horizontal bezel", new Vector3(0, side * 0.132f, -0.077f), new Vector3(0.24f, 0.026f, 0.055f), blue, false, 0.004f);
            Part(root, "Support foot", new Vector3(side * 0.105f, -0.176f, 0), new Vector3(0.085f, 0.058f, 0.245f), rubber, false, 0.007f);
            Part(root, "Handle upright", new Vector3(side * 0.102f, 0.194f, 0.015f), new Vector3(0.03f, 0.105f, 0.043f), rubber, false, 0.005f);
        }
        Part(root, "Inset reflector", new Vector3(0, 0, -0.071f), new Vector3(0.23f, 0.235f, 0.008f), steel, false, 0.002f);
        Part(root, "Inset diffuser", new Vector3(0, 0, -0.079f), new Vector3(0.205f, 0.206f, 0.009f), ivory, false, 0.003f);
        Part(root, "Carry grip", new Vector3(0, 0.252f, 0.015f), new Vector3(0.228f, 0.043f, 0.05f), rubber, false, 0.008f);
        Part(root, "Rocker switch", new Vector3(0.142f, 0.056f, 0.014f), new Vector3(0.014f, 0.045f, 0.038f), rubber, false, 0.003f);
        Part(root, "Battery hatch", new Vector3(0, -0.027f, 0.104f), new Vector3(0.21f, 0.19f, 0.012f), rubber, false, 0.004f);
        Part(root, "Hatch latch", new Vector3(0, 0.074f, 0.115f), new Vector3(0.047f, 0.02f, 0.014f), steel, false, 0.002f);
        var beam = new GameObject("Worklight beam").AddComponent<Light>();
        beam.transform.SetParent(root, false); beam.transform.localPosition = new Vector3(0, 0, -0.14f);
        beam.transform.localRotation = Quaternion.Euler(0, 180, 0);
        beam.type = LightType.Spot; beam.range = 5; beam.spotAngle = 68; beam.innerSpotAngle = 40;
        beam.color = new Color(1, 0.88f, 0.64f); beam.intensity = 3; beam.enabled = false;
    }

    private static void Environment()
    {
        Part(room, "Concrete floor", new Vector3(0, -0.12f, 0.2f), new Vector3(7, 0.24f, 6.2f), concrete, true, 0);
        Part(room, "Left plaster wall", new Vector3(-3.5f, 1.6f, 0.2f), new Vector3(0.2f, 3.2f, 6.4f), plaster, true, 0);
        Part(room, "Right plaster wall", new Vector3(3.5f, 1.6f, 0.2f), new Vector3(0.2f, 3.2f, 6.4f), plaster, true, 0);
        Part(room, "Front wall", new Vector3(0, 1.6f, -2.9f), new Vector3(7, 3.2f, 0.2f), plaster, true, 0);
        Part(room, "Rear wall left of door", new Vector3(-0.92f, 1.6f, 3.3f), new Vector3(5.16f, 3.2f, 0.2f), plaster, true, 0);
        Part(room, "Rear wall right of door", new Vector3(3.22f, 1.6f, 3.3f), new Vector3(0.56f, 3.2f, 0.2f), plaster, true, 0);
        Part(room, "Door lintel", new Vector3(2.3f, 2.85f, 3.3f), new Vector3(1.28f, 0.7f, 0.2f), plaster, true, 0);
        Part(room, "Ceiling", new Vector3(0, 3.3f, 0.2f), new Vector3(7.2f, 0.2f, 6.4f), plaster, true, 0);
        foreach (var x in new[] { 1.63f, 2.97f }) Part(room, "Blue door jamb", new Vector3(x, 1.25f, 3.18f), new Vector3(0.08f, 2.5f, 0.18f), blue, true, 0.008f);
        Part(room, "Door header", new Vector3(2.3f, 2.5f, 3.18f), new Vector3(1.42f, 0.08f, 0.18f), blue, true, 0.008f);
        var door = Anchor(room, "Open door hinge", new Vector3(1.67f, 0, 3.28f));
        door.rotation = Quaternion.Euler(0, -105, 0);
        Part(door, "Open steel door", new Vector3(0.62f, 1.23f, 0), new Vector3(1.24f, 2.46f, 0.07f), steel, true, 0.01f);
        Part(door, "Door handle", new Vector3(1.09f, 1.06f, -0.065f), new Vector3(0.15f, 0.035f, 0.07f), rubber, false, 0.008f);
        Part(room, "Courtyard paving", new Vector3(1.5f, -0.1f, 5.6f), new Vector3(7, 0.18f, 4.6f), concrete, true, 0);
        Part(room, "Courtyard boundary", new Vector3(1.5f, 1.3f, 7.8f), new Vector3(7, 2.6f, 0.2f), plaster, true, 0);
        Part(room, "Courtyard service door", new Vector3(2.65f, 1.05f, 7.68f), new Vector3(1.1f, 2.1f, 0.05f), steel, true, 0.006f);
        for (var i = 0; i < 5; i++) Part(room, "Service door reinforcement", new Vector3(2.65f, 0.3f + i * 0.39f, 7.64f), new Vector3(1.04f, 0.025f, 0.03f), rubber, false, 0.002f);
        Part(room, "Yard service wall", new Vector3(-1.7f, 1.65f, 6.3f), new Vector3(0.25f, 3.3f, 3), cardboard, true, 0);
        for (var i = 0; i < 5; i++) Part(room, "Paving joint", new Vector3(1.5f, 0.001f, 3.6f + i * 0.85f), new Vector3(6.5f, 0.002f, 0.012f), steel, false, 0);
        // Keep the table approach clear; only the tabletop and four legs block movement.
        for (var i = 0; i < 5; i++)
            Part(room, "Plywood worktop plank", new Vector3(-0.35f, 0.91f, 1.65f + i * 0.23f), new Vector3(3.75f, 0.10f, 0.225f), timber, true, 0.007f);
        foreach (var x in new[] { -2.05f, 1.35f })
        {
            foreach (var z in new[] { 1.68f, 2.49f }) Part(room, "Bench steel leg", new Vector3(x, 0.43f, z), new Vector3(0.075f, 0.86f, 0.075f), steel, true, 0.005f);
            Part(room, "Bench stretcher", new Vector3(x, 0.22f, 2.08f), new Vector3(0.06f, 0.07f, 0.85f), steel, false, 0.003f);
        }
        Part(room, "Bench lower shelf", new Vector3(-0.35f, 0.24f, 2.1f), new Vector3(3.5f, 0.045f, 0.75f), timber, false, 0.003f);
        // Rack is behind the crate: stored cartons, no arbitrary decorative clutter.
        foreach (var x in new[] { -3.12f, -1.36f }) foreach (var z in new[] { 2.69f, 3.02f })
            Part(room, "Rack upright", new Vector3(x, 1.22f, z), new Vector3(0.045f, 2.44f, 0.045f), steel, true, 0.003f);
        foreach (var y in new[] { 0.25f, 1.05f, 1.83f, 2.43f })
            Part(room, "Rack folded shelf", new Vector3(-2.24f, y, 2.86f), new Vector3(1.84f, 0.045f, 0.42f), steel, true, 0.003f);
        Carton(new Vector3(-2.68f, 1.29f, 2.82f), new Vector3(0.56f, 0.43f, 0.34f));
        Carton(new Vector3(-1.71f, 2.02f, 2.82f), new Vector3(0.48f, 0.34f, 0.34f));
        Carton(new Vector3(-2.5f, 0.48f, 2.82f), new Vector3(0.75f, 0.40f, 0.34f));
        Carton(new Vector3(-0.8f, 0.45f, 2.12f), new Vector3(0.7f, 0.37f, 0.55f));
        Part(room, "Ceiling fixture", new Vector3(-0.4f, 3.12f, 1.2f), new Vector3(1.1f, 0.10f, 0.2f), steel, false, 0.008f);
        Part(room, "Ceiling diffuser", new Vector3(-0.4f, 3.057f, 1.2f), new Vector3(1, 0.025f, 0.15f), ivory, false, 0.003f);
        Part(room, "Wall conduit", new Vector3(-3.36f, 1.2f, 0.4f), new Vector3(0.025f, 2.4f, 0.025f), steel, false, 0.002f);
        Part(room, "Wall socket", new Vector3(-3.32f, 1.18f, 0.4f), new Vector3(0.06f, 0.12f, 0.09f), paper, false, 0.004f);
    }

    private static void Carton(Vector3 position, Vector3 size)
    {
        Part(room, "Storage carton", position, size, cardboard, true, 0.006f);
        Part(room, "Packing tape", position + Vector3.up * (size.y * 0.5f + 0.001f), new Vector3(0.065f, 0.003f, size.z), timber, false, 0);
        Part(room, "Carton label", position + Vector3.back * (size.z * 0.5f + 0.002f), new Vector3(size.x * 0.29f, size.y * 0.22f, 0.003f), paper, false, 0);
    }

    private static void Crate(Transform root)
    {
        Part(root, "Crate bottom", new Vector3(0, 0.025f, 0), new Vector3(1.26f, 0.05f, 0.50f), timber, false, 0.004f);
        foreach (var x in new[] { -0.64f, 0.64f })
        {
            Part(root, "End panel", new Vector3(x, 0.12f, 0), new Vector3(0.035f, 0.23f, 0.54f), timber, false, 0.004f);
            foreach (var z in new[] { -0.225f, 0.225f })
                Part(root, "Handle rim end", new Vector3(x, 0.28f, z), new Vector3(0.035f, 0.10f, 0.09f), timber, false, 0.003f);
            Part(root, "Handle rim top", new Vector3(x, 0.335f, 0), new Vector3(0.035f, 0.035f, 0.54f), timber, false, 0.003f);
        }
        foreach (var z in new[] { -0.255f, 0.255f })
            for (var i = 0; i < 2; i++) Part(root, "Long side slat", new Vector3(0, 0.09f + i * 0.13f, z), new Vector3(1.3f, 0.105f, 0.035f), timber, false, 0.004f);
        foreach (var x in new[] { -0.20f, 0.20f }) Part(root, "Lamp compartment", new Vector3(x, 0.16f, 0), new Vector3(0.018f, 0.23f, 0.47f), timber, false, 0.002f);
    }

    private static void Tester(Transform root)
    {
        var face = Anchor(root, "Angled instrument", root.position);
        face.localRotation = Quaternion.Euler(-22, 0, 0);
        Part(face, "Instrument shell", new Vector3(0, 0.04f, 0), new Vector3(0.22f, 0.07f, 0.29f), blue, false, 0.009f);
        Part(face, "LCD surround", new Vector3(0, 0.078f, 0.065f), new Vector3(0.17f, 0.008f, 0.083f), rubber, false, 0.003f);
        Part(face, "LCD", new Vector3(0, 0.084f, 0.065f), new Vector3(0.14f, 0.006f, 0.059f), paper, false, 0.002f);
        for (var i = 0; i < 3; i++) Part(face, "LCD segment", new Vector3(-0.037f + i * 0.037f, 0.088f, 0.066f), new Vector3(0.02f, 0.002f, 0.025f), steel, false, 0);
        Part(face, "Test rocker", new Vector3(0, 0.084f, -0.052f), new Vector3(0.065f, 0.025f, 0.068f), rubber, false, 0.009f);
        var red = Mat("Test lead red", new Color(0.44f, 0.075f, 0.045f), 0, 0.1f);
        for (var i = 0; i < 2; i++)
        {
            var material = i == 0 ? rubber : red;
            var start = new Vector3(0.075f + i * 0.035f, 0.025f, -0.1f);
            var mid = new Vector3(0.22f + i * 0.04f, 0.025f, -0.15f);
            Segment(root, "Tester lead", start, mid, 0.004f, material);
            Segment(root, "Tester lead", mid, new Vector3(0.25f + i * 0.04f, 0.025f, 0.05f), 0.004f, material);
            Part(root, "Probe grip", new Vector3(0.25f + i * 0.04f, 0.025f, 0.09f), new Vector3(0.012f, 0.012f, 0.08f), material, false, 0.003f);
        }
    }

    private static void Battery(Transform root)
    {
        Part(root, "Battery pack", Vector3.zero, new Vector3(0.13f, 0.09f, 0.09f), rubber, false, 0.005f);
        Part(root, "Blue locking rail", new Vector3(0, 0.046f, 0), new Vector3(0.11f, 0.012f, 0.065f), blue, false, 0.002f);
        foreach (var x in new[] { -0.022f, 0.022f }) Part(root, "Battery contact", new Vector3(x, 0.053f, 0.023f), new Vector3(0.012f, 0.008f, 0.026f), steel, false, 0.001f);
    }

    private static void Worker(Transform root)
    {
        // 1.80m adult, 7.2 heads. Slightly asymmetric arms, quiet standing pose.
        foreach (var side in new[] { -1, 1 })
        {
            var x = side * 0.105f;
            Part(root, "Work shoe", new Vector3(x, 0.055f, -0.035f), new Vector3(0.125f, 0.11f, 0.265f), rubber, false, 0.018f);
            Limb(root, "Trouser leg", new Vector3(x, 0.13f, 0.025f), new Vector3(x * 0.9f, 0.92f, 0), 0.065f, 0.085f, trousers);
            var shoulder = new Vector3(side * 0.205f, 1.38f, 0.018f);
            var elbow = new Vector3(side * 0.245f, 1.12f, -0.018f);
            var wrist = new Vector3(side * 0.265f, 0.91f, side * 0.025f - 0.06f);
            Limb(root, "Jacket upper sleeve", shoulder, elbow, 0.08f, 0.06f, cloth);
            Limb(root, "Jacket folded sleeve", elbow, Vector3.Lerp(elbow, wrist, 0.55f), 0.062f, 0.057f, cloth);
            Limb(root, "Forearm", Vector3.Lerp(elbow, wrist, 0.55f), wrist, 0.046f, 0.035f, skin);
            Oval(root, "Hand", wrist + new Vector3(0, -0.055f, -0.005f), new Vector3(0.042f, 0.085f, 0.028f), skin);
            Oval(root, "Ear", new Vector3(side * 0.089f, 1.645f, 0.003f), new Vector3(0.025f, 0.039f, 0.021f), skin);
        }
        Limb(root, "Jacket torso", new Vector3(0, 0.91f, 0.018f), new Vector3(0, 1.46f, 0.018f), 0.17f, 0.205f, cloth, 0.62f);
        Part(root, "Undershirt", new Vector3(0, 1.22f, -0.106f), new Vector3(0.085f, 0.33f, 0.012f), paper, false, 0.003f);
        foreach (var side in new[] { -1, 1 })
        {
            var collar = Part(root, "Jacket collar", new Vector3(side * 0.055f, 1.438f, -0.105f), new Vector3(0.058f, 0.105f, 0.018f), cloth, false, 0.002f);
            collar.localRotation = Quaternion.Euler(0, 0, side * -20);
            Part(root, "Jacket pocket", new Vector3(side * 0.115f, 1.21f, -0.108f), new Vector3(0.073f, 0.086f, 0.012f), cloth, false, 0.002f);
        }
        Limb(root, "Neck", new Vector3(0, 1.39f, 0.012f), new Vector3(0, 1.57f, 0.012f), 0.047f, 0.048f, skin);
        Oval(root, "Head", new Vector3(0, 1.652f, 0), new Vector3(0.085f, 0.125f, 0.082f), skin);
        Oval(root, "Hair cap", new Vector3(0, 1.738f, 0.018f), new Vector3(0.089f, 0.063f, 0.078f), hair);
        Oval(root, "Side part sweep", new Vector3(-0.025f, 1.768f, -0.025f), new Vector3(0.07f, 0.026f, 0.055f), hair);
        Part(root, "Nose bridge", new Vector3(0, 1.646f, -0.084f), new Vector3(0.025f, 0.052f, 0.035f), skin, false, 0.007f);
        foreach (var side in new[] { -1, 1 })
        {
            Oval(root, "Eye white", new Vector3(side * 0.035f, 1.672f, -0.074f), new Vector3(0.020f, 0.012f, 0.009f), paper);
            Oval(root, "Pupil", new Vector3(side * 0.035f, 1.671f, -0.082f), new Vector3(0.007f, 0.008f, 0.0035f), rubber);
            Part(root, "Brow", new Vector3(side * 0.035f, 1.694f, -0.079f), new Vector3(0.04f, 0.007f, 0.009f), hair, false, 0.002f);
        }
        Part(root, "Mouth", new Vector3(0, 1.60f, -0.075f), new Vector3(0.04f, 0.004f, 0.005f), hair, false, 0.001f);
    }

    private static void HUD(ArtDirectionTestView view)
    {
        var canvas = new GameObject("Art sample HUD", typeof(Canvas), typeof(CanvasScaler));
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = 1;
        var safe = new GameObject("Safe area", typeof(RectTransform)).GetComponent<RectTransform>();
        safe.SetParent(canvas.transform, false); safe.anchorMin = Vector2.zero; safe.anchorMax = Vector2.one; safe.offsetMin = safe.offsetMax = Vector2.zero;
        Ref(view, "safeArea", safe);
        var title = Label(safe, "Sample title", new Vector2(0, 1), new Vector2(24, -20), new Vector2(430, 26), 16);
        title.text = "HUSTLE & SCALE   /   ХУДОЖЕСТВЕННАЯ ПРОБА";
        var fixture = Label(safe, "Fixture", new Vector2(0, 1), new Vector2(24, -48), new Vector2(530, 28), 14);
        fixture.text = "Демонстрация после покупки · 3 своих фонаря · без сохранения";
        var state = Label(safe, "Selected lamp", new Vector2(0, 1), new Vector2(24, -86), new Vector2(450, 60), 18);
        var result = Label(safe, "Test result", new Vector2(0.5f, 0), new Vector2(-410, 104), new Vector2(820, 40), 18);
        result.text = "Возьми любой фонарь из ящика и поставь на стенд рядом с прибором.";
        var prompt = Label(safe, "Available action", new Vector2(0.5f, 0), new Vector2(-410, 150), new Vector2(820, 40), 20);
        var keys = Label(safe, "Controls", new Vector2(0.5f, 0), new Vector2(-410, 48), new Vector2(820, 32), 16);
        keys.text = "WASD — идти    E — действие    Q — на стенд    F — свет    Esc — курсор";
        var cross = Label(safe, "Aim", new Vector2(0.5f, 0.5f), new Vector2(-8, 8), new Vector2(16, 22), 18);
        cross.text = "+";
        cross.GetComponentInParent<Image>().enabled = false;
        cross.rectTransform.offsetMin = cross.rectTransform.offsetMax = Vector2.zero;
        cross.alignment = TextAnchor.MiddleCenter;
        Ref(view, "stateLabel", state); Ref(view, "resultLabel", result); Ref(view, "promptLabel", prompt);
    }

    private static Text Label(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, int fontSize)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = anchor; rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = position; rect.sizeDelta = size;
        var image = rect.gameObject.AddComponent<Image>(); image.color = new Color(0.035f, 0.05f, 0.061f, 0.88f); image.raycastTarget = false;
        var text = new GameObject("Text", typeof(RectTransform)).AddComponent<Text>();
        text.transform.SetParent(rect, false); text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = new Vector2(8, 2); text.rectTransform.offsetMax = new Vector2(-8, -2);
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = fontSize;
        text.color = new Color(0.94f, 0.92f, 0.86f); text.alignment = TextAnchor.MiddleLeft; text.raycastTarget = false;
        return text;
    }

    private static void Interaction(GameObject target, ArtDirectionTestView view, ArtTestInteraction.ActionKind kind, int index = 0)
    {
        var action = target.AddComponent<ArtTestInteraction>(); Ref(action, "view", view);
        var data = new SerializedObject(action); data.FindProperty("action").enumValueIndex = (int)kind; data.FindProperty("index").intValue = index; data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Transform Part(Transform parent, string name, Vector3 position, Vector3 size, Material material, bool collision, float bevel)
    {
        var key = "Box_" + size.ToString("F4") + "_" + bevel.ToString("F4", System.Globalization.CultureInfo.InvariantCulture);
        if (!Meshes.TryGetValue(key, out var mesh)) { mesh = BeveledBox(size, bevel); SaveMesh(mesh, key); }
        var part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)).transform;
        part.SetParent(parent, false); part.localPosition = position;
        part.GetComponent<MeshFilter>().sharedMesh = mesh; part.GetComponent<MeshRenderer>().sharedMaterial = material;
        if (collision) part.gameObject.AddComponent<BoxCollider>().size = size;
        return part;
    }

    private static Mesh BeveledBox(Vector3 size, float bevel)
    {
        var vertices = new List<Vector3>(); var triangles = new List<int>(); var uv = new List<Vector2>();
        var half = size * 0.5f; bevel = Mathf.Min(bevel, Mathf.Min(half.x, Mathf.Min(half.y, half.z)) * 0.8f);
        for (var axis = 0; axis < 3; axis++) for (var sign = -1; sign <= 1; sign += 2)
        {
            var u = (axis + 1) % 3; var v = (axis + 2) % 3; var start = vertices.Count;
            var steps = bevel > 0 ? 3 : 1;
            for (var j = 0; j <= steps; j++) for (var i = 0; i <= steps; i++)
            {
                var p = Vector3.zero; p[axis] = sign * half[axis];
                p[u] = steps == 1 ? (i * 2 - 1) * half[u] : i == 0 ? -half[u] : i == 1 ? -half[u] + bevel : i == 2 ? half[u] - bevel : half[u];
                p[v] = steps == 1 ? (j * 2 - 1) * half[v] : j == 0 ? -half[v] : j == 1 ? -half[v] + bevel : j == 2 ? half[v] - bevel : half[v];
                if (bevel > 0)
                {
                    var inner = half - Vector3.one * bevel;
                    var clamp = new Vector3(Mathf.Clamp(p.x, -inner.x, inner.x), Mathf.Clamp(p.y, -inner.y, inner.y), Mathf.Clamp(p.z, -inner.z, inner.z));
                    p = clamp + (p - clamp).normalized * bevel;
                }
                vertices.Add(p); uv.Add(new Vector2(p[u], p[v]));
            }
            for (var j = 0; j < steps; j++) for (var i = 0; i < steps; i++)
            {
                var a = start + j * (steps + 1) + i; var b = a + 1; var c = a + steps + 1; var d = c + 1;
                if (sign > 0) { triangles.AddRange(new[] { a, b, d, a, d, c }); }
                else { triangles.AddRange(new[] { a, d, b, a, c, d }); }
            }
        }
        var mesh = new Mesh(); mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.SetUVs(0, uv); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
    }

    private static void Oval(Transform root, string name, Vector3 position, Vector3 radius, Material material)
    {
        const string key = "Unit oval";
        if (!Meshes.TryGetValue(key, out var mesh))
        {
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            for (var y = 0; y <= 10; y++) for (var x = 0; x <= 16; x++)
            {
                var latitude = Mathf.PI * y / 10; var longitude = Mathf.PI * 2 * x / 16;
                vertices.Add(new Vector3(Mathf.Sin(latitude) * Mathf.Cos(longitude), Mathf.Cos(latitude), Mathf.Sin(latitude) * Mathf.Sin(longitude)));
            }
            for (var y = 0; y < 10; y++) for (var x = 0; x < 16; x++)
            {
                var a = y * 17 + x; triangles.AddRange(new[] { a, a + 1, a + 18, a, a + 18, a + 17 });
            }
            mesh = new Mesh(); mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); SaveMesh(mesh, key);
        }
        var part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)).transform;
        part.SetParent(root, false); part.localPosition = position; part.localScale = radius;
        part.GetComponent<MeshFilter>().sharedMesh = mesh; part.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    private static void Limb(Transform root, string name, Vector3 from, Vector3 to, float bottom, float top, Material material, float depth = 0.85f)
    {
        var key = "Taper_" + bottom.ToString("F4") + "_" + top.ToString("F4") + "_" + depth.ToString("F3");
        if (!Meshes.TryGetValue(key, out var mesh))
        {
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            for (var y = 0; y < 2; y++) for (var i = 0; i < 12; i++)
            {
                var angle = (i + 0.5f) * Mathf.PI * 2 / 12; var radius = y == 0 ? bottom : top;
                vertices.Add(new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius * depth));
            }
            vertices.Add(Vector3.zero); vertices.Add(Vector3.up);
            for (var i = 0; i < 12; i++)
            {
                var next = (i + 1) % 12;
                triangles.AddRange(new[] { i, i + 12, next + 12, i, next + 12, next, 24, i, next, 25, next + 12, i + 12 });
            }
            mesh = new Mesh(); mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); SaveMesh(mesh, key);
        }
        var part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)).transform;
        part.SetParent(root, false); part.localPosition = from; part.localRotation = Quaternion.FromToRotation(Vector3.up, to - from); part.localScale = new Vector3(1, (to - from).magnitude, 1);
        part.GetComponent<MeshFilter>().sharedMesh = mesh; part.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    private static void Segment(Transform root, string name, Vector3 a, Vector3 b, float width, Material material)
    {
        var part = Part(root, name, (a + b) * 0.5f, new Vector3(width, width, Vector3.Distance(a, b)), material, false, 0);
        part.localRotation = Quaternion.LookRotation(b - a);
    }

    private static void SaveMesh(Mesh mesh, string key)
    {
        mesh.name = key; var path = Folder + "/Meshes/" + key.Replace("(", "").Replace(")", "").Replace(",", "_").Replace(" ", "") + ".asset";
        AssetDatabase.CreateAsset(mesh, path); Meshes.Add(key, mesh);
    }

    private static Material Mat(string name, Color color, float metallic, float smoothness)
    {
        var path = Folder + "/Materials/" + name + ".mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path); if (existing != null) return existing;
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, color = color };
        material.SetFloat("_Metallic", metallic); material.SetFloat("_Smoothness", smoothness); AssetDatabase.CreateAsset(material, path); return material;
    }

    private static void Grain(Material material, string name, bool wood)
    {
        var texture = new Texture2D(128, 128, TextureFormat.RGB24, false); texture.name = name;
        for (var y = 0; y < 128; y++) for (var x = 0; x < 128; x++)
        {
            var noise = Mathf.PerlinNoise(x * (wood ? 0.05f : 0.07f), y * (wood ? 0.5f : 0.07f));
            var value = 0.97f + noise * 0.03f;
            texture.SetPixel(x, y, new Color(value, value, value));
        }
        texture.wrapMode = TextureWrapMode.Repeat; texture.Apply();
        AssetDatabase.CreateAsset(texture, Folder + "/Materials/" + name + ".asset"); material.mainTexture = texture;
    }

    private static Transform Anchor(Transform parent, string name, Vector3 position)
    {
        var anchor = new GameObject(name).transform; anchor.SetParent(parent, false); anchor.position = position; return anchor;
    }

    private static void EnsureFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'), System.IO.Path.GetFileName(path));
    }

    private static void Ref(Object target, string field, Object value)
    {
        var data = new SerializedObject(target); data.FindProperty(field).objectReferenceValue = value; data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Refs(Object target, string field, Object[] values)
    {
        var data = new SerializedObject(target); var property = data.FindProperty(field); property.arraySize = values.Length;
        for (var i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        data.ApplyModifiedPropertiesWithoutUndo();
    }
}
