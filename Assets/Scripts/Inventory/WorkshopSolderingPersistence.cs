using System;
using System.Collections.Generic;

namespace Inventory
{
    public sealed partial class WorkshopSession
    {
        private static bool ValidateSoldering(CitySaveData save, HashSet<string> ids, ref int hands,
            ref int benches, ref long revenue, out long expenses)
        {
            expenses = 0;
            if (save.Version < 5) return true;
            var data = save.Soldering;
            if (data == null || data.Order == null || data.Order.Id != DealOrderId.Solder ||
                data.Doses < 0 || data.PurchasedDoses < 0 ||
                (long)data.Doses + (data.HasSolder ? 1 : 0) != data.PurchasedDoses ||
                !Finite(data.Progress) || data.Progress < 0 || data.Progress > 1 || data.Progress > 0 && !data.HasSolder ||
                !data.CoverAttached && (data.Secured0 || data.Secured1) ||
                !Enum.IsDefined(typeof(DealOrderState), data.Order.State) ||
                !Finite(data.Order.DeadlineSeconds) || data.Order.DeadlineSeconds < 0) return false;
            var assembled = data.CoverAttached && data.Secured0 && data.Secured1;
            if (data.FinalTest && (data.Progress != 1 || !assembled)) return false;
            if (!data.StationOwned && (data.LampPurchased || data.PurchasedDoses != 0 || data.Order.State != DealOrderState.Locked)) return false;
            if (data.StationOwned && (data.Order.State == DealOrderState.Locked || !save.LotPurchased ||
                save.PurchasedLots == 1 && Array.Exists(save.Lamps, lamp =>
                    lamp.Location != FlashlightLocation.BuyerA && lamp.Location != FlashlightLocation.BuyerB && lamp.Location != FlashlightLocation.BoughtBack))) return false;
            if (data.LampPurchased && data.Lamp == null) return false;
            if (!data.LampPurchased && data.Lamp != null && (!string.IsNullOrEmpty(data.Lamp.InstanceId) ||
                data.Lamp.Condition != 0 || data.Lamp.InitialCondition != 0 || data.Lamp.Location != 0 ||
                data.Lamp.IsTested || data.Lamp.LastTestPassed || data.Lamp.IsPowered)) return false;
            var lamp = data.LampPurchased ? data.Lamp : null;
            if (lamp == null)
            {
                if (data.Diagnosed || data.AtStation || data.HasSolder || data.Progress != 0 || data.FinalTest || !assembled) return false;
            }
            else
            {
                if (!Guid.TryParseExact(lamp.InstanceId, "N", out _) || !ids.Add(lamp.InstanceId) ||
                    lamp.InitialCondition != FlashlightCondition.ConnectionFault ||
                    lamp.Condition != (data.Progress == 1 ? FlashlightCondition.Working : FlashlightCondition.ConnectionFault) ||
                    !Enum.IsDefined(typeof(FlashlightLocation), lamp.Location) || lamp.Location == FlashlightLocation.Seller ||
                    lamp.Location == FlashlightLocation.BuyerA || lamp.Location == FlashlightLocation.BuyerB ||
                    (!data.AtStation && !assembled) || data.AtStation && lamp.Location != FlashlightLocation.Bench ||
                    (data.AtStation || data.HasSolder || !assembled || data.FinalTest) && !data.Diagnosed ||
                    lamp.LastTestPassed != data.FinalTest ||
                    lamp.IsTested != (data.FinalTest || data.Diagnosed && data.Progress < 1) ||
                    lamp.IsPowered && !data.FinalTest) return false;
                if (lamp.Location == FlashlightLocation.Hands) hands++;
                if (lamp.Location == FlashlightLocation.Bench) benches++;
                if (lamp.Location == FlashlightLocation.SolderBuyer)
                {
                    if (!data.FinalTest || data.Order.State != DealOrderState.Completed || data.DeliveredId != lamp.InstanceId) return false;
                    revenue += 400;
                }
                if (lamp.Location == FlashlightLocation.BoughtBack)
                {
                    if (!lamp.IsTested || !assembled || data.Order.State == DealOrderState.Accepted || data.Order.State == DealOrderState.Available) return false;
                    revenue += lamp.LastTestPassed ? 60 : 35;
                }
            }
            var order = data.Order;
            if ((order.State == DealOrderState.Completed) != (lamp != null && lamp.Location == FlashlightLocation.SolderBuyer) ||
                (order.State != DealOrderState.Completed && !string.IsNullOrEmpty(data.DeliveredId)) ||
                (order.State == DealOrderState.Unavailable && (lamp == null || lamp.Location != FlashlightLocation.BoughtBack)) ||
                order.State == DealOrderState.Expired) return false;
            var accepted = order.State == DealOrderState.Accepted || order.State == DealOrderState.Completed ||
                order.State == DealOrderState.Failed || order.State == DealOrderState.Cancelled;
            if (accepted ? order.DeadlineSeconds < 1080 || order.DeadlineSeconds > save.ElapsedSeconds + 1080 + 0.000001 :
                order.State != DealOrderState.Unavailable && order.DeadlineSeconds != 0) return false;
            if (order.State == DealOrderState.Accepted && order.DeadlineSeconds <= save.ElapsedSeconds ||
                order.State == DealOrderState.Failed && order.DeadlineSeconds > save.ElapsedSeconds ||
                order.State == DealOrderState.Unavailable && order.DeadlineSeconds > save.ElapsedSeconds + 1080 + 0.000001) return false;
            expenses = (data.StationOwned ? 300L : 0) + (lamp != null ? 40L : 0) + data.PurchasedDoses * 5L;
            return true;
        }
    }
}
