using System;
using System.Collections.Generic;
using Inventory;
using Player;
using Player.Movement;
using UnityEngine;

namespace Interactables
{
    // City-only composition. WorkshopSession remains the sole item/money/order authority.
    public sealed partial class CityTradeView : MonoBehaviour
    {
        private CitySaveController save;
        private CityGreyboxWalk walk;
        private PlayerController player;
        private Player.PlayerInput input;
        private PlayerRaycast raycast;
        private Camera eye;
        private WorkshopSession session;
        private CityUtilityVehicle vehicle;
        private Transform root, crate;
        private readonly Transform[] lamps = new Transform[3];
        private readonly Light[] lights = new Light[3];
        private readonly Renderer[] lenses = new Renderer[3];
        private MaterialPropertyBlock lampProperties;
        private readonly List<CityTradePoint> points = new List<CityTradePoint>();
        private Material bodyMaterial, accentMaterial;
        private CityTradePoint current;
        private string message = "Tab — карта района. Продавец фонарей — под жёлтым навесом на рынке.";
        private string displayedMessage;
        private float messageUntil;
        private readonly bool[] selected = new bool[3];
        private string hudText;
        public bool MenuOpen => panel != null && panel.activeSelf;
        public WorkshopSession Session => session;
        public IReadOnlyList<CityTradePoint> Points => points;
        public Transform Crate => crate;
        public Transform Lamp(int index) => lamps[index];
        public static readonly Vector3 SellerDock = new Vector3(83, 0.4f, -35);
        public static readonly Vector3 WorkshopDock = new Vector3(-69, 0.4f, 32);
        private bool Allowed => session != null && !session.IsPaused && !save.IsDriving && player.CanInteract;

        private void Start()
        {
            lampProperties = new MaterialPropertyBlock();
            save = GetComponent<CitySaveController>(); walk = GetComponent<CityGreyboxWalk>();
            player = GetComponent<PlayerController>(); input = GetComponent<Player.PlayerInput>();
            eye = GetComponentInChildren<Camera>();
            raycast = GetComponent<PlayerRaycast>();
            if (raycast == null) raycast = gameObject.AddComponent<PlayerRaycast>();
            // Low targets must not be hidden by the capsule containing the camera.
            gameObject.layer = 2; // Unity's built-in Ignore Raycast layer; collisions remain enabled.
            raycast.Configure(eye, Physics.DefaultRaycastLayers);
            vehicle = FindAnyObjectByType<CityUtilityVehicle>();
            BuildWorld(); BuildUI();
            save.SessionReplaced += Bind; save.BeforeSave += CaptureWorld;
            input.OnToggleInventory += Inventory;
            input.OnSelectSlot += Equip;
            input.OnStowItem += Stow;
            input.OnPlaceItem += Place;
            input.OnToggleLight += Power;
            Bind();
        }

        private void Bind()
        {
            if (session != null) session.Changed -= Refresh;
            session = save.Session;
            message = "Tab — карта района. Продавец фонарей — под жёлтым навесом на рынке.";
            if (MenuOpen) { panel.SetActive(false); current = null; }
            if (session == null) return;
            foreach (var pose in save.World.PlacedLamps)
                for (var i = 0; i < 3; i++)
                    if (session.Units[i].InstanceId == pose.InstanceId)
                    {
                        lamps[i].SetParent(root, true);
                        lamps[i].SetPositionAndRotation(pose.Position, Quaternion.Euler(0, pose.Yaw, 0));
                    }
            session.Changed += Refresh;
            Refresh();
        }

        private void CaptureWorld()
        {
            if (session == null) return;
            var poses = new List<CityPlacedLamp>();
            for (var i = 0; i < 3; i++)
                if (session.Units[i].Location == FlashlightLocation.World)
                    poses.Add(new CityPlacedLamp
                    {
                        InstanceId = session.Units[i].InstanceId,
                        Position = lamps[i].position, Yaw = lamps[i].eulerAngles.y
                    });
            save.World.PlacedLamps = poses.ToArray();
        }

