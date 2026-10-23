using System;

namespace Inventory
{
    public sealed partial class WorkshopSession
    {
        public long CheatMoneyAdjustment { get; private set; }
        public bool CheatsUsed { get; private set; }
        private bool CanCheat => IsCity && IsPaused;
        private const long MaxCheatAdjustment = 1000000000000L;

        public bool CheatSetBalance(int balance, out string message)
        {
            if (!CanCheat || balance < 0 || balance > 1000000)
                return Reject("Читы доступны в городской паузе; баланс от $0 до $1 000 000.", out message);
            var adjustment = CheatMoneyAdjustment + (long)balance - Balance;
            if (Math.Abs(adjustment) > MaxCheatAdjustment) return Reject("Достигнут предел чит-выдач.", out message);
            CheatMoneyAdjustment = adjustment;
            Balance = balance;
            CheatsUsed = true;
            return Complete("Чит: баланс $" + balance + ".", out message);
        }

        public bool CheatSupplies(bool solder, int count, out string message)
        {
            if (!CanCheat || count < 1 || count > 100 || (solder && !StationOwned))
                return Reject("Нужна городская пауза; для припоя сначала купи станцию.", out message);
            var stock = solder ? PurchasedSolderDoses : SpareBatteries;
            var cost = count * (solder ? 5L : 50L);
            if (stock > int.MaxValue - count || CheatMoneyAdjustment > MaxCheatAdjustment - cost)
                return Reject("Достигнут предел запаса или чит-выдач.", out message);
            // Record the funded purchase so normal repair/consumption invariants still hold.
            if (solder) { SolderDoses += count; PurchasedSolderDoses += count; }
            else SpareBatteries += count;
            Expenses += cost;
            CheatMoneyAdjustment += cost;
            CheatsUsed = true;
            return Complete(solder ? "Чит: добавлен припой." : "Чит: добавлены батареи.", out message);
        }

        public bool CheatDiagnose(out string message)
        {
            if (!CanCheat) return Reject("Нужна городская пауза.", out message);
            foreach (var unit in units)
            {
                if (!IsOwned(unit)) continue;
                unit.IsTested = true;
                unit.LastTestPassed = unit.Condition == FlashlightCondition.Working;
            }
            CheatsUsed = true;
            return Complete("Чит: все свои обычные фонари проверены.", out message);
        }

        public bool CheatAdvanceTime(double seconds, out string message)
        {
            if (!CanCheat || !Finite(seconds) || seconds <= 0 || seconds > 3600)
                return Reject("Перемотка в паузе: от 0 до 3600 секунд.", out message);
            AddElapsedSeconds(seconds);
            CheatsUsed = true;
            return Complete("Чит: время вперёд на " + seconds + " с; сроки заказов пересчитаны.", out message);
        }

        public void MarkCheatsUsed()
        {
            if (!CanCheat || CheatsUsed) return;
            CheatsUsed = true;
            Changed?.Invoke();
            TransactionCompleted?.Invoke();
        }
    }
}
