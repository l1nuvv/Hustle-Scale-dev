using System;
using System.Collections;
using System.IO;
using System.Linq;
using Inventory;
using Interactables;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class E6Verification
{
    private static int checks;
    private static CitySaveStore store;
    private static readonly CityWorldData World = new CityWorldData { PlacedLamps = Array.Empty<CityPlacedLamp>() };
    private static void Check(bool value, string context)
    {
        checks++;
        if (!value) throw new InvalidOperationException("E6: " + context);
    }
    private static void Must(bool value) => Check(value, "operation");
    private static string Snapshot(WorkshopSession session) => JsonUtility.ToJson(session.CaptureCity(World));
    private static WorkshopSession Round(WorkshopSession session)
    {
        var data = session.CaptureCity(World);
        Check(store.TrySave(data, out var message), "save: " + message);
        Check(store.TryLoad(false, out var loaded, out var restored, out message), "load: " + message);
        Check(JsonUtility.ToJson(data) == JsonUtility.ToJson(restored.CaptureCity(loaded.World)), "exact round trip");
        Check(restored.IsPaused, "restore pauses");
        restored.SetPaused(false);
        return restored;
    }
    private static WorkshopSession City(int seed)
    {
        var session = WorkshopSession.CreateCity(seed);
        session.SetPaused(false);
        return session;
    }
    private static void Diagnose(WorkshopSession session, bool repair)
    {
        for (var i = 0; i < 3; i++)
        {
            Must(session.ToggleUnit(i, out _)); Must(session.ToggleBench(out _)); Must(session.Test(out _));
            if (repair && !session.OnBench.LastTestPassed)
            {
                Must(session.BuyBattery(out _)); Must(session.Repair(out _));
                Check(!session.OnBench.IsTested, "repair requires retest");
                Round(session);
                Must(session.Test(out _));
            }
            Must(session.ToggleBench(out _)); Must(session.ToggleUnit(i, out _));
        }
    }
    private static void Reject(WorkshopSession session, Func<bool> operation, string context)
    {
        var before = Snapshot(session);
        Check(!operation() && before == Snapshot(session), context + " atomic rejection");
    }
    [MenuItem("Tools/E6/Verify Restock and RNG")]
    public static void Run()
    {
        checks = 0;
        Directory.CreateDirectory("Logs");
        var folder = Path.GetFullPath("Temp/E6-" + Guid.NewGuid().ToString("N"));
        store = new CitySaveStore(Path.Combine(folder, "city-save.json"));
        try
        {
            var patterns = new bool[2, 8];
            for (var seed = 0; seed < 128; seed++)
            {
                var session = City(seed);
                for (var lot = 0; lot < 2; lot++)
                {
                    var ids = session.Units.Select(u => u.InstanceId).ToArray();
                    Must(session.PurchaseLot(out _));
                    Check(!session.Units.Any(u => ids.Contains(u.InstanceId)), "new IDs on purchase");
                    var mask = 0;
                    for (var i = 0; i < 3; i++)
                    {
                        var lamp = session.Units[i];
                        if (lamp.Condition == FlashlightCondition.BatteryFault) mask |= 1 << i;
                        Check(!lamp.IsTested && !lamp.LastTestPassed && lamp.InitialCondition == lamp.Condition, "unknown rolled condition");
                    }
                    patterns[lot, mask] = true;
                    Round(session);
                    Reject(session, () => session.PurchaseLot(out _), "cannot overwrite owned stock");
                    Diagnose(session, false);
                    Must(session.ToggleCrate(out _));
                    Must(session.SellBack(session.Units.Select(u => u.InstanceId).ToArray(), out _));
                    Must(session.ToggleCrate(out _));
                    Round(session);
                }
                Reject(session, () => session.PurchaseLot(out _), "third lot blocked");
                Check(session.CaptureCity(World).PreviousLamps.Length == 3, "first lot retained");
            }
            for (var lot = 0; lot < 2; lot++)
                for (var mask = 0; mask < 8; mask++) Check(patterns[lot, mask], "all RNG patterns in each lot");

            var trade = City(41);
            trade.SetPaused(true);
            Reject(trade, () => trade.PurchaseLot(out _), "paused purchase");
            trade.SetPaused(false);
            Must(trade.PurchaseLot(out _)); Diagnose(trade, true);
            Must(trade.AcceptOrder(DealOrderId.B, out _)); Must(trade.AcceptOrder(DealOrderId.A, out _));
            Must(trade.ToggleCrate(out _));
            var firstIds = trade.Units.Select(u => u.InstanceId).ToArray();
            Must(trade.DeliverOrder(DealOrderId.B, new[] { firstIds[0] }, out _));
            Must(trade.DeliverOrder(DealOrderId.A, new[] { firstIds[1], firstIds[2] }, out _));
            Check(trade.Balance == 455, "original cycle balance");
            trade = Round(trade);
            Must(trade.PurchaseLot(out _));
            Check(trade.Balance == 275 && trade.PurchasedLots == 2 && trade.HasPrimarySale && trade.ContactBOpened, "second purchase preserves progress");
            Check(trade.PreviousOrder(DealOrderId.A).State == DealOrderState.Completed, "paid history");
            Must(trade.ToggleCrate(out _));
            Diagnose(trade, true);
            Must(trade.AcceptOrder(DealOrderId.A, out _)); Must(trade.AcceptOrder(DealOrderId.B, out _));
            Must(trade.ToggleCrate(out _));
            Reject(trade, () => trade.DeliverOrder(DealOrderId.A, new[] { firstIds[1], firstIds[2] }, out _), "old IDs cannot pay");
            trade = Round(trade);
            Must(trade.DeliverOrder(DealOrderId.A, trade.Units.Take(2).Select(u => u.InstanceId).ToArray(), out _));
            Must(trade.DeliverOrder(DealOrderId.B, new[] { trade.Units[2].InstanceId }, out _));
            Reject(trade, () => trade.DeliverOrder(DealOrderId.B, new[] { trade.Units[2].InstanceId }, out _), "repeat payout");
            trade = Round(trade);
            Reject(trade, () => trade.PurchaseLot(out _), "exhausted restock after reload");
            var valid = trade.CaptureCity(World);
            Action<CitySaveData>[] corruptions =
            {
                d => d.PurchasedLots = 3, d => d.PreviousLamps = null, d => d.PreviousOrders = null,
                d => d.PreviousLamps[0].InstanceId = d.Lamps[0].InstanceId,
                d => d.PreviousLamps[0].Location = FlashlightLocation.Crate,
                d => d.PreviousOrders[0].State = DealOrderState.Available,
                d => d.Balance++, d => d.RepairSeconds += 240,
                d => d.Lamps[0].InitialCondition = (FlashlightCondition)99,
                d => d.PreviousLamps[0].LastTestPassed = false
            };
            foreach (var corrupt in corruptions)
            {
                var data = JsonUtility.FromJson<CitySaveData>(JsonUtility.ToJson(valid)); corrupt(data);
                Check(!WorkshopSession.TryRestoreCity(data, out _, out _), "corrupt history rejected");
            }
            var legacy = City(41); Must(legacy.PurchaseLot(out _)); Diagnose(legacy, true);
            foreach (var version in new[] { 1, 2 })
            {
                var data = legacy.CaptureCity(World); data.Version = version;
                Check(WorkshopSession.TryRestoreCity(data, out var restored, out _), "legacy version " + version);
                Check(restored.PurchasedLots == 1 && restored.Units[1].InitialCondition == FlashlightCondition.BatteryFault, "legacy inferred initial condition");
                Round(restored);
            }
            var declined = City(41); Must(declined.PurchaseLot(out _)); Must(declined.CloseOrder(DealOrderId.A, out _));
            Must(declined.AcceptOrder(DealOrderId.B, out _)); Must(declined.CloseOrder(DealOrderId.B, out _));
            Diagnose(declined, false); Must(declined.ToggleCrate(out _));
            Must(declined.SellBack(declined.Units.Select(u => u.InstanceId).ToArray(), out _));
            Must(declined.PurchaseLot(out _)); declined = Round(declined);
            Check(declined.PreviousOrder(DealOrderId.A).State == DealOrderState.Declined &&
                declined.PreviousOrder(DealOrderId.B).State == DealOrderState.Cancelled, "refusal and violation preserved");
            var poor = City(4); Must(poor.PurchaseLot(out _));
            Must(poor.BuyBattery(out _)); Must(poor.BuyBattery(out _));
            Diagnose(poor, false); Must(poor.ToggleCrate(out _));
            Must(poor.SellBack(poor.Units.Select(u => u.InstanceId).ToArray(), out _));
            Check(poor.Balance < 180, "insufficient restock funds fixture");
            Reject(poor, () => poor.PurchaseLot(out _), "insufficient restock funds");
            Round(poor);
            var fixture = WorkshopSession.CreateFirstDeal(); Must(fixture.PurchaseLot(out _));
            Check(fixture.Units[0].Condition == FlashlightCondition.Working && fixture.Units[1].Condition == FlashlightCondition.BatteryFault &&
                fixture.Units[2].Condition == FlashlightCondition.Working, "FIRST DEAL unchanged");
            var result = "E6 DOMAIN PASS: " + checks + " assertions; Unity " + Application.unityVersion;
            File.WriteAllText("Logs/Codex-E6-domain.log", result + "\n"); Debug.Log(result);
        }
        catch (Exception exception)
        {
            File.WriteAllText("Logs/Codex-E6-domain.log", "E6 DOMAIN FAIL: " + exception + "\n"); throw;
        }
    }
}

