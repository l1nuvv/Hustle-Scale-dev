using System;
using System.IO;
using System.Reflection;
using Interactables;
using Inventory;
using Player;
using Player.Movement;
using UnityEditor;
using UnityEngine;

// Created only by this editor check; never serialized into a scene or shipped as content.
public sealed class PickupFeedbackProbe : MonoBehaviour, IWorldInteraction, IPickupTarget
{
    public bool Available = true;
    public Transform PickupTarget => Available ? transform : null;
    public string Prompt => "Verification pickup";
    public void Interact() { }
}

public static class PickupFeedbackVerification
{
    private static int checks;
    private static readonly System.Text.StringBuilder report = new System.Text.StringBuilder();
    private const string Log = "Logs/Codex-pickup-feedback.log";

    [MenuItem("Tools/Interaction/Verify Pickup Availability")]
    public static void Model()
    {
        Begin();
        try
        {
            var s = new WorkshopSession();
            var changes = 0;
            s.Changed += () => changes++;
            Check(s.CanPickUpCrate && s.CanPickUpUnit(0) && !s.CanPickUpBench, "initial crate/units available, empty bench unavailable");
            Check(!s.CanPickUpUnit(-1) && !s.CanPickUpPlaced(9), "invalid indices unavailable");
            for (var i = 0; i < 20; i++) { _ = s.CanPickUpCrate; _ = s.CanPickUpUnit(0); }
            Check(changes == 0 && s.InHands == null, "availability polling cannot mutate items or emit changes");
            Check(s.ToggleUnit(0, out _), "available unit pickup succeeds");
            Check(!s.CanPickUpCrate && !s.CanPickUpUnit(1), "occupied hands reject crate and stored unit");
            Check(s.ToggleBench(out _) && s.CanPickUpBench, "bench pickup available when hands free");
            Check(s.ToggleCrate(out _) && !s.CanPickUpBench && !s.CanPickUpUnit(1), "carried crate blocks lamp pickups");
            Check(s.ToggleCrate(out _) && s.ToggleBench(out _) && s.PlaceHeld(out _), "place a lamp in the world");
            Check(s.CanPickUpPlaced(0) && !s.CanPickUpUnit(0), "world lamp distinct from stored lamp");
            Check(s.ToggleUnit(1, out _) && s.CanPickUpPlaced(0), "placed lamp retains existing auto-stow swap rule");
            Check(s.PickUpPlaced(0, out _) && s.Units[1].Location == FlashlightLocation.Pocket, "allowed placed pickup performs existing swap");
            s.SetPaused(true);
            Check(!s.CanPickUpCrate && !s.CanPickUpBench && !s.CanPickUpPlaced(0), "pause removes pickup availability");
            var city = WorkshopSession.CreateCity(41);
            city.SetPaused(false);
            Check(!city.CanPickUpCrate && !city.CanPickUpUnit(0), "unowned seller lot is not a pickup");
            Check(city.PurchaseLot(out _) && city.CanPickUpCrate, "purchased city crate becomes available");
            Check(city.ToggleCrate(out _) && city.LoadVehicleCrate(out _) && city.CanUnloadCrate, "loaded owned crate can be unloaded");
            Check(!city.CanPickUpCrate && !city.CanPickUpUnit(0), "vehicle-loaded goods cannot be taken through crate interaction");
            city.SetPaused(true);
            Check(!city.CanUnloadCrate, "pause blocks cargo highlight too");
            Finish("MODEL");
        }
        catch (Exception error) { Fail(error); throw; }
    }

