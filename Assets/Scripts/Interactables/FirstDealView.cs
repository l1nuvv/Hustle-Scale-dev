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
        [SerializeField] private GameObject outcomeButtons;
        [SerializeField] private Button finishButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button summaryButton;
        private enum OutcomeScreen { None, Summary, ConfirmAbandon, Finished }
        private OutcomeScreen outcome;
        private bool hasFocus = true;
        private string menuMessage = "";
        public bool MenuOpen => menu.activeSelf;
        public bool OffersRead { get; private set; }

        private void Start()
        {
            workshop.Session.Changed += RefreshMenu;
            menu.SetActive(false);
            workshop.ShowMessage("Прочитай заказы на синей доске. Продавец снаружи; время начнётся, когда выйдешь из мастерской.");
            OpenMenu(false, true);
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
            if (action == FirstDealActionKind.Offers) { OffersRead = true; OpenMenu(false); return; }
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
            if (workshop.Session.ShiftEnded) { OpenSummary(); return; }
            outcome = OutcomeScreen.None;
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
            if (workshop.Session.ShiftEnded) { OpenSummary(); return; }
            outcome = OutcomeScreen.None;
            menu.SetActive(false);
            workshop.Session.SetPaused(false);
            player.SetMovementEnabled(true);
            player.SetCameraControlEnabled(true);
            player.SetCursorLocked(true);
        }

        public void Purchase(bool battery)
        {
            if (workshop.Session.ShiftEnded) return;
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

        public void SellBack()
        {
            if (workshop.Session.ShiftEnded) return;
            // The seller menu was opened through the physical E interaction.
            CloseMenu();
            var s = workshop.Session;
            var ids = new List<string>();
            foreach (var unit in s.Units)
                if (unit.Location == FlashlightLocation.Hands || (s.IsCrateCarried && unit.Location == FlashlightLocation.Crate)) ids.Add(unit.InstanceId);
            s.SellBack(ids, out var message);
            workshop.ShowMessage(message);
            OpenMenu(true);
            menuMessage = message;
            RefreshMenu();
        }

        public void OpenSummary()
        {
            outcome = workshop.Session.ShiftEnded ? OutcomeScreen.Finished : OutcomeScreen.Summary;
            menuMessage = "";
            menu.SetActive(true);
            sellerButtons.SetActive(false);
            orderButtons.SetActive(false);
            workshop.Session.SetPaused(true);
            player.SetMovementEnabled(false);
            player.SetCameraControlEnabled(false);
            player.SetCursorLocked(false);
            RefreshMenu();
            EventSystem.current.SetSelectedGameObject((workshop.Session.ShiftEnded ? restartButton : closeButton).gameObject);
        }

        public void FinishShift()
        {
            if (outcome != OutcomeScreen.Summary && outcome != OutcomeScreen.ConfirmAbandon) return;
            if (!workshop.Session.HasPrimarySale && outcome != OutcomeScreen.ConfirmAbandon)
            {
                outcome = OutcomeScreen.ConfirmAbandon;
                RefreshMenu();
                EventSystem.current.SetSelectedGameObject(closeButton.gameObject);
                return;
            }
            workshop.Session.EndShift(outcome == OutcomeScreen.ConfirmAbandon, out _);
            OpenSummary();
        }

        public void RestartShift()
        {
            if (!workshop.Session.ShiftEnded) return;
            UnityEngine.SceneManagement.SceneManager.LoadScene(gameObject.scene.name);
        }

        private void RefreshMenu()
        {
            if (!MenuOpen) return;
            var session = workshop.Session;
            outcomeButtons.SetActive(outcome != OutcomeScreen.None);
            finishButton.gameObject.SetActive(outcome != OutcomeScreen.Finished);
            restartButton.gameObject.SetActive(outcome == OutcomeScreen.Finished);
            summaryButton.gameObject.SetActive(outcome == OutcomeScreen.None);
            closeButton.gameObject.SetActive(!session.ShiftEnded);
            finishButton.GetComponentInChildren<Text>().text = outcome == OutcomeScreen.ConfirmAbandon ? "Да, отказаться и завершить" : "Завершить смену";
            if (outcome != OutcomeScreen.None)
            {
                menuText.text = FirstDealText.Summary(session);
                if (outcome == OutcomeScreen.ConfirmAbandon)
                    menuText.text += "\n\nОсновной продажи нет. Завершить неудачную попытку? Кнопка продолжения вернёт в игру без потерь.";
                return;
            }
            menuText.text = "ПЕРВАЯ ПАРТИЯ · ПАУЗА · $" + session.Balance + " · Время " + WorkshopView.Clock(session.ElapsedSeconds) +
                "\nТы начинающий торговец. Купи партию, проверь товар и выбери выгодную сделку.\n" +
                "1. Прочитай заказы на синей доске. У продавца снаружи купи 3 фонаря за $180.\n" +
                "2. Нажми E на ящике и принеси его к кнопке «ПОСТАВИТЬ ЯЩИК» в мастерской.\n" +
                "3. Кнопка 1 / 2 / 3 берёт фонарь. E на столе ставит его. «ПРОВЕРИТЬ» запускает тест.\n" +
                "4. Верни фонарь: E на столе, затем E на той же кнопке у ящика.\n" +
                "5. На доске прими заказ. Принеси ящик покупателю и нажми E на его стойке.\n\n";
            foreach (DealOrderId id in System.Enum.GetValues(typeof(DealOrderId)))
            {
                var order = session.Order(id);
                menuText.text += (id == DealOrderId.A ? "A" : id == DealOrderId.B ? "Б" : "Повтор Б") + " · " + FirstDealText.State(order.State) + " · " + order.Quantity + " исправных, проверенных · $" + order.Payment +
                    " · строго до " + WorkshopView.Clock(order.DeadlineSeconds) + "\n";
            }
            menuText.text += "\nРемонт: батарея у продавца $50 и +04:00, затем новый тест. Ремонт необязателен.\n" +
                "Повтор Б открывается один раз после Б. Принятие не продлевает срок.\n" +
                "Выкуп у продавца: исправный $60 / неисправный $35, только после проверки.\n" +
                (session.BuybackAvailable ? "Выкуп открыт. Принеси остатки в руках или ящике; кнопка продажи передаёт все принесённые.\n" : "Откроется после продажи A / Б или в 18:00. Итоги доступны кнопкой ниже.\n") +
                "WASD — ходить · мышь — смотреть · E — действие · Esc — пауза · стрелки / Enter — меню.";
            if (menuMessage.Length > 0) menuText.text += "\n" + FirstDealText.Message(menuMessage);
        }
    }
}
