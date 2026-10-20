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
        private Text hud, prompt, feedbackLabel;
        private Font font;
        private bool rebuilding;

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
            var hudPanel = Rect("Wallet and orders", safe, new Vector2(0.015f, 0.86f), new Vector2(0.58f, 0.985f));
            hudPanel.gameObject.AddComponent<Image>().color = new Color(0.025f, 0.045f, 0.05f, 0.92f);
            hud = Label(hudPanel, "", 18); hud.alignment = TextAnchor.MiddleLeft;
            hud.rectTransform.offsetMin = new Vector2(14, 4); hud.rectTransform.offsetMax = new Vector2(-10, -4);
            prompt = Label(safe, "", 20);
            prompt.rectTransform.anchorMin = new Vector2(0.08f, 0.34f); prompt.rectTransform.anchorMax = new Vector2(0.92f, 0.42f);
            feedbackLabel = Label(safe, "", 18);
            feedbackLabel.rectTransform.anchorMin = new Vector2(0.03f, 0.15f); feedbackLabel.rectTransform.anchorMax = new Vector2(0.97f, 0.27f);
            var controls = Label(safe, "Tab — карта и имущество · E — взаимодействие · Esc — пауза", 17);
            controls.rectTransform.anchorMin = new Vector2(0, 0.02f); controls.rectTransform.anchorMax = new Vector2(1, 0.10f);
            var cross = Label(safe, "+", 22); cross.rectTransform.anchorMin = new Vector2(0.49f, 0.48f);
            cross.rectTransform.anchorMax = new Vector2(0.51f, 0.52f);
            panel = Rect("Trade menu", safe, new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.9f)).gameObject;
            panel.AddComponent<Image>().color = new Color(0.035f, 0.055f, 0.06f, 1);
            var scroll = panel.AddComponent<ScrollRect>(); scroll.horizontal = false;
            var viewport = Rect("Viewport", panel.transform, new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.98f));
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
                foreach (Transform child in menuContent) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
                Line(current != null ? Title(current.Kind) : "Карта района и имущество", 25);
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
                            Line(session.LotPurchased ? "Партия распродана. Пополнения нет." : "Одна партия: 3 непроверенных фонаря за $180. Начальный капитал $300.");
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
                            Line("Ящик ставится на жёлтую площадку у продавца или в мастерской. С фонарём в руках подойди к ящику, чтобы вернуть его.");
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
                EventSystem.current?.SetSelectedGameObject(close.gameObject);
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
        private void Line(string value, int size = 18)
        {
            var label = Label(menuContent, value, size);
            label.alignment = TextAnchor.MiddleLeft;
            label.gameObject.AddComponent<LayoutElement>().minHeight = size + 12;
        }
        private Button Button(string value, Action action, bool enabled = true)
        {
            var target = Rect(value, menuContent, Vector2.zero, Vector2.one);
            target.gameObject.AddComponent<LayoutElement>().minHeight = 40;
            target.gameObject.AddComponent<Image>().color = new Color(0.14f, 0.26f, 0.28f);
            var button = target.gameObject.AddComponent<Button>(); button.interactable = enabled;
            button.onClick.AddListener(() => action());
            Label(target, value, 18);
            return button;
        }
        private Text Label(Transform parent, string value, int size)
        {
            var label = Rect("Text", parent, Vector2.zero, Vector2.one).gameObject.AddComponent<Text>();
            label.font = font; label.fontSize = size; label.text = value;
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
