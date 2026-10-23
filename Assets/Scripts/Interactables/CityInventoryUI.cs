using System.Collections.Generic;
using System.Linq;
using UnityEngine.EventSystems;
using Inventory;
using UnityEngine;
using UnityEngine.UI;

namespace Interactables
{
    public sealed partial class CityTradeView
    {
        private RectTransform inventoryBar;
        private readonly Text[] inventoryCounts = new Text[6];
        private readonly Text[] inventoryStatuses = new Text[6];
        private readonly Image[] inventoryPictures = new Image[6];
        private readonly Image[] inventoryFrames = new Image[6];
        private bool showInventoryMap;
        private int inventorySelection = -1;
        // A partition of the three ordinary slots. It changes presentation, never ownership/IDs.
        private readonly List<int> inventoryLampStacks = new List<int> { 7 };
        private int inventorySort;
        private static int LampMask(int kind) => kind == 0 ? 7 : kind >= 9 && kind <= 15 ? kind - 8 : 0;
        private static int StackKind(int mask) => mask == 7 ? 0 : mask + 8;
        private void RestoreInventoryLayout()
        {
            inventoryLampStacks.Clear();
            var stored = save.World.InventoryLampStacks;
            inventoryLampStacks.AddRange(stored == null || stored.Length == 0 ? new[] { 7 } : stored);
            inventorySort = save.World.InventorySort;
            inventorySelection = -1;
        }
        private void SaveInventoryLayout()
        {
            save.World.InventoryLampStacks = inventoryLampStacks.ToArray();
            save.World.InventorySort = inventorySort;
            save.RequestSave();
            RebuildMenu(); RefreshInventoryBar();
            menuScroll.verticalNormalizedPosition = 1;
        }
        private void SplitInventoryStack(bool all)
        {
            var mask = LampMask(inventorySelection);
            var at = inventoryLampStacks.FindIndex(parent => (parent & mask) == mask);
            if (at < 0 || InventoryCount(inventorySelection) < 2) return;
            var available = Enumerable.Range(0, 3).Where(i => (mask & (1 << i)) != 0 && InInventory(session.CityUnit(i))).ToArray();
            var detached = all ? available.Take(available.Length - 1).ToArray() : available.Take(1).ToArray();
            foreach (var index in detached)
            {
                mask &= ~(1 << index);
                inventoryLampStacks.Add(1 << index);
            }
            inventoryLampStacks[at] &= ~detached.Sum(index => 1 << index);
            inventorySelection = StackKind(mask);
            SaveInventoryLayout();
        }
        private void MergeInventoryStacks()
        {
            inventoryLampStacks.Clear(); inventoryLampStacks.Add(7);
            inventorySelection = InventoryItems().FirstOrDefault(); SaveInventoryLayout();
        }
        private string InventorySortKey(int kind)
        {
            var mask = LampMask(kind);
            if (mask == 0) return "1" + kind;
            if (inventorySort != 1) return "0" + Enumerable.Range(0, 3).First(i => (mask & (1 << i)) != 0);
            // Use only the known diagnosis: sorting must not reveal an untested RNG result.
            return "0" + string.Join("/", Enumerable.Range(0, 3).Where(i => (mask & (1 << i)) != 0 && InInventory(session.CityUnit(i)))
                .Select(i => CityCondition(session.CityUnit(i))).OrderBy(value => value));
        }
        private Sprite solderIcon;
        private Texture2D solderTexture;
        private static readonly Color SlotPaper = new Color(.84f, .83f, .77f);

        private static bool InInventory(FlashlightUnit unit) => unit != null &&
            (unit.Location == FlashlightLocation.Pocket || unit.Location == FlashlightLocation.Hands);

