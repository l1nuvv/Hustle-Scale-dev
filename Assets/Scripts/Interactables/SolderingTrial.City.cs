using Inventory;
using Player;
using Player.Movement;
using UnityEngine;

namespace Interactables
{
    public sealed partial class SolderingTrial
    {
        private CitySaveController citySave;
        private CityGreyboxWalk cityWalk;
        private CityTradeView cityTrade;
        public bool IsCityOperation => citySave != null;
        public bool IsActive => active;
        public string CityWorkMessage { get; private set; } = "";
        public bool CanAutosave => !active || !RepairAnimating && repair.HandsFree;
        public void ConsumeInputFrame() { enteredFrame = Time.frameCount; pointerConsumedUntilRelease = true; }

        public void ConfigureCity(CitySaveController save, CityGreyboxWalk walk, CityTradeView trade, PlayerController owner, Camera camera)
        {
            citySave = save; cityWalk = walk; cityTrade = trade; player = owner; view = camera;
        }

        private void BindCity()
        {
            Exit();
            var session = citySave.Session;
            state = session.SolderingState; repair = session.RepairState;
            CancelRepairMotion();
            titleLabel.text = "ПАЙКА";
            RefreshCityItem();
            RefreshFeedback();
        }

        private void RefreshCityItem()
        {
            if (!IsCityOperation || repair == null) return;
            var lamp = citySave.Session.SolderingLamp;
            var visible = lamp != null && WorkshopSession.IsOwned(lamp) && (active || citySave.Session.AtSolderingStation);
            flashlight.gameObject.SetActive(visible);
            cover.gameObject.SetActive(visible);
        }

        private void NormalizeCityWork()
        {
            if (!active) return;
            CancelRepairMotion(); CancelToolMotion();
            repair.Normalize();
            pointerConsumedUntilRelease = true;
            fasteningReleased = false;
            AdvanceTool(0, false, false);
            RefreshCityItem();
        }

        private void RefreshCityPause()
        {
            if (!IsCityOperation || !active) return;
            foreach (var behaviour in suspended)
                if (behaviour is Canvas && behaviour != null) behaviour.enabled = cityWalk.IsPaused;
            cityTrade.enabled = cityWalk.IsPaused;
            if (!cityWalk.IsPaused)
            {
                player.SetMovementEnabled(false); player.SetCameraControlEnabled(false);
                player.SetCursorLocked(focused);
            }
        }

        private bool RetrieveCityItem()
        {
            if (!IsCityOperation || !repair.Assembled || repairHover != RepairTarget.Flashlight) return false;
            if (!citySave.Session.RetrieveSolderingLamp(out var message)) { CityWorkMessage = message; return true; }
            Exit();
            return true;
        }
    }
}