public static partial class S3Verification
{
    [MenuItem("Tools/E6/Verify Second Trading Cycle in Play Mode")]
    public static void RunExpansion()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        SessionState.SetBool(Key + ".E6", true);
        RunCityVerification(false);
    }
    private static IEnumerator ExpansionScenario()
    {
        NewFixture(); walk.SetPaused(false);
        Open(CityTradeKind.Seller); Command("purchase"); trade.CloseMenu();
        for (var lot = 0; lot < 2; lot++)
        {
            var ids = save.Session.Units.Select(u => u.InstanceId).ToArray();
            for (var i = 0; i < 3; i++)
            {
                Open(CityTradeKind.Crate); Command("unit", i); trade.CloseMenu();
                Open(CityTradeKind.Workshop); Command("bench"); Command("test");
                if (!save.Session.OnBench.LastTestPassed)
                {
                    trade.CloseMenu(); Open(CityTradeKind.Battery); Command("battery"); trade.CloseMenu();
                    Open(CityTradeKind.Workshop); Command("repair"); Command("test");
                }
                Command("bench"); trade.CloseMenu(); Open(CityTradeKind.Crate); Command("unit", i); trade.CloseMenu();
            }
            RoundTrip("E6 diagnosed lot " + lot); walk.SetPaused(false);
            Open(CityTradeKind.Crate); Command("crate"); trade.CloseMenu();
            Open(CityTradeKind.A); Command("accept");
            SelectOnly(0, 1); Command("deliver"); trade.CloseMenu();
            Open(CityTradeKind.B); Command("accept"); SelectOnly(2); Command("deliver");
            Check(!trade.Execute("deliver"), "E6 paid order cannot pay again"); trade.CloseMenu();
            RoundTrip("E6 sold lot " + lot); walk.SetPaused(false);
            Check(!trade.Lamp(0).gameObject.activeSelf && !trade.Lamp(1).gameObject.activeSelf && !trade.Lamp(2).gameObject.activeSelf, "sold visuals hidden");
            if (lot == 0)
            {
                ToggleFixtureCrate(CityTradeView.WorkshopDock);
                Open(CityTradeKind.Seller);
                var before = JsonUtility.ToJson(save.Session.CaptureCity(save.World));
                Check(!trade.Execute("purchase") && before == JsonUtility.ToJson(save.Session.CaptureCity(save.World)), "remote empty crate blocks restock atomically");
                trade.CloseMenu(); ToggleFixtureCrate(CityTradeView.WorkshopDock);
            }
            Open(CityTradeKind.Seller);
            if (lot == 0)
            {
                trade.CloseMenu();
                ToggleFixtureCrate(new Vector3(88, 0.3f, -35));
                Open(CityTradeKind.Seller);
                Command("purchase");
                Check(save.Session.PurchasedLots == 2 && !save.Session.Units.Any(u => ids.Contains(u.InstanceId)), "E6 restock new identities");
                Check(trade.Lamp(0).gameObject.activeSelf && trade.Lamp(1).gameObject.activeSelf && trade.Lamp(2).gameObject.activeSelf, "restock visuals reappear");
                trade.CloseMenu();
                RoundTrip("E6 restocked"); walk.SetPaused(false);
                Open(CityTradeKind.Crate); Command("crate"); trade.CloseMenu();
                ToggleFixtureCrate(CityTradeView.WorkshopDock);
                yield return null;
            }
            else
            {
                Check(!trade.Execute("purchase"), "E6 third purchase rejected in scene"); trade.CloseMenu();
            }
        }
    }
}
