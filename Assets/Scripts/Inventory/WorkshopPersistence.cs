using System;
using System.Collections.Generic;

namespace Inventory
{
    public sealed partial class WorkshopSession
    {
        public static WorkshopSession CreateCity(int? randomSeed = null)
        {
            var session = new WorkshopSession(300)
            {
                IsCity = true, ShiftStarted = true, IsPaused = true,
                cityRandom = randomSeed.HasValue ? new Random(randomSeed.Value) : new Random()
            };
            foreach (var unit in session.units) unit.Location = FlashlightLocation.Seller;
            session.orders[0].DeadlineSeconds = 0;
            session.orders[1].DeadlineSeconds = 0;
            return session;
        }

        public CitySaveData CaptureCity(CityWorldData world)
        {
            if (!IsCity) throw new InvalidOperationException("Only city sessions use this save format.");
            var data = new CitySaveData
            {
                Version = 3, Balance = Balance, InitialBalance = InitialBalance, Revenue = Revenue,
                Expenses = Expenses, SpareBatteries = SpareBatteries, RepairSeconds = RepairSeconds,
                LotPurchased = LotPurchased, HasTransportedCrate = HasTransportedCrate,
                IsCrateCarried = IsCrateCarried, ElapsedSeconds = ElapsedSeconds, World = world,
                IsCrateInVehicle = IsCrateInVehicle,
                PurchasedLots = PurchasedLots,
                PreviousLamps = CaptureLamps(previousUnits), PreviousOrders = CaptureOrders(previousOrders),
                Lamps = new CityLampData[units.Length], Orders = new CityOrderData[2]
            };
            for (var i = 0; i < units.Length; i++)
            {
                var unit = units[i];
                data.Lamps[i] = new CityLampData
                {
                    InstanceId = unit.InstanceId, Condition = unit.Condition, InitialCondition = unit.InitialCondition, Location = unit.Location,
                    IsTested = unit.IsTested, LastTestPassed = unit.LastTestPassed, IsPowered = unit.IsPowered
                };
            }
            for (var i = 0; i < data.Orders.Length; i++)
                data.Orders[i] = new CityOrderData { Id = orders[i].Id, State = orders[i].State, DeadlineSeconds = orders[i].DeadlineSeconds };
            return data;
        }

        private static CityLampData[] CaptureLamps(FlashlightUnit[] source)
        {
            if (source == null) return null;
            return Array.ConvertAll(source, unit => new CityLampData
            {
                InstanceId = unit.InstanceId, Condition = unit.Condition, InitialCondition = unit.InitialCondition,
                Location = unit.Location, IsTested = unit.IsTested, LastTestPassed = unit.LastTestPassed, IsPowered = unit.IsPowered
            });
        }

        private static CityOrderData[] CaptureOrders(DealOrder[] source) => source == null ? null :
            Array.ConvertAll(source, order => new CityOrderData { Id = order.Id, State = order.State, DeadlineSeconds = order.DeadlineSeconds });

        private static FlashlightUnit RestoreLamp(CityLampData lamp, FlashlightCondition initial) =>
            new FlashlightUnit(lamp.Condition, initial, lamp.InstanceId)
            {
                Location = lamp.Location, IsTested = lamp.IsTested, LastTestPassed = lamp.LastTestPassed, IsPowered = lamp.IsPowered
            };

        private static DealOrder RestoreOrder(CityOrderData order) => new DealOrder(order.Id,
            order.Id == DealOrderId.A ? 2 : 1, order.Id == DealOrderId.A ? 260 : 125, 0, order.State)
        { DeadlineSeconds = order.DeadlineSeconds };

        // Build a separate candidate only after validating the entire snapshot. Never mutate a live session.
        public static bool TryRestoreCity(CitySaveData data, out WorkshopSession session, out string message)
        {
            session = null;
            if (!ValidateCity(data, out message)) return false;
            var restored = CreateCity();
            restored.Balance = data.Balance;
            restored.Revenue = data.Revenue;
            restored.Expenses = data.Expenses;
            restored.SpareBatteries = data.SpareBatteries;
            restored.RepairSeconds = data.RepairSeconds;
            restored.LotPurchased = data.LotPurchased;
            restored.PurchasedLots = data.Version < 3 ? (data.LotPurchased ? 1 : 0) : data.PurchasedLots;
            if (restored.PurchasedLots == 2)
            {
                restored.previousUnits = Array.ConvertAll(data.PreviousLamps, lamp => RestoreLamp(lamp, lamp.InitialCondition));
                restored.previousOrders = Array.ConvertAll(data.PreviousOrders, RestoreOrder);
            }
            restored.HasTransportedCrate = data.HasTransportedCrate;
            restored.IsCrateCarried = data.IsCrateCarried;
            restored.IsCrateInVehicle = data.IsCrateInVehicle;
            restored.ElapsedSeconds = data.ElapsedSeconds;
            for (var i = 0; i < restored.units.Length; i++)
            {
                var lamp = data.Lamps[i];
                restored.units[i] = RestoreLamp(lamp, data.Version < 3 ?
                    (i == 1 ? FlashlightCondition.BatteryFault : FlashlightCondition.Working) : lamp.InitialCondition);
            }
            for (var i = 0; i < 2; i++)
            {
                restored.orders[i].State = data.Orders[i].State;
                restored.orders[i].DeadlineSeconds = data.Orders[i].DeadlineSeconds;
            }
            session = restored;
            message = "Состояние восстановлено. Игра на паузе.";
            return true;
        }

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        private static bool ValidPosition(UnityEngine.Vector3 position) => Finite(position.x) &&
            Finite(position.y) && Finite(position.z) && Math.Abs(position.x) <= 500 &&
            Math.Abs(position.z) <= 500 && position.y >= -0.1f && position.y <= 30;

