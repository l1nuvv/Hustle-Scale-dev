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
        private GameObject canvasObject, panel, tradeBackdrop, ownedEventSystem;
        private RectTransform menuContent, safeArea;
        private UnityEngine.Rect lastSafeArea;
        private Vector2 lastScreen;
        private Text hud, orderHud, prompt, feedbackLabel, menuHeading;
        private bool showControls;
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
            if (orderHud != null) orderHud.fontSize = Mathf.RoundToInt(18 * textScale);
            if (prompt != null) prompt.fontSize = Mathf.RoundToInt(20 * textScale);
            if (feedbackLabel != null) feedbackLabel.fontSize = Mathf.RoundToInt(18 * textScale);
            if (menuHeading != null) menuHeading.fontSize = Mathf.RoundToInt(24 * textScale);
            if (textSizeButton != null) textSizeButton.GetComponentInChildren<Text>().fontSize = Mathf.RoundToInt(18 * textScale);
            ResizeHud();
            if (MenuOpen) RebuildMenu();
            pauseSignature = null;
        }

        public static string Title(CityTradeKind kind) => kind switch
        {
            CityTradeKind.Seller => "Фонари и батареи",
            CityTradeKind.Battery => "Магазин батарей",
            CityTradeKind.Buyback => "Скупка",
            CityTradeKind.A => "Покупатель A",
            CityTradeKind.B => "Покупатель Б",
            CityTradeKind.Workshop => "Мастерская",
            CityTradeKind.Crate => "Ящик",
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
            BuildUiArtwork();
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
            hudPanel = Rect("Wallet and orders", safe, new Vector2(0.025f, 0.975f), new Vector2(0.975f, 0.975f));
            hudPanel.pivot = new Vector2(0.5f, 1);
            var wallet = Rect("Wallet card", hudPanel, Vector2.zero, new Vector2(0.25f, 1));
            Round(wallet, new Color(0.055f, 0.07f, 0.085f, 0.94f));
            hud = Label(wallet, "", 18); hud.alignment = TextAnchor.MiddleLeft;
            hud.rectTransform.offsetMin = new Vector2(18, 10); hud.rectTransform.offsetMax = new Vector2(-14, -10);
            var orders = Rect("Orders card", hudPanel, new Vector2(0.62f, 0), Vector2.one);
            Round(orders, new Color(0.055f, 0.07f, 0.085f, 0.94f));
            orderHud = Label(orders, "", 18); orderHud.alignment = TextAnchor.MiddleLeft;
            orderHud.rectTransform.offsetMin = new Vector2(18, 10); orderHud.rectTransform.offsetMax = new Vector2(-14, -10);
            var context = Rect("Action context", safe, new Vector2(0.18f, 0.03f), new Vector2(0.82f, 0.03f));
            bottomContext = context; context.pivot = new Vector2(0.5f, 0);
            Round(context, new Color(0.055f, 0.07f, 0.085f, 0.94f));
            var contextLayout = context.gameObject.AddComponent<VerticalLayoutGroup>();
            contextLayout.padding = new RectOffset(10, 10, 8, 8); contextLayout.spacing = 4;
            contextLayout.childControlWidth = contextLayout.childControlHeight = true;
            contextLayout.childForceExpandHeight = false;
            context.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            prompt = Label(context, "", 20);
            prompt.rectTransform.anchorMin = new Vector2(0.015f, 0.50f); prompt.rectTransform.anchorMax = new Vector2(0.985f, 1);
            feedbackLabel = Label(context, "", 18);
            feedbackLabel.rectTransform.anchorMin = new Vector2(0.015f, 0); feedbackLabel.rectTransform.anchorMax = new Vector2(0.985f, 0.50f);
            var cross = Label(safe, "·", 22); cross.rectTransform.anchorMin = new Vector2(0.49f, 0.48f);
            cross.rectTransform.anchorMax = new Vector2(0.51f, 0.52f);
            tradeBackdrop = Rect("Trade backdrop", safe, Vector2.zero, Vector2.one).gameObject;
            Surface(tradeBackdrop.transform, new Color(0.008f, 0.012f, 0.018f, 0.78f));
            tradeBackdrop.SetActive(false);
            panel = Rect("Trade menu", safe, Vector2.one * 0.5f, Vector2.one * 0.5f).gameObject;
            panel.GetComponent<RectTransform>().sizeDelta = new Vector2(760, 480);
            Round(panel.transform, new Color(0.065f, 0.085f, 0.105f));
            var shadow = panel.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.3f); shadow.effectDistance = new Vector2(0, -6);
            var header = Rect("Menu header", panel.transform, new Vector2(0, 1), Vector2.one);
            header.pivot = new Vector2(0.5f, 1); header.sizeDelta = new Vector2(0, 96);
            var badge = Rect("Market emblem", header, new Vector2(0, 0.5f), new Vector2(0, 0.5f));
            badge.pivot = new Vector2(0, 0.5f); badge.anchoredPosition = new Vector2(28, 0); badge.sizeDelta = new Vector2(44, 44);
            Round(badge, new Color(0.16f, 0.23f, 0.22f));
            Label(badge, "H", 24).color = Mint;
            var caption = Label(header, "HUSTLE & SCALE", 11); caption.color = Muted; caption.alignment = TextAnchor.MiddleLeft;
            caption.rectTransform.offsetMin = new Vector2(88, 58); caption.rectTransform.offsetMax = new Vector2(-220, -18);
            menuHeading = Label(header, "", 24); menuHeading.alignment = TextAnchor.MiddleLeft; menuHeading.fontStyle = FontStyle.Bold;
            menuHeading.rectTransform.offsetMin = new Vector2(88, 12); menuHeading.rectTransform.offsetMax = new Vector2(-205, -30);
            var purse = Rect("Wallet badge", header, new Vector2(1, 0.5f), new Vector2(1, 0.5f));
            purse.pivot = new Vector2(1, 0.5f); purse.anchoredPosition = new Vector2(-28, 0); purse.sizeDelta = new Vector2(108, 44);
            Round(purse, new Color(0.12f, 0.17f, 0.17f));
            menuWallet = Label(purse, "", 20); menuWallet.color = Mint; menuWallet.fontStyle = FontStyle.Bold;
            var textSize = Rect("Text size", header, new Vector2(1, 0.5f), new Vector2(1, 0.5f));
            textSize.pivot = new Vector2(1, 0.5f); textSize.anchoredPosition = new Vector2(-144, 0); textSize.sizeDelta = new Vector2(44, 44);
            Round(textSize, new Color(0.12f, 0.15f, 0.175f));
            textSizeButton = textSize.gameObject.AddComponent<Button>();
            textSizeButton.targetGraphic = textSize.GetComponent<Image>();
            Label(textSize, "Aa", 18);
            textSizeButton.onClick.AddListener(() => SetTextScale(textScale < 1.5f ? 1.5f : 1));
            var scroll = panel.AddComponent<ScrollRect>(); scroll.horizontal = false;
            menuScroll = scroll; scroll.movementType = ScrollRect.MovementType.Clamped;
            var viewport = Rect("Viewport", panel.transform, Vector2.zero, Vector2.one);
            viewport.offsetMin = new Vector2(28, 20); viewport.offsetMax = new Vector2(-28, -100);
            viewport.gameObject.AddComponent<RectMask2D>();
            menuContent = Rect("Content", viewport, new Vector2(0, 1), Vector2.one);
            menuContent.pivot = new Vector2(0.5f, 1);
            var layout = menuContent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12; layout.padding = new RectOffset(0, 0, 0, 0);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            menuContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport; scroll.content = menuContent;
            panel.SetActive(false);
            BuildPauseUI(safe);
        }

        private void ResizeHud()
        {
            if (hud == null || hudPanel == null) return;
            hudPanel.sizeDelta = new Vector2(0, Mathf.Max(hud.preferredHeight, orderHud.preferredHeight) + 24);
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
                menuHeading.text = current != null ? Title(current.Kind) : "Карта и имущество";
                menuWallet.text = "$" + session.Balance;
                if (message.Length > 0 && (displayedMessage != message || Time.unscaledTime < messageUntil)) Line(CityMessage(message));
                if (current == null)
                {
                    BuildLocalMap();
                    Line("$" + session.Balance + " · Батареи: " + session.SpareBatteries);
                    Button(showControls ? "Скрыть управление" : "Управление", () => { showControls = !showControls; RebuildMenu(); });
                    if (showControls) Line("E — действие · 1–3 — фонарь · G — убрать · Q — выбросить · F — свет\nУ верстака: T — тест · R — замена батареи\nTab — карта · Esc — пауза · F5 / F9 в паузе — сохранить / загрузить");
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
                            var crateHere = !session.LotPurchased || CrateAtSeller;
                            ProductCard(session.LotPurchased ? "Вторая партия" : "Партия фонарей", "3 шт. · поломка каждого: 50%", "$180", lampIcon,
                                () => Execute("purchase"), session.CanPurchaseLot && session.Balance >= 180 && crateHere,
                                !crateHere && session.CanPurchaseLot ? "Принеси пустой ящик" : session.LotPurchaseStatus);
                            ProductCard("Батарея", "1 шт. · для ремонта", "$50", batteryIcon,
                                () => Execute("battery"), session.Balance >= 50);
                            break;
                        case CityTradeKind.Battery:
                            Line("Баланс $" + session.Balance + " · Батареи: " + session.SpareBatteries);
                            Button("Купить батарею · $50", () => Execute("battery")); break;
                        case CityTradeKind.Crate:
                            Button("Взять ящик", () => { if (Execute("crate")) CloseMenu(); });
                            Button("Забрать содержимое в инвентарь", () => Execute("contents"));
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
                            if (session.PurchasedLots == 2)
                                Line("Партия 2 · прежний заказ: " + State(session.PreviousOrder(id).State));
                            Line(order.Quantity + " исправных после теста · $" + order.Payment +
                                "\n" + (order.State == DealOrderState.Accepted ? "Осталось " + WorkshopView.Clock(Math.Max(0, order.DeadlineSeconds - session.ElapsedSeconds)) :
                                (id == DealOrderId.A ? "09:00" : "18:00") + " с принятия"));
                            if (order.State == DealOrderState.Available) Button("Принять заказ", () => Execute("accept"));
                            if (order.State == DealOrderState.Available || order.State == DealOrderState.Accepted)
                            {
                                Button(order.State == DealOrderState.Accepted ? "Отменить навсегда" : "Отказаться навсегда", () => Execute("close"));
                            }
                            else Line(State(order.State));
                            if (order.State == DealOrderState.Accepted)
                            {
                                SelectGoods();
                                Button("Передать · " + Selection().Count + "/" + order.Quantity, () => Execute("deliver"));
                            }
                            break;
                        case CityTradeKind.Buyback:
                            Line("После теста: исправный $60 · неисправный $35");
                            SelectGoods(); Button("Продать выбранные остатки", () => Execute("sell")); break;
                    }
                var close = BackButton();
                Button first = null, restore = null;
                foreach (var button in menuContent.GetComponentsInChildren<Button>())
                {
                    if (!button.interactable) continue;
                    if (first == null) first = button;
                    if (button.name.Replace("[x]", "[ ]") == focusName) restore = button;
                }
                EventSystem.current?.SetSelectedGameObject(focused == textSizeButton.gameObject ? textSizeButton.gameObject :
                    (restore != null ? restore : first != null ? first : close).gameObject);
                FitTradeMenu();
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
            var button = ActionButton(menuContent, value, action, enabled);
            if (value == "Принять заказ" || value.StartsWith("Передать", StringComparison.Ordinal)) SetButtonTone(button, true);
            return button;
        }

        private Button ActionButton(Transform parent, string value, Action action, bool enabled = true)
        {
            var target = Rect(value, parent, Vector2.zero, Vector2.one);
            target.gameObject.AddComponent<LayoutElement>().minHeight = 44;
            Round(target, Color.white);
            var layout = target.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 8, 8);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            var button = target.gameObject.AddComponent<Button>(); button.interactable = enabled;
            button.targetGraphic = target.GetComponent<Image>();
            button.onClick.AddListener(() => action());
            var label = Label(target, value, 18);
            label.alignment = TextAnchor.MiddleLeft;
            label.text = value;
            SetButtonTone(button, false);
            return button;
        }

        private static void Surface(Transform target, Color color)
        {
            target.gameObject.AddComponent<Image>().color = color;
        }

        private Text Label(Transform parent, string value, int size)
        {
            var label = Rect("Text", parent, Vector2.zero, Vector2.one).gameObject.AddComponent<Text>();
            label.font = font; label.fontSize = Mathf.RoundToInt(size * textScale); label.text = value;
            label.color = Paper; label.alignment = TextAnchor.MiddleCenter;
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
