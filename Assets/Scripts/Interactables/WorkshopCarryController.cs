using Inventory;
using Player.Movement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Interactables
{
    [RequireComponent(typeof(WorkshopView))]
    public sealed class WorkshopCarryController : MonoBehaviour
    {
        [SerializeField] private FirstDealView deal;
        [SerializeField] private PlayerController player;
        [SerializeField] private Camera eye;
        private WorkshopView workshop;
        private Player.PlayerInput input;
        private GameObject inventoryPanel;
        private readonly Text[] slotLabels = new Text[8];
        private readonly Image[] slotFaces = new Image[8];
        private readonly Button[] inventoryButtons = new Button[4];
        private GameObject canvasObject;
        private GameObject preview;
        private Material previewMaterial;
        private Font font;
        private GameObject quickSlots;
        private GameObject controls;
        public bool IsOpen => inventoryPanel != null && inventoryPanel.activeSelf;

        private void Start()
        {
            workshop = GetComponent<WorkshopView>();
            input = player.GetComponent<Player.PlayerInput>();
            BuildUI();
            BuildPreview();
            input.OnToggleInventory += ToggleInventory;
            input.OnToggleLight += ToggleLight;
            input.OnPlaceItem += PlaceItem;
            input.OnStowItem += StowItem;
            input.OnSelectSlot += SelectSlot;
            workshop.Session.Changed += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            if (input != null)
            {
                input.OnToggleInventory -= ToggleInventory;
                input.OnToggleLight -= ToggleLight;
                input.OnPlaceItem -= PlaceItem;
                input.OnStowItem -= StowItem;
                input.OnSelectSlot -= SelectSlot;
            }
            if (workshop != null && workshop.Session != null) workshop.Session.Changed -= Refresh;
            if (canvasObject != null) Destroy(canvasObject);
            if (preview != null) Destroy(preview);
            if (previewMaterial != null) Destroy(previewMaterial);
        }

        public void HideInventory()
        {
            if (inventoryPanel != null) inventoryPanel.SetActive(false);
        }

        private void ToggleInventory()
        {
            if (IsOpen) { deal.CloseMenu(); return; }
            if (deal.MenuOpen || workshop.Session.ShiftEnded) return;
            deal.OpenMenu(false, true);
            inventoryPanel.SetActive(true);
            EventSystem.current?.SetSelectedGameObject(inventoryButtons[0].gameObject);
        }

        private void SelectSlot(int index)
        {
            if (!IsOpen && !GameplayAllowed) return;
            if (IsOpen) deal.CloseMenu();
            if (index == 3)
            { workshop.ShowMessage("Батарей в запасе: " + workshop.Session.SpareBatteries + ". Замена — на ремонтном столе."); return; }
            workshop.Session.EquipSlot(index, out var message);
            workshop.ShowMessage(message);
        }

        private bool Surface(out RaycastHit hit) => Physics.Raycast(eye.transform.position,
            eye.transform.forward, out hit, 2.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        private bool GameplayAllowed => !deal.MenuOpen && !workshop.Session.IsPaused &&
            !workshop.Session.ShiftEnded && player.CanInteract;

        private void Update()
        {
            if (workshop == null) return;
            var session = workshop.Session;
            quickSlots.SetActive(!deal.MenuOpen);
            controls.SetActive(!deal.MenuOpen);
            var active = GameplayAllowed;
            var hasSurface = Surface(out var hit);
            preview.SetActive(active && session.InHands != null && hasSurface);
            if (preview.activeSelf)
            {
                var allowed = workshop.CanPlace(hit, out var position, out var rotation);
                preview.transform.SetPositionAndRotation(position + Vector3.up * 0.04f, rotation);
                previewMaterial.SetColor("_BaseColor", allowed ? new Color(0.35f, 0.8f, 0.6f, 0.22f) : new Color(0.9f, 0.25f, 0.2f, 0.22f));
            }
        }

        private void StowItem()
        {
            if (!GameplayAllowed) return;
            workshop.Session.StowHeld(out var message);
            workshop.ShowMessage(message);
        }

        private void PlaceItem()
        {
            if (!GameplayAllowed) return;
            if (Surface(out var hit) && workshop.Session.InHands != null) workshop.PlaceHeld(hit);
            else workshop.ShowMessage(workshop.Session.InHands == null ? "Сначала возьми фонарь в руки." : "Наведи прицел на поверхность в пределах 2,5 м.");
        }

        private void ToggleLight()
        {
            if (!GameplayAllowed) return;
            {
                var session = workshop.Session;
                var unit = session.InHands;
                if (unit == null && Surface(out var hit))
                {
                    var placed = hit.collider.GetComponentInParent<PlacedLampInteraction>();
                    if (placed != null) unit = session.Units[placed.Index];
                    else if (session.OnBench != null &&
                        hit.collider.GetComponentInParent<WorkshopAction>() is WorkshopAction station && station.Kind == WorkshopActionKind.Bench)
                        unit = session.OnBench;
                }
                var index = -1;
                for (var i = 0; i < session.Units.Count; i++) if (session.Units[i] == unit) index = i;
                session.TogglePower(index, out var message);
                workshop.ShowMessage(message);
            }
        }

        private void Refresh()
        {
            for (var i = 0; i < slotLabels.Length; i++)
            {
                var index = i % 4;
                var text = "4  БАТАРЕИ\n×" + workshop.Session.SpareBatteries;
                var carried = false;
                var held = false;
                if (index < 3)
                {
                    var unit = workshop.Session.Units[index];
                    held = unit.Location == FlashlightLocation.Hands;
                    carried = held || unit.Location == FlashlightLocation.Pocket;
                    var location = held ? (unit.IsPowered ? "В РУКАХ · СВЕТ" : "В РУКАХ") : carried ? "В ИНВЕНТАРЕ" :
                        unit.Location == FlashlightLocation.Crate ? "В ЯЩИКЕ" : unit.Location == FlashlightLocation.Bench ? "НА СТОЛЕ" :
                        unit.Location == FlashlightLocation.World ? "ПОСТАВЛЕН" : WorkshopSession.IsOwned(unit) ? "" : "ПУСТО";
                    text = (index + 1) + "  ФОНАРЬ " + (index + 1) + "\n" + location;
                }
                slotLabels[i].text = text;
                slotLabels[i].color = carried || index == 3 ? new Color(0.9f, 0.9f, 0.84f) : new Color(0.51f, 0.55f, 0.55f);
                slotFaces[i].color = held ? new Color(0.22f, 0.35f, 0.34f, 0.98f) : new Color(0.065f, 0.085f, 0.09f, 0.96f);
            }
        }

        private void BuildUI()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvasObject = new GameObject("Carry inventory HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = 0.5f;
            var hotbar = Rect("Quick slots", canvas.transform, new Vector2(0.5f, 0), new Vector2(560, 78), new Vector2(0, 76));
            quickSlots = hotbar.gameObject;
            Slots(hotbar, 0, false);
            controls = Label("Controls", canvas.transform, new Vector2(0.5f, 0), new Vector2(1000, 34), new Vector2(0, 22),
                "Tab — инвентарь   ·   1–3 — фонарь   ·   F — свет   ·   Q — поставить   ·   G — убрать", 18).gameObject;
            inventoryPanel = Rect("Inventory", canvas.transform, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero).gameObject;
            var full = inventoryPanel.GetComponent<RectTransform>();
            full.anchorMin = Vector2.zero; full.anchorMax = Vector2.one; full.offsetMin = full.offsetMax = Vector2.zero;
            inventoryPanel.AddComponent<Image>().color = new Color(0.025f, 0.035f, 0.04f, 1);
            Label("Title", full, new Vector2(0.5f, 0.5f), new Vector2(900, 50), new Vector2(0, 160), "ИНВЕНТАРЬ", 32);
            Label("Explanation", full, new Vector2(0.5f, 0.5f), new Vector2(1050, 70), new Vector2(0, 100),
                "Три слота для фонарей и запас батарей. Нажми на слот, чтобы взять фонарь.\nТовар для сделки нужно предъявить в руках или в ящике.", 20);
            var slots = Rect("Slots", full, new Vector2(0.5f, 0.5f), new Vector2(560, 78), new Vector2(0, 0));
            Slots(slots, 4, true);
            var close = Rect("Close", full, new Vector2(0.5f, 0.5f), new Vector2(300, 52), new Vector2(0, -135));
            close.gameObject.AddComponent<Image>().color = new Color(0.2f, 0.3f, 0.29f);
            close.gameObject.AddComponent<Button>().onClick.AddListener(deal.CloseMenu);
            Label("Label", close, new Vector2(0.5f, 0.5f), new Vector2(290, 48), Vector2.zero, "Вернуться · Tab / Esc", 21);
            inventoryPanel.SetActive(false);
        }

        private void Slots(RectTransform parent, int offset, bool interactive)
        {
            var layout = parent.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8; layout.childControlHeight = layout.childControlWidth = true;
            layout.childForceExpandHeight = layout.childForceExpandWidth = true;
            for (var i = 0; i < 4; i++)
            {
                var index = i;
                var card = Rect("Slot " + (i + 1), parent, new Vector2(0.5f, 0.5f), new Vector2(130, 78), Vector2.zero);
                slotFaces[offset + i] = card.gameObject.AddComponent<Image>();
                slotFaces[offset + i].raycastTarget = interactive;
                var label = Label("Label", card, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, "", 17);
                label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.offsetMin = new Vector2(4, 4); label.rectTransform.offsetMax = new Vector2(-4, -4);
                slotLabels[offset + i] = label;
                if (interactive)
                {
                    var button = card.gameObject.AddComponent<Button>();
                    var colors = button.colors; colors.selectedColor = new Color(0.7f, 0.85f, 0.8f); button.colors = colors;
                    button.onClick.AddListener(() => SelectSlot(index));
                    inventoryButtons[i] = button;
                }
            }
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 size, Vector2 position)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f); rect.sizeDelta = size; rect.anchoredPosition = position;
            return rect;
        }

        private Text Label(string name, Transform parent, Vector2 anchor, Vector2 size, Vector2 position, string value, int fontSize)
        {
            var label = Rect(name, parent, anchor, size, position).gameObject.AddComponent<Text>();
            label.font = font; label.fontSize = fontSize; label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(0.87f, 0.88f, 0.82f); label.text = value; label.raycastTarget = false;
            return label;
        }

        private void BuildPreview()
        {
            preview = GameObject.CreatePrimitive(PrimitiveType.Cube);
            preview.name = "Placement preview";
            preview.GetComponent<Collider>().enabled = false;
            Destroy(preview.GetComponent<Collider>());
            preview.transform.localScale = new Vector3(0.31f, 0.48f, 0.27f);
            previewMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            previewMaterial.SetFloat("_Surface", 1); previewMaterial.SetFloat("_ZWrite", 0);
            previewMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            previewMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            previewMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); previewMaterial.renderQueue = 3000;
            var renderer = preview.GetComponent<Renderer>();
            renderer.sharedMaterial = previewMaterial; renderer.shadowCastingMode = ShadowCastingMode.Off;
            preview.SetActive(false);
        }
    }
}