    [MenuItem("Tools/Interaction/Verify Pickup Focus In Sandbox Play Mode")]
    public static void World()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter DevelopmentSandbox Play Mode first.");
        var trial = UnityEngine.Object.FindAnyObjectByType<SolderingTrial>();
        if (trial == null) throw new InvalidOperationException("Open DevelopmentSandbox.");
        trial.Exit();
        var data = new SerializedObject(trial);
        var player = (PlayerController)data.FindProperty("player").objectReferenceValue;
        var deal = (FirstDealView)data.FindProperty("deal").objectReferenceValue;
        if (deal.MenuOpen) deal.CloseMenu();
        var saved = player.transform.position;
        var yaw = player.transform.eulerAngles.y;
        var pitch = player.CameraPitch;
        var feet = new Vector3(0, 0.05f, -80);
        var root = new GameObject("Pickup outline verification target");
        var shape = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var probe = root.AddComponent<PickupFeedbackProbe>();
        shape.transform.SetParent(root.transform, false);
        shape.transform.localScale = Vector3.one * 0.35f;
        root.transform.position = feet + Vector3.up * 1.65f + Vector3.forward * 2;
        var original = shape.GetComponent<Renderer>().sharedMaterial;
        GameObject wall = null;
        var ray = player.GetComponent<PlayerRaycast>();
        var refresh = typeof(PlayerRaycast).GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance);
        Begin();
        try
        {
            player.RestorePose(feet, 0, 0);
            player.SetCursorLocked(true);
            Physics.SyncTransforms();
            refresh.Invoke(ray, null);
            Check(ray.PickupFocus == root.transform, "child collider resolves the same parent pickup target");
            Check(shape.GetComponent<Renderer>().sharedMaterial == original, "outline leaves original material intact");
            var shader = Resources.Load<Shader>("PickupOutline");
            var mask = Resources.Load<Shader>("PickupOutlineMask");
            Check(shader != null && shader.isSupported && mask != null && mask.isSupported,
                "outline and silhouette mask shaders are included and supported in current URP");
            probe.Available = false;
            refresh.Invoke(ray, null);
            Check(ray.PickupFocus == null, "unavailable item cannot keep an outline");
            probe.Available = true;
            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = feet + Vector3.up * 1.5f + Vector3.forward;
            wall.transform.localScale = new Vector3(1, 3, 0.2f);
            Physics.SyncTransforms();
            refresh.Invoke(ray, null);
            Check(ray.PickupFocus == null, "nearest solid obstacle blocks pickup outline");
            UnityEngine.Object.DestroyImmediate(wall); wall = null;
            root.transform.position += Vector3.forward * 2;
            Physics.SyncTransforms();
            refresh.Invoke(ray, null);
            Check(ray.PickupFocus == null, "out of reach means no outline");
            root.transform.position -= Vector3.forward * 2;
            Physics.SyncTransforms();
            player.SetCursorLocked(false);
            refresh.Invoke(ray, null);
            Check(ray.PickupFocus == null, "released cursor/menu blocks focus");
            player.SetCursorLocked(true);
            refresh.Invoke(ray, null);
            Check(ray.PickupFocus == root.transform, "focus returns when gameplay resumes");
            ray.enabled = false;
            Check(ray.PickupFocus == null, "disabling ray adapter clears outline immediately");
            ray.enabled = true;
            UnityEngine.Object.DestroyImmediate(root); root = null;
            refresh.Invoke(ray, null);
            Check(ray.PickupFocus == null, "destroyed target cannot leave stale outline");
            Finish("WORLD PLAY MODE");
        }
        catch (Exception error) { Fail(error); throw; }
        finally
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            if (wall != null) UnityEngine.Object.DestroyImmediate(wall);
            player.RestorePose(saved, yaw, pitch);
        }
    }

    private static void Begin() { checks = 0; report.Clear(); }
    private static void Check(bool value, string label)
    {
        report.AppendLine((value ? "PASS " : "FAIL ") + label);
        if (!value) throw new InvalidOperationException(label);
        checks++;
    }
    private static void Finish(string mode)
    {
        report.AppendLine("PICKUP " + mode + ": PASS " + checks + "; Unity " + Application.unityVersion);
        Directory.CreateDirectory("Logs"); File.AppendAllText(Log, report.ToString());
        Debug.Log("PICKUP " + mode + ": PASS " + checks + "; " + Log);
    }
    private static void Fail(Exception error)
    {
        Directory.CreateDirectory("Logs"); File.AppendAllText(Log, report + error.ToString() + Environment.NewLine);
    }
}
