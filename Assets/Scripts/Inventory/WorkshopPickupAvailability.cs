namespace Inventory
{
    public sealed partial class WorkshopSession
    {
        public bool CanPickUpCrate => CanHandleItems(out _) && !IsCrateCarried && !IsCrateInVehicle && InHands == null && !KnifeInHands;
        public bool CanPickUpBench => CanHandleItems(out _) && !IsCrateCarried && OnBench != null && InHands == null && !KnifeInHands && !AtSolderingStation;
        public bool CanUnloadCrate => CanHandleItems(out _) && IsCity && IsCrateInVehicle && !IsCrateCarried && InHands == null && !KnifeInHands;
        public bool CanPickUpUnit(int index) => CanHandleItems(out _) && !IsCrateInVehicle && !IsCrateCarried &&
            InHands == null && !KnifeInHands && CityUnit(index) != null && CityUnit(index).Location == FlashlightLocation.Crate;
        public bool CanPickUpPlaced(int index) => CanHandleItems(out _) && !IsCrateCarried && !KnifeInHands && CityUnit(index) != null &&
            CityUnit(index).Location == FlashlightLocation.World;
    }
}
