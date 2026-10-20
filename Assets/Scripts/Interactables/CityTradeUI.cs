using System;
using Inventory;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Interactables
{
    public sealed partial class CityTradeView
    {
        private GameObject canvasObject, panel, ownedEventSystem;
        private RectTransform menuContent, safeArea;
        private UnityEngine.Rect lastSafeArea;
        private Vector2 lastScreen;
        private Text hud, prompt, feedbackLabel, menuHeading;
        private RectTransform hudPanel, bottomContext;
        private Font font;
        private bool rebuilding;
        private ScrollRect menuScroll;
        private float textScale = 1;
        private Button textSizeButton;
        public float TextScale => textScale;

        public void SetTextScale(float scale)
        {
            textScale = Mathf.Clamp(scale, 1, 1.5f);
            if (hud != null) hud.fontSize = Mathf.RoundToInt(18 * textScale);
            if (prompt != null) prompt.fontSize = Mathf.RoundToInt(20 * textScale);
            if (feedbackLabel != null) feedbackLabel.fontSize = Mathf.RoundToInt(18 * textScale);
            ResizeHud();
            if (MenuOpen) RebuildMenu();
        }

        public static string Title(CityTradeKind kind) => kind switch
        {
            CityTradeKind.Seller => "Продавец фонарей",
            CityTradeKind.Battery => "Магазин батарей",
            CityTradeKind.Buyback => "Скупка",
            CityTradeKind.A => "Покупатель A",
            CityTradeKind.B => "Покупатель Б",
            CityTradeKind.Workshop => "Мастерская",
            CityTradeKind.Crate => "Ящик · взять / выбрать фонарь",
            CityTradeKind.SellerDock => "Место для ящика у продавца",
            CityTradeKind.WorkshopDock => "Место для ящика в мастерской",
            _ => "Фонарь"
        };
        private static string State(DealOrderState state) => state switch
        {
            DealOrderState.Available => "доступен",
            DealOrderState.Accepted => "принят",
            DealOrderState.Completed => "оплачен",
            DealOrderState.Failed => "срок истёк",
            DealOrderState.Declined => "отказ",
            DealOrderState.Cancelled => "отменён",
            DealOrderState.Unavailable => "недостаточно товара",
            _ => "закрыт"
        };

        private void BuildUI()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (EventSystem.current == null)
                ownedEventSystem = new GameObject("City UI input", typeof(EventSystem), typeof(InputSystemUIInputModule));
            canvasObject = new GameObject("City trading UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = 0.5f;
            var safe = Rect("Safe area", canvas.transform, Vector2.zero, Vector2.one);
            safeArea = safe;
            safe.anchorMin = Screen.safeArea.min / new Vector2(Screen.width, Screen.height);
            safe.anchorMax = Screen.safeArea.max / new Vector2(Screen.width, Screen.height);
            hudPanel = Rect("Wallet and orders", safe, new Vector2(0.015f, 0.985f), new Vector2(0.73f, 0.985f));
            hudPanel.pivot = new Vector2(0.5f, 1);
            hudPanel.gameObject.AddComponent<Image>().color = new Color(0.025f, 0.045f, 0.05f, 1);
            hud = Label(hudPanel, "", 18); hud.alignment = TextAnchor.UpperLeft;
            hud.rectTransform.offsetMin = new Vector2(14, 4); hud.rectTransform.offsetMax = new Vector2(-10, -4);
            var context = Rect("Action context", safe, new Vector2(0.015f, 0.02f), new Vector2(0.985f, 0.02f));
            bottomContext = context; context.pivot = new Vector2(0.5f, 0);
            context.gameObject.AddComponent<Image>().color = new Color(0.025f, 0.045f, 0.05f, 1);
            var contextLayout = context.gameObject.AddComponent<VerticalLayoutGroup>();
            contextLayout.padding = new RectOffset(10, 10, 8, 8); contextLayout.spacing = 4;
            contextLayout.childControlWidth = contextLayout.childControlHeight = true;
            contextLayout.childForceExpandHeight = false;
            context.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            prompt = Label(context, "", 20);
            prompt.rectTransform.anchorMin = new Vector2(0.015f, 0.50f); prompt.rectTransform.anchorMax = new Vector2(0.985f, 1);
            feedbackLabel = Label(context, "", 18);
            feedbackLabel.rectTransform.anchorMin = new Vector2(0.015f, 0); feedbackLabel.rectTransform.anchorMax = new Vector2(0.985f, 0.50f);
            var cross = Label(safe, "+", 22); cross.rectTransform.anchorMin = new Vector2(0.49f, 0.48f);
            cross.rectTransform.anchorMax = new Vector2(0.51f, 0.52f);
            panel = Rect("Trade menu", safe, new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.9f)).gameObject;
            panel.AddComponent<Image>().color = new Color(0.035f, 0.055f, 0.06f, 1);
            var header = Rect("Menu header", panel.transform, new Vector2(0.02f, 0.88f), new Vector2(0.98f, 0.98f));
            menuHeading = Label(header, "Esc / Tab — вернуться в город", 18);
            menuHeading.rectTransform.anchorMax = new Vector2(0.65f, 1);
            var textSize = Rect("Text size", header, new Vector2(0.66f, 0), Vector2.one);
            textSize.gameObject.AddComponent<Image>().color = new Color(0.09f, 0.17f, 0.18f);
            textSizeButton = textSize.gameObject.AddComponent<Button>();
            Label(textSize, "Текст 100% / 150%", 18);
            textSizeButton.onClick.AddListener(() => SetTextScale(textScale < 1.5f ? 1.5f : 1));
            var scroll = panel.AddComponent<ScrollRect>(); scroll.horizontal = false;
            menuScroll = scroll; scroll.movementType = ScrollRect.MovementType.Clamped;
            var viewport = Rect("Viewport", panel.transform, new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.86f));
            viewport.gameObject.AddComponent<RectMask2D>();
            menuContent = Rect("Content", viewport, new Vector2(0, 1), Vector2.one);
            menuContent.pivot = new Vector2(0.5f, 1);
            var layout = menuContent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 7; layout.padding = new RectOffset(12, 12, 12, 12);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            menuContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport; scroll.content = menuContent;
            panel.SetActive(false);
        }

        private void ResizeHud()
        {
            if (hud == null || hudPanel == null) return;
            hudPanel.anchorMax = new Vector2(Mathf.Lerp(0.50f, 0.73f, (textScale - 1) * 2), 0.985f);
            hudPanel.sizeDelta = new Vector2(0, hud.preferredHeight + 12);
        }

        private void UpdateSafeArea()
        {
            var screen = new Vector2(Screen.width, Screen.height);
            if (lastScreen == screen && lastSafeArea == Screen.safeArea) return;
            lastScreen = screen; lastSafeArea = Screen.safeArea;
            safeArea.anchorMin = lastSafeArea.min / screen;
            safeArea.anchorMax = lastSafeArea.max / screen;
            if (MenuOpen) RebuildMenu();
        }

        private void RebuildMenu()
        {
            if (rebuilding) return;
            rebuilding = true;
            try
            {
                var focused = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
                var focusName = focused != null && focused.transform.IsChildOf(menuContent) ? focused.name.Replace("[x]", "[ ]") : null;
                var scrollPosition = menuScroll.verticalNormalizedPosition;
                foreach (Transform child in menuContent) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
                menuHeading.text = (current != null ? Title(current.Kind) : "Карта и имущество") + " · Esc / Tab — город";
                Line(hudText + "\n" + CityMessage(message));
                if (current == null)
                {
                    BuildLocalMap();
                    Line("1–3 — взять фонарь · G — убрать · Q — поставить · F — свет. E у ящика или стола — действия.");
                    Line("Три слота фонарей; батареи хранятся отдельно. Для сдачи принеси товар в руках или в ящике.\n" +
                        "Выручка $" + session.Revenue + " · расходы $" + session.Expenses +
                        " · прибыль $" + (session.Balance - session.InitialBalance) + ". Продажа не заканчивает игру.");
                    for (var i = 0; i < 3; i++)
                    {
                        var index = i; var unit = session.Units[i];
                        Button("Фонарь " + (i + 1) + " · " + FirstDealText.Condition(unit) + " · " + Location(unit.Location),
                            () => Execute("equip", index), unit.Location == FlashlightLocation.Hands || unit.Location == FlashlightLocation.Pocket);
                    }
                }
                else switch (current.Kind)
                    {
                        case CityTradeKind.Seller:
                            Line(session.LotPurchased ? "Партия куплена. Пополнения нет." : "Одна партия: 3 непроверенных фонаря за $180. Начальный капитал $300. Фонари уже лежат в готовом ящике у продавца.");
                            Line("После покупки: E на ящике → «Взять ящик». Подойди к заднему борту машины и нажми E — погрузить в багажник. Остановись задним бортом у жёлтого дока мастерской или продавца; E на багажнике — выгрузить. Освободи руки от фонаря.");
                            Button("Купить партию · $180", () => Execute("purchase"), !session.LotPurchased); break;
                        case CityTradeKind.Battery:
                            Line("Одна запасная батарея стоит $50. Замена в мастерской расходует батарею и добавляет 04:00 игрового времени.");
                            Button("Купить батарею · $50", () => Execute("battery")); break;
                        case CityTradeKind.Workshop:
                            Line("На столе: " + (session.OnBench == null ? "ничего" : FirstDealText.Condition(session.OnBench)) +
                                "\nРемонт: 1 запасная батарея и +04:00. Заказ может истечь. После ремонта обязателен новый тест.");
                            Button("Положить / забрать фонарь", () => Execute("bench"));
                            Button("Проверить на столе", () => Execute("test"));
                            Button("Заменить батарею · +04:00 · риск просрочки", () => Execute("repair")); break;
                        case CityTradeKind.SellerDock:
                        case CityTradeKind.WorkshopDock:
                        case CityTradeKind.Crate:
                            Line("Ящик ставится на жёлтую площадку у продавца или в мастерской. Взять ящик → E у заднего борта машины — погрузить. Выгрузка — E у багажника рядом с доком. С фонарём в руках сначала верни или убери его.");
                            Button(session.IsCrateCarried ? "Поставить ящик у этого дока" : "Взять ящик", () => Execute("crate"));
                            if (current.Kind == CityTradeKind.Crate)
                                for (var i = 0; i < 3; i++)
                                {
                                    var index = i;
                                    Button("Фонарь " + (i + 1) + " · " + FirstDealText.Condition(session.Units[i]) + " · " + Location(session.Units[i].Location),
                                        () => Execute("unit", index));
                                }
                            break;
                        case CityTradeKind.A:
                        case CityTradeKind.B:
                            var id = current.Kind == CityTradeKind.A ? DealOrderId.A : DealOrderId.B;
                            var order = session.Order(id);
                            Line("Нужно " + order.Quantity + " проверенных исправных фонаря · выплата $" + order.Payment +
                                ". Срок " + (id == DealOrderId.A ? "09:00" : "18:00") + " с принятия. При принятии оплаты нет.\n" +
                                "Принадлежит тебе: " + session.OwnedLampCount + ". Товар не резервируется; оба заказа могут оказаться невыполнимы.");
                            if (order.State == DealOrderState.Available) Button("Принять заказ", () => Execute("accept"));
                            if (order.State == DealOrderState.Available || order.State == DealOrderState.Accepted)
                            {
                                Line("Отказ / отмена навсегда закрывает только этот заказ. Товар и деньги остаются у тебя; штрафа нет.");
                                Button(order.State == DealOrderState.Accepted ? "Отменить принятый заказ навсегда" : "Отказаться навсегда", () => Execute("close"));
                            }
                            else Line(order.State == DealOrderState.Cancelled ? "Заказ отменён навсегда. Товар и деньги сохранены." : session.OrderUnavailableReason(id));
                            SelectGoods();
                            Button("Передать выбранные · нужно ровно " + order.Quantity, () => Execute("deliver"), order.State == DealOrderState.Accepted);
                            break;
                        case CityTradeKind.Buyback:
                            Line("Выкуп доступен после покупки. Только проверенные: исправный $60, неисправный $35. Передача всей выбранной группы без возврата.");
                            SelectGoods(); Button("Продать выбранные остатки", () => Execute("sell")); break;
                    }
                var close = Button("Вернуться в город · Esc / Tab", CloseMenu);
                Button first = null, restore = null;
                foreach (var button in menuContent.GetComponentsInChildren<Button>())
                {
                    if (!button.interactable) continue;
                    if (first == null) first = button;
                    if (button.name.Replace("[x]", "[ ]") == focusName) restore = button;
                }
                EventSystem.current?.SetSelectedGameObject(focused == textSizeButton.gameObject ? textSizeButton.gameObject :
                    (restore != null ? restore : first != null ? first : close).gameObject);
                Canvas.ForceUpdateCanvases();
                menuScroll.verticalNormalizedPosition = focusName == null ? 1 : scrollPosition;
            }
            finally { rebuilding = false; }
        }

        private void SelectGoods()
        {
            for (var i = 0; i < 3; i++)
            {
                var index = i; var unit = session.Units[i];
                Button((selected[i] ? "[x] " : "[ ] ") + "Фонарь " + (i + 1) + " · " + FirstDealText.Condition(unit) +
                    (Presented(i) ? " · предъявлен" : " · принеси сюда"),
                    () => { selected[index] = !selected[index]; RebuildMenu(); }, Presented(i));
            }
        }
        private GameObject lastFocused;
        private void KeepMenuFocusVisible()
        {
            if (!MenuOpen || EventSystem.current == null) return;
            var focused = EventSystem.current.currentSelectedGameObject;
            if (focused == lastFocused) return;
            lastFocused = focused;
            if (focused == null || !focused.transform.IsChildOf(menuContent)) return;
            Canvas.ForceUpdateCanvases();
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(menuScroll.viewport, focused.transform);
            var viewport = menuScroll.viewport.rect;
            var offset = bounds.min.y < viewport.yMin ? viewport.yMin - bounds.min.y :
                bounds.max.y > viewport.yMax ? viewport.yMax - bounds.max.y : 0;
            menuContent.anchoredPosition += Vector2.up * offset;
        }
        private void Line(string value, int size = 18)
        {
            var label = Label(menuContent, value, size);
            label.alignment = TextAnchor.MiddleLeft;
            label.gameObject.AddComponent<LayoutElement>().minHeight = size + 12;
        }
        private Button Button(string value, Action action, bool enabled = true)
        {
            var target = Rect(value, menuContent, Vector2.zero, Vector2.one);
            target.gameObject.AddComponent<LayoutElement>().minHeight = 44;
            target.gameObject.AddComponent<Image>().color = new Color(0.09f, 0.17f, 0.18f);
            var layout = target.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 8, 8);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            var button = target.gameObject.AddComponent<Button>(); button.interactable = enabled;
            var colors = button.colors;
            colors.highlightedColor = colors.selectedColor = new Color(1.7f, 1.7f, 1.7f);
            colors.disabledColor = new Color(0.75f, 0.75f, 0.75f); button.colors = colors;
            button.onClick.AddListener(() => action());
            Label(target, value, 18).text = enabled ? value : value + " · недоступно";
            return button;
        }
        private Text Label(Transform parent, string value, int size)
        {
            var label = Rect("Text", parent, Vector2.zero, Vector2.one).gameObject.AddComponent<Text>();
            label.font = font; label.fontSize = Mathf.RoundToInt(size * textScale); label.text = value;
            label.color = new Color(0.97f, 0.95f, 0.85f); label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false; label.horizontalOverflow = HorizontalWrapMode.Wrap;
            return label;
        }
        private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }
    }
}
