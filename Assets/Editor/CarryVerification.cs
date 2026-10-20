using System;
using System.Linq;
using Inventory;
using UnityEditor;
using UnityEngine;

public static class CarryVerification
{
    [MenuItem("Tools/Workshop/Verify Carrying Rules")]
    public static void Run()
    {
        var checks = 0;
        void Check(bool condition, string description)
        {
            if (!condition) throw new Exception(description);
            checks++;
        }
        var session = WorkshopSession.CreateFirstDeal();
        var identities = session.Units.Select(u => u.InstanceId).ToArray();
        Check(!session.TogglePower(0, out _) && !session.StowHeld(out _) && !session.PlaceHeld(out _), "seller goods cannot be handled");
        Check(session.PurchaseLot(out _), "purchase lot");
        Check(session.ToggleUnit(0, out _) && session.TogglePower(0, out _) && session.InHands.IsPowered, "working lamp switches on before diagnosis");
        Check(!session.InHands.IsTested, "power is not a professional diagnosis");
        Check(session.TogglePower(0, out _) && !session.InHands.IsPowered, "lamp switches off");
        Check(session.ToggleBench(out _) && session.Test(out _) && session.OnBench.IsPowered, "test switches working lamp on");
        Check(session.TogglePower(0, out _) && !session.OnBench.IsPowered && session.OnBench.LastTestPassed, "switching off preserves diagnosis");
        Check(session.ToggleBench(out _) && session.TogglePower(0, out _) && session.StowHeld(out _), "store held lamp");
        Check(session.InHands == null && session.Units[0].Location == FlashlightLocation.Pocket && !session.Units[0].IsPowered, "pocket extinguishes lamp and frees hands");
        Check(session.ToggleUnit(1, out _) && !session.TogglePower(1, out _) && !session.InHands.IsPowered, "faulty lamp cannot light");
        Check(session.StowHeld(out _) && session.EquipSlot(0, out _) && session.InHands.InstanceId == identities[0], "equip original identity");
        Check(session.EquipSlot(1, out _) && session.InHands.InstanceId == identities[1] && session.Units[0].Location == FlashlightLocation.Pocket, "slot swap is atomic");
        Check(session.PlaceHeld(out _) && session.Units[1].Location == FlashlightLocation.World, "place held item in world");
        Check(session.EquipSlot(0, out _) && session.PickUpPlaced(1, out _) && session.InHands.InstanceId == identities[1], "world pickup stores previously held item");
        Check(!session.PickUpPlaced(1, out _) && session.Units.Count(u => u.Location == FlashlightLocation.Hands) == 1, "repeat pickup cannot duplicate");
        Check(session.EquipSlot(0, out _) && session.TogglePower(0, out _) && session.PlaceHeld(out _) && session.Units[0].IsPowered, "placed working lamp retains power");
        Check(session.TogglePower(0, out _) && !session.Units[0].IsPowered, "placed lamp switches off");
        Check(session.OwnedLampCount == 3 && session.CanPursueOrder(DealOrderId.A), "pocket and world items remain owned stock");
        Check(session.AcceptOrder(DealOrderId.B, out _) && !session.DeliverOrder(DealOrderId.B, new[] { identities[0] }, out _), "remote world item cannot settle a sale");
        Check(session.PickUpPlaced(0, out _) && session.DeliverOrder(DealOrderId.B, new[] { identities[0] }, out _), "picked tested lamp delivers normally");
        Check(session.Balance == 120 + session.Order(DealOrderId.B).Payment && session.OwnedLampCount == 2, "delivery preserves approved price and ownership count");
        Check(!session.EquipSlot(0, out _) && !session.TogglePower(0, out _), "sold inventory slot cannot recreate stock");
        var balance = session.Balance;
        session.SetPaused(true);
        Check(!session.EquipSlot(1, out _) && !session.TogglePower(1, out _) && !session.PickUpPlaced(2, out _), "paused mutations rejected");
        session.SetPaused(false);
        Check(session.Balance == balance && session.Units.Select(u => u.InstanceId).SequenceEqual(identities), "all moves preserve money and unique identities");
        Check(!session.EquipSlot(-1, out _) && !session.TogglePower(99, out _) && !session.PickUpPlaced(-1, out _), "invalid slots rejected safely");
        Debug.Log("CARRY DOMAIN PASS: " + checks + " assertions");
    }
}
