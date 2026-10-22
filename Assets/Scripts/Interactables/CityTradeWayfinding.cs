using System;
using System.Text.RegularExpressions;
using Inventory;
using UnityEngine;
using UnityEngine.UI;

namespace Interactables
{
    // Presentation for this fixed neighbourhood; no change to trade rules or save data.
    public sealed partial class CityTradeView
    {

        private static string Location(FlashlightLocation location) => location switch
        {
            FlashlightLocation.Crate => "в ящике",
            FlashlightLocation.Hands => "в руках",
            FlashlightLocation.Bench => "на столе",
            FlashlightLocation.Seller => "у продавца",
            FlashlightLocation.BuyerA => "продан покупателю А",
            FlashlightLocation.BuyerB => "продан покупателю Б",
            FlashlightLocation.SolderBuyer => "передан по заказу пайки",
            FlashlightLocation.BoughtBack => "продан в скупку",
            FlashlightLocation.Pocket => "в кармане",
            _ => "в мире"
        };

        private static string CityMessage(string text)
        {
            if (text.StartsWith("Purchased three lamps", StringComparison.Ordinal)) return "Партия куплена · −$180";
            if (text.StartsWith("Battery purchased", StringComparison.Ordinal)) return "Батарея куплена · −$50";
            if (text.StartsWith("Delivered A", StringComparison.Ordinal)) return "Заказ A · +$260";
            if (text.StartsWith("Delivered B", StringComparison.Ordinal)) return "Заказ Б · +$125";
            if (text.StartsWith("Accepted ", StringComparison.Ordinal)) return "Заказ принят";
            if (text.StartsWith("Order ", StringComparison.Ordinal)) return "Заказ закрыт навсегда";
            return text switch
            {
                "Crate returned to its dock." => "Ящик на площадке",
                "Crate in hands. All stored lamps move with it." => "Ящик в руках",
                "Lamp retrieved from bench." => "Фонарь в руках",
                "Take a lamp from the crate first." => "Сначала возьми фонарь из ящика",
                "Lamp placed on bench. Run the test." => "Фонарь на столе · нужен тест",
                "Dock the crate first." => "Сначала выброси ящик из рук: Q.",
                "Place a lamp on the bench first." => "Возьми фонарь из ящика. E у рабочего стола — положить фонарь.",
                "Unknown lamp." => "Такого фонаря нет.",
                "Every lamp must have a different owned identity." => "Выбери разные принадлежащие тебе фонари.",
                "Repair time limit reached." => "Лимит времени ремонта достигнут.",
                _ => FirstDealText.Message(text)
            };
        }

        private Vector2 MapPosition(float x, float z)
        {
            var bounds = sceneBindings.MapBounds;
            return new Vector2((x - bounds.xMin) / bounds.width, (z - bounds.yMin) / bounds.height);
        }
        private void BuildLocalMap()
        {
            Line("МЕСТА РЯДОМ · вид сверху · север ↑", 21);
            var map = Rect("Карта района", menuContent, Vector2.zero, Vector2.one);
            map.gameObject.AddComponent<LayoutElement>().minHeight = 300 * textScale;
            map.gameObject.AddComponent<Image>().color = new Color(0.07f, 0.13f, 0.14f);
            foreach (var site in sceneBindings.Sites)
                MapLabel(map, Title(site.Role), site.transform.position.x, site.transform.position.z);
            MapLabel(map, "ПАЙКА", StationPosition.x, StationPosition.z);
            var p = transform.position;
            if (sceneBindings.MapBounds.Contains(new Vector2(p.x, p.z)))
            {
                var marker = Rect("Ты здесь", map, MapPosition(p.x, p.z), MapPosition(p.x, p.z));
                marker.sizeDelta = new Vector2(14, 14); marker.gameObject.AddComponent<Image>().color = new Color(1, 0.78f, 0.2f);
            }
            Line(!sceneBindings.MapBounds.Contains(new Vector2(p.x, p.z)) ?
                "Ты за пределами области карты." :
                "Жёлтый квадрат — ты. Точки соответствуют объектам сцены. Выбирай любую сделку; обязательного маршрута нет.");
        }

        private void MapLabel(Transform map, string value, float x, float z)
        {
            var label = Label(map, value, 15); label.alignment = TextAnchor.MiddleCenter;
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = MapPosition(x, z);
            label.rectTransform.sizeDelta = new Vector2(205, 48 * textScale);
            label.rectTransform.sizeDelta = new Vector2(205, Mathf.Max(48 * textScale, label.preferredHeight + 4));
        }
    }
}
