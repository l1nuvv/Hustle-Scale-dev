using System;

namespace Inventory
{
    public enum FlashlightCondition { Working, BatteryFault, ConnectionFault }
    public enum FlashlightLocation { Crate, Hands, Bench, Seller, BuyerA, BuyerB, BoughtBack, Pocket, World, SolderBuyer }

    // Runtime identity and actual condition are separate from the player's knowledge.
    public sealed class FlashlightUnit
    {
        public string InstanceId { get; } = Guid.NewGuid().ToString("N");
        public FlashlightCondition Condition { get; internal set; }
        public FlashlightCondition InitialCondition { get; }
        public FlashlightLocation Location { get; internal set; }
        public bool IsTested { get; internal set; }
        public bool LastTestPassed { get; internal set; }
        public bool IsPowered { get; internal set; }
        public string KnownCondition => !IsTested ? "Not tested" :
            LastTestPassed ? "Tested: working" : Condition == FlashlightCondition.ConnectionFault ?
            "Батарея исправна; нарушено соединение — нужна пайка" : "Tested: battery fault";

        internal FlashlightUnit(FlashlightCondition condition, string instanceId = null)
        {
            if (instanceId != null) InstanceId = instanceId;
            Condition = condition;
            InitialCondition = condition;
            Location = FlashlightLocation.Crate;
        }

        internal FlashlightUnit(FlashlightCondition condition, FlashlightCondition initialCondition, string instanceId)
            : this(condition, instanceId) => InitialCondition = initialCondition;
    }
}
