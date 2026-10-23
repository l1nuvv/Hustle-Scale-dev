using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Interactables
{
    public sealed partial class CityTradeView
    {
        private GameObject pausePanel, pauseLastFocused;
        private Text pauseHeading, pauseStatus;
        private ScrollRect pauseScroll;
        private RectTransform pauseContent;
        private string pauseSignature;
        private bool pauseConfirm;
        private int pauseBalance;
        private float pauseTextScale;
        private CheatMenu cheats;
        public bool CheatsMenuOpen { get; private set; }

        public void ToggleCheatsMenu()
        {
            if (session == null || save.IsDriving || MenuOpen || walk.ConfirmNewGame ||
                walk.SolderingStation != null && walk.SolderingStation.IsActive ||
                walk.SharpeningStation != null && walk.SharpeningStation.Active) return;
            if (!walk.IsPaused) walk.SetPaused(true);
            CheatsMenuOpen = !CheatsMenuOpen;
            pauseSignature = null;
        }

        public bool CheatAction(string command) => CheatsMenuOpen && walk.IsPaused && !save.IsDriving && cheats.Execute(command);
        private void CheatChanged() => pauseSignature = null;
        public void CloseCheatsMenu() { CheatsMenuOpen = false; pauseSignature = null; }

        private Button BuildCheatActions()
        {
            pauseHeading.text = "ЧИТ-МЕНЮ · F8";
            pauseStatus.text = cheats.Status;
            var first = ActionButton(pauseContent, "Назад к паузе · Esc / F8", ToggleCheatsMenu);
            foreach (var entry in CheatMenu.Entries)
            {
                var command = entry.Command;
                ActionButton(pauseContent, entry.Label, () => CheatAction(command), cheats.Available(command));
            }
            return first;
        }

        private void BuildPauseUI(Transform safe)
        {
            pausePanel = Rect("City pause menu", safe, Vector2.zero, Vector2.one).gameObject;
            Surface(pausePanel.transform, new Color(0.008f, 0.012f, 0.018f, 0.78f));
            var card = Rect("Pause card", pausePanel.transform, new Vector2(0.28f, 0.12f), new Vector2(0.72f, 0.88f));
            Round(card, new Color(0.065f, 0.085f, 0.105f));
            var header = Rect("Pause header", card, new Vector2(0.06f, 0.82f), new Vector2(0.94f, 0.96f));
            pauseHeading = Label(header, "ПАУЗА", 32); pauseHeading.alignment = TextAnchor.MiddleLeft;
            pauseHeading.fontStyle = FontStyle.Bold;
            var status = Rect("Pause status", card, new Vector2(0.06f, 0.64f), new Vector2(0.94f, 0.8f));
            pauseStatus = Label(status, "", 16); pauseStatus.color = Muted; pauseStatus.alignment = TextAnchor.UpperLeft;
            var actions = Rect("Pause actions", card, new Vector2(0.06f, 0.07f), new Vector2(0.94f, 0.62f));
            pauseScroll = actions.gameObject.AddComponent<ScrollRect>();
            pauseScroll.horizontal = false; pauseScroll.movementType = ScrollRect.MovementType.Clamped;
            actions.gameObject.AddComponent<RectMask2D>();
            pauseContent = Rect("Pause content", actions, new Vector2(0, 1), Vector2.one);
            pauseContent.pivot = new Vector2(0.5f, 1);
            var layout = pauseContent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10; layout.padding = new RectOffset(0, 0, 0, 8);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            pauseContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            pauseScroll.viewport = actions; pauseScroll.content = pauseContent;
            var hint = Rect("Pause scroll hint", card, new Vector2(.06f, .015f), new Vector2(.94f, .065f));
            var hintText = Label(hint, "↑ ↓ / колесо мыши — прокрутка списка", 13);
            hintText.color = Muted; hintText.alignment = TextAnchor.MiddleLeft;
            pausePanel.SetActive(false);
        }

        private void UpdatePauseUI()
        {
            var visible = walk.IsPaused && !MenuOpen && !save.IsDriving;
            if (pausePanel.activeSelf != visible)
            {
                pausePanel.SetActive(visible); pauseSignature = null; pauseLastFocused = null;
            }
            if (!visible) return;
            var balance = session != null ? session.Balance : 0;
            if (pauseSignature != save.Message || pauseConfirm != walk.ConfirmNewGame || pauseBalance != balance || pauseTextScale != textScale)
            {
                var focusedBefore = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
                var selectedName = CheatsMenuOpen && focusedBefore != null && focusedBefore.transform.IsChildOf(pauseContent) ? focusedBefore.name : null;
                var scrollBefore = pauseScroll.verticalNormalizedPosition;
                pauseSignature = save.Message; pauseConfirm = walk.ConfirmNewGame; pauseBalance = balance; pauseTextScale = textScale;
                foreach (Transform child in pauseContent) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
                pauseHeading.fontSize = Mathf.RoundToInt(32 * textScale);
                pauseStatus.fontSize = Mathf.RoundToInt(16 * textScale);
                pauseHeading.text = pauseConfirm ? "НОВАЯ ИГРА" : "ПАУЗА";
                pauseStatus.text = pauseConfirm ? "Сбросить текущую игру?\n\nДеньги, фонари и заказы будут потеряны." :
                    "$" + balance + " · " + save.Message;
                Button first;
                if (CheatsMenuOpen && session != null)
                    first = BuildCheatActions();
                else if (pauseConfirm)
                {
                    first = ActionButton(pauseContent, "Назад · Esc", () => walk.PauseAction("cancel"));
                    ActionButton(pauseContent, "Начать новую игру · Enter", () => walk.PauseAction("confirm"));
                }
                else
                {
                    first = ActionButton(pauseContent, "Продолжить · Esc", () => walk.PauseAction("resume"), session != null);
                    SetButtonTone(first, true);
                    ActionButton(pauseContent, "Сохранить · F5", () => walk.PauseAction("save"), session != null);
                    ActionButton(pauseContent, "Загрузить · F9", () => walk.PauseAction("load"));
                    ActionButton(pauseContent, "Резервная копия · B", () => walk.PauseAction("backup"), save.HasBackup);
                    ActionButton(pauseContent, "Новая игра… · N", () => walk.PauseAction("new"));
                    ActionButton(pauseContent, "Чит-меню · F8", ToggleCheatsMenu, session != null);
                }
                var nextFocus = first.gameObject;
                if (selectedName != null)
                    foreach (Transform child in pauseContent)
                        if (child.gameObject.activeSelf && child.name == selectedName) { nextFocus = child.gameObject; break; }
                EventSystem.current?.SetSelectedGameObject(nextFocus);
                Canvas.ForceUpdateCanvases(); pauseScroll.verticalNormalizedPosition = nextFocus == first.gameObject ? 1 : scrollBefore;
            }
            var focused = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (focused == pauseLastFocused) return;
            pauseLastFocused = focused;
            if (focused == null || !focused.transform.IsChildOf(pauseContent)) return;
            Canvas.ForceUpdateCanvases();
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(pauseScroll.viewport, focused.transform);
            var rect = pauseScroll.viewport.rect;
            var offset = bounds.min.y < rect.yMin ? rect.yMin - bounds.min.y : bounds.max.y > rect.yMax ? rect.yMax - bounds.max.y : 0;
            pauseContent.anchoredPosition += Vector2.up * offset;
        }
    }
}
