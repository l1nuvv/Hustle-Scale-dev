using System;
using System.IO;
using System.Reflection;
using Inventory;
using Interactables;
using Player;
using Player.Movement;
using UnityEditor;
using UnityEngine;

public static partial class M5SolderingVerification
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static object Part(SolderingTrial trial, string name) => typeof(SolderingTrial).GetField(name, Private).GetValue(trial);
    private static void Call(SolderingTrial trial, string name, params object[] args) => typeof(SolderingTrial).GetMethod(name, Private).Invoke(trial, args);

    [MenuItem("Tools/M5/Verify City Interruptions In Play Mode")]
    public static void VerifyInterruptions()
    {
        if (!EditorApplication.isPlaying || !SessionState.GetString("HustleScale.S2SaveOverride", "").Contains("M5"))
            throw new InvalidOperationException("M5 isolated CityGreybox Play Mode required.");
        report.Clear(); checks = 0;
        var save = UnityEngine.Object.FindAnyObjectByType<CitySaveController>();
        var trade = save.GetComponent<CityTradeView>(); var walk = save.GetComponent<CityGreyboxWalk>();
        var player = save.GetComponent<PlayerController>(); var trial = trade.SolderingStation;
        Action<WorkshopSession> bind = session =>
        {
            var world = World(); world.PlayerPosition = CityTradeView.StationPosition + new Vector3(0, .08f, -1.4f);
            typeof(CitySaveController).GetMethod("Bind", Private).Invoke(save, new object[] { session, world });
            player.RestorePose(world.PlayerPosition, 0, 26); walk.SetPaused(false); trial.Interact();
            Check(trial.IsActive && trial.Repair == session.RepairState && trial.State == session.SolderingState, "adapter binds authoritative state");
        };
        try
        {
            for (var stage = 0; stage <= 12; stage++)
            {
                var s = Prepared(); bind(s); var r = s.RepairState;
                if (stage >= 1) Must(r.TakeFlashlight());
                if (stage >= 2) Must(r.PlaceFlashlight());
                if (stage >= 3) Must(r.TurnFastener(0));
                if (stage >= 4) Must(r.TurnFastener(1));
                if (stage >= 5) Must(r.LiftCover());
                if (stage >= 6) Must(r.PutCoverInTray());
                if (stage >= 7) Solder(s, 1);
                if (stage >= 8) Solder(s, 3);
                if (stage >= 9) Must(r.TakeCover());
                if (stage >= 10) Must(r.ReplaceCover());
                if (stage >= 11) Must(r.TurnFastener(0));
                if (stage >= 12) Must(r.TurnFastener(1));
                var progress = s.SolderingState.Progress;
                Call(trial, "OnApplicationFocus", false);
                Check(s.SolderingState.Progress == progress && !s.SolderingState.Working, "stage " + stage + " focus interrupts work");
                Call(trial, "OnApplicationFocus", true);
                walk.SetPaused(true);
                Check(r.HandsFree && s.SolderingState.Progress == progress, "stage " + stage + " pause save normalizes only transient hand");
                var cover = r.Cover; var first = r.IsSecured(0); var second = r.IsSecured(1);
                var count = trial.GetComponentsInChildren<MeshRenderer>(true).Length;
                trial.Exit(); Must(save.Save());
                Must(save.Load(false));
                Check(trial.Repair == save.Session.RepairState && trial.State == save.Session.SolderingState &&
                    trial.Repair.Cover == cover && trial.Repair.IsSecured(0) == first && trial.Repair.IsSecured(1) == second &&
                    trial.State.Progress == progress && trial.State.Heat == 0, "stage " + stage + " actual save/load retains stable work");
                Check(!(bool)typeof(CitySaveController).GetField("dirty", Private).GetValue(save), "load does not request overwrite of loaded file");
                Must(save.Load(false)); Must(save.Load(true));
                Check(trial.GetComponentsInChildren<MeshRenderer>(true).Length == count &&
                    save.Session.SolderingLamp.InstanceId == s.SolderingLamp.InstanceId, "stage " + stage + " repeated primary/backup load no duplicate parts");
            }
            foreach (var motion in new[] { "TakeFlashlight", "PlaceFlashlight", "LiftCover", "PutCover", "TakeCover", "ReplaceCover" })
            {
                var s = Prepared(); bind(s); var r = s.RepairState;
                if (motion != "TakeFlashlight") Must(r.TakeFlashlight());
                if (motion != "TakeFlashlight" && motion != "PlaceFlashlight")
                {
                    Must(r.PlaceFlashlight()); Must(r.TurnFastener(0)); Must(r.TurnFastener(1));
                    if (motion != "LiftCover") Must(r.LiftCover());
                    if (motion == "TakeCover" || motion == "ReplaceCover") Must(r.PutCoverInTray());
                    if (motion == "ReplaceCover") Must(r.TakeCover());
                }
                Call(trial, "AdvanceTool", 0f, false, false);
                var part = (Transform)Part(trial, motion.Contains("Flashlight") ? "flashlight" : "cover");
                var kind = Enum.Parse(typeof(SolderingTrial).GetNestedType("RepairMotion", BindingFlags.NonPublic), motion);
                Call(trial, "StartRepairMotion", kind, part, part.localPosition + Vector3.up * .1f);
                Call(trial, "TickRepairMotion", .12f);
                Check(trial.RepairAnimating && !trial.CanAutosave, motion + " animation defers autosave");
                Must(save.Save());
                Check(!trial.RepairAnimating && trial.Repair.HandsFree, motion + " explicit save cancels pending transition");
                if (motion == "ReplaceCover" || motion == "PutCover" || motion == "TakeCover")
                    Check(trial.Repair.Cover == FlashlightRepairState.CoverPlace.Tray, motion + " hand normalizes to tray without auto assembly");
                if (motion == "LiftCover") Check(trial.Repair.Cover == FlashlightRepairState.CoverPlace.Attached, "unfinished lift retains source cover pose");
                Must(save.Load(false));
            }
            var final = Prepared(); bind(final); Open(final);
            Solder(final, 1); Close(final); Must(final.RetrieveSolderingLamp(out _)); trial.Exit();
            Must(final.PlaceHeld(out _));
            var lamp = trade.Lamp(3); lamp.SetParent(trade.transform.parent, true);
            lamp.SetPositionAndRotation(new Vector3(-68, .5f, 31), Quaternion.identity);
            Must(save.Save()); Must(save.Load(false));
            Check(save.Session.SolderingLamp.Location == FlashlightLocation.World && trade.Lamp(3).position == new Vector3(-68, .5f, 31), "new ID world pose round trip");
            var old = final.CaptureCity(World());
            old.World.PlacedLamps = new[] { new CityPlacedLamp { InstanceId = final.SolderingLamp.InstanceId, Position = new Vector3(-68, .5f, 31) } };
            old.Soldering.Lamp.InstanceId = old.Lamps[0].InstanceId;
            Check(!WorkshopSession.TryRestoreCity(old, out _, out _), "duplicate ID rejects whole candidate");
            report.AppendLine("INTERRUPTIONS PASS " + checks);
            Debug.Log("M5 INTERRUPTIONS PASS " + checks);
        }
        catch (Exception e) { report.AppendLine("INTERRUPTIONS FAIL " + e); throw; }
        finally { trial.Exit(); File.WriteAllText("Logs/Codex-M5-interruptions.log", report.ToString()); }
    }
}
