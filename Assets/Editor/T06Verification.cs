using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Inventory;
using UnityEditor;
using UnityEngine;

// Deterministic domain checks complement the physical/input fixtures in T02-T05.
// This is Editor automation, not a substitute for an independent player playtest.
public static class T06Verification
{
    private const string LogPath = "Logs/Codex-T06-verification.log";
    private static int checks;
    private static readonly DealOrderId[] OrderIds = { DealOrderId.A, DealOrderId.B, DealOrderId.RepeatB };

    [MenuItem("Tools/T06/Verify Invariants and Routes")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode first.");
        Directory.CreateDirectory("Logs");
        checks = 0;
        File.WriteAllText(LogPath, "T06 Unity " + Application.unityVersion + " UTC " + DateTime.UtcNow.ToString("O") + "\n");
        try
        {
            VerifyRoutes();
            for (var seed = 0; seed < 64; seed++) VerifySequence(seed);
            File.AppendAllText(LogPath, "T06 RESULT PASS: " + checks + " assertions; 64 seeds x 256 actions\n");
            Debug.Log("T06 RESULT PASS: " + checks + " assertions");
        }
        catch (Exception e)
        {
            File.AppendAllText(LogPath, "T06 RESULT FAIL: " + e + "\n");
            throw;
        }
    }

    private static void Check(bool ok, string context)
    {
        checks++;
        if (!ok) throw new Exception(context);
    }

    private static string Snapshot(WorkshopSession s) => string.Join("|", s.InitialBalance, s.Balance,
        s.Revenue, s.Expenses, s.ElapsedSeconds.ToString("R", CultureInfo.InvariantCulture), s.RepairSeconds,
        s.SpareBatteries, s.IsFirstDeal, s.LotPurchased, s.HasTransportedCrate, s.ShiftStarted, s.IsPaused, s.ShiftEnded,
        s.IsCrateCarried, string.Join(";", s.Units.Select(u => string.Join(":", u.InstanceId, u.Location,
            u.Condition, u.IsTested, u.LastTestPassed))), string.Join(";", OrderIds.Select(id => s.Order(id).State)));

    private static void Attempt(WorkshopSession s, Func<bool> action, string context)
    {
        var before = Snapshot(s);
        var events = 0;
        Action changed = () => events++;
        s.Changed += changed;
        bool result;
        try { result = action(); }
        finally { s.Changed -= changed; }
        Check(result ? events == 1 : events == 0 && before == Snapshot(s), context + " atomicity/event count");
    }

    private static void Invariants(WorkshopSession s, string[] identities, string context)
    {
        Check(s.Balance >= 0 && (long)s.InitialBalance + s.Revenue - s.Expenses == s.Balance,
            context + " wallet/ledger conservation");
        Check(s.Revenue >= 0 && s.Expenses >= 0 && s.SpareBatteries >= 0, context + " nonnegative resources");
        Check(s.Units.Count == 3 && s.Units.Select(u => u.InstanceId).SequenceEqual(identities) &&
            identities.Distinct().Count() == 3, context + " stable distinct identities");
        Check(s.Units.Count(u => u.Location == FlashlightLocation.Hands) <= 1 &&
            s.Units.Count(u => u.Location == FlashlightLocation.Bench) <= 1 &&
            (!s.IsCrateCarried || s.InHands == null), context + " hands/bench capacity");
        Check(s.Units.All(u => !u.LastTestPassed || u.IsTested && u.Condition == FlashlightCondition.Working),
            context + " diagnosis consistency");
        Check(s.RepairSeconds % 240 == 0 && s.RepairSeconds <= 240 && s.ElapsedSeconds >= s.RepairSeconds,
            context + " single faulty lamp repair/time");
        Check(!s.ShiftEnded || s.IsPaused, context + " finished shift frozen");
        Check(s.OwnedLampCount + s.Units.Count(u => u.Location == FlashlightLocation.Seller ||
            u.Location == FlashlightLocation.BuyerA || u.Location == FlashlightLocation.BuyerB ||
            u.Location == FlashlightLocation.BoughtBack) == 3, context + " stock conservation");
        foreach (var id in OrderIds)
        {
            var order = s.Order(id);
            Check(order.State != DealOrderState.Accepted || s.ElapsedSeconds < order.DeadlineSeconds,
                context + " active order deadline " + id);
            if (order.State == DealOrderState.Completed)
                Check(s.Units.Count(u => u.Location == (id == DealOrderId.A ? FlashlightLocation.BuyerA :
                    FlashlightLocation.BuyerB)) >= order.Quantity, context + " completed order owns goods " + id);
        }
    }

