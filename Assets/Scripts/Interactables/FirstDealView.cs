using System.Collections.Generic;
using Inventory;
using Player.Movement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Interactables
{
    // Composition, clock input and presentation; the session owns all trade rules.
    [DefaultExecutionOrder(-200)]
    public sealed class FirstDealView : MonoBehaviour
    {
        [SerializeField] private WorkshopView workshop;
        [SerializeField] private PlayerController player;
        [SerializeField] private GameObject menu;
        [SerializeField] private Text menuText;
        [SerializeField] private GameObject sellerButtons;
        [SerializeField] private GameObject orderButtons;
        [SerializeField] private Button closeButton;
        private bool hasFocus = true;
        private string menuMessage = "";
        public bool MenuOpen => menu.activeSelf;

        private void Start()
        {
            workshop.Session.Changed += RefreshMenu;
            menu.SetActive(false);
            workshop.ShowMessage("Read the offers board. Seller outside; shift starts when you leave through the passage.");
        }

        private void OnDestroy()
        {
            if (workshop != null && workshop.Session != null) workshop.Session.Changed -= RefreshMenu;
        }

        private void Update()
        {
            var session = workshop.Session;
            if (MenuOpen || !hasFocus) return;
            // Tick before PlayerInput dispatches E, so deliveries see this frame's deadline.
            if (player.transform.position.z < 0.8f) session.StartShift();
            session.AdvanceActiveSeconds(Time.deltaTime);
        }

        private void LateUpdate()
        {
            // Resolve Escape after PlayerController's cursor-release handling.
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (MenuOpen) CloseMenu();
                else OpenMenu(false, true);
                return;
            }
        }

        private void OnApplicationFocus(bool focused)
        {
            hasFocus = focused;
            if (!focused && workshop != null && workshop.Session != null) OpenMenu(false, true);
        }

        public void Perform(FirstDealActionKind action)
        {
            if (MenuOpen) return;
            // E can open a paused seller screen in the same frame that the player exits.
            // Record the exit before pausing, rather than waiting for this adapter's Update.
            if (player.transform.position.z < 0.8f) workshop.Session.StartShift();
            if (action == FirstDealActionKind.Offers) { OpenMenu(false); return; }
            if (action == FirstDealActionKind.Seller) { OpenMenu(true); return; }
            var id = action == FirstDealActionKind.DeliverA ? DealOrderId.A :
                action == FirstDealActionKind.DeliverB ? DealOrderId.B : DealOrderId.RepeatB;
            var session = workshop.Session;
            var presented = new List<string>();
            foreach (var unit in session.Units)
            {
                if (unit.Location != FlashlightLocation.Hands &&
                    !(session.IsCrateCarried && unit.Location == FlashlightLocation.Crate)) continue;
                if (unit.IsTested && unit.LastTestPassed && unit.Condition == FlashlightCondition.Working)
                    presented.Add(unit.InstanceId);
                if (presented.Count == session.Order(id).Quantity) break;
            }
            session.DeliverOrder(id, presented, out var message);
            workshop.ShowMessage(message);
        }

        public void OpenMenu(bool seller, bool pauseOnly = false)
        {
            menuMessage = "";
            menu.SetActive(true);
            sellerButtons.SetActive(seller && !pauseOnly);
            orderButtons.SetActive(!seller && !pauseOnly);
            workshop.Session.SetPaused(true);
            player.SetMovementEnabled(false);
            player.SetCameraControlEnabled(false);
            player.SetCursorLocked(false);
            RefreshMenu();
            EventSystem.current.SetSelectedGameObject(closeButton.gameObject);
        }

        public void CloseMenu()
        {
            menu.SetActive(false);
            workshop.Session.SetPaused(false);
            player.SetMovementEnabled(true);
            player.SetCameraControlEnabled(true);
            player.SetCursorLocked(true);
        }

        public void Purchase(bool battery)
        {
            CloseMenu();
            string message;
            if (battery) workshop.Session.BuyBattery(out message);
            else workshop.Session.PurchaseLot(out message);
            workshop.ShowMessage(message);
        }

        public void ChangeOrder(int index, bool close)
        {
            string message;
            if (close) workshop.Session.CloseOrder((DealOrderId)index, out message);
            else workshop.Session.AcceptOrder((DealOrderId)index, out message);
            menuMessage = message;
            workshop.ShowMessage(message);
            RefreshMenu();
        }

        public void Accept(int index) => ChangeOrder(index, false);
        public void Decline(int index) => ChangeOrder(index, true);

        private void RefreshMenu()
        {
            if (!MenuOpen) return;
            var session = workshop.Session;
            menuText.text = "FIRST DEAL | PAUSED | $" + session.Balance + " | Shift " + WorkshopView.Clock(session.ElapsedSeconds) +
                "\nOne lot: 3 used lamps for $180. Spare battery: $50. No credit.\n" +
                "Test each lamp on the workshop bench. Buyers require tested working lamps.\n" +
                "Repair consumes one battery and adds 04:00; retest afterwards.\n" +
                "Carry the crate for a two-lamp delivery. No partial payment.\n\n";
            foreach (DealOrderId id in System.Enum.GetValues(typeof(DealOrderId)))
            {
                var order = session.Order(id);
                menuText.text += id + " | " + order.State + " | " + order.Quantity + " tested working | $" + order.Payment +
                    " | strictly before " + WorkshopView.Clock(order.DeadlineSeconds) + "\n";
            }
            menuText.text += "\nRepeat B opens once after B. Accepting never extends a deadline.\n" +
                "Cancelling an accepted order records a broken commitment.\n" +
                "Time stops here. Esc / Resume returns to play. Arrows + Enter navigate.";
            if (menuMessage.Length > 0) menuText.text += "\n" + menuMessage;
        }
    }
}
