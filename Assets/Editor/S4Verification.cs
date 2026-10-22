using System.Collections;
using System.IO;
using System.Linq;
using Interactables;
using Inventory;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

// Share the existing isolated scene/input harness. S4 adds free choices and usability checks.
public static partial class S3Verification
{
    [MenuItem("Tools/S4/Verify Free Routes and Wayfinding in Play Mode")]
    public static void RunFreeRoutes() => RunCityVerification(true);

    private static void RoundTrip(string context)
    {
        if (trade.MenuOpen) trade.CloseMenu();
        Check(save.Save(), context + " save");
        var snapshot = JsonUtility.ToJson(save.Session.CaptureCity(save.World));
        for (var load = 0; load < 3; load++)
        {
            Check(save.Load(false), context + " load " + load);
            // JsonUtility may round the last double digit; all non-time state remains exact.
            Check(PersistenceMatches(snapshot), context + " entire snapshot unchanged");
            Check(trade.Session == save.Session && save.Session.IsPaused && walk.IsPaused, context + " rebind on pause");
            Check(save.Session.Units.Select(u => u.InstanceId).Distinct().Count() == 3, context + " unique identities");
            for (var i = 0; i < 3; i++)
            {
                var location = save.Session.Units[i].Location;
                Check(trade.Lamp(i).gameObject.activeSelf == (location != FlashlightLocation.Pocket &&
                    (WorkshopSession.IsOwned(save.Session.Units[i]) || location == FlashlightLocation.Seller)), context + " visual ownership " + i);
            }
        }
        walk.SetPaused(false);
        File.AppendAllText(Log, "PASS repeated load: " + context + "\n");
    }

    private static void PrepareGoods(bool repair)
    {
        NewFixture(); walk.SetPaused(false);
        Open(CityTradeKind.Seller); Command("purchase"); trade.CloseMenu();
        Open(CityTradeKind.Crate); Command("crate"); trade.CloseMenu();
        WalkRoute("seller to workshop for free choice", -61, 8, -62.5f, 8, -62.5f, 14, -65, 14, -65, 25, -69, 30);
        ToggleFixtureCrate(CityTradeView.WorkshopDock);
        if (repair) { Open(CityTradeKind.Battery); Command("battery"); trade.CloseMenu(); }
        for (var i = 0; i < 3; i++)
        {
            Open(CityTradeKind.Crate); Command("unit", i); trade.CloseMenu();
            Open(CityTradeKind.Workshop); Command("bench"); Command("test");
            if (i == 1 && repair) { Command("repair"); Command("test"); }
            Command("bench"); trade.CloseMenu();
            Open(CityTradeKind.Crate); Command("unit", i); trade.CloseMenu();
        }
        Open(CityTradeKind.Crate); Command("crate"); trade.CloseMenu();
    }