        // Presentation stacks only: lamps keep their domain IDs and individual diagnoses.
        private int InventoryCount(int kind)
        {
            var mask = LampMask(kind);
            if (mask != 0)
            {
                var count = 0;
                for (var i = 0; i < 3; i++) if ((mask & (1 << i)) != 0 && InInventory(session.CityUnit(i))) count++;
                return count;
            }
            return kind == 1 ? InInventory(session.CityUnit(3)) ? 1 : 0 :
                kind == 2 ? session.SpareBatteries : kind == 3 ? session.SolderDoses : kind == 4 && session.KnifePresented ? 1 : 0;
        }

        private List<int> InventoryItems()
        {
            var items = new List<int>();
            foreach (var parent in inventoryLampStacks)
                for (var condition = 0; condition < 3; condition++)
                {
                    var mask = 0;
                    for (var i = 0; i < 3; i++)
                        if ((parent & (1 << i)) != 0 && InInventory(session.CityUnit(i)) && InventoryDiagnosis(session.CityUnit(i)) == condition) mask |= 1 << i;
                    if (mask != 0) items.Add(StackKind(mask));
                }
            for (var kind = 1; kind < 5; kind++) if (InventoryCount(kind) > 0) items.Add(kind);
            items.Sort((a, b) => string.CompareOrdinal(InventorySortKey(a), InventorySortKey(b)));
            return items;
        }

        private static int InventoryDiagnosis(FlashlightUnit unit) => !unit.IsTested ? 0 : unit.LastTestPassed ? 2 : 1;
        private string InventoryDiagnosisLabel(int kind)
        {
            var mask = LampMask(kind);
            var unit = Enumerable.Range(0, 3).Where(i => (mask & (1 << i)) != 0).Select(session.CityUnit).FirstOrDefault();
            return unit == null ? "" : InventoryDiagnosis(unit) == 0 ? "Не проверены" : InventoryDiagnosis(unit) == 1 ? "Нужна замена" : "Исправны";
        }
        private int StackDiagnosis(int kind)
        {
            var mask = LampMask(kind);
            return mask == 0 ? -1 : InventoryDiagnosis(session.CityUnit(Enumerable.Range(0, 3).First(i => (mask & (1 << i)) != 0)));
        }
        private string InventoryShortStatus(int kind) => StackDiagnosis(kind) switch
        { 0 => "Не пров.", 1 => "Ремонт", 2 => "Исправен", _ => "" };
        private Color InventorySlotColor(int kind) => StackDiagnosis(kind) switch
        {
            0 => new Color(.72f, .81f, .87f),
            1 => new Color(.95f, .74f, .44f),
            2 => new Color(.64f, .83f, .67f),
            _ => SlotPaper
        };
        private Sprite InventoryIcon(int kind) => kind == 4 ? knifeIcon : LampMask(kind) != 0 || kind == 1 ? lampIcon : kind == 2 ? batteryIcon : solderIcon;
        private bool InventoryHeld(int kind) => kind == 4 ? session.KnifeInHands : session.InHands != null &&
            (LampMask(kind) != 0 && Enumerable.Range(0, 3).Any(i => (LampMask(kind) & (1 << i)) != 0 && session.CityUnit(i) == session.InHands) || kind == 1 && session.InHands == session.SolderingLamp);

