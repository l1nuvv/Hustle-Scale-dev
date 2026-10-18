using System;
using System.Collections.Generic;

namespace Inventory
{
    public enum DealOrderId { A, B, RepeatB }
    public enum DealOrderState { Locked, Available, Accepted, Completed, Expired, Failed, Declined, Cancelled }

    public sealed class DealOrder
    {
        public DealOrderId Id { get; }
        public int Quantity { get; }
        public int Payment { get; }
        public int DeadlineSeconds { get; }
        public DealOrderState State { get; internal set; }

        internal DealOrder(DealOrderId id, int quantity, int payment, int deadline, DealOrderState state)
        {
            Id = id; Quantity = quantity; Payment = payment; DeadlineSeconds = deadline; State = state;
        }
    }

    public sealed partial class WorkshopSession
    {
        private readonly DealOrder[] orders =
        {
            new DealOrder(DealOrderId.A, 2, 260, 540, DealOrderState.Available),
            new DealOrder(DealOrderId.B, 1, 125, 1080, DealOrderState.Available),
            new DealOrder(DealOrderId.RepeatB, 1, 110, 1080, DealOrderState.Locked)
        };

        public bool IsFirstDeal { get; }
        public bool LotPurchased { get; private set; }
        public bool HasTransportedCrate { get; private set; }
        public bool ShiftStarted { get; private set; }
        public bool IsPaused { get; private set; }
        public double ElapsedSeconds { get; private set; }
        public static WorkshopSession CreateFirstDeal() => new WorkshopSession(300, true);

        public DealOrder Order(DealOrderId id)
        {
            if ((int)id < 0 || (int)id >= orders.Length) throw new ArgumentOutOfRangeException(nameof(id));
            return orders[(int)id];
        }

        private bool CanHandleItems(out string message)
        {
            if (ShiftEnded) return Reject("Смена завершена. Начни заново для новой попытки.", out message);
            if (IsPaused) return Reject("Close the menu first.", out message);
            if (IsFirstDeal && !LotPurchased) return Reject("Buy the seller's lot first: three lamps for $180.", out message);
            message = "";
            return true;
        }

        public bool PurchaseLot(out string message)
        {
            if (ShiftEnded) return Reject("Смена завершена. Начни заново для новой попытки.", out message);
            if (!IsFirstDeal) return Reject("The workshop fixture already owns its lamps.", out message);
            if (IsPaused) return Reject("Close the menu first.", out message);
            if (LotPurchased) return Reject("This single lot has already been purchased.", out message);
            if (Balance < 180) return Reject("Not enough money: the lot costs $180.", out message);
            Balance -= 180;
            Expenses += 180;
            LotPurchased = true;
            foreach (var unit in units) unit.Location = FlashlightLocation.Crate;
            return Complete("Purchased three lamps: -$180. Carry the crate back to the workshop.", out message);
        }

        public void StartShift()
        {
            if (!IsFirstDeal || ShiftStarted || IsPaused || ShiftEnded) return;
            ShiftStarted = true;
            Changed?.Invoke();
        }

        public void SetPaused(bool paused)
        {
            if (ShiftEnded) return;
            if (IsPaused == paused) return;
            IsPaused = paused;
            Changed?.Invoke();
        }

        public void AdvanceActiveSeconds(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0)
                throw new ArgumentOutOfRangeException(nameof(seconds));
            if (!IsFirstDeal || !ShiftStarted || IsPaused || ShiftEnded || seconds == 0) return;
            var before = Math.Floor(ElapsedSeconds);
            AddElapsedSeconds(seconds);
            if (Math.Floor(ElapsedSeconds) != before) Changed?.Invoke();
        }

        private void AddElapsedSeconds(double seconds)
        {
            ElapsedSeconds = Math.Min(ElapsedSeconds + seconds, int.MaxValue);
            foreach (var order in orders)
            {
                if (ElapsedSeconds < order.DeadlineSeconds) continue;
                if (order.State == DealOrderState.Available) order.State = DealOrderState.Expired;
                else if (order.State == DealOrderState.Accepted) order.State = DealOrderState.Failed;
            }
        }

        public bool AcceptOrder(DealOrderId id, out string message)
        {
            var order = Order(id);
            if (!IsFirstDeal || ShiftEnded || order.State != DealOrderState.Available)
                return Reject("This offer is not available.", out message);
            if (ElapsedSeconds >= order.DeadlineSeconds) return Reject("The deadline has passed.", out message);
            order.State = DealOrderState.Accepted;
            return Complete("Accepted " + id + ". Deliver tested working lamps before the fixed deadline.", out message);
        }

        public bool CloseOrder(DealOrderId id, out string message)
        {
            var order = Order(id);
            if (!IsFirstDeal || ShiftEnded || (order.State != DealOrderState.Available && order.State != DealOrderState.Accepted))
                return Reject("This order is already closed or locked.", out message);
            order.State = order.State == DealOrderState.Accepted ? DealOrderState.Cancelled : DealOrderState.Declined;
            return Complete("Order " + id + ": " + order.State + ". No money or items changed.", out message);
        }

        // The scene supplies the exact transported identities; the model revalidates every one.
        public bool DeliverOrder(DealOrderId id, IReadOnlyList<string> presentedIds, out string message)
        {
            var order = Order(id);
            if (!CanHandleItems(out message)) return false;
            if (order.State != DealOrderState.Accepted) return Reject("Accept this order before delivering; closed orders cannot pay again.", out message);
            if (ElapsedSeconds >= order.DeadlineSeconds) return Reject("The deadline has passed. No payment.", out message);
            if (presentedIds == null || presentedIds.Count != order.Quantity)
                return Reject("Present exactly " + order.Quantity + " tested working lamps together. Carry the crate for A.", out message);
            var selected = new List<FlashlightUnit>();
            var unique = new HashSet<string>();
            foreach (var identity in presentedIds)
            {
                if (identity == null || !unique.Add(identity)) return Reject("Every lamp must have a different owned identity.", out message);
                FlashlightUnit found = null;
                foreach (var unit in units) if (unit.InstanceId == identity) { found = unit; break; }
                if (found == null || !(found.Location == FlashlightLocation.Hands ||
                    (IsCrateCarried && found.Location == FlashlightLocation.Crate)))
                    return Reject("Bring your lamps here in your hands or carried crate; stored and sold lamps cannot transfer.", out message);
                if (!found.IsTested) return Reject("Test every presented lamp on the workbench first.", out message);
                if (!found.LastTestPassed || found.Condition != FlashlightCondition.Working)
                    return Reject("Only tested working lamps qualify. Repair and retest the faulty lamp.", out message);
                selected.Add(found);
            }
            if (Balance > int.MaxValue - order.Payment) return Reject("Wallet limit reached.", out message);
            foreach (var unit in selected) unit.Location = id == DealOrderId.A ? FlashlightLocation.BuyerA : FlashlightLocation.BuyerB;
            Balance += order.Payment;
            Revenue += order.Payment;
            order.State = DealOrderState.Completed;
            if (id == DealOrderId.B) orders[2].State = DealOrderState.Available;
            return Complete("Delivered " + id + ": +$" + order.Payment + ". " +
                (id == DealOrderId.B ? "One repeat B offer is now available until 18:00." : "Stock and balance updated."), out message);
        }
    }
}