        private static bool ValidateCity(CitySaveData data, out string message)
        {
            message = "Некорректное или несовместимое сохранение города.";
            if (data == null || data.Version < 1 || data.Version > 3 || data.InitialBalance != 300 || data.Balance < 0 ||
                data.Revenue < 0 || data.Expenses < 0 || data.Revenue > int.MaxValue ||
                data.Expenses > (long)int.MaxValue * 50 + 360 || data.SpareBatteries < 0 ||
                (long)data.InitialBalance + data.Revenue - data.Expenses != data.Balance ||
                !Finite(data.ElapsedSeconds) || data.ElapsedSeconds < data.RepairSeconds ||
                data.ElapsedSeconds > int.MaxValue || data.RepairSeconds < 0 || data.RepairSeconds > 1440 || data.RepairSeconds % 240 != 0 ||
                data.Lamps == null || data.Lamps.Length != 3 || data.Orders == null || data.Orders.Length != 2)
                return false;

            var lots = data.Version < 3 ? (data.LotPurchased ? 1 : 0) : data.PurchasedLots;
            if (lots < 0 || lots > 2 || data.LotPurchased != (lots > 0) ||
                (lots == 2 && (data.PreviousLamps == null || data.PreviousLamps.Length != 3 ||
                    data.PreviousOrders == null || data.PreviousOrders.Length != 2)) ||
                (lots != 2 && ((data.PreviousLamps != null && data.PreviousLamps.Length != 0) ||
                    (data.PreviousOrders != null && data.PreviousOrders.Length != 0)))) return false;

            var ids = new HashSet<string>();
            var hands = 0;
            var benches = 0;
            var buyerA = 0;
            var buyerB = 0;
            var owned = 0;
            long revenue = 0;
            var repairs = 0;
            for (var lot = 0; lot < (lots == 2 ? 2 : 1); lot++)
            {
                var lamps = lot == 0 ? data.Lamps : data.PreviousLamps;
                var lotOrders = lot == 0 ? data.Orders : data.PreviousOrders;
                buyerA = buyerB = owned = 0;
                for (var i = 0; i < lamps.Length; i++)
                {
                    var lamp = lamps[i];
                    var initial = data.Version < 3 ? (i == 1 ? FlashlightCondition.BatteryFault : FlashlightCondition.Working) : lamp?.InitialCondition;
                    if (lamp == null || !Guid.TryParseExact(lamp.InstanceId, "N", out _) || !ids.Add(lamp.InstanceId) ||
                        !Enum.IsDefined(typeof(FlashlightCondition), lamp.Condition) ||
                        !initial.HasValue || !Enum.IsDefined(typeof(FlashlightCondition), initial.Value) ||
                        !Enum.IsDefined(typeof(FlashlightLocation), lamp.Location) ||
                        (initial == FlashlightCondition.Working && lamp.Condition != FlashlightCondition.Working) ||
                        (lamp.LastTestPassed && !lamp.IsTested) ||
                        (lamp.IsTested && lamp.LastTestPassed != (lamp.Condition == FlashlightCondition.Working)) ||
                        (lamp.IsPowered && lamp.Condition != FlashlightCondition.Working) ||
                        (!data.LotPurchased && (lamp.Location != FlashlightLocation.Seller || lamp.IsTested || lamp.IsPowered)) ||
                        (data.LotPurchased && lamp.Location == FlashlightLocation.Seller)) return false;
                    if (initial == FlashlightCondition.BatteryFault && lamp.Condition == FlashlightCondition.Working) repairs++;
                    if (lamp.Location == FlashlightLocation.Hands) hands++;
                    if (lamp.Location == FlashlightLocation.Bench) benches++;
                    if (lamp.Location == FlashlightLocation.BuyerA) buyerA++;
                    if (lamp.Location == FlashlightLocation.BuyerB) buyerB++;
                    if (lamp.Location == FlashlightLocation.BoughtBack)
                    {
                        if (!lamp.IsTested) return false;
                        revenue += lamp.LastTestPassed ? 60 : 35;
                    }
                    else if (lamp.Location == FlashlightLocation.BuyerA || lamp.Location == FlashlightLocation.BuyerB)
                    {
                        if (!lamp.IsTested || !lamp.LastTestPassed) return false;
                    }
                    else if (lamp.Location != FlashlightLocation.Seller) owned++;
                }
                if (lot == 1 && owned != 0) return false;

                for (var i = 0; i < 2; i++)
                {
                    var order = lotOrders[i];
                    var duration = i == 0 ? 540 : 1080;
                    // JsonUtility can round a deadline upward by a few double ULPs.
                    // This tolerance only validates its origin; actual expiry checks remain exact.
                    const double deadlineOriginTolerance = 0.000001;
                    if (order == null || order.Id != (DealOrderId)i || !Enum.IsDefined(typeof(DealOrderState), order.State) ||
                        order.State == DealOrderState.Locked || order.State == DealOrderState.Expired ||
                        !Finite(order.DeadlineSeconds) || order.DeadlineSeconds < 0 ||
                        order.DeadlineSeconds > data.ElapsedSeconds + duration + deadlineOriginTolerance) return false;
                    var wasAccepted = order.State == DealOrderState.Accepted || order.State == DealOrderState.Completed ||
                        order.State == DealOrderState.Failed || order.State == DealOrderState.Cancelled;
                    if ((wasAccepted && order.DeadlineSeconds < duration) ||
                        ((order.State == DealOrderState.Available || order.State == DealOrderState.Declined) && order.DeadlineSeconds != 0) ||
                        (order.State == DealOrderState.Accepted && order.DeadlineSeconds <= data.ElapsedSeconds) ||
                        (order.State == DealOrderState.Failed && order.DeadlineSeconds > data.ElapsedSeconds)) return false;
                    var delivered = i == 0 ? buyerA : buyerB;
                    var quantity = i == 0 ? 2 : 1;
                    if (delivered != (order.State == DealOrderState.Completed ? quantity : 0) ||
                        (data.LotPurchased && owned < quantity && (order.State == DealOrderState.Available || order.State == DealOrderState.Accepted)) ||
                        (order.State == DealOrderState.Unavailable && (!data.LotPurchased || owned >= quantity))) return false;
                    if (order.State == DealOrderState.Completed) revenue += i == 0 ? 260 : 125;
                }
            }
            if (hands > 1 || benches > 1 || (data.IsCrateCarried && hands != 0) ||
                (data.IsCrateInVehicle && (data.Version < 2 || data.IsCrateCarried || !data.LotPurchased || !data.HasTransportedCrate)) ||
                (!data.LotPurchased && (data.IsCrateCarried || data.HasTransportedCrate)) ||
                (data.IsCrateCarried && !data.HasTransportedCrate) ||
                data.Expenses != lots * 180L + ((long)data.SpareBatteries + repairs) * 50 ||
                data.RepairSeconds != repairs * 240 || data.Revenue != revenue) return false;

            var world = data.World;
            if (data.IsCrateInVehicle && (world == null || !world.HasVehiclePose)) return false;
            if (world != null && world.HasVehiclePose &&
                (!ValidPosition(world.VehiclePosition) || !Finite(world.VehicleEuler.x) ||
                 !Finite(world.VehicleEuler.y) || !Finite(world.VehicleEuler.z) ||
                 Math.Abs(world.VehicleEuler.x) > 360 || Math.Abs(world.VehicleEuler.y) > 360 ||
                 Math.Abs(world.VehicleEuler.z) > 360)) return false;
            if (world == null || !ValidPosition(world.PlayerPosition) || !Finite(world.PlayerYaw) ||
                Math.Abs(world.PlayerYaw) > 360 || !Finite(world.CameraPitch) || Math.Abs(world.CameraPitch) > 85 ||
                !Enum.IsDefined(typeof(CityCrateDock), world.CrateDock) ||
                (!data.HasTransportedCrate && world.CrateDock != CityCrateDock.Seller) || world.PlacedLamps == null)
                return false;
            var placed = new HashSet<string>();
            foreach (var pose in world.PlacedLamps)
            {
                if (pose == null || !placed.Add(pose.InstanceId) || !ValidPosition(pose.Position) ||
                    !Finite(pose.Yaw) || Math.Abs(pose.Yaw) > 360) return false;
                var lamp = Array.Find(data.Lamps, item => item.InstanceId == pose.InstanceId);
                if (lamp == null || lamp.Location != FlashlightLocation.World) return false;
            }
            foreach (var lamp in data.Lamps)
                if ((lamp.Location == FlashlightLocation.World) != placed.Contains(lamp.InstanceId)) return false;
            message = "";
            return true;
        }
    }
}
