using System;
using System.IO;
using System.Linq;
using Interactables;
using Inventory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Exercises the real scene adapter and its existing item/light references.
[InitializeOnLoad]
public static class V2Verification
{
    private const string Key = "V2.verify";
    private const string LogPath = "Logs/Codex-V2-verification.log";
    private static int checks;
    private static double started;
    private static bool previousBackground;
    static V2Verification() => EditorApplication.playModeStateChanged += StateChanged;

    [MenuItem("Tools/V2/Verify Models and Trade")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Stop Play Mode and save the scene first.");
        EditorSceneManager.OpenScene(FirstDealTradeBuilder.ScenePath);
        Directory.CreateDirectory("Logs");
        File.WriteAllText(LogPath, "V2 Unity " + Application.unityVersion + "\n");
        checks = 0;
        var assets = AssetDatabase.FindAssets("", new[] { FirstDealPropBuilder.Folder })
            .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p).ToArray();
        var guids = assets.Select(AssetDatabase.AssetPathToGUID).ToArray();
        var colliderCount = UnityEngine.Object.FindObjectsByType<Collider>().Length;
        FirstDealPropBuilder.ApplyCurrent();
        Check(assets.SequenceEqual(AssetDatabase.FindAssets("", new[] { FirstDealPropBuilder.Folder })
            .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p)), "reapply creates no extra assets");
        Check(guids.SequenceEqual(assets.Select(AssetDatabase.AssetPathToGUID)), "reapply preserves asset GUIDs");
        Check(colliderCount == UnityEngine.Object.FindObjectsByType<Collider>().Length, "reapply adds no colliders");
        Check(UnityEngine.Object.FindObjectsByType<Transform>().Count(t => t.name == FirstDealPropBuilder.VisualName) == 7,
            "exactly three lamp models and four other models");
        foreach (var name in new[] { "Flashlight", "Crate", "Battery", "Workbench", "Tester" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FirstDealPropBuilder.Folder + "/" + name + ".prefab");
            Check(prefab != null && prefab.GetComponentsInChildren<Collider>().Length == 0, name + " is a collider-free prefab");
            Check(prefab.GetComponentsInChildren<Renderer>().Length <= 5, name + " uses at most five material batches");
            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>())
            {
                Check(filter.sharedMesh != null && filter.sharedMesh.triangles.Length > 0, name + " persisted mesh");
                Check(filter.sharedMesh.vertices.All(v => !float.IsNaN(v.x) && !float.IsInfinity(v.x) &&
                    !float.IsNaN(v.y) && !float.IsInfinity(v.y) && !float.IsNaN(v.z) && !float.IsInfinity(v.z)), name + " finite vertices");
                var material = filter.GetComponent<Renderer>().sharedMaterial;
                Check(material != null && material.shader.isSupported && !ShaderUtil.ShaderHasError(material.shader), name + " supported material");
            }
        }
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        SessionState.SetInt("V2.checks", checks);
        SessionState.SetBool(Key, true);
        EditorApplication.isPlaying = true;
    }

    private static void StateChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            started = EditorApplication.timeSinceStartup;
            previousBackground = Application.runInBackground;
            Application.runInBackground = true;
            EditorApplication.update += Verify;
        }
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            EditorApplication.update -= Verify;
            Application.runInBackground = previousBackground;
        }
        if (state == PlayModeStateChange.EnteredEditMode) SessionState.SetBool(Key, false);
    }

    private static void Verify()
    {
        if (Time.timeSinceLevelLoad < 0.5f && EditorApplication.timeSinceStartup - started < 20)
        { EditorApplication.QueuePlayerLoopUpdate(); return; }
        EditorApplication.update -= Verify;
        checks = SessionState.GetInt("V2.checks", 0);
        try
        {
            Check(Time.timeSinceLevelLoad >= 0.5f, "scene initialized");
            var workshop = UnityEngine.Object.FindAnyObjectByType<WorkshopView>();
            var deal = UnityEngine.Object.FindAnyObjectByType<FirstDealView>();
            deal.CloseMenu();
            var s = workshop.Session;
            var lamps = new[] { GameObject.Find("Lamp 1").transform, GameObject.Find("Lamp 2").transform, GameObject.Find("Lamp 3").transform };
            var crate = GameObject.Find("Carry crate").transform;
            var ids = s.Units.Select(u => u.InstanceId).ToArray();
            Check(s.PurchaseLot(out _), "purchase original three item identities");
            Check(s.BuyBattery(out _), "purchase spare battery");
            Check(s.ToggleCrate(out _), "carry crate");
            Check(crate.parent == Camera.main.transform && crate.GetComponentsInChildren<Collider>().All(c => !c.enabled),
                "carried crate follows hands without ray blockers");
            Check(s.ToggleCrate(out _), "dock crate");
            Capture("crate", new Vector3(-2.5f, 2.15f, 2.0f), crate.position + Vector3.up * 0.15f);
            for (var i = 0; i < 3; i++)
            {
                Check(s.ToggleUnit(i, out _), "take lamp " + i);
                Check(lamps[i].parent == Camera.main.transform, "model follows held lamp " + i);
                Check(s.ToggleBench(out _), "place lamp " + i);
                Check(lamps[i].parent.name == "Bench lamp", "model follows bench lamp " + i);
                Check(s.Test(out _), "test lamp " + i);
                if (i == 1)
                {
                    Check(!s.OnBench.LastTestPassed, "battery fault remains unknown until diagnosed");
                    Check(s.Repair(out _), "repair consumes the spare");
                    Check(s.SpareBatteries == 0 && !s.OnBench.IsTested, "repair requires retest");
                    Check(s.Test(out _), "retest repaired lamp");
                }
                Check(lamps[i].GetComponentInChildren<Light>().enabled, "existing successful-test light remains wired " + i);
                Check(Vector3.Distance(lamps[i].Find(FirstDealPropBuilder.VisualName).lossyScale, Vector3.one) < 0.001f,
                    "model retains metre scale after reparenting " + i);
                if (i == 0) Capture("workbench", new Vector3(-0.3f, 2.1f, 3.9f), new Vector3(0, 1.15f, 5.3f));
                Check(s.ToggleBench(out _) && s.ToggleUnit(i, out _), "return lamp " + i);
            }
            Check(ids.SequenceEqual(s.Units.Select(u => u.InstanceId)), "visual replacement preserves all item identities");
            Check(s.AcceptOrder(DealOrderId.A, out _) && s.AcceptOrder(DealOrderId.B, out _), "accept A and B");
            Check(s.ToggleCrate(out _), "carry tested batch");
            Check(s.DeliverOrder(DealOrderId.A, new[] { ids[0], ids[2] }, out _), "deliver A");
            Check(!lamps[0].gameObject.activeSelf && !lamps[2].gameObject.activeSelf, "sold A models disappear");
            Check(s.DeliverOrder(DealOrderId.B, new[] { ids[1] }, out _), "deliver repaired lamp B");
            Check(!lamps[1].gameObject.activeSelf && s.Balance == 455, "all models sold and original $455 ledger retained");
            Check(!s.DeliverOrder(DealOrderId.B, new[] { ids[1] }, out _) && s.Balance == 455, "repeated delivery cannot pay twice");
            File.AppendAllText(LogPath, "V2 RESULT PASS " + checks + " checks\n");
            Debug.Log("V2 RESULT PASS " + checks + " checks");
        }
        catch (Exception e) { File.AppendAllText(LogPath, "V2 RESULT FAIL " + e + "\n"); Debug.LogException(e); }
        finally { EditorApplication.isPlaying = false; }
    }

    private static void Capture(string name, Vector3 position, Vector3 target)
    {
        var go = new GameObject("V2 verification camera", typeof(Camera));
        var camera = go.GetComponent<Camera>();
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
            File.WriteAllBytes("Logs/V2-" + name + ".png", pixels.EncodeToPNG());
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
        checks++;
        if (!condition) throw new InvalidOperationException(message);
        File.AppendAllText(LogPath, "PASS " + message + "\n");
    }
}
