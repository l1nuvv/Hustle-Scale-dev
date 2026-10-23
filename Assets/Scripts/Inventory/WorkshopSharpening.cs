using System;
using System.Collections.Generic;
using Interactables;
using UnityEngine;

namespace Inventory
{
    public sealed partial class WorkshopSession
    {
        private CitySharpeningData sharpening = new CitySharpeningData();
        private SharpeningTrialState sharpeningState;
        private readonly DealOrder sharpeningOrder = new DealOrder(DealOrderId.Sharpen, 1, 500, 0, DealOrderState.Locked);
        public bool SharpeningStationOwned => sharpening.StationOwned;
        public bool KnifePurchased => sharpening.KnifePurchased;
        public string KnifeId => sharpening.InstanceId;
        public KnifeLocation KnifePlace => sharpening.Location;
        public Vector3 KnifePosition => sharpening.Position;
        public float KnifeYaw => sharpening.Yaw;
        public int KnifeCoatingSeed => sharpening.CoatingSeed;
        public bool KnifeInHands => IsCity && KnifePurchased && KnifePlace == KnifeLocation.Hands;
        public bool KnifeOwned => KnifePurchased && KnifePlace != KnifeLocation.Buyer && KnifePlace != KnifeLocation.BoughtBack;
        public bool KnifePresented => KnifeOwned && (KnifePlace == KnifeLocation.Hands || KnifePlace == KnifeLocation.Pocket);
        public bool KnifeReady => KnifePurchased && SharpeningState.Complete && sharpening.FinalTest;
        public bool SharpeningOperating { get; private set; }
        public bool CanBuySharpeningStation => IsCity && !SharpeningStationOwned && PurchasedLots > 0 &&
            (previousUnits != null || OwnedLampCount == 0 && SellerLampCount == 0);
        public SharpeningTrialState SharpeningState
        {
            get
            {
                if (sharpeningState == null)
                {
                    sharpeningState = new SharpeningTrialState();
                    sharpeningState.OperatingAllowed = () => IsCity && !IsPaused && SharpeningOperating && KnifePlace == KnifeLocation.Station;
                    sharpeningState.Changed += () => { sharpening.FinalTest = false; };
                }
                return sharpeningState;
            }
        }
        public bool PurchaseSharpeningStation(bool placeAvailable, out string message)
        {
            if (IsPaused || !CanBuySharpeningStation) return Reject("Продай всю первую партию. Станок покупается один раз.", out message);
            if (!placeAvailable) return Reject("Место станка занято; деньги не списаны.", out message);
            if (Balance < 400) return Reject("Станок стоит $400.", out message);
            Balance -= 400; Expenses += 400; sharpening.StationOwned = true;
            sharpeningOrder.State = DealOrderState.Available;
            return Complete("Заточка открыта · станок установлен · −$400", out message);
        }
        public bool PurchaseKnife(out string message)
        {
            if (!IsCity || IsPaused || !SharpeningStationOwned || KnifePurchased) return Reject("Нужен станок; нож покупается один раз.", out message);
            if (Balance < 40) return Reject("Нож стоит $40.", out message);
            Balance -= 40; Expenses += 40;
            sharpening.KnifePurchased = true; sharpening.InstanceId = Guid.NewGuid().ToString("N");
            sharpening.CoatingSeed = new System.Random().Next(1, int.MaxValue);
            sharpening.Location = KnifeLocation.Pocket;
            return Complete("Тупой кухонный нож в инвентаре · −$40 · Tab — выбрать", out message);
        }
        public bool EquipKnife(out string message)
        {
            if (!CanHandleItems(out message)) return false;
            if (!KnifePresented || IsCrateCarried || InHands != null) return Reject("Освободи руки; выбери свой нож в инвентаре.", out message);
            sharpening.Location = KnifeInHands ? KnifeLocation.Pocket : KnifeLocation.Hands;
            return Complete(KnifeInHands ? "Нож в руках · Q — поставить · G — убрать" : "Нож убран в инвентарь", out message);
        }
        public bool MoveKnife(KnifeLocation destination, Vector3 position, float yaw, out string message)
        {
            if (!CanHandleItems(out message)) return false;
            if (!KnifeOwned || SharpeningOperating) return Reject("Нож недоступен.", out message);
            var take = destination == KnifeLocation.Hands && (KnifePlace == KnifeLocation.World || KnifePlace == KnifeLocation.Station);
            var drop = destination == KnifeLocation.World && KnifeInHands;
            if (!take && !drop || take && (IsCrateCarried || InHands != null || KnifeInHands) ||
                drop && (!ValidPosition(position) || !Finite(yaw) || Math.Abs(yaw) > 360))
                return Reject("Освободи руки и место для ножа.", out message);
            sharpening.Location = destination;
            if (drop) { sharpening.Position = position; sharpening.Yaw = yaw; }
            SharpeningState.StopMotor();
            return Complete(take ? "Нож в руках · прогресс сохранён" : "Нож поставлен · E — подобрать", out message);
        }
        public bool BeginSharpening(out string message)
        {
            if (!CanHandleItems(out message)) return false;
            if (!SharpeningStationOwned || !KnifeOwned || IsCrateCarried || InHands != null ||
                KnifePlace != KnifeLocation.Hands && KnifePlace != KnifeLocation.Station)
                return Reject("Принеси нож в свободных руках или продолжи оставленный на станке.", out message);
            SharpeningOperating = true; sharpening.Location = KnifeLocation.Station;
            SharpeningState.StopMotor();
            return Complete("ЛКМ — поставить / удерживать · Esc — отойти", out message);
        }
        public void EndSharpening()
        {
            SharpeningOperating = false; SharpeningState.StopMotor();
            TransactionCompleted?.Invoke(); Changed?.Invoke();
        }
        public bool InspectKnife(bool withdrawn, out string message)
        {
            if (!IsCity || IsPaused || !SharpeningOperating || !KnifeOwned || !SharpeningState.Placed || !withdrawn || !SharpeningState.Complete)
                return Reject("Обработай всю кромку и отведи нож перед F.", out message);
            sharpening.FinalTest = true;
            return Complete("Нож проверен и готов · Esc — отойти · E — забрать", out message);
        }
        public bool DeliverKnife(IReadOnlyList<string> ids, out string message)
        {
            if (!CanHandleItems(out message)) return false;
            if (sharpeningOrder.State != DealOrderState.Accepted || ElapsedSeconds >= sharpeningOrder.DeadlineSeconds ||
                !KnifePresented || !KnifeReady || ids == null || ids.Count != 1 || ids[0] != KnifeId)
                return Reject("Предъяви свой проверенный нож по принятому заказу до срока.", out message);
            if (Balance > int.MaxValue - 500) return Reject("Кошелёк заполнен.", out message);
            sharpening.Location = KnifeLocation.Buyer; sharpening.DeliveredId = KnifeId;
            sharpeningOrder.State = DealOrderState.Completed; Balance += 500; Revenue += 500;
            return Complete("Заточка: нож передан · +$500", out message);
        }
        public bool SellKnife(string id, out string message)
        {
            if (!CanHandleItems(out message)) return false;
            var payment = KnifeReady ? 60 : 20;
            if (!KnifePresented || id != KnifeId || Balance > int.MaxValue - payment) return Reject("Принеси свой нож в руках или инвентаре.", out message);
            sharpening.Location = KnifeLocation.BoughtBack;
            if (sharpeningOrder.State == DealOrderState.Available || sharpeningOrder.State == DealOrderState.Accepted)
                sharpeningOrder.State = DealOrderState.Unavailable;
            Balance += payment; Revenue += payment;
            return Complete("Нож продан · +$" + payment, out message);
        }
        private CitySharpeningData CaptureSharpening() => new CitySharpeningData
        {
            StationOwned = SharpeningStationOwned, KnifePurchased = KnifePurchased, InstanceId = KnifeId,
            Location = KnifePlace, Position = KnifePosition, Yaw = KnifeYaw, CoatingSeed = KnifeCoatingSeed,
            FinalTest = sharpening.FinalTest, Coverage = SharpeningState.CaptureCoverage(), DeliveredId = sharpening.DeliveredId,
            Order = new CityOrderData { Id = DealOrderId.Sharpen, State = sharpeningOrder.State, DeadlineSeconds = sharpeningOrder.DeadlineSeconds }
        };
        private void RestoreSharpening(CitySharpeningData data)
        {
            // Copy values; callers cannot mutate the restored session through their DTO.
            sharpening = new CitySharpeningData
            {
                StationOwned = data.StationOwned, KnifePurchased = data.KnifePurchased,
                InstanceId = data.InstanceId, Location = data.Location, Position = data.Position, Yaw = data.Yaw,
                CoatingSeed = data.CoatingSeed, FinalTest = data.FinalTest, DeliveredId = data.DeliveredId
            };
            SharpeningState.Restore(data.Coverage, data.Location == KnifeLocation.Station);
            sharpeningOrder.State = data.Order.State; sharpeningOrder.DeadlineSeconds = data.Order.DeadlineSeconds;
        }
    }
}
