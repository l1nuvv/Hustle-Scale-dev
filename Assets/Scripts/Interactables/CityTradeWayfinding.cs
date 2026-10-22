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
        private Material signMaterial, workshopMaterial, plasterMaterial, yardMaterial;

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

        private void BuildWayfinding()
        {
            // LocalBoard creates sibling text and board objects. Hiding only TextMesh
            // leaves its orange backing coplanar with our replacement.
            var replacedBoards = new[] { new Vector3(-42, 2.4f, 3), new Vector3(-65, 3.3f, 19.55f),
                new Vector3(-77.45f, 3, 30), new Vector3(65, 3.2f, 19.55f), new Vector3(-65, 3.2f, -55.7f) };
            foreach (var renderer in FindObjectsByType<MeshRenderer>())
            {
                if (!renderer.name.StartsWith("Local board |", StringComparison.Ordinal)) continue;
                foreach (var position in replacedBoards)
                    if (Vector3.Distance(renderer.transform.position, position) < 0.8f)
                    { renderer.gameObject.SetActive(false); break; }
            }
            var oldCounter = GameObject.Find("MARKET future counter");
            if (oldCounter != null)
            {
                oldCounter.GetComponent<Renderer>().enabled = false;
                // Match the actual replacement counter, instead of retaining a 2m
                // invisible solid box through the seller's body.
                oldCounter.transform.position = SellerPosition + new Vector3(0, -0.7f, 0.1f);
                oldCounter.transform.localScale = new Vector3(4.5f, 1.2f, 1);
            }
            foreach (var label in FindObjectsByType<TextMesh>(FindObjectsSortMode.None))
            {
                if (label.transform.IsChildOf(root)) continue;
                if (label.text.Contains("CITY / NORTH") || label.text.Contains("NO ACTIVE") ||
                    label.text.Contains("NO TRADING") || label.text.Contains("NO TRADE") ||
                    label.text == "BASE" || label.text == "BATTERIES" || label.text == "BUYBACK" ||
                    label.text.Contains("WORKSHOP / BASE") || label.text.Contains("FUTURE TRADE") &&
                    Mathf.Abs(label.transform.position.x) < 100 || label.text.Contains("BASE") && label.text.Contains("MARKET"))
                { label.gameObject.SetActive(false); continue; }
                var oldWidth = label.GetComponent<Renderer>().bounds.size.magnitude;
                var value = label.text;
                // Preserve the arrows already authored on each side of the physical boards.
                foreach (var pair in new[]
                {
                    ("RESIDENTIAL BUYER", "ПОКУПАТЕЛЬ В ЖИЛОМ РАЙОНЕ"),
                    ("OUTSKIRTS BUYER", "ПОКУПАТЕЛЬ НА ОКРАИНЕ"), ("GARAGE BUYER", "ПОКУПАТЕЛЬ У ГАРАЖЕЙ"),
                    ("MARKET BUYER", "РЫНОЧНЫЙ ПРИЛАВОК"), ("FUTURE TRADE", "ПОКА ЗАКРЫТО"),
                    ("WORKSHOP / BASE", "МАСТЕРСКАЯ"), ("WORKSHOP 01", "МАСТЕРСКАЯ"),
                    ("BATTERY SHOP", "БАТАРЕИ"), ("BATTERIES", "БАТАРЕИ"), ("BUYBACK", "СКУПКА"),
                    ("PARK / OPEN COMMON", "ПАРК"), ("PARK EDGE", "КРАЙ ПАРКА"),
                    ("WATER TOWER", "ВОДОНАПОРНАЯ БАШНЯ"), ("SUPPLIER", "СКЛАДЫ"),
                    ("WAREHOUSES", "СКЛАДЫ"), ("RESIDENTIAL", "ЖИЛОЙ РАЙОН"), ("GARAGES", "ГАРАЖИ"),
                    ("CENTRE", "ЦЕНТР"), ("MARKET", "РЫНОК"), ("LOADING", "СКЛАДСКОЙ ПРОЕЗД"),
                    ("COURT", "ДВОР"), ("SHOP", "БАТАРЕИ"), ("BASE", "МАСТЕРСКАЯ"),
                    ("CITY", "ГОРОД"), ("HOMES", "ЖИЛОЙ РАЙОН"), ("PARK", "ПАРК"),
                    ("NW", "СЗ"), ("NE", "СВ"), ("SW", "ЮЗ"), ("SE", "ЮВ"),
                    ("N:", "С:"), ("S:", "Ю:"), ("E:", "В:"), ("W:", "З:"), ("km/h", "км/ч")
                }) value = value.Replace(pair.Item1, pair.Item2);
                value = Regex.Replace(value, @"\s*/\s*~\d+s", "").Replace("m", " м");
                label.text = value;
                var width = label.GetComponent<Renderer>().bounds.size.magnitude;
                if (width > oldWidth) label.transform.localScale *= oldWidth / width;
            }

            workshopMaterial = new Material(bodyMaterial) { color = new Color(0.16f, 0.38f, 0.4f) };
            plasterMaterial = new Material(bodyMaterial) { color = new Color(0.64f, 0.62f, 0.54f) };
            var room = GameObject.Find("Interior | WORKSHOP / BASE");
            if (room != null)
                foreach (var renderer in room.GetComponentsInChildren<MeshRenderer>())
                    if (renderer.GetComponent<TextMesh>() == null)
                        renderer.sharedMaterial = renderer.name == "Roof" ? bodyMaterial :
                            renderer.name.Contains("Door") ? workshopMaterial : plasterMaterial;

            Board("МАСТЕРСКАЯ", new Vector3(-65, 3.2f, 19.3f), 0, new Vector2(6.6f, 0.9f));
            Board("МАСТЕРСКАЯ", new Vector3(-77.6f, 3, 30), 90, new Vector2(6.6f, 0.9f));
            Board("ФОНАРИ И БАТАРЕИ", SellerPosition + new Vector3(0, 1.75f, 1.15f), 180, new Vector2(4.5f, 0.5f));
            Board("БАТАРЕИ · $50", new Vector3(65, 3.2f, 19.3f), 0, new Vector2(6, 0.9f));
            Board("СКУПКА ФОНАРЕЙ", new Vector3(-65, 3.2f, -55.7f), 0, new Vector2(6.6f, 0.9f));
            Board("← МАСТЕРСКАЯ И ПРОДАВЕЦ", new Vector3(-42, 2.4f, 3), 0, new Vector2(5.6f, 1.2f));
            Board("← ФОНАРИ И БАТАРЕИ", new Vector3(65, 2.4f, -14), 0, new Vector2(5.6f, 0.65f));

            // Recognisable workbench and stall silhouettes, outside all movement clearances.
            BuildSliceWorkshop();
            Decor("Панель инструментов", new Vector3(-72, 2, 35), new Vector3(3, 1.1f, 0.12f), bodyMaterial);
            for (var i = 0; i < 4; i++)
                Decor("Инструмент", new Vector3(-73 + i * 0.65f, 2, 34.9f), new Vector3(0.1f, 0.65f, 0.08f), accentMaterial);
            var workLight = new GameObject("Свет над рабочим столом", typeof(Light)).GetComponent<Light>();
            workLight.transform.SetParent(root, false); workLight.transform.position = new Vector3(-72, 2.8f, 33);
            workLight.type = LightType.Point; workLight.range = 7; workLight.intensity = 3;
            workLight.color = new Color(1, 0.88f, 0.65f);
            Decor("Прилавок фонарей", SellerPosition + new Vector3(0, -0.7f, 0.1f), new Vector3(4.5f, 1.2f, 1), accentMaterial, true);
            Decor("Навес прилавка", SellerPosition + new Vector3(0, 1.35f, 0.1f), new Vector3(5, 0.18f, 2), accentMaterial, true);
            foreach (var x in new[] { -2.4f, 2.4f })
                Decor("Стойка навеса", SellerPosition + new Vector3(x, 0, 0.6f), new Vector3(0.12f, 2.6f, 0.12f), bodyMaterial, true);
        }

        private void Decor(string title, Vector3 position, Vector3 size, Material material, bool solid = false)
        { Box(title, root, position, size, material).GetComponent<Collider>().enabled = solid; }

        private Transform Board(string title, Vector3 position, float yaw, Vector2 size)
        {
            if (signMaterial == null)
            {
                signMaterial = new Material(Shader.Find("HustleScale/WorldSign"));
                root.gameObject.AddComponent<WorldSignFont>().Bind(Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"), signMaterial);
            }
            var board = Box("Вывеска | " + title, root, position, new Vector3(size.x, size.y, 0.12f), bodyMaterial);
            board.rotation = Quaternion.Euler(0, yaw, 0); board.GetComponent<Collider>().enabled = false;
            foreach (var side in new[] { 0, 180 })
            {
                var label = new GameObject("Надпись", typeof(TextMesh)).GetComponent<TextMesh>();
                label.transform.SetParent(board, false);
                label.transform.localRotation = Quaternion.Euler(0, side, 0);
                label.transform.localPosition = new Vector3(0, 0, side == 0 ? -0.071f : 0.071f);
                label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                label.fontSize = 64; label.characterSize = 0.12f;
                label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center;
                label.color = new Color(1, 0.9f, 0.55f);
                label.text = side == 0 ? title : title.Replace('←', '|').Replace('→', '←').Replace('|', '→')
                    .Replace('↑', '|').Replace('↓', '↑').Replace('|', '↓');
                label.GetComponent<Renderer>().sharedMaterial = signMaterial;
                var bounds = label.GetComponent<Renderer>().localBounds.size;
                label.transform.localScale = Vector3.one * Mathf.Min(1, (size.x - 0.2f) / Mathf.Max(0.01f, bounds.x),
                    (size.y - 0.1f) / Mathf.Max(0.01f, bounds.y));
            }
            return board;
        }

        private static Vector2 MapPosition(float x, float z) => new Vector2((x + 105) / 220, (z + 70) / 135);
        private void BuildLocalMap()
        {
            Line("МЕСТА РЯДОМ · вид сверху · север ↑", 21);
            var map = Rect("Карта района", menuContent, Vector2.zero, Vector2.one);
            map.gameObject.AddComponent<LayoutElement>().minHeight = 300 * textScale;
            map.gameObject.AddComponent<Image>().color = new Color(0.07f, 0.13f, 0.14f);
            var paths = Rect("Пешие проходы", map, Vector2.zero, Vector2.one).gameObject.AddComponent<CityTradeMapPaths>();
            paths.color = new Color(0.34f, 0.49f, 0.48f); paths.raycastTarget = false;
            paths.AddPath(-65, 25, -65, 14, -60, 14, -60, 8.35f, -53, 8.35f);
            paths.AddPath(-65, 14, -65, 0, -42, 0, -15, -7, 35, -7, 65, 0);
            paths.AddPath(65, 0, 65, 26);
            paths.AddPath(-65, 0, -80, 0, -80, -65, -65, -65, -65, -49);
            paths.AddPath(-42, 0, -29, 5, -29, 8.4f, -7, 8.4f, -7, -7);
            MapLabel(map, "МАСТЕРСКАЯ\nпроверка и ремонт", -65, 47);
            MapLabel(map, "БАТАРЕИ\n$50", 65, 47);
            MapLabel(map, "ПОКУПАТЕЛИ\nА: 2 за $260 · Б: 1 за $125", -16, 27);
            MapLabel(map, "СКУПКА\n$60 / $35", -65, -38);
            MapLabel(map, "ФОНАРИ И БАТАРЕИ\nпартия $180 · батарея $50", -40, -9);
            var p = transform.position;
            if (p.x >= -105 && p.x <= 115 && p.z >= -70 && p.z <= 65)
            {
                var marker = Rect("Ты здесь", map, MapPosition(p.x, p.z), MapPosition(p.x, p.z));
                marker.sizeDelta = new Vector2(14, 14); marker.gameObject.AddComponent<Image>().color = new Color(1, 0.78f, 0.2f);
            }
            Line(p.x < -105 || p.x > 115 || p.z < -70 || p.z > 65 ?
                "Ты за пределами этой карты. Мастерская и рынок — в центре города." :
                "Жёлтый квадрат — ты. Светлые линии — пешие проходы. Выбирай любую сделку; обязательного маршрута нет.");
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