    private static void VerifySequence(int seed)
    {
        var random = new System.Random(seed);
        var s = WorkshopSession.CreateFirstDeal();
        var identities = s.Units.Select(u => u.InstanceId).ToArray();
        if (seed % 2 == 0)
        {
            s.PurchaseLot(out _);
            for (var i = 0; i < 3; i++) Diagnose(s, i, seed % 4 == 0 && i == 1);
        }
        s.StartShift();
        for (var step = 0; step < 256; step++)
        {
            var context = "seed " + seed + " step " + step;
            var oldLocations = s.Units.Select(u => u.Location).ToArray();
            var oldOrders = OrderIds.Select(id => s.Order(id).State).ToArray();
            var id = OrderIds[random.Next(3)];
            var index = random.Next(-1, 4);
            var presented = random.Next(7);
            string[] ids = presented == 0 ? null : presented == 1 ? Array.Empty<string>() :
                presented == 2 ? new[] { identities[0], identities[0] } :
                presented == 3 ? new[] { identities[0], "unknown" } :
                presented == 4 ? identities : presented == 5 ? new[] { identities[0], identities[2] } :
                new[] { identities[random.Next(3)] };
            switch (random.Next(16))
            {
                case 0: Attempt(s, () => s.PurchaseLot(out _), context); break;
                case 1: Attempt(s, () => s.ToggleUnit(index, out _), context); break;
                case 2: Attempt(s, () => s.ToggleCrate(out _), context); break;
                case 3: Attempt(s, () => s.ToggleBench(out _), context); break;
                case 4: Attempt(s, () => s.Test(out _), context); break;
                case 5: Attempt(s, () => s.BuyBattery(out _), context); break;
                case 6: Attempt(s, () => s.Repair(out _), context); break;
                case 7: Attempt(s, () => s.AcceptOrder(id, out _), context); break;
                case 8: Attempt(s, () => s.CloseOrder(id, out _), context); break;
                case 9: Attempt(s, () => s.DeliverOrder(id, ids, out _), context); break;
                case 10: Attempt(s, () => s.SellBack(ids, out _), context); break;
                case 11: s.SetPaused(!s.IsPaused); break;
                case 12:
                    var time = s.ElapsedSeconds;
                    var frozen = s.IsPaused || s.ShiftEnded;
                    s.AdvanceActiveSeconds(random.Next(121));
                    Check(frozen ? s.ElapsedSeconds == time : s.ElapsedSeconds >= time, context + " clock");
                    break;
                case 13: s.StartShift(); break;
                case 14: if (step > 220) Attempt(s, () => s.EndShift(seed % 2 == 0, out _), context); break;
                case 15:
                    var snapshot = Snapshot(s);
                    var threw = false;
                    try { s.AdvanceActiveSeconds(double.NaN); }
                    catch (ArgumentOutOfRangeException) { threw = true; }
                    Check(threw && Snapshot(s) == snapshot, context + " invalid clock atomicity");
                    break;
            }
            for (var i = 0; i < 3; i++)
            {
                if (oldLocations[i] == FlashlightLocation.BuyerA || oldLocations[i] == FlashlightLocation.BuyerB ||
                    oldLocations[i] == FlashlightLocation.BoughtBack)
                    Check(s.Units[i].Location == oldLocations[i], context + " sold identity never returns");
                var state = oldOrders[i];
                if (state != DealOrderState.Available && state != DealOrderState.Accepted && state != DealOrderState.Locked)
                    Check(s.Order(OrderIds[i]).State == state, context + " terminal order never reopens");
            }
            Invariants(s, identities, context);
        }
        s.EndShift(true, out _);
        var ended = Snapshot(s);
        Attempt(s, () => s.PurchaseLot(out _), "finished purchase");
        Attempt(s, () => s.BuyBattery(out _), "finished battery");
        Attempt(s, () => s.ToggleUnit(0, out _), "finished pickup");
        Attempt(s, () => s.ToggleCrate(out _), "finished crate");
        Attempt(s, () => s.ToggleBench(out _), "finished bench");
        Attempt(s, () => s.Test(out _), "finished test");
        Attempt(s, () => s.Repair(out _), "finished repair");
        Attempt(s, () => s.SellBack(identities, out _), "finished buyback");
        foreach (var id in OrderIds)
        {
            Attempt(s, () => s.AcceptOrder(id, out _), "finished accept");
            Attempt(s, () => s.CloseOrder(id, out _), "finished close");
            Attempt(s, () => s.DeliverOrder(id, identities, out _), "finished delivery");
        }
        s.StartShift(); s.SetPaused(false); s.AdvanceActiveSeconds(10000);
        Check(ended == Snapshot(s), "finished state immutable seed " + seed);
        File.AppendAllText(LogPath, "PASS seed " + seed + " (256 actions and frozen-state checks)\n");
    }

