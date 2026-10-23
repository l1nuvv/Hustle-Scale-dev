using System;
using System.IO;
using System.Linq;
using Inventory;
using Interactables;
using UnityEditor;
using UnityEngine;

public static partial class M7SharpeningVerification
{
    private static int checks;
    private const string Log = "Logs/Codex-M7-domain.log";
    private static void Check(bool ok, string label)
    {
        File.AppendAllText(Log, (ok ? "PASS " : "FAIL ") + label + "\n");
        if (!ok) throw new InvalidOperationException(label);
        checks++;
    }
    private static void Must(bool ok) => Check(ok, "operation accepted");
    public static CityWorldData World() => new CityWorldData { PlacedLamps = Array.Empty<CityPlacedLamp>() };
    private static string Snapshot(WorkshopSession s) => JsonUtility.ToJson(s.CaptureCity(World()));
    private static WorkshopSession Round(WorkshopSession s)
    {
        Must(WorkshopSession.TryRestoreCity(JsonUtility.FromJson<CitySaveData>(Snapshot(s)), out var next, out _));
        Check(next.IsPaused && next.KnifeId == s.KnifeId && !next.SharpeningState.Running && !next.SharpeningOperating, "load preserves knife ID; paused, motor and work off");
        next.SetPaused(false); return next;
    }
    public static WorkshopSession Funded()
    {
        var seed = 0;
        for (; ; seed++) { var r = new System.Random(seed); if (r.Next(2) == 0 && r.Next(2) == 0 && r.Next(2) == 0) break; }
        var s = WorkshopSession.CreateCity(seed); s.SetPaused(false);
        for (var i = 0; i < 3; i++)
        {
            if (!s.PurchaseCityLamp(out _) || !s.EquipSlot(i, out _) || !s.ToggleBench(out _) || !s.Test(out _) ||
                !s.ToggleBench(out _) || !s.StowHeld(out _)) throw new InvalidOperationException("M7 fund route");
        }
        if (!s.AcceptOrder(DealOrderId.A, out _) || !s.DeliverOrder(DealOrderId.A, s.Units.Take(2).Select(u => u.InstanceId).ToArray(), out _) ||
            !s.AcceptOrder(DealOrderId.B, out _) || !s.DeliverOrder(DealOrderId.B, new[] { s.Units[2].InstanceId }, out _))
            throw new InvalidOperationException("M7 fund delivery");
        return s;
    }
    private static WorkshopSession Prepared()
    {
        var s = Funded();
        Must(s.PurchaseSharpeningStation(true, out _)); Must(s.PurchaseKnife(out _)); Must(s.EquipKnife(out _));
        Must(s.BeginSharpening(out _)); s.SharpeningState.Place(); s.SharpeningState.ToggleMotor(); return s;
    }
    public static void Pass(SharpeningTrialState state, float angle = 20, float depth = .012f, bool held = true)
    {
        for (var i = 0; i < 160; i++)
        {
            var x = -.2f + i * .0025f;
            var a = Quaternion.Euler(angle, 0, 0) * SharpeningTrialState.EdgePoint(Mathf.Clamp01(.5f - x / .28f));
            var b = Quaternion.Euler(angle, 0, 0) * SharpeningTrialState.EdgePoint(Mathf.Clamp01(.5f - (x + .0025f) / .28f));
            state.Sweep(new Vector3(x, 1.27f, depth - a.z), new Vector3(x + .0025f, 1.27f, depth - b.z), angle, .025f, held, 20, 5, .04f, .12f);
        }
    }
    public static void Finish(WorkshopSession s)
    {
        s.SharpeningState.Place(); if (!s.SharpeningState.Running) s.SharpeningState.ToggleMotor();
        Pass(s.SharpeningState); Pass(s.SharpeningState);
        if (!s.SharpeningState.Complete || !s.InspectKnife(true, out _)) throw new InvalidOperationException("M7 full operation");
        s.EndSharpening(); if (!s.MoveKnife(KnifeLocation.Hands, Vector3.zero, 0, out _)) throw new InvalidOperationException("M7 retrieve");
    }
    [MenuItem("Tools/M7/Verify Domain and Saves")]
    public static void Verify()
    {
        Directory.CreateDirectory("Logs"); File.WriteAllText(Log, "M7 Unity " + Application.unityVersion + " domain / saves\n"); checks = 0;
        var fresh = WorkshopSession.CreateCity(); fresh.SetPaused(false); var before = Snapshot(fresh);
        Check(!fresh.PurchaseSharpeningStation(true, out _) && !fresh.PurchaseKnife(out _) && Snapshot(fresh) == before, "locked purchases reject atomically");
        var s = Funded(); before = Snapshot(s);
        Check(s.Balance == 505 && s.CanBuySharpeningStation && !s.StationOwned, "first lot unlocks sharpening independently of soldering");
        Check(!s.PurchaseSharpeningStation(false, out _) && Snapshot(s) == before, "blocked place no debit");
        Must(s.BuyBattery(out _)); Must(s.BuyBattery(out _)); Must(s.BuyBattery(out _)); before = Snapshot(s);
        Check(!s.PurchaseSharpeningStation(true, out _) && Snapshot(s) == before, "insufficient station funds no debit");
        s = Funded(); Must(s.PurchaseSharpeningStation(true, out _));
        before = Snapshot(s); Check(!s.PurchaseSharpeningStation(true, out _) && Snapshot(s) == before, "station once");
        Must(s.BuyBattery(out _)); Must(s.BuyBattery(out _)); before = Snapshot(s);
        Check(!s.PurchaseKnife(out _) && Snapshot(s) == before, "insufficient knife funds no debit");
        s = Prepared(); before = Snapshot(s); Check(!s.PurchaseKnife(out _) && Snapshot(s) == before, "knife once");
        var id = s.KnifeId;
        Check(!s.Units.Any(u => u.InstanceId == id) && s.SolderingLamp == null, "separate knife ID and slots");
        var state = s.SharpeningState;
        Pass(state, 35); Pass(state, 20, -.10f); Pass(state, 20, .012f, false);
        var point = new Vector3(0, 1.27f, .012f);
        state.Sweep(point, point, 20, 1, true, 20, 5, .04f, .12f);
        Check(state.Progress == 0, "wrong angle, away, released, stationary: no progress");
        state.Sweep(point, point + Vector3.right * .01f, 20, .1f, true, 20, 5, .04f, .12f);
        Check(state.Progress > 0 && !state.Complete && !s.KnifeReady, "contact pass is partial and unchecked");
        Check(!s.InspectKnife(true, out _), "partial F refused");
        var coverage = state.CaptureCoverage(); var seed = s.KnifeCoatingSeed;
        s = Round(s); Check(s.SharpeningState.CaptureCoverage().SequenceEqual(coverage) && s.KnifeCoatingSeed == seed && s.KnifePlace == KnifeLocation.Station, "save during contact preserves coverage and coating");
        state = s.SharpeningState; state.ToggleMotor(); Pass(state);
        Check(state.CaptureCoverage().SequenceEqual(coverage), "no work outside operation"); state.StopMotor();
        Must(s.BeginSharpening(out _)); state.ToggleMotor(); s.SetPaused(true); Pass(state);
        Check(state.CaptureCoverage().SequenceEqual(coverage), "model pause blocks processing"); s.SetPaused(false);
        Pass(state); Pass(state); Check(state.Complete && !s.KnifeReady, "full edge still needs F");
        Check(!s.InspectKnife(false, out _) && !s.KnifeReady, "F at wheel refused"); Must(s.InspectKnife(true, out _));
        var balance = s.Balance; Must(s.InspectKnife(true, out _)); Check(s.Balance == balance, "repeat F no payment");
        s.EndSharpening(); Must(s.MoveKnife(KnifeLocation.Hands, Vector3.zero, 0, out _));
        before = Snapshot(s); Check(!s.ToggleCrate(out _) && !s.ToggleBench(out _) && !s.ToggleUnit(0, out _) && Snapshot(s) == before, "knife occupies shared hands and cannot enter flashlight bench/crate");
        Must(s.MoveKnife(KnifeLocation.World, new Vector3(-66, .5f, 30), 42, out _)); s = Round(s);
        Check(s.KnifePosition == new Vector3(-66, .5f, 30) && s.KnifeYaw == 42 && s.KnifeReady, "world pose and ready knife persist");
        Must(s.AcceptOrder(DealOrderId.Sharpen, out _)); before = Snapshot(s);
        Check(!s.DeliverKnife(new[] { id }, out _) && Snapshot(s) == before, "distant world knife cannot transfer");
        Must(s.MoveKnife(KnifeLocation.Hands, Vector3.zero, 0, out _));
        before = Snapshot(s); Check(!s.DeliverKnife(new[] { id, id }, out _) && !s.DeliverKnife(new[] { s.Units[0].InstanceId }, out _) && Snapshot(s) == before, "wrong/duplicate ID no mutation");
        Must(s.StowHeld(out _)); Must(s.DeliverKnife(new[] { id }, out _));
        Check(s.Balance == 565 && !s.KnifeOwned && s.Revenue - s.Expenses + 300 == s.Balance, "atomic $500 transfer; full investment profit $60");
        s = Round(s); before = Snapshot(s);
        Check(!s.DeliverKnife(new[] { id }, out _) && !s.SellKnife(id, out _) && !s.PurchaseKnife(out _) && Snapshot(s) == before, "reload cannot duplicate payout/purchase/buyback");
        var ready = s.CaptureCity(World());
        foreach (var version in Enumerable.Range(1, 6))
        {
            var old = WorkshopSession.CreateCity(1).CaptureCity(World()); old.Version = version; old.Sharpening = null;
            if (version < 5) old.Soldering = null;
            if (version < 3) old.Lamps[1].Condition = old.Lamps[1].InitialCondition = FlashlightCondition.BatteryFault;
            Check(WorkshopSession.TryRestoreCity(old, out var migrated, out _) && !migrated.KnifePurchased && !migrated.SharpeningStationOwned, "legacy v" + version + " empty sharpening migration");
        }
        Action<Action<CitySaveData>, string> corrupt = (change, label) =>
        {
            var data = JsonUtility.FromJson<CitySaveData>(JsonUtility.ToJson(ready)); change(data);
            Check(!WorkshopSession.TryRestoreCity(data, out var rejected, out _) && rejected == null, label);
        };
        corrupt(d => d.Sharpening.Coverage = new float[11], "reject coverage count");
        corrupt(d => d.Sharpening.Coverage[0] = float.NaN, "reject NaN");
        corrupt(d => d.Sharpening.Coverage[0] = 1.01f, "reject range");
        corrupt(d => d.Sharpening.Coverage[0] = .9f, "reject paid knife with incomplete edge");
        corrupt(d => d.Sharpening.InstanceId = d.Lamps[0].InstanceId, "reject colliding ID");
        corrupt(d => d.Sharpening.DeliveredId = Guid.NewGuid().ToString("N"), "reject wrong paid identity");
        corrupt(d => d.Sharpening.StationOwned = false, "reject knife without station");
        corrupt(d => d.Balance++, "reject money mismatch");
        corrupt(d => d.Sharpening.Order.DeadlineSeconds = double.PositiveInfinity, "reject infinite deadline");
        corrupt(d => d.Sharpening.Location = KnifeLocation.Hands, "reject paid owned knife");
        var store = new CitySaveStore("Temp/M7/domain-save.json"); Must(store.TrySave(ready, out _));
        Must(store.TryLoad(false, out _, out var loaded, out _)); Check(loaded.KnifeReady && !loaded.KnifeOwned, "disk roundtrip paid knife");
        foreach (var outcome in new[] { "deadline", "cancel", "decline", "unfinished", "ready" })
        {
            s = Prepared(); id = s.KnifeId;
            if (outcome != "decline") Must(s.AcceptOrder(DealOrderId.Sharpen, out _));
            if (outcome == "deadline")
            {
                s.AdvanceActiveSeconds(1080); Check(s.Order(DealOrderId.Sharpen).State == DealOrderState.Failed, "exact deadline closes order");
            }
            if (outcome == "cancel" || outcome == "decline") Must(s.CloseOrder(DealOrderId.Sharpen, out _));
            if (outcome != "unfinished") Finish(s);
            else { s.EndSharpening(); Must(s.MoveKnife(KnifeLocation.Hands, Vector3.zero, 0, out _)); }
            if (outcome == "deadline" || outcome == "cancel" || outcome == "decline") Check(!s.DeliverKnife(new[] { id }, out _), "closed order no payout: " + outcome);
            balance = s.Balance; var expected = s.KnifeReady ? 60 : 20;
            Must(s.SellKnife(id, out _)); Check(s.Balance == balance + expected && !s.KnifeOwned && !s.AcceptOrder(DealOrderId.Sharpen, out _), "buyback closes knife/order: " + outcome);
            s = Round(s); before = Snapshot(s); Check(!s.SellKnife(id, out _) && Snapshot(s) == before, "buyback only once after load: " + outcome);
        }
        s = Prepared(); s.EndSharpening(); Must(s.MoveKnife(KnifeLocation.Hands, Vector3.zero, 0, out _));
        Must(s.PurchaseCityLamp(out _));
        before = Snapshot(s);
        Check(!s.EquipSlot(0, out _) && Snapshot(s) == before, "knife blocks equipping second-lot flashlight without mutation");
        var occupied = s.CaptureCity(World()); occupied.Lamps[0].Location = FlashlightLocation.Hands;
        Check(!WorkshopSession.TryRestoreCity(occupied, out _, out _), "invalid multiple hands rejected");
        File.AppendAllText(Log, "PASS " + checks + " assertions\n"); Debug.Log("M7 domain PASS " + checks + " assertions; " + Log);
    }
}