        private void Refresh()
        {
            if (session == null) return;
            crate.SetParent(session.IsCrateInVehicle ? vehicle.transform : session.IsCrateCarried ? eye.transform : root, false);
            crate.localPosition = session.IsCrateCarried ? new Vector3(0, -0.85f, 1.1f) :
                session.IsCrateInVehicle ? vehicle.CargoPosition :
                save.World.CrateDock == CityCrateDock.Seller ? SellerDock : WorkshopDock;
            crate.localRotation = Quaternion.identity;
            crate.GetComponent<Collider>().enabled = !session.IsCrateCarried && !session.IsCrateInVehicle;
            for (var i = 0; i < 3; i++)
            {
                var unit = session.Units[i];
                var visible = unit.Location != FlashlightLocation.Pocket &&
                    (WorkshopSession.IsOwned(unit) || unit.Location == FlashlightLocation.Seller);
                lamps[i].gameObject.SetActive(visible);
                lamps[i].GetComponent<Collider>().enabled = unit.Location == FlashlightLocation.World;
                lights[i].enabled = visible && unit.IsPowered &&
                    unit.Location != FlashlightLocation.Crate && unit.Location != FlashlightLocation.Seller;
                lampProperties.SetColor("_EmissionColor", lights[i].enabled ? new Color(1.6f, 1.3f, 0.8f) : Color.black);
                lenses[i].SetPropertyBlock(lampProperties);
                if (unit.Location == FlashlightLocation.World) continue;
                var parent = unit.Location == FlashlightLocation.Hands ? eye.transform :
                    unit.Location == FlashlightLocation.Bench ? root : crate;
                lamps[i].SetParent(parent, false);
                lamps[i].localRotation = Quaternion.identity;
                lamps[i].localPosition = unit.Location == FlashlightLocation.Hands ? new Vector3(0.35f, -0.22f, 0.82f) :
                    unit.Location == FlashlightLocation.Bench ? new Vector3(-72, 1.37f, 34) : new Vector3((i - 1) * 0.36f, 0.415f, 0);
            }
            hudText = "Деньги: $" + session.Balance + " · Время: " + WorkshopView.Clock(session.ElapsedSeconds) +
                "\nБатареи: " + session.SpareBatteries + " · В руках: " +
                (session.IsCrateCarried ? "ящик" : session.InHands != null ? "фонарь" : "ничего") +
                "\n" + OrderSummary(DealOrderId.A) + "   |   " + OrderSummary(DealOrderId.B) + "\nTab — карта · Esc — пауза";
            hud.text = hudText;
            ResizeHud();
            if (MenuOpen) RebuildMenu();
        }

        private string OrderSummary(DealOrderId id)
        {
            var order = session.Order(id);
            return FirstDealText.OrderName(id) + ": " + State(order.State) + (order.State == DealOrderState.Accepted ?
                " · осталось " + WorkshopView.Clock(Math.Max(0, order.DeadlineSeconds - session.ElapsedSeconds)) : "");
        }

