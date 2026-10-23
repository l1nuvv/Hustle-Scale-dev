using System;
using UnityEngine;

namespace Inventory
{
    // Values only. No scene references or commands are serialized.
    [Serializable]
    public sealed class CitySaveData
    {
        public int Version;
        public int Balance, InitialBalance, SpareBatteries, RepairSeconds;
        public long Revenue, Expenses;
        public long CheatMoneyAdjustment;
        public bool CheatsUsed;
        public bool LotPurchased, HasTransportedCrate, IsCrateCarried;
        public bool IsCrateInVehicle;
        public int PurchasedLots;
        public CityLampData[] PreviousLamps;
        public CityOrderData[] PreviousOrders;
        public double ElapsedSeconds;
        public CityLampData[] Lamps;
        public CityOrderData[] Orders;
        public CityWorldData World;
        public CitySolderingData Soldering;
        public CitySharpeningData Sharpening;
    }

    public enum KnifeLocation { Pocket, Hands, World, Station, Buyer, BoughtBack }

    [Serializable]
    public sealed class CitySharpeningData
    {
        public bool StationOwned, KnifePurchased, FinalTest;
        public string InstanceId, DeliveredId;
        public KnifeLocation Location;
        public Vector3 Position;
        public float Yaw;
        public float[] Coverage = new float[12];
        public int CoatingSeed;
        public CityOrderData Order = new CityOrderData { Id = DealOrderId.Sharpen, State = DealOrderState.Locked };
    }

    [Serializable]
    public sealed class CitySolderingData
    {
        public bool StationOwned, LampPurchased, Diagnosed, AtStation;
        public int Doses, PurchasedDoses;
        public CityLampData Lamp;
        public bool CoverAttached = true, Secured0 = true, Secured1 = true;
        public bool HasSolder, FinalTest;
        public float Progress;
        public CityOrderData Order;
        public string DeliveredId;
    }

    [Serializable]
    public sealed class CityLampData
    {
        public string InstanceId;
        public FlashlightCondition Condition;
        public FlashlightCondition InitialCondition;
        public FlashlightLocation Location;
        public bool IsTested, LastTestPassed, IsPowered;
    }

    [Serializable]
    public sealed class CityOrderData
    {
        public DealOrderId Id;
        public DealOrderState State;
        public double DeadlineSeconds;
    }

    public enum CityCrateDock { Seller, Workshop }

    [Serializable]
    public sealed class CityWorldData
    {
        public int[] InventoryLampStacks;
        public int InventorySort;
        public Vector3 PlayerPosition;
        public float PlayerYaw, CameraPitch;
        public CityCrateDock CrateDock;
        public bool HasCratePose;
        public Vector3 CratePosition;
        public float CrateYaw;
        public bool HasVehiclePose;
        public Vector3 VehiclePosition, VehicleEuler;
        public CityPlacedLamp[] PlacedLamps;
    }

    [Serializable]
    public sealed class CityPlacedLamp
    {
        public string InstanceId;
        public Vector3 Position;
        public float Yaw;
    }
}
