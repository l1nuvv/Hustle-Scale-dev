using System.Collections.Generic;

namespace Inventory
{
    public sealed partial class WorkshopSession
    {
        public int InitialBalance { get; }
        public long Revenue { get; private set; }
        public long Expenses { get; private set; }
        public bool ShiftEnded { get; private set; }
        public bool HasPrimarySale => Order(DealOrderId.A).State == DealOrderState.Completed ||
            Order(DealOrderId.B).State == DealOrderState.Completed;
        public bool BuybackAvailable => !ShiftEnded && (IsCity ? LotPurchased : IsFirstDeal && (HasPrimarySale || ElapsedSeconds >= 1080));
        public bool ContactBOpened => Order(DealOrderId.B).State == DealOrderState.Completed;
        public int OwnedLampCount
        {
            get
            {
                var count = 0;
                foreach (var unit in units) if (IsOwned(unit)) count++;
                return count;
            }
        }

        public static bool IsOwned(FlashlightUnit unit) => unit.Location == FlashlightLocation.Hands ||
            unit.Location == FlashlightLocation.Crate || unit.Location == FlashlightLocation.Bench ||
            unit.Location == FlashlightLocation.Pocket || unit.Location == FlashlightLocation.World;

        // Exact physically presented identities are validated together before any transfer.
        public bool SellBack(IReadOnlyList<string> presentedIds, out string message)
        {
            if (!CanHandleItems(out message)) return false;
            if (!BuybackAvailable) return Reject("Выкуп откроется после продажи A или Б, либо в 18:00 после истечения обоих сроков.", out message);
            if (presentedIds == null || presentedIds.Count == 0)
                return Reject("Принеси остатки продавцу в руках или в переносимом ящике.", out message);
            var selected = new List<FlashlightUnit>();
            var unique = new HashSet<string>();
            var payment = 0;
            foreach (var identity in presentedIds)
            {
                FlashlightUnit found = null;
                foreach (var unit in units) if (unit.InstanceId == identity) { found = unit; break; }
                if (identity == null || !unique.Add(identity) || found == null ||
                    !(found.Location == FlashlightLocation.Hands || (IsCrateCarried && found.Location == FlashlightLocation.Crate)))
                    return Reject("Принеси каждый свой фонарь сюда. Проданные, хранящиеся и повторяющиеся экземпляры не принимаются.", out message);
                if (!found.IsTested) return Reject("Для выкупа сначала проверь каждый предъявленный фонарь на столе; после ремонта нужен новый тест.", out message);
                payment += found.LastTestPassed && found.Condition == FlashlightCondition.Working ? 60 : 35;
                selected.Add(found);
            }
            if (Balance > int.MaxValue - payment) return Reject("Wallet limit reached.", out message);
            foreach (var unit in selected) unit.Location = FlashlightLocation.BoughtBack;
            Balance += payment;
            Revenue += payment;
            UpdateStockAvailability();
            return Complete("Выкуп: +$" + payment + ". Передано фонарей: " + selected.Count + ". Остаток: " + OwnedLampCount + ".", out message);
        }

        public bool EndShift(bool abandon, out string message)
        {
            if (!IsFirstDeal || ShiftEnded) return Reject("Смена уже завершена или недоступна.", out message);
            if (!HasPrimarySale && !abandon)
                return Reject("Основной продажи нет. Подтверди отказ от продолжения или вернись к сделкам и выкупу.", out message);
            ShiftEnded = true;
            IsPaused = true;
            return Complete(HasPrimarySale ? "Смена завершена по твоему выбору." : "Смена завершена без основной продажи.", out message);
        }
    }
}
