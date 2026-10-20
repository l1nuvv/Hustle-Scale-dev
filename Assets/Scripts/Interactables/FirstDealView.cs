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
        [SerializeField] private Text[] orderCards = new Text[3];
        [SerializeField] private Text[] sellerCards = new Text[3];
        [SerializeField] private Text menuFeedback;
        private enum OutcomeScreen { None, Summary, ConfirmAbandon, Finished }
        private OutcomeScreen outcome;
        private bool hasFocus = true;
        private string menuMessage = "";
        private Button lotButton;
        private Button batteryButton;
        private Button buybackButton;
        private readonly Button[] acceptButtons = new Button[3];
        private readonly Button[] declineButtons = new Button[3];
        private WorkshopCarryController carry;
        public bool MenuOpen => menu.activeSelf;
        public bool OffersRead { get; private set; }
        // Development scenes may hold the clock without changing the session rules.
        public bool ClockSuspended { get; set; }

        private void Awake()
        {
            carry = workshop.GetComponent<WorkshopCarryController>();
            // Reuse the existing serialized button groups without changing scene GUIDs.
            foreach (var button in sellerButtons.GetComponentsInChildren<Button>(true))
            {
                if (button.name == "Confirm lot - $180") lotButton = button;
                if (button.name == "Buy battery - $50") batteryButton = button;
                if (button.name == "Sell brought lamps") buybackButton = button;
            }
            var buttons = orderButtons.GetComponentsInChildren<Button>(true);
            for (var i = 0; i < 3; i++)
            {
                var id = ((DealOrderId)i).ToString();
                // Names containing '/' are not Transform.Find paths.
                foreach (var button in buttons)
                {
                    if (button.name == "Accept " + id) acceptButtons[i] = button;
                    if (button.name == "Decline / cancel " + id) declineButtons[i] = button;
                }
            }
        }

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
            if (!ClockSuspended) session.AdvanceActiveSeconds(Time.deltaTime);
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
            var blocked = FirstDealText.DeliveryBlockReason(session, id);
            if (blocked.Length > 0)
            {
                workshop.ShowMessage(blocked);
                workshop.PlaySound(FirstDealSound.Warning);
                return;
            }
            var presented = new List<string>();
            foreach (var unit in session.Units)
            {
                if (unit.Location != FlashlightLocation.Hands &&
                    !(session.IsCrateCarried && unit.Location == FlashlightLocation.Crate)) continue;
                if (unit.IsTested && unit.LastTestPassed && unit.Condition == FlashlightCondition.Working)
                    presented.Add(unit.InstanceId);
                if (presented.Count == session.Order(id).Quantity) break;
            }
            var success = session.DeliverOrder(id, presented, out var message);
            workshop.ShowMessage(message);
            workshop.PlaySound(success ? FirstDealSound.Sale : FirstDealSound.Warning);
        }

        public void OpenMenu(bool seller, bool pauseOnly = false)
        {
            carry?.HideInventory();
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
            carry?.HideInventory();
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
            var sellerWasOpen = MenuOpen;
            // Domain purchases require an unpaused session. Keep the screen visible until
            // the operation succeeds, and restore its paused state on rejection.
            workshop.Session.SetPaused(false);
            string message;
            var success = battery ? workshop.Session.BuyBattery(out message) : workshop.Session.PurchaseLot(out message);
            if (success) CloseMenu();
            else if (sellerWasOpen)
            {
                workshop.Session.SetPaused(true);
                menuMessage = message;
                RefreshMenu();
                EventSystem.current.SetSelectedGameObject(closeButton.gameObject);
            }
            workshop.ShowMessage(message);
            workshop.PlaySound(success ? FirstDealSound.Purchase : FirstDealSound.Warning, true);
        }

        public string GetPrompt(FirstDealActionKind action)
        {
            var s = workshop.Session;
            if (action == FirstDealActionKind.Offers) return "E — Открыть заказы (время остановится)";
            if (action == FirstDealActionKind.Seller) return s.LotPurchased ?
                "E — Продавец: партия уже куплена · батарея / выкуп" : "E — Продавец: партия $180 / батарея $50";
            var id = action == FirstDealActionKind.DeliverA ? DealOrderId.A :
                action == FirstDealActionKind.DeliverB ? DealOrderId.B : DealOrderId.RepeatB;
            var reason = FirstDealText.DeliveryBlockReason(s, id);
            if (reason.Length > 0) return reason;
            return "E — Передать: " + s.Order(id).Quantity + " исправных, проверенных фон. (в руках или ящике)";
        }

        public void ChangeOrder(int index, bool close)
        {
            if (index < 0 || index >= 3) return;
            var order = workshop.Session.Order((DealOrderId)index);
            var previous = order.State;
            string message;
            var success = close ? workshop.Session.CloseOrder((DealOrderId)index, out message) :
                workshop.Session.AcceptOrder((DealOrderId)index, out message);
            menuMessage = close && (order.State == DealOrderState.Declined || order.State == DealOrderState.Cancelled) && order.State != previous ?
                "Заказ " + FirstDealText.OrderName(order.Id) + (order.State == DealOrderState.Cancelled ?
                    " отменён. Это нарушение; восстановить заказ нельзя." : " отклонён. Принять его снова нельзя.") +
                " Деньги и товар сохранены." : FirstDealText.Message(message);
            if (!success)
            {
                var reason = workshop.Session.OrderUnavailableReason((DealOrderId)index);
                if (reason.Length > 0) menuMessage = reason;
            }
            workshop.ShowMessage(menuMessage);
            if (!success) workshop.PlaySound(FirstDealSound.Warning, true);
            RefreshMenu();
            EventSystem.current.SetSelectedGameObject((declineButtons[index].interactable ? declineButtons[index] : closeButton).gameObject);
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
            var success = s.SellBack(ids, out var message);
            workshop.ShowMessage(message);
            OpenMenu(true);
            menuMessage = message;
            RefreshMenu();
            workshop.PlaySound(success ? FirstDealSound.Sale : FirstDealSound.Warning, true);
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
            lotButton.interactable = !session.LotPurchased && session.Balance >= 180 && !session.ShiftEnded;
            lotButton.GetComponentInChildren<Text>().text = session.LotPurchased ? "Партия уже куплена" :
                session.Balance < 180 ? "Партия $180 · мало денег" : "Купить партию · $180";
            batteryButton.interactable = session.Balance >= 50 && !session.ShiftEnded;
            var buybackReason = FirstDealText.BuybackBlockReason(session);
            var brought = 0;
            foreach (var unit in session.Units)
                if (unit.Location == FlashlightLocation.Hands || (session.IsCrateCarried && unit.Location == FlashlightLocation.Crate)) brought++;
            buybackButton.interactable = buybackReason.Length == 0;
            sellerCards[0].text = "ПАРТИЯ · $180\nКоличество: 3 фонаря\nСостояние неизвестно.\nЗакупка только один раз.\nДо завершения смены.\n\nСтатус: " +
                (session.LotPurchased ? "уже куплена; пополнения нет." : session.ShiftEnded ? "смена завершена." :
                    session.Balance < 180 ? "не хватает $" + (180 - session.Balance) + "." : "доступна.");
            sellerCards[1].text = "БАТАРЕЯ · $50\nКоличество: 1 запасная\nДля ремонта на столе.\nРемонт: +04:00, затем тест.\nЗапас: " + session.SpareBatteries + "\nДо завершения смены.\n\nСтатус: " +
                (session.ShiftEnded ? "смена завершена." : session.Balance < 50 ? "не хватает $" + (50 - session.Balance) + "." : "доступна; ремонт необязателен.");
            sellerCards[2].text = "ВЫКУП ОСТАТКОВ\nПринесено: " + brought + " фон.\nИсправный: $60 за фонарь\nНеисправный: $35 за фонарь\nВсе должны быть проверены.\nДо завершения смены.\n\n" +
                (buybackReason.Length == 0 ? "Статус: доступен. Кнопка продаст все принесённые фонари." : "Недоступен: " + buybackReason);
            for (var i = 0; i < 3; i++)
            {
                var order = session.Order((DealOrderId)i);
                acceptButtons[i].interactable = !session.ShiftEnded && order.State == DealOrderState.Available;
                declineButtons[i].interactable = session.CanPursueOrder((DealOrderId)i);
                declineButtons[i].GetComponentInChildren<Text>().text = order.State == DealOrderState.Accepted ? "Отменить заказ" : "Отказаться";
                orderCards[i].text = FirstDealText.OrderCard(session, (DealOrderId)i);
            }
            menuFeedback.text = FirstDealText.Message(menuMessage);
            var trading = outcome == OutcomeScreen.None && (sellerButtons.activeSelf || orderButtons.activeSelf);
            menuFeedback.gameObject.SetActive(trading);
            // Trade screens use a short heading above the cards; pause/results retain their reading area.
            menuText.rectTransform.anchorMin = new Vector2(0.035f, trading ? 0.87f : 0.27f);
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
            if (!sellerButtons.activeSelf && !orderButtons.activeSelf)
            {
                menuText.text = "ПЕРВАЯ ПАРТИЯ · ПАУЗА\n\n" +
                    "Проверь товар и выбери выгодную сделку.\n" +
                    (session.LotPurchased ? "Продолжи с текущей целью в HUD. Условия сделок — на доске.\n" :
                        "Начни с синей доски «ЗАКАЗЫ» в мастерской.\n") +
                    (session.ShiftStarted ? "В меню время остановлено.\n\n" :
                        "Время начнётся при выходе наружу; в меню оно остановлено.\n\n") +
                    "WASD — ходить · мышь — смотреть\nE — действие под прицелом · Esc — пауза и итоги\n" +
                    "Стрелки / Enter — выбор в меню.";
                return;
            }
            menuText.text = (sellerButtons.activeSelf ? "ПРОДАВЕЦ" : "ЗАКАЗЫ") + " · ПАУЗА · $" + session.Balance +
                " · Время " + WorkshopView.Clock(session.ElapsedSeconds) + "\n" +
                (sellerButtons.activeSelf ? "Покупки и выкуп — отдельные сделки. Деньги меняются только после подтверждения." :
                    "Передача — через E у покупателя. Повтор Б: один раз после Б, с тем же сроком 18:00.");
        }
    }
}
