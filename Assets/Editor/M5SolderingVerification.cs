using System;
using System.IO;
using System.Linq;
using Inventory;
using Interactables;
using UnityEditor;
using UnityEngine;

public static partial class M5SolderingVerification
{
    private static int checks;
    private static readonly System.Text.StringBuilder report = new System.Text.StringBuilder();
    private static CityWorldData World() => new CityWorldData { PlacedLamps = Array.Empty<CityPlacedLamp>() };
    private static void Check(bool ok, string text)
    {
        report.AppendLine((ok ? "PASS " : "FAIL ") + text);
        if (!ok) throw new InvalidOperationException("M5: " + text);
        checks++;
    }
    private static void Must(bool ok) => Check(ok, "operation accepted");
    private static string Snapshot(WorkshopSession session) => JsonUtility.ToJson(session.CaptureCity(World()));
    private static WorkshopSession Round(WorkshopSession session)
    {
        var data = JsonUtility.FromJson<CitySaveData>(Snapshot(session));
        Must(WorkshopSession.TryRestoreCity(data, out var next, out _));
        Check(next.IsPaused && next.SolderingLamp?.InstanceId == session.SolderingLamp?.InstanceId, "restore pauses and preserves ID");
        if (next.SolderingLamp != null) Check(!next.SolderingLamp.IsPowered && next.SolderingState.Heat == 0, "loaded light off and iron cold");
        next.SetPaused(false);
        return next;
    }

    private static WorkshopSession Funded()
    {
        var session = WorkshopSession.CreateCity(19); session.SetPaused(false);
        Must(session.PurchaseLot(out _));
        for (var i = 0; i < 3; i++)
        {
            Must(session.ToggleUnit(i, out _)); Must(session.ToggleBench(out _)); Must(session.Test(out _));
            if (!session.OnBench.LastTestPassed)
            {
                Must(session.BuyBattery(out _)); Must(session.Repair(out _));
                Check(!session.OnBench.IsTested, "battery repair still needs retest"); Must(session.Test(out _));
            }
            Must(session.ToggleBench(out _)); Must(session.ToggleUnit(i, out _));
        }
        Must(session.ToggleCrate(out _)); Must(session.AcceptOrder(DealOrderId.A, out _));
        Must(session.DeliverOrder(DealOrderId.A, session.Units.Take(2).Select(lamp => lamp.InstanceId).ToArray(), out _));
        Must(session.AcceptOrder(DealOrderId.B, out _));
        Must(session.DeliverOrder(DealOrderId.B, new[] { session.Units[2].InstanceId }, out _));
        Must(session.ToggleCrate(out _));
        return session;
    }

    private static WorkshopSession Prepared(bool material = true)
    {
        var session = Funded();
        var before = Snapshot(session);
        Check(!session.PurchaseStation(false, out _) && Snapshot(session) == before, "blocked installation is atomic");
        Must(session.PurchaseStation(true, out _));
        before = Snapshot(session);
        Check(!session.PurchaseStation(true, out _) && Snapshot(session) == before, "station one purchase");
        Must(session.PurchaseSolderingLamp(out _));
        Check(!session.PurchaseSolderingLamp(out _), "lamp one purchase");
        if (material) Must(session.BuySolderDose(out _));
        Check(!session.CanStartSoldering(out _), "diagnosis required");
        Must(session.ToggleBench(out _)); Must(session.Test(out _));
        before = Snapshot(session);
        Check(!session.Repair(out _) && Snapshot(session) == before, "battery R cannot fix connection");
        Must(session.ToggleBench(out _)); Must(session.CanStartSoldering(out _));
        return session;
    }

    private static void Open(WorkshopSession session)
    {
        var r = session.RepairState;
        Must(r.TakeFlashlight()); Must(r.PlaceFlashlight());
        Must(r.TurnFastener(0)); Must(r.TurnFastener(1)); Must(r.LiftCover()); Must(r.PutCoverInTray());
    }

    private static void Solder(WorkshopSession session, float seconds)
    {
        var state = session.SolderingState;
        state.SelectTool(SolderingTrialState.Tool.Solder); state.Tick(.3f, true, true);
        state.SelectTool(SolderingTrialState.Tool.Iron); state.Tick(4, false, true); state.Tick(seconds, true, true);
        state.SelectTool(SolderingTrialState.Tool.None);
    }