        private void BuildInventoryBar(Transform parent)
        {
            solderTexture = new Texture2D(96, 96, TextureFormat.RGBA32, false);
            solderTexture.wrapMode = TextureWrapMode.Clamp;
            for (var y = 0; y < 96; y++)
                for (var x = 0; x < 96; x++)
                {
                    var radius = new Vector2(x - 43, y - 48).magnitude;
                    var color = radius < 30 && radius > 9 ? new Color(.45f, .52f, .54f) : Color.clear;
                    if (radius < 32 && radius > 25 || radius < 13 && radius > 9) color = Ink;
                    if (Segment(new Vector2(x, y), new Vector2(69, 43), new Vector2(82, 21), 2)) color = Ink;
                    solderTexture.SetPixel(x, y, color);
                }
            solderTexture.Apply(false, true);
            solderIcon = Sprite.Create(solderTexture, new Rect(0, 0, 96, 96), Vector2.one * .5f);
            inventoryBar = Rect("Inventory bar", parent, new Vector2(.5f, .025f), new Vector2(.5f, .025f));
            inventoryBar.pivot = new Vector2(.5f, 0); inventoryBar.sizeDelta = new Vector2(385, 60);
            var row = Rect("Quick slots", inventoryBar, Vector2.zero, Vector2.one);
            row.offsetMin = row.offsetMax = Vector2.zero;
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 5; layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = true;
            for (var i = 0; i < 6; i++)
            {
                var slot = Rect("Quick slot " + i, row, Vector2.zero, Vector2.one);
                slot.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
                inventoryFrames[i] = slot.gameObject.AddComponent<Image>();
                inventoryFrames[i].color = SlotPaper;
                var image = Rect("Thumbnail", slot, Vector2.zero, Vector2.one).gameObject.AddComponent<Image>();
                image.rectTransform.offsetMin = new Vector2(9, 9); image.rectTransform.offsetMax = new Vector2(-9, -9);
                image.preserveAspect = true; image.raycastTarget = false; inventoryPictures[i] = image;
                var status = Label(slot, "", 10); status.color = Ink; status.fontStyle = FontStyle.Bold;
                status.alignment = TextAnchor.UpperCenter;
                status.rectTransform.offsetMin = new Vector2(2, 0); status.rectTransform.offsetMax = new Vector2(-2, -2);
                inventoryStatuses[i] = status;
                var count = Label(slot, "", 17); count.color = Ink; count.fontStyle = FontStyle.Bold;
                count.alignment = TextAnchor.LowerRight;
                count.rectTransform.offsetMin = new Vector2(0, 2); count.rectTransform.offsetMax = new Vector2(-4, 0);
                inventoryCounts[i] = count;
            }
        }

        private void RefreshInventoryBar()
        {
            if (inventoryBar == null) return;
            var items = InventoryItems();
            for (var i = 0; i < 6; i++)
            {
                var kind = i < items.Count ? items[i] : -1;
                inventoryCounts[i].text = kind >= 0 ? "×" + InventoryCount(kind) : "";
                inventoryPictures[i].enabled = kind >= 0;
                inventoryPictures[i].sprite = kind >= 0 ? InventoryIcon(kind) : null;
                inventoryStatuses[i].text = kind >= 0 ? InventoryShortStatus(kind) : "";
                var tone = InventorySlotColor(kind);
                inventoryFrames[i].color = InventoryHeld(kind) ? Color.Lerp(tone, Color.white, .35f) : tone;
            }
        }

