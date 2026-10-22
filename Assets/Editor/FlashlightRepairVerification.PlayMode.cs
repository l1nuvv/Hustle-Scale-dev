using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Interactables;
using Player.Movement;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static partial class FlashlightRepairVerification
{
    private static SolderingTrial M3() => UnityEngine.Object.FindObjectsByType<SolderingTrial>().Single(t => t.IsFullOperation);
    private static object Field(SolderingTrial t, string name) => typeof(SolderingTrial).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(t);
    private static void Call(SolderingTrial t, string name, params object[] args) => typeof(SolderingTrial).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(t, args);
    private static PlayerController Prepare(SolderingTrial t)
    {
        foreach (var trial in UnityEngine.Object.FindObjectsByType<SolderingTrial>()) trial.Exit();
        var player = (PlayerController)Field(t, "player");
        var deal = (FirstDealView)Field(t, "deal");
        if (deal.MenuOpen) deal.CloseMenu();
        player.RestorePose(t.transform.position + new Vector3(0, 0.05f, -1.6f), 0, 28);
        player.SetCursorLocked(true);
        return player;
    }

    [MenuItem("Tools/Development Sandbox/Verify M3 In Play Mode")]
    public static void VerifyPlayMode()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter DevelopmentSandbox Play Mode first.");
        Begin();
        var t = M3();
        try
        {
            Prepare(t); t.Interact(); t.ResetTrial();
            var r = t.Repair;
            var s = t.State;
            var cover = (Transform)Field(t, "cover");
            var flashlight = (Transform)Field(t, "flashlight");
            var count = t.GetComponentsInChildren<MeshRenderer>().Length;
            var aim = typeof(SolderingTrial).GetField("aim", BindingFlags.Instance | BindingFlags.NonPublic);
            Action<float, bool, bool> tick = (dt, pressing, operating) => Call(t, "AdvanceTool", dt, pressing, operating);
            Check(t.UsesAuthoredVisuals && cover.GetComponentsInChildren<MeshFilter>().Length >= 7, "authored textured cover, switch and captive fasteners loaded");
            var lidBounds = cover.GetComponentsInChildren<MeshRenderer>().Single(m => m.name == "M3 shaped removable blue lid").bounds;
            Check(lidBounds.size.x > 0.15f && lidBounds.size.z > 0.075f && lidBounds.center.y > 1.02f,
                "imported lid is full scale and physically covers the connection");
            Check(t.Instruction.Contains("сначала припой") && !t.BeamEnabled, "initial instruction explains solder first; broken lamp is dark");
            r.TakeFlashlight(); r.PlaceFlashlight(); Call(t, "SyncRepairParts");
            s.SelectTool(SolderingTrialState.Tool.Solder); aim.SetValue(t, Vector2.zero); tick(1, true, true);
            Check(!s.HasSolder && !t.HasContact, "closed body gates actual wire-end contact");
            s.SelectTool(SolderingTrialState.Tool.Iron); tick(4, false, true); tick(3, true, true);
            Check(s.Progress == 0 && !t.SmokePlaying && !t.SoundPlaying, "hot iron through closed body has no progress or work feedback");
            s.SelectTool(SolderingTrialState.Tool.None);
            r.TogglePower(); tick(0, false, true);
            Check(!t.BeamEnabled && !r.Complete, "real Light remains disabled for pre-repair switch test");
            Open(r); tick(0, false, true);
            Check(Vector3.Distance(cover.position, t.WorkMat.Find("CoverTray").position) < 0.001f && s.Accessible, "removed cover rests at mat tray anchor and joint opens");
            s.SelectTool(SolderingTrialState.Tool.Solder); tick(0.3f, true, true);
            Check(s.HasSolder && s.Progress == 0, "physical wire contact prepares single connection");
            s.SelectTool(SolderingTrialState.Tool.Iron); aim.SetValue(t, new Vector2(0.2f, 0)); tick(1, true, true);
            Check(!t.HasContact && s.Progress == 0, "lowering iron away from joint cannot solder");
            aim.SetValue(t, Vector2.zero); tick(0.5f, true, true);
            var partial = s.Progress;
            Check(partial > 0 && partial < 1 && t.SmokePlaying && t.SoundPlaying, "prepared hot actual contact runs smoke and audio");
            Call(t, "OnApplicationFocus", false);
            Check(s.Progress == partial && !t.HasContact && !t.SmokePlaying && !t.SoundPlaying, "focus loss callback immediately stops work without losing progress");
            Call(t, "OnApplicationFocus", true);
            aim.SetValue(t, new Vector2(0.2f, 0)); tick(1, true, true);
            Check(s.Progress == partial && !s.Working && !t.SmokePlaying && !t.SoundPlaying, "moving tip away stops work while button remains held");
            aim.SetValue(t, Vector2.zero); tick(0, false, false);
            Check(s.Progress == partial && !t.HasContact && !t.SmokePlaying && !t.SoundPlaying, "pause/focus interruption preserves partial work and stops effects");
            t.Exit(); t.Interact(); tick(0, false, true);
            Check(s.Progress == partial && r.Cover == FlashlightRepairState.CoverPlace.Tray && !r.LightOn,
                "exit/return preserves joint and tray without lighting");
            s.SelectTool(SolderingTrialState.Tool.Iron); tick(3, true, true);
            Check(s.Complete && !r.Complete && !t.BeamEnabled, "soldering alone cannot complete operation or light lamp");
            s.SelectTool(SolderingTrialState.Tool.None); Close(r); tick(0, false, true);
            Check(cover.position == flashlight.position && cover.parent == flashlight && !t.BeamEnabled && !r.Complete, "returning single lid and fastening still require test");
            r.TogglePower(); tick(0, false, true);
            var beam = (Light)Field(t, "flashlightBeam");
            var lens = (Material)Field(t, "lensMaterial");
            Check(t.BeamEnabled && beam.type == LightType.Spot && beam.intensity > 0 &&
                lens.GetColor("_EmissionColor").maxColorComponent > 1 && r.Complete, "actual spotlight and emissive lens confirm assembled working flashlight");
            t.Exit(); t.Interact(); tick(0, false, true);
            Check(t.BeamEnabled && r.CompletionCount == 1, "working result persists through exit/return once");
            r.TogglePower(); r.TogglePower(); tick(0, false, true);
            Check(r.CompletionCount == 1 && s.CompletionCount == 1 && t.BeamEnabled, "repeated testing does not repeat completion");
            for (var stage = 0; stage < 6; stage++)
            {
                t.ResetTrial();
                if (stage >= 1) r.TakeFlashlight();
                if (stage >= 2) r.PlaceFlashlight();
                if (stage >= 3) { r.TurnFastener(0); r.TurnFastener(1); r.LiftCover(); }
                if (stage >= 4) r.PutCoverInTray();
                if (stage >= 5) { r.TakeCover(); r.ReplaceCover(); }
                tick(0, false, true);
                var place = r.Flashlight; var lidPlace = r.Cover;
                t.Exit(); t.Interact(); tick(0, false, true);
                Check(r.Flashlight == place && r.Cover == lidPlace && t.GetComponentsInChildren<MeshRenderer>().Length == count,
                    "exit/return preserves single item and cover at stage " + stage);
                t.ResetTrial();
                Check(!s.Accessible && !t.BeamEnabled && !r.Complete && r.IsSecured(0) && r.IsSecured(1) &&
                    flashlight.position == t.WorkMat.Find("Supply").position && cover.position == flashlight.position && cover.parent == flashlight,
                    "reset restores visible closed broken supply at stage " + stage);
            }
            t.Exit();
            Finish("PLAY MODE (geometry and lifecycle adapter)");
        }
        catch (Exception e) { t.Exit(); Fail(e); throw; }
    }

    [MenuItem("Tools/Development Sandbox/Verify M3 Placement Previews In Play Mode")]
    public static void VerifyPlacementPreviews()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Sandbox Play Mode first.");
        Begin();
        var t = M3();
        var background = Application.runInBackground;
        Application.runInBackground = true;
        try
        {
            Prepare(t); t.Interact(); t.ResetTrial();
            var meshCount = t.GetComponentsInChildren<MeshFilter>().Length;
            Action refresh = () => Call(t, "AdvanceTool", 0f, false, true);
            Action clearHover = () => typeof(SolderingTrial).GetField("repairHover", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(t, SolderingTrial.RepairTarget.None);
            Check(!t.PlacementPreviewVisible, "empty hand has no placement ghost");
            t.Repair.TakeFlashlight(); refresh();
            Check(t.PlacementPreviewVisible && t.PlacementPreviewTarget == SolderingTrial.RepairTarget.WorkMat &&
                Vector3.Distance(t.PlacementPreviewPosition, t.WorkMat.Find("RepairOrigin").position) < .001f,
                "held device previews exact central destination before hovering");
            var material = (Material)Field(t, "ghostFill");
            Check(material.GetColor("_BaseColor").a > 0 && material.GetColor("_BaseColor").a < .3f && material.GetFloat("_ZWrite") == 0,
                "preview is translucent and cannot write opaque depth");
            t.Repair.PlaceFlashlight(); Open(t.Repair); t.Repair.TakeCover(); t.Repair.ReplaceCover();
            t.Repair.LiftCover(); clearHover(); refresh();
            Check(t.PlacementPreviewVisible && t.PlacementPreviewTarget == SolderingTrial.RepairTarget.Tray &&
                Vector3.Distance(t.PlacementPreviewPosition, t.WorkMat.Find("CoverTray").position) < .001f && !t.PlacementReady,
                "newly removed lid always reveals its tray silhouette");
            Check(t.AlternatePlacementVisible, "both valid lid destinations remain visible while cursor is over source");
            typeof(SolderingTrial).GetField("repairHover", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(t, SolderingTrial.RepairTarget.Tray);
            refresh();
            Check(t.PlacementReady && material.GetColor("_BaseColor").g > material.GetColor("_BaseColor").b,
                "hovering valid destination turns translucent preview green");
            t.Repair.PutCoverInTray(); clearHover(); refresh();
            Check(!t.PlacementPreviewVisible, "placed lid removes ghost");
            t.State.SelectTool(SolderingTrialState.Tool.Iron); refresh();
            Check(t.PlacementPreviewVisible && t.PlacementPreviewPosition == t.ToolHomePosition(SolderingTrialState.Tool.Iron),
                "held iron previews its own return dock");
            t.State.SelectTool(SolderingTrialState.Tool.Solder); refresh();
            Check(t.PlacementPreviewVisible && t.PlacementPreviewPosition == t.ToolHomePosition(SolderingTrialState.Tool.Solder),
                "held solder previews its own return dock");
            t.State.SelectTool(SolderingTrialState.Tool.None); t.Repair.TakeCover(); clearHover(); refresh();
            Check(t.Repair.ReturningCover && t.PlacementPreviewTarget == SolderingTrial.RepairTarget.WorkMat,
                "lid picked up from tray previews reassembly instead of tray");
            t.Exit(); Check(!t.PlacementPreviewVisible, "leaving clears ghost");
            t.Interact(); refresh();
            Check(t.PlacementPreviewVisible && t.PlacementPreviewTarget == SolderingTrial.RepairTarget.WorkMat, "return keeps held lid intent and destination");
            typeof(SolderingTrial).GetField("workPaused", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(t, true);
            refresh(); Check(!t.PlacementPreviewVisible, "pause hides placement feedback");
            typeof(SolderingTrial).GetField("workPaused", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(t, false);
            Call(t, "OnApplicationFocus", false); Check(!t.PlacementPreviewVisible, "focus loss hides placement feedback");
            Call(t, "OnApplicationFocus", true); refresh();
            Check(t.PlacementPreviewVisible && t.GetComponentsInChildren<MeshFilter>().Length == meshCount,
                "ghosts create no extra item geometry, colliders or lights in hierarchy");
            t.ResetTrial(); Check(!t.PlacementPreviewVisible && !t.Repair.ReturningCover, "reset clears ghost and lid intent");
            Finish("PLACEMENT PREVIEWS");
        }
        catch (Exception e) { Fail(e); throw; }
        finally { t.Exit(); Application.runInBackground = background; }
    }

    [MenuItem("Tools/Development Sandbox/Verify M3 Controls In Play Mode")]
    public static void VerifyControls()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isPaused) throw new InvalidOperationException("Run unpaused DevelopmentSandbox first.");
        Begin();
        var t = M3();
        var player = Prepare(t);
        t.ResetTrial();
        var keyboard = InputSystem.AddDevice<Keyboard>();
        var mouse = InputSystem.AddDevice<Mouse>();
        keyboard.MakeCurrent(); mouse.MakeCurrent();
        var background = Application.runInBackground;
        Application.runInBackground = true;
        var steps = new List<(Action action, double delay)>();
        Action<Key?> key = k => InputSystem.QueueStateEvent(keyboard, k.HasValue ? new KeyboardState(k.Value) : new KeyboardState());
        Action<bool> click = down => InputSystem.QueueStateEvent(mouse, new MouseState { buttons = (ushort)(down ? 1 : 0) });
        Action<Vector2> guide = point => InputSystem.QueueDeltaStateEvent(mouse.delta, point - t.CursorScreenPosition);
        Action<SolderingTrial.RepairTarget> aimAt = target => guide(t.RepairScreenPosition(target));
        Action<Action, double> add = (action, delay) => steps.Add((action, delay));
        Action<SolderingTrial.RepairTarget> takePart = target =>
        {
            add(() => aimAt(target), 0.15);
            add(() =>
            {
                Check(t.HoveredRepairTarget == target, "cursor reaches " + target);
                var outline = t.GetComponent<PickupOutline>().Target;
                var pickup = target == SolderingTrial.RepairTarget.Flashlight || target == SolderingTrial.RepairTarget.Cover;
                Check(pickup ? outline == (Transform)Field(t, target == SolderingTrial.RepairTarget.Flashlight ? "flashlight" : "cover") : outline == null,
                    "pickup-only outline agrees with current action " + target);
                key(Key.E);
            }, 0.10);
            add(() => { Check(t.RepairAnimating && !t.PlacementPreviewVisible, "E starts short part transfer and hides preview " + target); key(null); }, 0.50);
        };
        Action<SolderingTrial.RepairTarget> turn = target =>
        {
            add(() => aimAt(target), 0.15);
            add(() => { Check(t.HoveredRepairTarget == target && t.GetComponent<PickupOutline>().Target == null, "captive fastener has action feedback without pickup outline " + target); click(true); }, 0.75);
            add(() => click(false), 0.15);
        };
        Action exitReturn = () =>
        {
            add(() => key(Key.Escape), 0.20);
            add(() => { Check(!t.Active && player.CanInteract, "Esc restores ordinary controls"); key(null); }, 0.20);
            add(() => key(Key.E), 0.20);
            add(() => { Check(t.Active, "world E returns to M3"); key(null); }, 0.20);
        };
        add(() => key(Key.E), 0.25);
        add(() => { Check(t.Active && t.Instruction.Contains("сначала припой"), "world E enters M3 and explains solder first"); key(null); }, 0.15);
        takePart(SolderingTrial.RepairTarget.Flashlight);
        add(() => Check(t.Repair.Flashlight == FlashlightRepairState.ItemPlace.Held, "closed flashlight is held before placement"), 0.1);
        add(() => key(Key.P), 0.15);
        add(() => { Check(t.WorkPaused, "P pauses with flashlight in hand"); key(null); aimAt(SolderingTrial.RepairTarget.WorkMat); }, 0.15);
        add(() => key(Key.E), 0.15);
        add(() => { Check(!t.RepairAnimating && t.Repair.Flashlight == FlashlightRepairState.ItemPlace.Held, "E cannot place item during pause"); key(Key.P); }, 0.15);
        add(() => key(null), 0.15);
        exitReturn();
        takePart(SolderingTrial.RepairTarget.WorkMat);
        add(() => Check(t.Repair.Assembled && !t.BeamEnabled, "closed broken flashlight placed on mat"), 0.1);
        add(() => aimAt(SolderingTrial.RepairTarget.Power), 0.15);
        add(() => click(true), 0.15);
        add(() => { Check(t.Repair.Tested && !t.BeamEnabled && !t.Repair.Complete, "switch before repair gives no working light"); click(false); }, 0.15);
        // Partial fastener release must not remove the lid or persist an unfinished turn.
        add(() => aimAt(SolderingTrial.RepairTarget.Fastener0), 0.15);
        add(() => click(true), 0.20);
        add(() => click(false), 0.15);
        add(() => Check(t.Repair.IsSecured(0), "interrupted fastener remains secured"), 0.1);
        turn(SolderingTrial.RepairTarget.Fastener0);
        add(() => Check(!t.Repair.IsSecured(0) && t.Repair.IsSecured(1) && !t.State.Accessible, "one fastener leaves body closed"), 0.1);
        turn(SolderingTrial.RepairTarget.Fastener1);
        takePart(SolderingTrial.RepairTarget.Cover);
        add(() => Check(t.Repair.Cover == FlashlightRepairState.CoverPlace.Held && !t.State.Accessible, "cover held until explicit placement"), 0.1);
        exitReturn();
        takePart(SolderingTrial.RepairTarget.Tray);
        add(() => Check(t.Repair.CanUseTools && t.State.Accessible, "lid in visible tray opens tool access"), 0.1);
        add(() => guide(t.ToolScreenPosition(SolderingTrialState.Tool.Solder)), 0.15);
        add(() => { Check(t.HoveredTool == SolderingTrialState.Tool.Solder, "M2 spool pickup remains available with cover removed"); key(Key.E); }, 0.1);
        add(() => { key(null); guide(t.JointScreenPosition); }, 0.4);
        add(() => click(true), 0.5);
        add(() => { Check(t.State.HasSolder && t.State.Progress == 0, "LMB prepares joint with actual wire contact"); click(false); key(Key.E); }, 0.4);
        add(() => { key(null); guide(t.ToolScreenPosition(SolderingTrialState.Tool.Iron)); }, 0.15);
        add(() => { Check(t.HoveredTool == SolderingTrialState.Tool.Iron, "M2 iron pickup remains available"); key(Key.E); }, 0.1);
        add(() => { key(null); guide(t.JointScreenPosition); }, 0.4);
        float pausedHeat = 0;
        add(() => key(Key.P), 0.15);
        add(() => { Check(t.WorkPaused, "P pauses during heat-up"); pausedHeat = t.State.Heat; key(null); }, 0.5);
        add(() => { Check(t.State.Heat == pausedHeat && !t.State.Ready, "paused iron does not heat"); key(Key.P); }, 0.15);
        add(() => key(null), 0.15);
        add(() => { Check(!t.State.Ready, "newly picked iron is cold"); click(true); }, 0.5);
        add(() => { Check(t.State.Progress == 0, "cold contact gives no solder progress"); click(false); }, 4.2);
        float partial = 0, heat = 0;
        add(() => { Check(t.State.Ready, "iron heats with real game frames"); click(true); }, 0.8);
        add(() => { Check(t.State.Working && t.SmokePlaying && t.SoundPlaying, "prepared hot contact starts M2 effects"); guide(t.JointScreenPosition + Vector2.right * 150); }, 0.2);
        add(() => { partial = t.State.Progress; Check(!t.State.Working && !t.HasContact, "moving actual tip away stops work with LMB held"); }, 0.3);
        add(() => { Check(t.State.Progress == partial, "withdrawn iron cannot advance"); guide(t.JointScreenPosition); }, 0.4);
        add(() => { partial = t.State.Progress; heat = t.State.Heat; Time.timeScale = 0; }, 0.4);
        add(() => { Check(t.State.Progress == partial && t.State.Heat == heat && !t.SmokePlaying && !t.SoundPlaying, "game pause stops progress, heat and effects"); click(false); Time.timeScale = 1; }, 0.15);
        exitReturn();
        add(() => Check(t.State.Progress == partial && t.Repair.Cover == FlashlightRepairState.CoverPlace.Tray, "return preserves partial solder and removed lid"), 0.1);
        add(() => guide(t.ToolScreenPosition(SolderingTrialState.Tool.Iron)), 0.15);
        add(() => key(Key.E), 0.1);
        add(() => { key(null); guide(t.JointScreenPosition); }, 0.4);
        add(() => click(true), 3.0);
        add(() => { Check(t.State.Complete && !t.Repair.Complete && !t.BeamEnabled, "solder completion alone is not operation success"); click(false); key(Key.E); }, 0.4);
        add(() => key(null), 0.15);
        takePart(SolderingTrial.RepairTarget.Cover);
        takePart(SolderingTrial.RepairTarget.WorkMat);
        add(() => Check(!t.Repair.Assembled && !t.State.Accessible && !t.BeamEnabled, "cover returned but still needs fasteners"), 0.1);
        turn(SolderingTrial.RepairTarget.Fastener0);
        turn(SolderingTrial.RepairTarget.Fastener1);
        add(() => { Check(t.Repair.Assembled && !t.Repair.Complete, "manual fastening completes assembly without auto-success"); aimAt(SolderingTrial.RepairTarget.Power); }, 0.15);
        add(() => click(true), 0.25);
        add(() => { Check(t.Repair.Complete && t.BeamEnabled && t.Repair.CompletionCount == 1, "assembled repaired flashlight really lights after click"); click(false); }, 0.15);
        exitReturn();
        add(() => { Check(t.BeamEnabled && t.Repair.CompletionCount == 1, "working light survives return"); key(Key.R); }, 0.2);
        add(() =>
        {
            Check(!t.BeamEnabled && !t.State.HasSolder && !t.State.Powered && !t.State.Accessible &&
            t.Repair.Flashlight == FlashlightRepairState.ItemPlace.Supply && t.Repair.IsSecured(0) && t.Repair.IsSecured(1), "R resets entire operation to closed broken supply"); key(null);
        }, 0.15);
        // A second pickup followed by a mid-animation R must not duplicate a part.
        add(() => aimAt(SolderingTrial.RepairTarget.Flashlight), 0.15);
        add(() => key(Key.E), 0.1);
        Vector3 pausedPosition = Vector3.zero;
        add(() => { Check(t.RepairAnimating, "second sequence starts after R"); key(Key.P); }, 0.15);
        add(() => { Check(t.WorkPaused && t.RepairAnimating, "P freezes in-flight part transfer"); pausedPosition = ((Transform)Field(t, "flashlight")).localPosition; key(null); }, 0.3);
        add(() => { Check(((Transform)Field(t, "flashlight")).localPosition == pausedPosition, "paused transfer holds exact part pose"); key(Key.R); }, 0.2);
        add(() => { Check(!t.WorkPaused && !t.RepairAnimating && t.Repair.Flashlight == FlashlightRepairState.ItemPlace.Supply, "R during paused transfer cancels pending transition and unpauses"); key(null); }, 0.1);
        var index = 0;
        var frame = Time.frameCount;
        var next = EditorApplication.timeSinceStartup;
        var deadline = next + 100;
        EditorApplication.CallbackFunction runner = null;
        Action cleanup = () =>
        {
            EditorApplication.update -= runner;
            Time.timeScale = 1;
            InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse);
            Application.runInBackground = background;
            if (t != null) t.Exit();
        };
        runner = () =>
        {
            try
            {
                if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup > deadline)
                    throw new InvalidOperationException("M3 input route stopped/timed out at step " + index);
                if (Time.frameCount < frame + 3 || EditorApplication.timeSinceStartup < next) return;
                if (index == steps.Count) { cleanup(); Finish("INPUT SYSTEM"); return; }
                var step = steps[index++]; step.action(); frame = Time.frameCount;
                next = EditorApplication.timeSinceStartup + step.delay;
            }
            catch (Exception e) { cleanup(); Fail(e); Debug.LogException(e); }
        };
        EditorApplication.update += runner;
    }
}
