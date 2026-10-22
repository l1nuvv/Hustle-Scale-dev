using System;
using System.Collections.Generic;
using Interactables;

namespace Inventory
{
    public sealed partial class WorkshopSession
    {
        public bool StationOwned { get; private set; }
        public FlashlightUnit SolderingLamp { get; private set; }
        public int SolderDoses { get; private set; }
        public int PurchasedSolderDoses { get; private set; }
        public bool ConnectionDiagnosed { get; private set; }
        public bool AtSolderingStation { get; private set; }
        public string SolderingDeliveredId { get; private set; }
        private SolderingTrialState solderingState;
        private FlashlightRepairState repairState;
        private readonly DealOrder solderingOrder = new DealOrder(DealOrderId.Solder, 1, 400, 0, DealOrderState.Locked);
        public SolderingTrialState SolderingState { get { EnsureSolderingState(); return solderingState; } }
        public FlashlightRepairState RepairState { get { EnsureSolderingState(); return repairState; } }
        public FlashlightUnit CityUnit(int index) => index == 3 && IsCity ? SolderingLamp :
            index >= 0 && index < units.Length ? units[index] : null;
        private IEnumerable<FlashlightUnit> CarryUnits()
        {
            foreach (var unit in units) yield return unit;
            if (IsCity && SolderingLamp != null) yield return SolderingLamp;
        }
        public bool CanBuyStation => IsCity && !StationOwned && PurchasedLots > 0 &&
            (previousUnits != null || OwnedLampCount == 0 && SellerLampCount == 0);
        public bool CanAutosaveSoldering => repairState == null || repairState.HandsFree;
        public bool SolderingAssembled => RepairState.Cover == FlashlightRepairState.CoverPlace.Attached &&
            RepairState.IsSecured(0) && RepairState.IsSecured(1);
        public bool SolderingReady => SolderingLamp != null && SolderingAssembled && SolderingState.Complete &&
            SolderingLamp.IsTested && SolderingLamp.LastTestPassed;

        private void EnsureSolderingState()
        {
            if (solderingState != null) return;
            solderingState = new SolderingTrialState();
            repairState = new FlashlightRepairState(solderingState);
            solderingState.Supply = () =>
            {
                if (!IsCity || IsPaused || SolderingLamp == null || !AtSolderingStation || SolderDoses == 0) return false;
                SolderDoses--;
                return true;
            };
            solderingState.OperatingAllowed = () => !IsPaused && AtSolderingStation && SolderingLamp != null;
            repairState.CanAct = () => IsCity && !IsPaused && SolderingLamp != null && IsOwned(SolderingLamp) && ConnectionDiagnosed;
            solderingState.Changed += SolderingChanged;
            repairState.Changed += SolderingChanged;
        }

        private void SolderingChanged()
        {
            var lamp = SolderingLamp;
            if (lamp == null) return;
            if (repairState.Flashlight == FlashlightRepairState.ItemPlace.Bench)
            { lamp.Location = FlashlightLocation.Bench; AtSolderingStation = true; }
            lamp.Condition = solderingState.Complete ? FlashlightCondition.Working : FlashlightCondition.ConnectionFault;
            lamp.LastTestPassed = solderingState.Complete && SolderingAssembled && repairState.Tested;
            lamp.IsTested = lamp.LastTestPassed || (!solderingState.Complete && ConnectionDiagnosed);
            lamp.IsPowered = lamp.LastTestPassed && repairState.LightOn;
            Changed?.Invoke();
            TransactionCompleted?.Invoke();
        }

        public bool PurchaseStation(bool placeAvailable, out string message)
        {
            if (!IsCity || IsPaused || !CanBuyStation) return Reject("Сначала продай всю первую партию. Станция покупается один раз.", out message);
            if (!placeAvailable) return Reject("Место станции недоступно; деньги не списаны.", out message);
            if (Balance < 300) return Reject("Станция стоит $300.", out message);
            Balance -= 300; Expenses += 300; StationOwned = true;
            solderingOrder.State = DealOrderState.Available;
            return Complete("Пайка открыта · станция установлена · −$300", out message);
        }

        public bool PurchaseSolderingLamp(out string message)
        {
            if (!IsCity || IsPaused || !StationOwned || SolderingLamp != null)
                return Reject("Нужна станция; этот фонарик покупается один раз.", out message);
            if (InHands != null || IsCrateCarried) return Reject("Освободи руки.", out message);
            if (Balance < 40) return Reject("Фонарик стоит $40.", out message);
            SolderingLamp = new FlashlightUnit(FlashlightCondition.ConnectionFault) { Location = FlashlightLocation.Hands };
            Balance -= 40; Expenses += 40;
            EnsureSolderingState();
            return Complete("Фонарик в руках · −$40 · проверь соединение на верстаке", out message);
        }

