namespace Inventory
{
    public sealed partial class WorkshopSession
    {
        public bool CanPickUpCrate => CanHandleItems(out _) && !IsCrateCarried && !IsCrateInVehicle && InHands == null;
        public bool CanPickUpBench => CanHandleItems(out _) && !IsCrateCarried && OnBench != null && InHands == null && !AtSolderingStation;
        public bool CanUnloadCrate => CanHandleItems(out _) && IsCity && IsCrateInVehicle && !IsCrateCarried && InHands == null;
        public bool CanPickUpUnit(int index) => CanHandleItems(out _) && !IsCrateInVehicle && !IsCrateCarried &&
            InHands == null && CityUnit(index) != null && CityUnit(index).Location == FlashlightLocation.Crate;
        public bool CanPickUpPlaced(int index) => CanHandleItems(out _) && !IsCrateCarried && CityUnit(index) != null &&
            CityUnit(index).Location == FlashlightLocation.World;
    }
}
