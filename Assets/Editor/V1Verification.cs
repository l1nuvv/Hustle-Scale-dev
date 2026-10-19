using System;
using System.IO;
using System.Linq;
using Interactables;
using Inventory;
using Player.Movement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Geometry/integration check, not a substitute for observing a player.
[InitializeOnLoad]
public static class V1Verification
{
    private const string Key = "V1.verify";
    private const string LogPath = "Logs/Codex-V1-verification.log";
    private static CharacterController body;
    private static FirstDealView deal;
    private static WorkshopView workshop;
    private static int checks;
    private static bool previousBackground;
    private static double started;

    static V1Verification() => EditorApplication.playModeStateChanged += StateChanged;

    [MenuItem("Tools/V1/Verify Routes and Interactions")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Stop Play Mode and save the scene first.");
        EditorSceneManager.OpenScene(FirstDealTradeBuilder.ScenePath);
        Directory.CreateDirectory("Logs");
        File.WriteAllText(LogPath, "V1 Unity " + Application.unityVersion + "\n");
        SessionState.SetBool(Key, true);
        EditorApplication.isPlaying = true;
    }

    private static void StateChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            previousBackground = Application.runInBackground;
            Application.runInBackground = true;
            started = EditorApplication.timeSinceStartup;
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
        // Let Start initialize the real scene before controlling its fixture.
        if (Time.timeSinceLevelLoad < 0.5f && EditorApplication.timeSinceStartup - started < 20)
        { EditorApplication.QueuePlayerLoopUpdate(); return; }
        EditorApplication.update -= Verify;
        checks = 0;
        try
        {
            Check(Time.timeSinceLevelLoad >= 0.5f, "Play Mode initialized within 20 seconds");
            workshop = UnityEngine.Object.FindAnyObjectByType<WorkshopView>();
            deal = UnityEngine.Object.FindAnyObjectByType<FirstDealView>();
            var player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
            body = player.GetComponent<CharacterController>();
            deal.CloseMenu();
            // Exercise CharacterController collision directly in 0.1m steps; no frame-rate dependency.
            player.enabled = false;
            var s = workshop.Session;
            s.SetPaused(false);
            Check(body.height == 1.8f && body.radius == 0.3f, "actual 1.8m / 0.6m player metrics");
            Check(GameObject.Find(FirstDealEnvironmentBuilder.RootName).GetComponentsInChildren<Collider>().Length == 0,
                "new presentation adds no invisible interaction blockers");
            var left = GameObject.Find("Partition left").GetComponent<Collider>().bounds;
            var right = GameObject.Find("Partition right").GetComponent<Collider>().bounds;
            Check(Mathf.Abs(right.min.x - left.max.x - 2) < 0.001f, "2m clear opening");
            foreach (var name in new[] { "Step 0.2m", "Step 0.4m", "Collision crate" })
                Check(GameObject.Find(name) == null, "obsolete obstacle removed: " + name);
            Walk(-4, 4.5f); Hit("OFFERS BOARD", true); deal.Accept(0); deal.CloseMenu();
            Walk(0, 3); Walk(0, -3.1f); Walk(0, -4.4f);
            s.StartShift();
            Hit("SELLER |", true); deal.Purchase(false);
            Check(s.Balance == 120 && s.LotPurchased, "seller ray and real purchase");
            Walk(-1.8f, -4.1f); Hit("Crate visual", true);
            Check(s.IsCrateCarried, "crate ray picks up bought lot");
            Walk(0, -3.1f); Walk(0, 2); Walk(-2.5f, 1.8f);
            Hit("Carry / dock crate", true);
            Check(!s.IsCrateCarried && s.HasTransportedCrate, "carried crate crosses doorway and docks");
            for (var i = 0; i < 3; i++)
            {
                Hit("Crate selector " + i, true);
                Walk(0, 3.5f); Hit("Work bench", true); Hit("TEST", true);
                Check(s.OnBench.IsTested, "reachable bench/test for lamp " + i);
                Hit("REPLACE BATTERY", false);
                Hit("Work bench", true);
                Walk(-2.5f, 1.8f); Hit("Crate selector " + i, true);
            }
            // Reach every destination with a loose lamp as well as the complete crate.
            Hit("Crate selector 0", true);
            Walk(0, 2); Walk(0, -3.1f); Walk(6, -3.1f);
            Hit("B |", false); Walk(8, -3.1f); Hit("REPEAT B |", false);
            Walk(0, -3.1f); Walk(0, 2); Walk(-2.5f, 1.8f); Hit("Crate selector 0", true);
            Hit("Carry / dock crate", true);
            Walk(0, 2); Walk(0, -3.1f); Walk(-7, -3.2f); Hit("A |", true);
            Check(s.Balance == 380 && s.Order(DealOrderId.A).State == DealOrderState.Completed,
                "crate delivery to A through existing interaction");
            Hit("A |", true); Check(s.Balance == 380, "repeat interaction cannot pay twice");
            Walk(0, -3.1f); Walk(0, -4.4f); Hit("SELLER |", true); deal.SellBack(); deal.CloseMenu();
            Check(s.Balance == 415 && s.OwnedLampCount == 0, "A plus faulty-lamp buyback finishes at $415");
            File.AppendAllText(LogPath, "INFO " + SystemInfo.processorType + " | " + SystemInfo.graphicsDeviceName +
                " | Game View " + Screen.width + "x" + Screen.height + "\n");
            File.AppendAllText(LogPath, "V1 RESULT PASS " + checks + " checks\n");
            Debug.Log("V1 RESULT PASS " + checks + " checks");
        }
        catch (Exception e) { File.AppendAllText(LogPath, "V1 RESULT FAIL " + e + "\n"); Debug.LogException(e); }
        finally { EditorApplication.isPlaying = false; }
    }

    private static void Walk(float x, float z)
    {
        var target = new Vector3(x, body.transform.position.y, z);
        for (var step = 0; step < 500; step++)
        {
            var offset = target - body.transform.position; offset.y = 0;
            if (offset.magnitude < 0.025f) break;
            body.Move(Vector3.ClampMagnitude(offset, 0.1f) + Vector3.down * 0.02f);
            Camera.main.transform.position = body.transform.position + Vector3.up * 1.65f;
            Physics.SyncTransforms();
        }
        var remaining = new Vector2(body.transform.position.x - x, body.transform.position.z - z).magnitude;
        Check(remaining < 0.05f, "physical walk to " + x + ", " + z + " carrying=" + workshop.Session.IsCrateCarried);
    }

    private static void Hit(string prefix, bool interact)
    {
        var target = UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None)
            .First(c => c.enabled && c.name.StartsWith(prefix));
        var origin = body.transform.position + Vector3.up * 1.65f;
        Physics.SyncTransforms();
        Check(Physics.Raycast(origin, (target.bounds.center - origin).normalized, out var hit, 2.5f,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide) && hit.collider == target,
            "unoccluded 2.5m interaction ray: " + prefix);
        if (interact) target.GetComponentInParent<IWorldInteraction>().Interact();
    }

    private static void Check(bool pass, string message)
    {
        checks++;
        if (!pass) throw new Exception(message);
        File.AppendAllText(LogPath, "PASS " + message + "\n");
    }
}