        private void BuildInventoryGrid()
        {
            Line("Tab / Esc · закрыть     Выбери предмет", 16);
            Line("Серо-голубой — не проверены · Янтарный — ремонт · Зелёный — исправны", 13);
            Button(inventorySort == 0 ? "Сортировка: тип → состояние" : "Сортировка: состояние → тип", () =>
            { inventorySort = 1 - inventorySort; SaveInventoryLayout(); });
            BuildInventorySelection();
            var items = InventoryItems();
            var grid = Rect("Inventory grid", menuContent, Vector2.zero, Vector2.one);
            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount; layout.constraintCount = 6;
            layout.childAlignment = TextAnchor.UpperCenter; layout.spacing = new Vector2(5, 5);
            var side = Mathf.Min(72, (Mathf.Min(620, safeArea.rect.width - 48) - 81) / 6);
            layout.cellSize = Vector2.one * side;
            grid.gameObject.AddComponent<LayoutElement>().preferredHeight = side * 4 + 15;
            for (var i = 0; i < 24; i++)
            {
                var slot = Rect("Inventory cell " + i, grid, Vector2.zero, Vector2.one);
                var kind = i < items.Count ? items[i] : -1;
                var background = slot.gameObject.AddComponent<Image>();
                var tone = InventorySlotColor(kind);
                background.color = kind >= 0 && (inventorySelection == kind || InventoryHeld(kind)) ? Color.Lerp(tone, Color.white, .35f) : tone;
                if (kind < 0) continue;
                var art = Rect("Thumbnail", slot, Vector2.zero, Vector2.one).gameObject.AddComponent<Image>();
                art.rectTransform.offsetMin = Vector2.one * 10; art.rectTransform.offsetMax = Vector2.one * -10;
                art.sprite = InventoryIcon(kind); art.preserveAspect = true; art.raycastTarget = false;
                if (LampMask(kind) != 0)
                {
                    var status = Label(slot, InventoryShortStatus(kind), 11);
                    status.fontStyle = FontStyle.Bold;
                    status.color = Ink; status.alignment = TextAnchor.UpperCenter;
                    status.rectTransform.offsetMin = new Vector2(2, 0); status.rectTransform.offsetMax = new Vector2(-2, -2);
                }
                var count = Label(slot, "×" + InventoryCount(kind), 18); count.color = Ink; count.fontStyle = FontStyle.Bold;
                count.alignment = TextAnchor.LowerRight;
                count.rectTransform.offsetMin = new Vector2(0, 3); count.rectTransform.offsetMax = new Vector2(-5, 0);
                var button = slot.gameObject.AddComponent<Button>(); button.targetGraphic = background;
                button.onClick.AddListener(() =>
                {
                    inventorySelection = kind; RebuildMenu();
                    var action = menuContent.GetComponentsInChildren<Button>().FirstOrDefault(b => b.name.StartsWith("Взять ·") || b.name.StartsWith("Убрать ·"));
                    if (action != null) EventSystem.current?.SetSelectedGameObject(action.gameObject);
                    menuScroll.verticalNormalizedPosition = 1;
                });
                var colors = button.colors; colors.highlightedColor = colors.selectedColor = new Color(.75f, .93f, .84f);
                button.colors = colors;
            }

        }

        private void BuildInventorySelection()
        {
            if (inventorySelection < 0 || InventoryCount(inventorySelection) == 0) return;
            var mask = LampMask(inventorySelection);
            if (mask != 0 || inventorySelection == 1)
            {
                if (mask != 0) Line("Фонари · " + InventoryDiagnosisLabel(inventorySelection), 16);
                for (var i = mask != 0 ? 0 : 3; i < (mask != 0 ? 3 : 4); i++)
                {
                    var index = i; var unit = session.CityUnit(i);
                    if (!InInventory(unit) || i < 3 && (mask & (1 << i)) == 0) continue;
                    Button((unit == session.InHands ? "Убрать" : "Взять") + " · " + (i == 3 ? "Фонарик · пайка" : "Фонарь " + (i + 1)) +
                        " · " + CityCondition(unit), () => Execute("equip", index), !session.IsCrateCarried);
                }
                if (mask != 0 && InventoryCount(inventorySelection) > 1)
                {
                    Button("Отделить один фонарь", () => SplitInventoryStack(false));
                    Button("Разделить стопку по одному", () => SplitInventoryStack(true));
                }
                if (mask != 0 && inventoryLampStacks.Count > 1)
                    Button("Объединить одинаковые стопки", MergeInventoryStacks);
            }
            else if (inventorySelection == 4)
                Button((session.KnifeInHands ? "Убрать" : "Взять") + " · кухонный нож · " + (session.KnifeReady ? "проверен" : "нужна заточка / проверка"),
                    () => Execute("knifeequip"), !session.IsCrateCarried && session.InHands == null);
            else Line(inventorySelection == 2 ? "Батареи ×" + session.SpareBatteries + " · замена на верстаке: R" :
                "Припой ×" + session.SolderDoses + " · дозы для паяльной станции");
        }
    }
}