        public bool Reachable(CityTradePoint point)
        {
            if (point == null || point.View != this || !point.gameObject.activeInHierarchy) return false;
            var collider = point.GetComponent<Collider>();
            if (collider == null || !collider.enabled) return false;
            var target = collider.bounds.center;
            return Vector3.Distance(eye.transform.position, collider.ClosestPoint(eye.transform.position)) <= 2.5f &&
                Physics.Linecast(eye.transform.position, target, out var hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
                hit.collider == collider;
        }

        public void Open(CityTradePoint point)
        {
            if (!Allowed || !Reachable(point)) return;
            if (point.Kind == CityTradeKind.Cargo) { TransferCargo(); return; }
            if (point.Kind == CityTradeKind.Lamp)
            { ActionFeedback("unit", session.PickUpPlaced(point.Index, out message)); Refresh(); return; }
            current = point;
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
            for (var i = 0; i < 3; i++) selected[i] = Presented(i) && session.Units[i].IsTested;
            walk.SetPaused(true);
            panel.SetActive(true);
            RebuildMenu();
        }

        private void Inventory()
        {
            if (MenuOpen) { CloseMenu(); return; }
            if (!Allowed) return;
            current = null;
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
            walk.SetPaused(true); panel.SetActive(true); RebuildMenu();
        }

        public void CloseMenu()
        {
            if (!MenuOpen) return;
            panel.SetActive(false); current = null;
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
            walk.SetPaused(false);
        }

        // Menu commands resume only for their atomic model operation, then remain paused.
        public bool Execute(string command, int index = -1)
        {
            if (!MenuOpen || session == null || save.IsDriving ||
                (current != null && !Reachable(current))) return false;
            var kind = current != null ? current.Kind : (CityTradeKind?)null;
            bool success = false;
            session.SetPaused(false);
            try
            {
                switch (command)
                {
                    case "purchase" when kind == CityTradeKind.Seller: success = session.PurchaseLot(out message); break;
                    case "battery" when kind == CityTradeKind.Battery: success = session.BuyBattery(out message); break;
                    case "bench" when kind == CityTradeKind.Workshop: success = session.ToggleBench(out message); break;
                    case "test" when kind == CityTradeKind.Workshop: success = session.Test(out message); break;
                    case "repair" when kind == CityTradeKind.Workshop: success = session.Repair(out message); break;
                    case "crate" when kind == CityTradeKind.Crate || kind == CityTradeKind.SellerDock || kind == CityTradeKind.WorkshopDock:
                        var dock = kind == CityTradeKind.SellerDock ? CityCrateDock.Seller :
                            kind == CityTradeKind.WorkshopDock ? CityCrateDock.Workshop : save.World.CrateDock;
                        var position = dock == CityCrateDock.Seller ? SellerDock : WorkshopDock;
                        if (Vector3.Distance(transform.position, position) > 2.5f)
                        { message = "Подойди к обозначенному доку ящика (2,5 м)."; break; }
                        if (!session.IsCrateCarried && save.World.CrateDock != dock)
                        { message = "Ящик находится у другого дока."; break; }
                        var previousDock = save.World.CrateDock;
                        save.World.CrateDock = dock;
                        success = session.ToggleCrate(out message);
                        if (!success) save.World.CrateDock = previousDock;
                        break;
                    case "unit" when kind == CityTradeKind.Crate:
                        success = session.ToggleUnit(index, out message); break;
                    case "equip" when kind == null: success = session.EquipSlot(index, out message); break;
                    case "accept" when kind == CityTradeKind.A || kind == CityTradeKind.B:
                        success = session.AcceptOrder(kind == CityTradeKind.A ? DealOrderId.A : DealOrderId.B, out message); break;
                    case "close" when kind == CityTradeKind.A || kind == CityTradeKind.B:
                        success = session.CloseOrder(kind == CityTradeKind.A ? DealOrderId.A : DealOrderId.B, out message); break;
                    case "deliver" when kind == CityTradeKind.A || kind == CityTradeKind.B:
                        success = session.DeliverOrder(kind == CityTradeKind.A ? DealOrderId.A : DealOrderId.B, Selection(), out message); break;
                    case "sell" when kind == CityTradeKind.Buyback: success = session.SellBack(Selection(), out message); break;
                }
            }
            finally { session.SetPaused(true); }
            ActionFeedback(command, success);
            Refresh();
            return success;
        }

        private bool Presented(int i) => session.Units[i].Location == FlashlightLocation.Hands ||
            session.IsCrateCarried && session.Units[i].Location == FlashlightLocation.Crate;

        public string CargoPrompt => session == null ? "Багажник" :
            session.IsCrateInVehicle ? "E — выгрузить ящик на док продавца / мастерской (подъедь задним бортом)" :
            "E — погрузить ящик в багажник · сначала возьми ящик в руки";

        private void TransferCargo()
        {
            if (vehicle == null || vehicle.IsDriving || vehicle.Speed > 0.1f)
            { message = "Останови машину перед погрузкой или выгрузкой."; return; }
            if (!session.IsCrateInVehicle) ActionFeedback("cargo", session.LoadVehicleCrate(out message));
            else
            {
                var rear = vehicle.CargoGate.position;
                CityCrateDock dock;
                if (Vector3.Distance(rear, SellerDock) <= 2.5f) dock = CityCrateDock.Seller;
                else if (Vector3.Distance(rear, WorkshopDock) <= 2.5f) dock = CityCrateDock.Workshop;
                else { message = "Подъедь задним бортом к доку продавца или мастерской (2,5 м)."; return; }
                // Never place the crate inside the parked vehicle, player or another obstacle.
                var destination = dock == CityCrateDock.Seller ? SellerDock : WorkshopDock;
                if (Physics.CheckBox(destination, new Vector3(0.625f, 0.175f, 0.325f),
                        Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                { message = "Док занят. Освободи место для ящика."; return; }
                var previous = save.World.CrateDock;
                save.World.CrateDock = dock;
                var success = session.UnloadVehicleCrate(out message);
                if (!success) save.World.CrateDock = previous;
                ActionFeedback("cargo", success);
            }
            Refresh();
        }
        private List<string> Selection()
        {
            var ids = new List<string>();
            for (var i = 0; i < 3; i++) if (selected[i]) ids.Add(session.Units[i].InstanceId);
            return ids;
        }

        private void Equip(int index)
        { if (Allowed) { ActionFeedback("unit", session.EquipSlot(index, out message)); Refresh(); } }
        private void Stow()
        { if (Allowed) { ActionFeedback("unit", session.StowHeld(out message)); Refresh(); } }
        private void Power()
        {
            if (!Allowed) return;
            var index = -1;
            for (var i = 0; i < 3; i++) if (session.Units[i] == session.InHands) index = i;
            if (index < 0 && Physics.Raycast(eye.transform.position, eye.transform.forward, out var hit, 2.5f, Physics.DefaultRaycastLayers))
                for (var i = 0; i < 3; i++) if (hit.collider.transform == lamps[i]) index = i;
            ActionFeedback("power", session.TogglePower(index, out message)); Refresh();
        }
        private void Place()
        {
            if (!Allowed || session.InHands == null) return;
            if (!Physics.Raycast(eye.transform.position, eye.transform.forward, out var hit, 2.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ||
                hit.normal.y < 0.85f)
            { message = "Наведи прицел на свободную горизонтальную поверхность в 2,5 м."; return; }
            var position = hit.point + Vector3.up * 0.25f;
            var rotation = Quaternion.Euler(0, eye.transform.eulerAngles.y, 0);
            if (Physics.CheckBox(position, new Vector3(0.16f, 0.24f, 0.14f), rotation, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            { message = "Недостаточно свободного места."; return; }
            for (var i = 0; i < 3; i++)
                if (session.Units[i] == session.InHands)
                {
                    lamps[i].SetParent(root, true);
                    lamps[i].SetPositionAndRotation(position, rotation);
                    ActionFeedback("place", session.PlaceHeld(out message)); break;
                }
            Refresh();
        }

        private void BuildWorld()
        {
            if (walk.WorklightPrefab == null)
                throw new InvalidOperationException("CityGreybox requires the accepted Worklight prefab on CityGreyboxWalk.");
            root = new GameObject("City trade runtime").transform;
            bodyMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            bodyMaterial.color = new Color(0.12f, 0.2f, 0.22f);
            accentMaterial = new Material(bodyMaterial) { color = new Color(0.9f, 0.69f, 0.25f) };
            Point("ПРОДАВЕЦ ФОНАРЕЙ", CityTradeKind.Seller, new Vector3(88, 1.3f, -37));
            Point("БАТАРЕИ · $50", CityTradeKind.Battery, new Vector3(65, 1.1f, 26));
            Point("СКУПКА · $60 / $35", CityTradeKind.Buyback, new Vector3(-65, 1.1f, -47));
            Point("A · 2 исправных · $260", CityTradeKind.A, new Vector3(-29, 1.3f, 10.5f));
            Point("Б · 1 исправный · $125", CityTradeKind.B, new Vector3(-7, 1.3f, 10.5f));
            Point("МАСТЕРСКАЯ · тест / ремонт", CityTradeKind.Workshop, new Vector3(-72, 1.3f, 33.5f));
            Point("МЕСТО ДЛЯ ЯЩИКА", CityTradeKind.SellerDock, SellerDock + Vector3.right * 1.2f);
            Point("МЕСТО ДЛЯ ЯЩИКА", CityTradeKind.WorkshopDock, WorkshopDock + Vector3.right * 1.2f);
            crate = Box("Ящик партии", root, SellerDock, new Vector3(1.25f, 0.35f, 0.65f), bodyMaterial);
            AddPoint(crate, CityTradeKind.Crate);
            if (vehicle != null && vehicle.CargoGate != null && vehicle.CargoFloor != null)
            {
                AddPoint(vehicle.CargoGate, CityTradeKind.Cargo);
            }
            for (var i = 0; i < 3; i++)
            {
                lamps[i] = new GameObject("Фонарь " + (i + 1), typeof(BoxCollider)).transform;
                lamps[i].SetParent(root, false);
                lamps[i].GetComponent<BoxCollider>().size = new Vector3(0.3f, 0.48f, 0.26f);
                AddPoint(lamps[i], CityTradeKind.Lamp, i);
                var visual = Instantiate(walk.WorklightPrefab, lamps[i], false).transform;
                // Keep the city's centre pivot, collider and saved poses. The sample faces -Z.
                visual.localPosition = new Vector3(0, -0.034f, 0);
                visual.localRotation = Quaternion.Euler(0, 180, 0);
                lenses[i] = visual.Find("Inset diffuser").GetComponent<Renderer>();
                lights[i] = visual.GetComponentInChildren<Light>(true);
                lights[i].transform.localPosition = new Vector3(0, 0.14f, -0.18f);
                lights[i].type = LightType.Spot; lights[i].range = 12; lights[i].spotAngle = 55; lights[i].intensity = 3;
            }
            BuildWayfinding();
            BuildSliceCharacters();
        }

        private Transform Box(string name, Transform parent, Vector3 position, Vector3 size, Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
            box.name = name; box.SetParent(parent, false); box.localPosition = position;
            // Scale the mesh child instead so item parents/attachments retain metre units.
            var mesh = new GameObject("Visual", typeof(MeshFilter), typeof(MeshRenderer)).transform;
            mesh.SetParent(box, false); mesh.localScale = size;
            mesh.GetComponent<MeshFilter>().sharedMesh = box.GetComponent<MeshFilter>().sharedMesh;
            mesh.GetComponent<Renderer>().sharedMaterial = material;
            Destroy(box.GetComponent<MeshRenderer>()); Destroy(box.GetComponent<MeshFilter>());
            box.GetComponent<BoxCollider>().size = size;
            return box;
        }
        private CityTradePoint AddPoint(Transform target, CityTradeKind kind, int index = -1)
        {
            var point = target.gameObject.AddComponent<CityTradePoint>(); point.Bind(this, kind, index);
            points.Add(point); return point;
        }
        private void Point(string title, CityTradeKind kind, Vector3 position)
        {
            var target = Box(title, root, position, new Vector3(0.7f, 0.35f, 0.3f), bodyMaterial);
            AddPoint(target, kind);
            if (kind == CityTradeKind.Seller || kind == CityTradeKind.A || kind == CityTradeKind.Workshop)
                target.Find("Visual").gameObject.SetActive(false);
            // A label belongs on the counter, with depth testing, rather than floating through walls.
            var dock = kind == CityTradeKind.SellerDock || kind == CityTradeKind.WorkshopDock;
            Board(title, position + new Vector3(0, dock ? 0.1f : -0.35f, -0.2f), 0, new Vector2(2, 0.28f));
        }

        private void LateUpdate()
        {
            if (session == null) return;
            UpdateSafeArea();
            KeepMenuFocusVisible();
            canvasObject.SetActive(!save.IsDriving);
            hud.transform.parent.gameObject.SetActive(!walk.IsPaused);
            prompt.text = !walk.IsPaused && !save.IsDriving ? raycast.FocusLabel : "";
            if (displayedMessage != message)
            { displayedMessage = message; messageUntil = Time.unscaledTime + 8; }
            feedbackLabel.text = !walk.IsPaused && Time.unscaledTime < messageUntil ? CityMessage(message) : "";
            bottomContext.gameObject.SetActive(!walk.IsPaused && (prompt.text.Length > 0 || feedbackLabel.text.Length > 0));
        }
        private void OnDestroy()
        {
            if (session != null) session.Changed -= Refresh;
            if (save != null) { save.SessionReplaced -= Bind; save.BeforeSave -= CaptureWorld; }
            if (input != null)
            {
                input.OnToggleInventory -= Inventory; input.OnSelectSlot -= Equip;
                input.OnStowItem -= Stow; input.OnPlaceItem -= Place; input.OnToggleLight -= Power;
            }
            if (root != null) Destroy(root.gameObject);
            if (canvasObject != null) Destroy(canvasObject);
            if (ownedEventSystem != null) Destroy(ownedEventSystem);
            if (bodyMaterial != null) Destroy(bodyMaterial);
            if (accentMaterial != null) Destroy(accentMaterial);
            if (signMaterial != null) Destroy(signMaterial);
            if (workshopMaterial != null) Destroy(workshopMaterial);
            if (plasterMaterial != null) Destroy(plasterMaterial);
            DisposeFeedback();
        }
    }
}
