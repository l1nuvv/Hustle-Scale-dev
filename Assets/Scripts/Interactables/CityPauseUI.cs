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

        private void BuildPauseUI(Transform safe)
        {
            pausePanel = Rect("City pause menu", safe, Vector2.zero, Vector2.one).gameObject;
            pausePanel.AddComponent<Image>().color = new Color(0.015f, 0.025f, 0.03f, 0.95f);
            var card = Rect("Pause card", pausePanel.transform, new Vector2(0.12f, 0.1f), new Vector2(0.88f, 0.9f));
            card.gameObject.AddComponent<Image>().color = new Color(0.035f, 0.055f, 0.06f);
            var header = Rect("Pause header", card, new Vector2(0.035f, 0.79f), new Vector2(0.965f, 0.96f));
            header.gameObject.AddComponent<Image>().color = new Color(0.11f, 0.23f, 0.24f);
            pauseHeading = Label(header, "ПАУЗА", 32); pauseHeading.alignment = TextAnchor.MiddleLeft;
            pauseHeading.rectTransform.offsetMin = new Vector2(16, 0);
            var status = Rect("Pause status", card, new Vector2(0.04f, 0.06f), new Vector2(0.45f, 0.74f));
            pauseStatus = Label(status, "", 18); pauseStatus.alignment = TextAnchor.UpperLeft;
            var actions = Rect("Pause actions", card, new Vector2(0.5f, 0.06f), new Vector2(0.96f, 0.74f));
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
                pauseSignature = save.Message; pauseConfirm = walk.ConfirmNewGame; pauseBalance = balance; pauseTextScale = textScale;
                foreach (Transform child in pauseContent) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
                pauseHeading.fontSize = Mathf.RoundToInt(32 * textScale);
                pauseStatus.fontSize = Mathf.RoundToInt(18 * textScale);
                pauseHeading.text = pauseConfirm ? "НОВАЯ ИГРА" : "ПАУЗА";
                pauseStatus.text = pauseConfirm ? "Текущая игра будет заменена.\n\nДеньги, фонари и заказы будут сброшены.\n\nEsc — вернуться без изменений." :
                    "ГОРОД · МАСТЕРСКАЯ\n\n$" + balance + "\nВремя и заказы на паузе.\n\n" + save.Message + "\n\nEsc — продолжить\nTab — карта в игре";
                Button first;
                if (pauseConfirm)
                {
                    first = ActionButton(pauseContent, "Назад · Esc", () => walk.PauseAction("cancel"));
                    ActionButton(pauseContent, "Начать новую игру · Enter", () => walk.PauseAction("confirm"));
                }
                else
                {
                    first = ActionButton(pauseContent, "Продолжить · Esc", () => walk.PauseAction("resume"), session != null);
                    ActionButton(pauseContent, "Сохранить · F5", () => walk.PauseAction("save"), session != null);
                    ActionButton(pauseContent, "Загрузить · F9", () => walk.PauseAction("load"));
                    ActionButton(pauseContent, "Резервная копия · B", () => walk.PauseAction("backup"), save.HasBackup);
                    ActionButton(pauseContent, "Новая игра… · N", () => walk.PauseAction("new"));
                }
                EventSystem.current?.SetSelectedGameObject(first.gameObject);
                Canvas.ForceUpdateCanvases(); pauseScroll.verticalNormalizedPosition = 1;
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
