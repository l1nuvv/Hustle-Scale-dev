using System;
using System.Collections.Generic;

namespace Inventory
{
    // T03 starts after the lot purchase. T04 will supply purchase, wallet and clock integration.
    public sealed class WorkshopSession
    {
        private readonly FlashlightUnit[] units =
        {
            new FlashlightUnit(FlashlightCondition.Working),
            new FlashlightUnit(FlashlightCondition.BatteryFault),
            new FlashlightUnit(FlashlightCondition.Working)
        };

        public IReadOnlyList<FlashlightUnit> Units { get; }
        public bool IsCrateCarried { get; private set; }
        public int Balance { get; private set; }
        public int SpareBatteries { get; private set; }
        public int RepairSeconds { get; private set; }
        public event Action Changed;
        public FlashlightUnit InHands => Find(FlashlightLocation.Hands);
        public FlashlightUnit OnBench => Find(FlashlightLocation.Bench);

        public WorkshopSession(int balance = 120)
        {
            if (balance < 0) throw new ArgumentOutOfRangeException(nameof(balance));
            Balance = balance;
            Units = Array.AsReadOnly(units);
        }

        private FlashlightUnit Find(FlashlightLocation location)
        {
            foreach (var unit in units) if (unit.Location == location) return unit;
            return null;
        }

        private bool Complete(string text, out string message)
        {
            message = text;
            Changed?.Invoke();
            return true;
        }

        private static bool Reject(string text, out string message)
        {
            message = text;
            return false;
        }

        public bool ToggleCrate(out string message)
        {
            if (InHands != null) return Reject("Return the lamp before carrying the crate.", out message);
            IsCrateCarried = !IsCrateCarried;
            return Complete(IsCrateCarried ? "Crate in hands. All stored lamps move with it." :
                "Crate returned to its dock.", out message);
        }

        public bool ToggleUnit(int index, out string message)
        {
            if (index < 0 || index >= units.Length) return Reject("Unknown lamp.", out message);
            if (IsCrateCarried) return Reject("Dock the crate first.", out message);
            var unit = units[index];
            if (unit.Location == FlashlightLocation.Hands)
            {
                unit.Location = FlashlightLocation.Crate;
                return Complete("Lamp returned to the crate.", out message);
            }
            if (unit.Location != FlashlightLocation.Crate) return Reject("This lamp is on the bench.", out message);
            if (InHands != null) return Reject("Hands occupied. Store or place that lamp first.", out message);
            unit.Location = FlashlightLocation.Hands;
            return Complete("Lamp in hands: " + unit.KnownCondition, out message);
        }

        public bool ToggleBench(out string message)
        {
            if (IsCrateCarried) return Reject("Dock the crate first.", out message);
            if (OnBench != null)
            {
                if (InHands != null) return Reject("Hands occupied; bench already contains a lamp.", out message);
                OnBench.Location = FlashlightLocation.Hands;
                return Complete("Lamp retrieved from bench.", out message);
            }
            if (InHands == null) return Reject("Take a lamp from the crate first.", out message);
            InHands.Location = FlashlightLocation.Bench;
            return Complete("Lamp placed on bench. Run the test.", out message);
        }

        public bool Test(out string message)
        {
            var unit = OnBench;
            if (unit == null) return Reject("Place a lamp on the bench first.", out message);
            unit.IsTested = true;
            unit.LastTestPassed = unit.Condition == FlashlightCondition.Working;
            return Complete(unit.KnownCondition, out message);
        }

        public bool BuyBattery(out string message)
        {
            if (Balance < 50) return Reject("Not enough money: battery costs $50.", out message);
            if (SpareBatteries == int.MaxValue) return Reject("Battery stock is full.", out message);
            Balance -= 50;
            SpareBatteries++;
            return Complete("Battery purchased: -$50. It remains in stock until used.", out message);
        }

        public bool Repair(out string message)
        {
            var unit = OnBench;
            if (unit == null) return Reject("Place a lamp on the bench first.", out message);
            if (!unit.IsTested) return Reject("Test the lamp before replacing its battery.", out message);
            if (unit.Condition != FlashlightCondition.BatteryFault)
                return Reject("This lamp does not need a replacement battery.", out message);
            if (SpareBatteries == 0) return Reject("Buy a spare battery for $50 first.", out message);
            if (RepairSeconds > int.MaxValue - 240) return Reject("Repair time limit reached.", out message);
            SpareBatteries--;
            RepairSeconds += 240;
            unit.Condition = FlashlightCondition.Working;
            unit.IsTested = false;
            unit.LastTestPassed = false;
            return Complete("Battery replaced. +04:00 game time. Run a new test.", out message);
        }
    }
}
