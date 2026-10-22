using System.Collections.Generic;
using Inventory;
using UnityEngine;
using UnityEngine.UI;

namespace Interactables
{
    public sealed partial class CityTradeView
    {
        private RectTransform inventoryBar;
        private readonly Text[] inventoryCounts = new Text[6];
        private readonly Image[] inventoryPictures = new Image[6];
        private readonly Image[] inventoryFrames = new Image[6];
        private bool showInventoryMap;
        private int inventorySelection = -1;
        private Sprite solderIcon;
        private Texture2D solderTexture;
        private static readonly Color SlotPaper = new Color(.84f, .83f, .77f);

        private static bool InInventory(FlashlightUnit unit) => unit != null &&
            (unit.Location == FlashlightLocation.Pocket || unit.Location == FlashlightLocation.Hands);

        // Presentation stacks only: lamps keep their domain IDs and individual diagnoses.
        private int InventoryCount(int kind)
        {
            if (kind == 0)
            {
                var count = 0;
                for (var i = 0; i < 3; i++) if (InInventory(session.CityUnit(i))) count++;
                return count;
            }
            return kind == 1 ? InInventory(session.CityUnit(3)) ? 1 : 0 :
                kind == 2 ? session.SpareBatteries : kind == 3 ? session.SolderDoses : 0;
        }

        private List<int> InventoryItems()
        {
            var items = new List<int>();
            for (var kind = 0; kind < 4; kind++) if (InventoryCount(kind) > 0) items.Add(kind);
            return items;
        }

        private Sprite InventoryIcon(int kind) => kind < 2 ? lampIcon : kind == 2 ? batteryIcon : solderIcon;
        private bool InventoryHeld(int kind) => session.InHands != null &&
            (kind == 0 && session.InHands != session.SolderingLamp || kind == 1 && session.InHands == session.SolderingLamp);

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
            inventoryBar.pivot = new Vector2(.5f, 0); inventoryBar.sizeDelta = new Vector2(510, 104);
            Round(inventoryBar, new Color(.055f, .07f, .085f, .96f));
            var row = Rect("Quick slots", inventoryBar, Vector2.zero, Vector2.one);
            row.offsetMin = new Vector2(9, 30); row.offsetMax = new Vector2(-9, -8);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 5; layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = true;
            for (var i = 0; i < 6; i++)
            {
                var slot = Rect("Quick slot " + i, row, Vector2.zero, Vector2.one);
                slot.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
                inventoryFrames[i] = Round(slot, SlotPaper);
                var image = Rect("Thumbnail", slot, Vector2.zero, Vector2.one).gameObject.AddComponent<Image>();
                image.rectTransform.offsetMin = new Vector2(9, 9); image.rectTransform.offsetMax = new Vector2(-9, -9);
                image.preserveAspect = true; image.raycastTarget = false; inventoryPictures[i] = image;
                var count = Label(slot, "", 17); count.color = Ink; count.fontStyle = FontStyle.Bold;
                count.alignment = TextAnchor.LowerRight;
                count.rectTransform.offsetMin = new Vector2(0, 2); count.rectTransform.offsetMax = new Vector2(-4, 0);
                inventoryCounts[i] = count;
            }
            var hint = Label(inventoryBar, "Tab · инвентарь     1–3 · фонарь     G · убрать", 13);
            hint.color = Muted; hint.rectTransform.anchorMax = new Vector2(1, 0);
            hint.rectTransform.pivot = new Vector2(.5f, 0); hint.rectTransform.sizeDelta = new Vector2(0, 28);
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
                inventoryFrames[i].color = InventoryHeld(kind) ? Mint : SlotPaper;
            }
        }

        private void BuildInventoryGrid()
        {
            Line("Tab / Esc · закрыть     Выбери предмет", 16);
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
                var background = Round(slot, kind >= 0 && (inventorySelection == kind || InventoryHeld(kind)) ? Mint : SlotPaper);
                if (kind < 0) continue;
                var art = Rect("Thumbnail", slot, Vector2.zero, Vector2.one).gameObject.AddComponent<Image>();
                art.rectTransform.offsetMin = Vector2.one * 10; art.rectTransform.offsetMax = Vector2.one * -10;
                art.sprite = InventoryIcon(kind); art.preserveAspect = true; art.raycastTarget = false;
                var count = Label(slot, "×" + InventoryCount(kind), 18); count.color = Ink; count.fontStyle = FontStyle.Bold;
                count.alignment = TextAnchor.LowerRight;
                count.rectTransform.offsetMin = new Vector2(0, 3); count.rectTransform.offsetMax = new Vector2(-5, 0);
                var button = slot.gameObject.AddComponent<Button>(); button.targetGraphic = background;
                button.onClick.AddListener(() => { inventorySelection = kind; RebuildMenu(); });
                var colors = button.colors; colors.highlightedColor = colors.selectedColor = new Color(.75f, .93f, .84f);
                button.colors = colors;
            }
            if (inventorySelection < 0 || InventoryCount(inventorySelection) == 0) return;
            if (inventorySelection < 2)
            {
                for (var i = inventorySelection == 0 ? 0 : 3; i < (inventorySelection == 0 ? 3 : 4); i++)
                {
                    var index = i; var unit = session.CityUnit(i);
                    if (!InInventory(unit)) continue;
                    Button((unit == session.InHands ? "Убрать" : "Взять") + " · " + (i == 3 ? "Фонарик · пайка" : "Фонарь " + (i + 1)) +
                        " · " + CityCondition(unit), () => Execute("equip", index), !session.IsCrateCarried);
                }
            }
            else Line(inventorySelection == 2 ? "Батареи ×" + session.SpareBatteries + " · замена на верстаке: R" :
                "Припой ×" + session.SolderDoses + " · дозы для паяльной станции");
        }
    }
}
