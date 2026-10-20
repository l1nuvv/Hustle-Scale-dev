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
        public bool LotPurchased, HasTransportedCrate, IsCrateCarried;
        public bool IsCrateInVehicle;
        public int PurchasedLots;
        public CityLampData[] PreviousLamps;
        public CityOrderData[] PreviousOrders;
        public double ElapsedSeconds;
        public CityLampData[] Lamps;
        public CityOrderData[] Orders;
        public CityWorldData World;
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
        public Vector3 PlayerPosition;
        public float PlayerYaw, CameraPitch;
        public CityCrateDock CrateDock;
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
