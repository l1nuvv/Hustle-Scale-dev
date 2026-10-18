using System;

namespace Inventory
{
    public enum FlashlightCondition { Working, BatteryFault }
    public enum FlashlightLocation { Crate, Hands, Bench, Seller, BuyerA, BuyerB, BoughtBack }

    // Runtime identity and actual condition are separate from the player's knowledge.
    public sealed class FlashlightUnit
    {
        public string InstanceId { get; } = Guid.NewGuid().ToString("N");
        public FlashlightCondition Condition { get; internal set; }
        public FlashlightLocation Location { get; internal set; }
        public bool IsTested { get; internal set; }
        public bool LastTestPassed { get; internal set; }
        public string KnownCondition => !IsTested ? "Not tested" :
            LastTestPassed ? "Tested: working" : "Tested: battery fault";

        internal FlashlightUnit(FlashlightCondition condition)
        {
            Condition = condition;
            Location = FlashlightLocation.Crate;
        }
    }
}
