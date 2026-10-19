using System;
using System.IO;
using System.Linq;
using Interactables;
using Inventory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Real frame-driven poses plus existing domain transitions; V1 covers physical routes.
[InitializeOnLoad]
public static class V4Verification
{
    private const string Key = "V4.verify";
    private const string LogPath = "Logs/Codex-V4-verification.log";
    private static int checks, phase;
    private static double next, timeout;
    private static bool previousBackground;
    private static WorkshopSession session;
    private static FirstDealCharacter seller, a, b;
    private static Quaternion pausedPose;
    static V4Verification() => EditorApplication.playModeStateChanged += StateChanged;

    [MenuItem("Tools/V4/Verify Characters and Integration")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Stop Play Mode and save the scene first.");
        EditorSceneManager.OpenScene(FirstDealTradeBuilder.ScenePath);
        Directory.CreateDirectory("Logs");
        File.WriteAllText(LogPath, "V4 Unity " + Application.unityVersion + "\n");
        checks = 0;
        var colliders = UnityEngine.Object.FindObjectsByType<Collider>();
        var actions = UnityEngine.Object.FindObjectsByType<FirstDealAction>();
        var assets = AssetDatabase.FindAssets("", new[] { FirstDealCharacterBuilder.Folder }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p).ToArray();
        var guids = assets.Select(AssetDatabase.AssetPathToGUID).ToArray();
        FirstDealCharacterBuilder.ApplyCurrent();
        FirstDealCharacterBuilder.ApplyCurrent();
        Check(colliders.SequenceEqual(UnityEngine.Object.FindObjectsByType<Collider>()), "original collision surfaces and identities preserved");
        Check(actions.SequenceEqual(UnityEngine.Object.FindObjectsByType<FirstDealAction>()), "original action identities preserved");
        Check(assets.SequenceEqual(AssetDatabase.FindAssets("", new[] { FirstDealCharacterBuilder.Folder }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p)), "reapply creates no extra assets");
        Check(guids.SequenceEqual(assets.Select(AssetDatabase.AssetPathToGUID)), "reapply preserves prefab mesh and material GUIDs");
        Check(UnityEngine.Object.FindObjectsByType<Transform>().Count(t => t.name == FirstDealCharacterBuilder.RootName) == 1, "one V4 root");
        Check(!UnityEngine.Object.FindObjectsByType<Transform>().Any(t => t.name.StartsWith("Readable.") && t.name.EndsWith(".Trader")), "prototype capsules replaced");
        var characters = UnityEngine.Object.FindObjectsByType<FirstDealCharacter>();
        Check(characters.Length == 3, "exactly three characters");
        foreach (var character in characters)
        {
            var so = new SerializedObject(character);
            foreach (var field in new[] { "workshop", "head", "arm" })
                Check(so.FindProperty(field).objectReferenceValue != null, character.name + " bound " + field);
            Check(character.GetComponentsInChildren<Collider>().Length == 0, character.name + " has no interaction or movement blockers");
            Check(PrefabUtility.IsPartOfPrefabInstance(character), character.name + " reusable prefab");
            var renderers = character.GetComponentsInChildren<Renderer>();
            Check(renderers.Length <= 11, character.name + " bounded material batches");
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
                Check(renderer.sharedMaterial != null && renderer.sharedMaterial.shader.isSupported && !ShaderUtil.ShaderHasError(renderer.sharedMaterial.shader), character.name + " supported material");
                var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                Check(AssetDatabase.Contains(mesh) && mesh.triangles.Length > 0, character.name + " persisted mesh");
            }
            Check(bounds.min.y >= 0 && bounds.max.y >= 1.65f && bounds.max.y <= 1.85f, character.name + " grounded human scale");
            Check(bounds.max.z < -5, character.name + " stays behind counters and outside walking routes");
        }
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Capture("seller", new Vector3(1.9f, 1.65f, -3.7f), new Vector3(0, 1.15f, -6.3f));
        Capture("buyer-a", new Vector3(-5.5f, 1.65f, -2.8f), new Vector3(-7, 1.2f, -5.5f));
        Capture("buyer-b", new Vector3(5.9f, 1.65f, -2.7f), new Vector3(7.4f, 1.2f, -5.5f));
        SessionState.SetInt("V4.checks", checks);
        SessionState.SetBool(Key, true);
        EditorApplication.isPlaying = true;
    }

    private static void StateChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            checks = SessionState.GetInt("V4.checks", 0);
            phase = 0;
            timeout = EditorApplication.timeSinceStartup + 45;
            next = Time.timeAsDouble + 1;
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
        EditorApplication.QueuePlayerLoopUpdate();
        if (EditorApplication.timeSinceStartup >= timeout)
        {
            File.AppendAllText(LogPath, "V4 RESULT FAIL frame timeout at phase " + phase + "\n");
            Debug.LogError("V4 frame timeout at phase " + phase);
            EditorApplication.isPlaying = false;
            return;
        }
        // Reactions use game deltaTime; background Editor frames may lag wall time.
        if (Time.timeAsDouble < next) return;
        try
        {
            Check(EditorApplication.timeSinceStartup < timeout, "frame-driven verification within timeout");
            var wait = 0.22;
            switch (phase++)
            {
                case 0:
                    UnityEngine.Object.FindAnyObjectByType<FirstDealView>().CloseMenu();
                    session = UnityEngine.Object.FindAnyObjectByType<WorkshopView>().Session;
                    seller = GameObject.Find(FirstDealCharacterBuilder.RootName + "/Seller").GetComponent<FirstDealCharacter>();
                    a = GameObject.Find(FirstDealCharacterBuilder.RootName + "/BuyerA").GetComponent<FirstDealCharacter>();
                    b = GameObject.Find(FirstDealCharacterBuilder.RootName + "/BuyerB").GetComponent<FirstDealCharacter>();
                    Check(session.PurchaseLot(out _), "real lot purchase");
                    break;
                case 1:
                    Check(Angle(seller) > 2, "seller visibly nods after purchase");
                    Check(Angle(a) < 0.1f && Angle(b) < 0.1f, "unrelated buyers remain still");
                    session.SetPaused(true);
                    pausedPose = Head(seller).localRotation;
                    break;
                case 2:
                    Check(Quaternion.Angle(pausedPose, Head(seller).localRotation) < 0.01f, "pause freezes reaction");
                    session.SetPaused(false);
                    wait = 1.1;
                    break;
                case 3:
                    Check(Angle(seller) < 0.1f, "purchase reaction settles");
                    Check(session.BuyBattery(out _), "real battery purchase");
                    break;
                case 4:
                    Check(Angle(seller) > 2, "battery purchase also receives seller reaction");
                    session.StartShift();
                    Check(session.AcceptOrder(DealOrderId.A, out _), "accept A");
                    break;
                case 5:
                    Check(Angle(a) > 2, "A acknowledges acceptance");
                    Check(session.CloseOrder(DealOrderId.A, out _), "cancel A");
                    Check(session.AcceptOrder(DealOrderId.B, out _), "accept B");
                    break;
                case 6:
                    Check(Angle(b) > 2, "B acknowledges acceptance");
                    for (var i = 0; i < 3; i++)
                    {
                        Check(session.ToggleUnit(i, out _) && session.ToggleBench(out _) && session.Test(out _) &&
                            session.ToggleBench(out _) && session.ToggleUnit(i, out _), "test and return original lamp " + i);
                    }
                    Check(session.ToggleCrate(out _), "carry original crate");
                    Check(session.DeliverOrder(DealOrderId.B, new[] { session.Units[0].InstanceId }, out _), "B delivery");
                    break;
                case 7:
                    Check(Angle(b) > 2, "B acknowledges completed delivery and repeat availability");
                    Check(session.AcceptOrder(DealOrderId.RepeatB, out _) && session.DeliverOrder(DealOrderId.RepeatB,
                        new[] { session.Units[2].InstanceId }, out _), "repeat B delivery");
                    Check(!session.DeliverOrder(DealOrderId.RepeatB, new[] { session.Units[2].InstanceId }, out _) && session.Balance == 305,
                        "presentation preserves ledger and cannot repeat payout");
                    wait = 1.1;
                    break;
                case 8:
                    Check(Mathf.Abs(Angle(a) - 7) < 0.1f && Mathf.Abs(Angle(b) - 7) < 0.1f,
                        "closed buyers retain quiet resting poses; A=" + Angle(a) + " B=" + Angle(b) + " paused=" + session.IsPaused + " delta=" + Time.deltaTime);
                    Check(Angle(seller) < 0.1f, "other deals do not retrigger seller");
                    Check(session.SellBack(new[] { session.Units[1].InstanceId }, out _), "faulty-lamp buyback");
                    break;
                case 9:
                    Check(Angle(seller) > 2 && session.Balance == 340, "seller acknowledges buyback with unchanged economy");
                    seller.enabled = false;
                    Check(Angle(seller) < 0.1f, "disable restores authored pose");
                    seller.enabled = true;
                    session.AdvanceActiveSeconds(0.1);
                    wait = 1.1;
                    break;
                case 10:
                    Check(Angle(seller) < 0.1f, "reenable snapshots state without replay or duplicate subscription");
                    Check(session.EndShift(true, out _), "end actual shift");
                    pausedPose = Head(b).localRotation;
                    break;
                case 11:
                    Check(Quaternion.Angle(pausedPose, Head(b).localRotation) < 0.01f, "ended shift remains still");
                    File.AppendAllText(LogPath, "V4 RESULT PASS " + checks + " checks\n");
                    Debug.Log("V4 RESULT PASS " + checks + " checks");
                    EditorApplication.isPlaying = false;
                    break;
            }
            next = Time.timeAsDouble + wait;
        }
        catch (Exception e)
        {
            File.AppendAllText(LogPath, "V4 RESULT FAIL " + e + "\n");
            Debug.LogException(e);
            EditorApplication.isPlaying = false;
        }
    }

    private static Transform Head(FirstDealCharacter character) => character.transform.Find("Head pivot");
    private static float Angle(FirstDealCharacter character) => Quaternion.Angle(Quaternion.identity, Head(character).localRotation);

    private static void Capture(string name, Vector3 position, Vector3 target)
    {
        var go = new GameObject("V4 temporary camera", typeof(Camera));
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
            File.WriteAllBytes("Logs/V4-" + name + ".png", pixels.EncodeToPNG());
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
        if (!condition) throw new InvalidOperationException("V4 FAIL: " + message);
        checks++;
        File.AppendAllText(LogPath, "PASS " + message + "\n");
    }
}
