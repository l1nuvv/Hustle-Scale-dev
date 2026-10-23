using System;
using System.Collections.Generic;

namespace Inventory
{
    public sealed partial class WorkshopSession
    {
        private static bool ValidateSharpening(CitySaveData save, HashSet<string> ids, ref int hands, ref long revenue, out long expenses)
        {
            expenses = 0;
            if (save.Version < 7) return true;
            var d = save.Sharpening;
            if (d == null || d.Order == null || d.Order.Id != DealOrderId.Sharpen ||
                d.Coverage == null || d.Coverage.Length != 12 || !Enum.IsDefined(typeof(KnifeLocation), d.Location) ||
                !Enum.IsDefined(typeof(DealOrderState), d.Order.State) || !ValidPosition(d.Position) ||
                !Finite(d.Yaw) || Math.Abs(d.Yaw) > 360) return false;
            var full = true; var any = false;
            foreach (var value in d.Coverage)
            {
                if (!Finite(value) || value < 0 || value > 1) return false;
                full &= value == 1; any |= value > 0;
            }
            if (d.FinalTest && !full) return false;
            if (!d.StationOwned && (d.KnifePurchased || d.Order.State != DealOrderState.Locked)) return false;
            if (d.StationOwned && (!save.LotPurchased || d.Order.State == DealOrderState.Locked ||
                save.PurchasedLots == 1 && Array.Exists(save.Lamps, lamp => lamp.Location != FlashlightLocation.BuyerA &&
                    lamp.Location != FlashlightLocation.BuyerB && lamp.Location != FlashlightLocation.BoughtBack))) return false;
            if (d.KnifePurchased)
            {
                if (!d.StationOwned || !Guid.TryParseExact(d.InstanceId, "N", out _) || !ids.Add(d.InstanceId) || d.CoatingSeed <= 0) return false;
                if (d.Location == KnifeLocation.Hands) hands++;
                if (d.Location == KnifeLocation.Buyer)
                {
                    if (!d.FinalTest || d.Order.State != DealOrderState.Completed || d.DeliveredId != d.InstanceId) return false;
                    revenue += 500;
                }
                if (d.Location == KnifeLocation.BoughtBack)
                {
                    if (d.Order.State == DealOrderState.Available || d.Order.State == DealOrderState.Accepted) return false;
                    revenue += d.FinalTest ? 60 : 20;
                }
            }
            else if (!string.IsNullOrEmpty(d.InstanceId) || any || d.FinalTest || d.CoatingSeed != 0 || d.Location != KnifeLocation.Pocket ||
                d.Position != UnityEngine.Vector3.zero || d.Yaw != 0) return false;
            var o = d.Order;
            if (!Finite(o.DeadlineSeconds) || o.DeadlineSeconds < 0 || o.DeadlineSeconds > save.ElapsedSeconds + 1080 + .000001 ||
                o.State == DealOrderState.Expired ||
                (o.State == DealOrderState.Completed) != (d.KnifePurchased && d.Location == KnifeLocation.Buyer) ||
                o.State != DealOrderState.Completed && !string.IsNullOrEmpty(d.DeliveredId) ||
                o.State == DealOrderState.Unavailable && (!d.KnifePurchased || d.Location != KnifeLocation.BoughtBack)) return false;
            var accepted = o.State == DealOrderState.Accepted || o.State == DealOrderState.Completed || o.State == DealOrderState.Failed || o.State == DealOrderState.Cancelled;
            if (accepted && o.DeadlineSeconds < 1080 ||
                o.State == DealOrderState.Unavailable && o.DeadlineSeconds != 0 && o.DeadlineSeconds < 1080 ||
                (o.State == DealOrderState.Locked || o.State == DealOrderState.Available || o.State == DealOrderState.Declined) && o.DeadlineSeconds != 0 ||
                o.State == DealOrderState.Accepted && o.DeadlineSeconds <= save.ElapsedSeconds ||
                o.State == DealOrderState.Failed && o.DeadlineSeconds > save.ElapsedSeconds) return false;
            expenses = (d.StationOwned ? 400L : 0) + (d.KnifePurchased ? 40L : 0);
            return true;
        }
    }
}
