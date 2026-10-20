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
        private RectTransform menuContent;
        private Text hud, prompt, feedbackLabel;
        private Font font;
        private bool rebuilding;

        public static string Title(CityTradeKind kind) => kind switch
        {
            CityTradeKind.Seller => "Продавец партии",
            CityTradeKind.Battery => "Магазин батарей",
            CityTradeKind.Buyback => "Скупка",
            CityTradeKind.A => "Покупатель A",
            CityTradeKind.B => "Покупатель B",
            CityTradeKind.Workshop => "Мастерская",
            CityTradeKind.Crate => "Ящик · взять / выбрать фонарь",
            CityTradeKind.SellerDock => "Док у продавца",
            CityTradeKind.WorkshopDock => "Док в мастерской",
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
            safe.anchorMin = Screen.safeArea.min / new Vector2(Screen.width, Screen.height);
            safe.anchorMax = Screen.safeArea.max / new Vector2(Screen.width, Screen.height);
            hud = Label(safe, "", 18);
            hud.rectTransform.anchorMin = new Vector2(0.02f, 0.66f); hud.rectTransform.anchorMax = new Vector2(0.98f, 0.80f);
            prompt = Label(safe, "", 20);
            prompt.rectTransform.anchorMin = new Vector2(0.08f, 0.30f); prompt.rectTransform.anchorMax = new Vector2(0.92f, 0.41f);
            feedbackLabel = Label(safe, "", 18);
            feedbackLabel.rectTransform.anchorMin = new Vector2(0.03f, 0.15f); feedbackLabel.rectTransform.anchorMax = new Vector2(0.97f, 0.27f);
            var controls = Label(safe, "Tab — имущество · 1–3 — фонарь · G — убрать · Q — поставить · F — свет", 17);
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

        private void RebuildMenu()
        {
            if (rebuilding) return;
            rebuilding = true;
            try
            {
                foreach (Transform child in menuContent) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
                Line(current != null ? Title(current.Kind) : "Имущество и учёт", 25);
                Line(hudText + "\n" + message);
                if (current == null)
                {
                    Line("Три слота фонарей; батареи хранятся отдельно. Для сдачи принеси товар в руках или в ящике.\n" +
                        "Выручка $" + session.Revenue + " · расходы $" + session.Expenses +
                        " · прибыль $" + (session.Balance - session.InitialBalance) + ". Продажа не заканчивает игру.");
                    for (var i = 0; i < 3; i++)
                    {
                        var index = i; var unit = session.Units[i];
                        Button("Фонарь " + (i + 1) + " · " + FirstDealText.Condition(unit) + " · " + unit.Location,
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
                            Line("Ящик ставится только у двух обозначенных доков. С фонарём в руках подойди к ящику, чтобы вернуть его.");
                            Button(session.IsCrateCarried ? "Поставить ящик у этого дока" : "Взять ящик", () => Execute("crate"));
                            if (current.Kind == CityTradeKind.Crate)
                                for (var i = 0; i < 3; i++)
                                {
                                    var index = i;
                                    Button("Фонарь " + (i + 1) + " · " + FirstDealText.Condition(session.Units[i]) + " · " + session.Units[i].Location,
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