    private static void Must(bool ok) => Check(ok, "route fixture operation");
    private static void Diagnose(WorkshopSession s, int index, bool repair = false)
    {
        Must(s.ToggleUnit(index, out _)); Must(s.ToggleBench(out _)); Must(s.Test(out _));
        if (repair) { Must(s.BuyBattery(out _)); Must(s.Repair(out _)); Must(s.Test(out _)); }
        Must(s.ToggleBench(out _)); Must(s.ToggleUnit(index, out _));
    }

    private static void VerifyRoutes()
    {
        foreach (var route in new[] { "A+buyback", "A+B", "B+A", "B+repeat+buyback", "fallback", "repair+fallback", "abandon" })
        {
            var s = WorkshopSession.CreateFirstDeal(); s.StartShift(); Must(s.PurchaseLot(out _));
            var ids = s.Units.Select(u => u.InstanceId).ToArray();
            var repair = route == "A+B" || route == "B+A" || route == "repair+fallback";
            for (var i = 0; i < 3; i++) Diagnose(s, i, repair && i == 1);
            Must(s.ToggleCrate(out _));
            if (route.StartsWith("B"))
            {
                Must(s.AcceptOrder(DealOrderId.B, out _)); Must(s.DeliverOrder(DealOrderId.B, new[] { ids[0] }, out _));
                if (route == "B+A")
                {
                    Must(s.AcceptOrder(DealOrderId.A, out _)); Must(s.DeliverOrder(DealOrderId.A, new[] { ids[1], ids[2] }, out _));
                }
                else
                {
                    Must(s.AcceptOrder(DealOrderId.RepeatB, out _)); Must(s.DeliverOrder(DealOrderId.RepeatB, new[] { ids[2] }, out _));
                    Must(s.SellBack(new[] { ids[1] }, out _));
                }
            }
            else if (route.StartsWith("A"))
            {
                Must(s.AcceptOrder(DealOrderId.A, out _)); Must(s.DeliverOrder(DealOrderId.A, new[] { ids[0], ids[2] }, out _));
                if (route == "A+B") { Must(s.AcceptOrder(DealOrderId.B, out _)); Must(s.DeliverOrder(DealOrderId.B, new[] { ids[1] }, out _)); }
                else Must(s.SellBack(new[] { ids[1] }, out _));
            }
            else if (route != "abandon") { s.AdvanceActiveSeconds(1080); Must(s.SellBack(ids, out _)); }
            var expected = route == "A+buyback" ? 415 : route == "A+B" || route == "B+A" ? 455 :
                route == "B+repeat+buyback" ? 390 : route == "fallback" ? 275 : route == "repair+fallback" ? 250 : 120;
            Check(s.Balance == expected && s.OwnedLampCount == (route == "abandon" ? 3 : 0), route + " final cash/stock");
            Must(s.EndShift(!s.HasPrimarySale, out _)); Invariants(s, ids, route);
            File.AppendAllText(LogPath, "PASS route " + route + " cash $" + s.Balance + " revenue $" + s.Revenue + " expenses $" + s.Expenses + "\n");
        }
    }
}