    private static IEnumerator FreeRoutes()
    {
        PrepareGoods(true);
        Open(CityTradeKind.A); Command("accept"); trade.CloseMenu();
        Open(CityTradeKind.B); Command("accept"); trade.CloseMenu();
        WalkRoute("workshop to A first carrying", -69, 30, -65, 25, -65, 0, -42, 0, -29, 5, -29, 8.4f);
        Open(CityTradeKind.A); SelectOnly(0, 1); Command("deliver"); trade.CloseMenu();
        Check(save.Session.Balance == 330 && save.Session.OwnedLampCount == 1, "A first leaves B feasible");
        RoundTrip("A paid with one remaining lamp");
        WalkRoute("A to B carrying", -29, 8.4f, -29, 5, -7, 5, -7, 8.4f);
        Open(CityTradeKind.B); SelectOnly(2); Command("deliver"); trade.CloseMenu();
        Check(save.Session.Balance == 455 && save.Session.OwnedLampCount == 0 && !save.Session.ShiftEnded, "A then B free city");
        RoundTrip("A then B stock exhausted");
        WalkRoute("explore after both sales", -7, 8.4f, -7, -7, -15, -7, -42, 0, -65, 0, -65, 25);
        Open(CityTradeKind.Seller); Check(save.Session.CanPurchaseLot && save.Session.PurchasedLots == 1,
            "optional second lot offered without automatic purchase"); trade.CloseMenu();
        Check(save.Session.Order(DealOrderId.RepeatB).State == DealOrderState.Locked && save.Session.Balance == 455 && player.CanInteract,
            "exploration neither resets cash nor unlocks repeat offers");

        PrepareGoods(false);
        Check(!save.Session.HasPrimarySale && save.Session.BuybackAvailable, "early buyback before any primary sale");
        WalkRoute("early buyback carrying", -69, 30, -65, 25, -65, 0, -80, 0, -80, -65, -65, -65, -65, -49);
        Open(CityTradeKind.Buyback); SelectOnly(0, 1, 2); Command("sell"); trade.CloseMenu();
        Check(save.Session.Balance == 275 && save.Session.Revenue == 155 && save.Session.OwnedLampCount == 0,
            "early buyback 60 plus 35 plus 60 without repair");
        RoundTrip("early buyback exhausted stock");
        Open(CityTradeKind.A); Check(!trade.Execute("accept") && !trade.Execute("deliver"), "stock shortage cannot pay A"); trade.CloseMenu();

        PrepareGoods(false);
        var identities = save.Session.Units.Select(u => u.InstanceId).ToArray();
        Open(CityTradeKind.A); Command("accept"); Command("close"); trade.CloseMenu();
        Open(CityTradeKind.B); Command("accept"); Command("close"); trade.CloseMenu();
        Check(save.Session.Balance == 120 && save.Session.OwnedLampCount == 3 &&
            identities.SequenceEqual(save.Session.Units.Select(u => u.InstanceId)), "cancel both retains money and goods");
        RoundTrip("cancelled orders with carried stock");
        Open(CityTradeKind.A); Check(!trade.Execute("accept") && !trade.Execute("deliver"), "cancelled A final"); trade.CloseMenu();
        Open(CityTradeKind.B); Check(!trade.Execute("accept") && !trade.Execute("deliver"), "cancelled B final"); trade.CloseMenu();
        Open(CityTradeKind.Buyback); SelectOnly(0, 1, 2); Command("sell"); trade.CloseMenu();
        Check(save.Session.Balance == 275 && !save.Session.ShiftEnded, "cancel both can still sell leftovers");

        PrepareGoods(false);
        Open(CityTradeKind.A); Command("accept"); trade.CloseMenu();
        Open(CityTradeKind.B); Command("accept"); trade.CloseMenu();
        save.Session.AdvanceActiveSeconds(1080);
        Check(save.Session.Order(DealOrderId.A).State == DealOrderState.Failed &&
            save.Session.Order(DealOrderId.B).State == DealOrderState.Failed && save.Session.OwnedLampCount == 3 &&
            save.Session.Balance == 120 && !save.Session.ShiftEnded, "both deadlines retain goods and free world");
        RoundTrip("both deadlines expired carrying");
        Open(CityTradeKind.B); Check(!trade.Execute("deliver"), "expired B cannot pay"); trade.CloseMenu();
        Open(CityTradeKind.Buyback); SelectOnly(0, 1, 2); Command("sell"); trade.CloseMenu();
        Check(save.Session.Balance == 275, "expired orders can still use buyback");

        // Reproduce the reported startup confusion: inspect the actual UI and world labels.
        NewFixture(); walk.SetPaused(false);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.Tab)); yield return null; yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
        Check(trade.MenuOpen && walk.IsPaused, "Tab opens map and inventory");
        var map = GameObject.Find("Карта района");
        Check(map != null && GameObject.Find("Ты здесь") != null, "local map and current player position shown");
        Canvas.ForceUpdateCanvases();
        var paths = map.GetComponentInChildren<CityTradeMapPaths>();
        var mesh = paths.canvasRenderer.GetMesh();
        Check(mesh != null && mesh.vertexCount > 0, "responsive walking paths actually render");
        var labels = UnityEngine.Object.FindObjectsByType<Text>(FindObjectsSortMode.None).Where(t => t.isActiveAndEnabled).ToArray();
        Check(labels.Any(t => t.text.Contains("ФОНАРИ И БАТАРЕИ")) && labels.Any(t => t.text.Contains("МАСТЕРСКАЯ")), "map names both start and seller");
        Check(!labels.Any(t => t.text.Contains("Seller") || t.text.Contains("Hands") || t.text.Contains("Crate") || t.text.Contains("Not tested")),
            "visible inventory vocabulary is Russian");
        foreach (var sign in UnityEngine.Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None))
        {
            Check(!System.Text.RegularExpressions.Regex.IsMatch(sign.text, "[a-zA-Z]{2,}"), "Russian world sign: " + sign.text);
            Check(!(Mathf.Abs(sign.transform.position.x) < 100 && sign.text.Contains("ПОКА ЗАКРЫТО")), "active market is not labelled closed");
        }
        foreach (var sign in trade.Crate.parent.GetComponentsInChildren<TextMesh>())
            if (sign.transform.IsChildOf(GameObject.Find("City trade runtime").transform))
                Check(sign.GetComponent<Renderer>().sharedMaterial.shader.name == "HustleScale/WorldSign", "world labels depth tested");
        trade.CloseMenu();
        File.AppendAllText(Log, "PASS S4 free choices, repeated loads, exhausted exploration and Russian wayfinding; human interest not evaluated automatically\n");
    }
}