        public bool BuySolderDose(out string message)
        {
            if (!IsCity || IsPaused || !StationOwned || Balance < 5 || PurchasedSolderDoses == int.MaxValue)
                return Reject("Нужна станция и $5 за дозу припоя.", out message);
            Balance -= 5; Expenses += 5; SolderDoses++; PurchasedSolderDoses++;
            return Complete("Припой +1 доза · −$5", out message);
        }

        public bool CanStartSoldering(out string message)
        {
            if (!CanHandleItems(out message)) return false;
            if (!StationOwned || SolderingLamp == null || !IsOwned(SolderingLamp) || !ConnectionDiagnosed)
                return Reject("Нужен свой фонарик с диагностированным дефектом соединения.", out message);
            if (IsCrateCarried || InHands != null && InHands != SolderingLamp ||
                OnBench != null && (!AtSolderingStation || OnBench != SolderingLamp))
                return Reject("Освободи руки и обычный верстак.", out message);
            if (!AtSolderingStation && SolderingLamp.Location != FlashlightLocation.Hands)
                return Reject("Принеси фонарик в руках.", out message);
            message = ""; return true;
        }

        public bool RetrieveSolderingLamp(out string message)
        {
            if (!CanHandleItems(out message)) return false;
            if (!AtSolderingStation || !SolderingAssembled || InHands != null || IsCrateCarried)
                return Reject("Сначала вручную собери фонарик и освободи руки.", out message);
            var tested = repairState.Tested;
            repairState.Restore(false, true, true, true, tested);
            SolderingLamp.IsPowered = false;
            SolderingLamp.Location = FlashlightLocation.Hands; AtSolderingStation = false;
            return Complete("Фонарик в руках · готовность сохранена", out message);
        }

        public bool DeliverSoldering(IReadOnlyList<string> ids, out string message)
        {
            if (!CanHandleItems(out message)) return false;
            var lamp = SolderingLamp;
            if (solderingOrder.State != DealOrderState.Accepted || ElapsedSeconds >= solderingOrder.DeadlineSeconds ||
                lamp == null || !SolderingReady || ids == null || ids.Count != 1 || ids[0] != lamp.InstanceId ||
                !IsPresented(lamp))
                return Reject("Предъяви один восстановленный и проверенный фонарик по принятому заказу до срока.", out message);
            if (Balance > int.MaxValue - 400) return Reject("Кошелёк заполнен.", out message);
            lamp.Location = FlashlightLocation.SolderBuyer; lamp.IsPowered = false;
            solderingOrder.State = DealOrderState.Completed; SolderingDeliveredId = lamp.InstanceId;
            Balance += 400; Revenue += 400;
            return Complete("Пайка: фонарик передан · +$400", out message);
        }

        private CitySolderingData CaptureSoldering()
        {
            EnsureSolderingState();
            return new CitySolderingData
            {
                StationOwned = StationOwned, Doses = SolderDoses, PurchasedDoses = PurchasedSolderDoses,
                Diagnosed = ConnectionDiagnosed, AtStation = AtSolderingStation,
                LampPurchased = SolderingLamp != null,
                Lamp = SolderingLamp == null ? new CityLampData() : CaptureLamps(new[] { SolderingLamp })[0],
                CoverAttached = repairState.Cover == FlashlightRepairState.CoverPlace.Attached,
                Secured0 = repairState.IsSecured(0), Secured1 = repairState.IsSecured(1),
                HasSolder = solderingState.HasSolder, Progress = solderingState.Progress,
                FinalTest = repairState.Tested && solderingState.Complete,
                Order = new CityOrderData { Id = DealOrderId.Solder, State = solderingOrder.State, DeadlineSeconds = solderingOrder.DeadlineSeconds },
                DeliveredId = SolderingDeliveredId
            };
        }

        private void RestoreSoldering(CitySolderingData data)
        {
            EnsureSolderingState();
            StationOwned = data.StationOwned; SolderDoses = data.Doses; PurchasedSolderDoses = data.PurchasedDoses;
            ConnectionDiagnosed = data.Diagnosed; AtSolderingStation = data.AtStation; SolderingDeliveredId = data.DeliveredId;
            SolderingLamp = !data.LampPurchased ? null : RestoreLamp(data.Lamp, FlashlightCondition.ConnectionFault);
            if (SolderingLamp != null) SolderingLamp.IsPowered = false;
            solderingState.Restore(data.HasSolder, data.Progress);
            repairState.Restore(data.AtStation, data.CoverAttached, data.Secured0, data.Secured1, data.FinalTest);
            solderingOrder.State = data.Order.State; solderingOrder.DeadlineSeconds = data.Order.DeadlineSeconds;
        }
    }
}