    private static void Close(WorkshopSession session)
    {
        var r = session.RepairState;
        Must(r.TakeCover()); Must(r.ReplaceCover()); Must(r.TurnFastener(0)); Must(r.TurnFastener(1));
    }

    [MenuItem("Tools/M5/Verify Domain and Saves")]
    public static void VerifyDomain()
    {
        checks = 0; report.Clear();
        try
        {
            var initial = WorkshopSession.CreateCity(); initial.SetPaused(false);
            Round(initial);
            var emptyStation = Funded(); Must(emptyStation.PurchaseStation(true, out _)); Round(emptyStation);
            Check(!initial.PurchaseStation(true, out _), "station gated by first full sale");
            for (var version = 1; version <= 4; version++)
            {
                var old = initial.CaptureCity(World()); old.Version = version; old.Soldering = null;
                Must(WorkshopSession.TryRestoreCity(old, out var loaded, out _));
                Check(!loaded.StationOwned && loaded.SolderingLamp == null && loaded.CaptureCity(World()).Version == initial.CaptureCity(World()).Version, "v" + version + " upgrades empty extension");
            }
            for (var stage = 0; stage <= 12; stage++)
            {
                var s = Prepared(); var r = s.RepairState;
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
                r.Normalize();
                var copy = Round(s);
                Check(copy.SolderingState.Progress == progress && copy.SolderDoses == (stage >= 7 ? 0 : 1), "stage " + stage + " retains progress and exact dose");
                Check(copy.RepairState.Cover == r.Cover && copy.RepairState.IsSecured(0) == r.IsSecured(0) &&
                    copy.RepairState.IsSecured(1) == r.IsSecured(1), "stage " + stage + " retains disassembly");
                copy = Round(copy); Check(copy.SolderingLamp.InstanceId == s.SolderingLamp.InstanceId, "repeat load no duplicate ID");
            }
            var noDose = Prepared(false); Open(noDose); Solder(noDose, 4);
            Check(!noDose.SolderingState.HasSolder && noDose.SolderingState.Progress == 0, "no dose no repair");
            var s2 = Prepared(); Open(s2);
            s2.SolderingState.SelectTool(SolderingTrialState.Tool.Iron); s2.SolderingState.Tick(.2f, true, true);
            Check(s2.SolderingState.Progress == 0, "cold bare iron no repair");
            Solder(s2, 1); var partial = s2.SolderingState.Progress;
            Check(partial > 0 && partial < 1 && s2.SolderDoses == 0, "partial work consumes one dose");
            s2.SetPaused(true); s2.SolderingState.Tick(5, true, true);
            Check(s2.SolderingState.Progress == partial, "paused work cannot advance"); s2.SetPaused(false);
            Close(s2); Must(s2.RetrieveSolderingLamp(out _)); Must(s2.StowHeld(out _)); Must(s2.EquipSlot(3, out _));
            s2 = Round(s2); Check(s2.SolderingState.Progress == partial, "portable partial work persists");
            Open(s2); Solder(s2, 3); Check(s2.SolderDoses == 0 && !s2.SolderingReady, "repeat contact no dose and open repair not ready");
            Close(s2); Check(!s2.SolderingReady && !s2.Test(out _), "ordinary test cannot bypass final switch");
            Must(s2.RepairState.TogglePower()); Check(s2.SolderingReady, "manual assembly and switch ready");
            Must(s2.RepairState.TurnFastener(0)); Check(!s2.SolderingReady, "opening resets final test");
            Must(s2.RepairState.TurnFastener(0)); Must(s2.RepairState.TogglePower());
            Must(s2.RetrieveSolderingLamp(out _)); s2.RepairState.Normalize();
            Check(s2.SolderingReady, "leaving work preserves final test on portable lamp");
            Must(s2.AcceptOrder(DealOrderId.Solder, out _));
            var id = s2.SolderingLamp.InstanceId; var before = Snapshot(s2);
            Must(s2.ToggleUnit(3, out _)); before = Snapshot(s2);
            Check(!s2.DeliverSoldering(new[] { id }, out _) && Snapshot(s2) == before, "stored item cannot transfer remotely");
            Must(s2.ToggleUnit(3, out _)); before = Snapshot(s2);
            Check(!s2.DeliverSoldering(new[] { id, id }, out _) && Snapshot(s2) == before, "duplicate presentation atomic rejection");
            Check(!s2.DeliverOrder(DealOrderId.B, new[] { id }, out _), "new lamp cannot pay normal B");
            var wallet = s2.Balance; Must(s2.DeliverSoldering(new[] { id }, out _));
            Check(s2.Balance == wallet + 400, "one physical transfer pays $400");
            before = Snapshot(s2); Check(!s2.DeliverSoldering(new[] { id }, out _) && Snapshot(s2) == before, "repeat transfer cannot pay");
            s2 = Round(s2); Check(!s2.DeliverSoldering(new[] { id }, out _), "load cannot pay again");
            var good = s2.CaptureCity(World());
            Action<Action<CitySaveData>, string> corrupt = (mutate, text) =>
            {
                var data = JsonUtility.FromJson<CitySaveData>(JsonUtility.ToJson(good)); mutate(data);
                Check(!WorkshopSession.TryRestoreCity(data, out var rejected, out _) && rejected == null, text);
            };
            corrupt(d => d.Soldering.Lamp.InstanceId = d.Lamps[0].InstanceId, "duplicate global ID rejected");
            corrupt(d => d.Soldering.Doses++, "inconsistent material rejected");
            corrupt(d => d.Soldering.Progress = float.NaN, "nonfinite progress rejected");
            corrupt(d => d.Soldering.Secured0 = false, "impossible final test rejected");
            corrupt(d => d.Soldering.DeliveredId = "other", "payment ID mismatch rejected");
            corrupt(d => d.Soldering.Order.State = DealOrderState.Accepted, "sold item cannot revive order");
            corrupt(d => d.Soldering.Lamp.Location = FlashlightLocation.BuyerB, "new sale cannot impersonate B");
            var store = new CitySaveStore(Path.GetFullPath("Temp/M5-domain-save.json"));
            Must(store.TrySave(good, out _)); Must(store.TrySave(good, out _));
            Must(store.TryLoad(false, out _, out _, out _)); Must(store.TryLoad(true, out _, out _, out _));
            var timed = Prepared(); Must(timed.AcceptOrder(DealOrderId.Solder, out _)); timed.AdvanceActiveSeconds(1080);
            Check(timed.Order(DealOrderId.Solder).State == DealOrderState.Failed && !timed.DeliverSoldering(new[] { timed.SolderingLamp.InstanceId }, out _), "exact deadline expires");
            var cancelled = Prepared(); Must(cancelled.AcceptOrder(DealOrderId.Solder, out _)); Must(cancelled.CloseOrder(DealOrderId.Solder, out _));
            Check(!cancelled.AcceptOrder(DealOrderId.Solder, out _), "cancel is permanent"); Round(cancelled);
            var boughtBack = Prepared(); Must(boughtBack.SellBack(new[] { boughtBack.SolderingLamp.InstanceId }, out _));
            Check(boughtBack.Order(DealOrderId.Solder).State == DealOrderState.Unavailable, "buyback closes open craft order"); Round(boughtBack);
            var crateDelivery = Prepared(); Open(crateDelivery); Solder(crateDelivery, 3); Close(crateDelivery);
            Must(crateDelivery.RepairState.TogglePower()); Must(crateDelivery.RetrieveSolderingLamp(out _));
            Must(crateDelivery.ToggleUnit(3, out _)); Must(crateDelivery.ToggleCrate(out _));
            Must(crateDelivery.AcceptOrder(DealOrderId.Solder, out _));
            Must(crateDelivery.DeliverSoldering(new[] { crateDelivery.SolderingLamp.InstanceId }, out _)); Round(crateDelivery);
            report.AppendLine("DOMAIN PASS " + checks);
        }
        finally { Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/Codex-M5-domain.log", report.ToString()); }
        Debug.Log("M5 DOMAIN PASS " + checks + " — Logs/Codex-M5-domain.log");
    }
}
