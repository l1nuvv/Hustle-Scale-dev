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
        private Transform root, crate, sellerCounter;
        private readonly Transform[] lamps = new Transform[4];
        private readonly Light[] lights = new Light[4];
        private readonly Renderer[] lenses = new Renderer[4];
        private MaterialPropertyBlock lampProperties;
        private readonly List<CityTradePoint> points = new List<CityTradePoint>();
        private Material bodyMaterial, accentMaterial;
        private CityTradePoint current;
        private string message = "";
        private string displayedMessage;
        private bool executingCommand;
        private float messageUntil;
        private readonly bool[] selected = new bool[4];
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
            CityStartupDiagnostics.Stage("trade Start");
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
            BuildWorld();
            CityStartupDiagnostics.Stage("world built");
            BuildUI();
            CityStartupDiagnostics.Stage("UI built");
            save.SessionReplaced += Bind; save.BeforeSave += CaptureWorld;
            input.OnToggleInventory += Inventory;
            input.OnSelectSlot += Equip;
            input.OnStowItem += Stow;
            input.OnPlaceItem += Place;
            input.OnToggleLight += Power;
            input.OnTestItem += TestFocused;
            input.OnRepairItem += RepairFocused;
            Bind();
            CityStartupDiagnostics.Stage("trade bound to session");
        }

        private void Bind()
        {
            if (session != null) session.Changed -= Refresh;
            session = save.Session;
            Array.Clear(selected, 0, selected.Length);
            message = "";
            if (MenuOpen) { panel.SetActive(false); current = null; }
            if (session == null) return;
            foreach (var pose in save.World.PlacedLamps)
                for (var i = 0; i < lamps.Length; i++)
                    if (session.CityUnit(i)?.InstanceId == pose.InstanceId)
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
            for (var i = 0; i < lamps.Length; i++)
                if (session.CityUnit(i)?.Location == FlashlightLocation.World)
                    poses.Add(new CityPlacedLamp
                    {
                        InstanceId = session.CityUnit(i).InstanceId,
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
                save.World.HasCratePose ? save.World.CratePosition :
                save.World.CrateDock == CityCrateDock.Seller ? SellerDock : WorkshopDock;
            crate.localRotation = !session.IsCrateCarried && !session.IsCrateInVehicle && save.World.HasCratePose ?
                Quaternion.Euler(0, save.World.CrateYaw, 0) : Quaternion.identity;
            crate.GetComponent<Collider>().enabled = !session.IsCrateCarried && !session.IsCrateInVehicle;
            RefreshSolderingStation();
            for (var i = 0; i < lamps.Length; i++)
            {
                var unit = session.CityUnit(i);
                if (unit == null) { lamps[i].gameObject.SetActive(false); continue; }
                var visible = unit.Location != FlashlightLocation.Pocket &&
                    (WorkshopSession.IsOwned(unit) || unit.Location == FlashlightLocation.Seller) &&
                    !(i == 3 && (session.AtSolderingStation || solderingStation != null && solderingStation.IsActive));
                lamps[i].gameObject.SetActive(visible);
                lamps[i].GetComponent<Collider>().enabled = unit.Location == FlashlightLocation.World || unit.Location == FlashlightLocation.Bench;
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
                    unit.Location == FlashlightLocation.Bench ? new Vector3(-72, 1.37f, 34) : new Vector3(i == 3 ? 0 : (i - 1) * 0.36f, 0.415f, i == 3 ? 0.26f : 0);
            }
            hud.text = "<b>$" + session.Balance + "</b>  ·  " + WorkshopView.Clock(session.ElapsedSeconds);
            var activeA = session.Order(DealOrderId.A).State == DealOrderState.Accepted;
            var activeB = session.Order(DealOrderId.B).State == DealOrderState.Accepted;
            orderHud.text = (activeA ? OrderSummary(DealOrderId.A) : "") + (activeA && activeB ? "\n" : "") +
                (activeB ? OrderSummary(DealOrderId.B) : "");
            var solderOrder = session.Order(DealOrderId.Solder);
            if (solderOrder.State == DealOrderState.Accepted)
                orderHud.text += (orderHud.text.Length > 0 ? "\n" : "") + "Пайка · " + WorkshopView.Clock(Math.Max(0, solderOrder.DeadlineSeconds - session.ElapsedSeconds));
            orderHud.transform.parent.gameObject.SetActive(activeA || activeB || solderOrder.State == DealOrderState.Accepted);
            ResizeHud();
            if (MenuOpen && !executingCommand) RebuildMenu();
        }

        private string OrderSummary(DealOrderId id)
        {
            var order = session.Order(id);
            return FirstDealText.OrderName(id) + " · " + order.Quantity + " шт. · " +
                WorkshopView.Clock(Math.Max(0, order.DeadlineSeconds - session.ElapsedSeconds));
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

        public Transform PickupTarget(CityTradePoint point)
        {
            if (!Allowed || !Reachable(point)) return null;
            switch (point.Kind)
            {
                case CityTradeKind.Lamp:
                    return session.CityUnit(point.Index).Location == FlashlightLocation.Bench ?
                        session.CanPickUpBench ? lamps[point.Index] : null :
                        session.CanPickUpPlaced(point.Index) ? lamps[point.Index] : null;
                case CityTradeKind.Workshop:
                    if (!session.CanPickUpBench) return null;
                    for (var i = 0; i < lamps.Length; i++) if (session.CityUnit(i) == session.OnBench) return lamps[i];
                    return null;
                case CityTradeKind.Crate: return session.CanPickUpCrate ? crate : null;
                case CityTradeKind.Cargo:
                    return session.CanUnloadCrate && vehicle != null && !vehicle.IsDriving && vehicle.Speed <= 0.1f ? crate : null;
                default: return null;
            }
        }

        public void Open(CityTradePoint point)
        {
            if (!Allowed || !Reachable(point)) return;
            if (point.Kind == CityTradeKind.Cargo) { TransferCargo(); return; }
            if (point.Kind == CityTradeKind.Workshop || point.Kind == CityTradeKind.Lamp &&
                session.CityUnit(point.Index).Location == FlashlightLocation.Bench)
            { WorkshopAction(point, "bench"); return; }
            if (point.Kind == CityTradeKind.Lamp)
            { ActionFeedback("unit", session.PickUpPlaced(point.Index, out message)); Refresh(); return; }
            current = point;
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
            for (var i = 0; i < lamps.Length; i++) selected[i] = Presented(i) && session.CityUnit(i).IsTested && (i < 3 || point.Kind == CityTradeKind.Buyback);
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
            executingCommand = true;
            try
            {
                session.SetPaused(false);
                switch (command)
                {
                    case "station" when kind == CityTradeKind.Seller: success = session.PurchaseStation(StationPlaceAvailable(), out message); break;
                    case "solderlamp" when kind == CityTradeKind.Seller: success = session.PurchaseSolderingLamp(out message); break;
                    case "dose" when kind == CityTradeKind.Seller: success = session.BuySolderDose(out message); break;
                    case "solderaccept" when kind == CityTradeKind.B: success = session.AcceptOrder(DealOrderId.Solder, out message); break;
                    case "solderclose" when kind == CityTradeKind.B: success = session.CloseOrder(DealOrderId.Solder, out message); break;
                    case "solderdeliver" when kind == CityTradeKind.B:
                        success = session.DeliverSoldering(Presented(3) ? new[] { session.SolderingLamp.InstanceId } : Array.Empty<string>(), out message); break;
                    case "purchase" when kind == CityTradeKind.Seller:
                        if (session.LotPurchased && !CrateAtSeller)
                        { message = "Принеси пустой ящик к продавцу."; break; }
                        success = session.PurchaseLot(out message);
                        if (success) Array.Clear(selected, 0, selected.Length);
                        break;
                    case "battery" when kind == CityTradeKind.Battery || kind == CityTradeKind.Seller:
                        success = session.BuyBattery(out message); break;
                    case "crate" when kind == CityTradeKind.Crate:
                        success = session.ToggleCrate(out message); break;
                    case "contents" when kind == CityTradeKind.Crate:
                        success = session.TakeCrateContents(out message); break;
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
            finally
            {
                // Model events still refresh the world/HUD synchronously. Rebuild the
                // menu once below, after all pause and transaction events have settled.
                try { session.SetPaused(true); }
                finally { executingCommand = false; }
            }
            ActionFeedback(command, success);
            Refresh();
            return success;
        }

        private bool Presented(int i) => session.CityUnit(i) != null && (session.CityUnit(i).Location == FlashlightLocation.Hands ||
            session.IsCrateCarried && session.CityUnit(i).Location == FlashlightLocation.Crate);

        private bool CrateAtSeller => session.IsCrateCarried || !session.IsCrateInVehicle &&
            sellerCounter != null && Vector3.ProjectOnPlane(crate.position - sellerCounter.position, Vector3.up).magnitude <= 2.5f;

        public string WorkshopPrompt => session == null ? "Верстак" : session.OnBench == null ?
            "E — поставить фонарь" : "E — взять · T — проверить · R — заменить батарею";

        public bool WorkshopAction(CityTradePoint point, string command)
        {
            if (!Allowed || !Reachable(point) || !(point.Kind == CityTradeKind.Workshop ||
                point.Kind == CityTradeKind.Lamp && session.CityUnit(point.Index).Location == FlashlightLocation.Bench)) return false;
            bool success;
            switch (command)
            {
                case "bench": success = session.ToggleBench(out message); break;
                case "test": success = session.Test(out message); break;
                case "repair": success = session.Repair(out message); break;
                default: return false;
            }
            ActionFeedback(command, success); Refresh(); return success;
        }

        private void FocusedWorkshop(string command)
        {
            if (Allowed && Physics.Raycast(eye.transform.position, eye.transform.forward, out var hit, 2.5f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                var point = hit.collider.GetComponentInParent<CityTradePoint>();
                if (point != null) WorkshopAction(point, command);
            }
        }
        private void TestFocused() => FocusedWorkshop("test");
        private void RepairFocused() => FocusedWorkshop("repair");

        public string CargoPrompt => session == null ? "Багажник" :
            session.IsCrateInVehicle ? "E — взять ящик из багажника" :
            "E — погрузить ящик в багажник · сначала возьми ящик в руки";

        private void TransferCargo()
        {
            if (vehicle == null || vehicle.IsDriving || vehicle.Speed > 0.1f)
            { message = "Останови машину перед погрузкой или выгрузкой."; return; }
            if (!session.IsCrateInVehicle) ActionFeedback("cargo", session.LoadVehicleCrate(out message));
            else ActionFeedback("cargo", session.UnloadVehicleCrate(out message));
            Refresh();
        }
        private List<string> Selection()
        {
            var ids = new List<string>();
            for (var i = 0; i < lamps.Length; i++) if (selected[i]) ids.Add(session.CityUnit(i).InstanceId);
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
            for (var i = 0; i < lamps.Length; i++) if (session.CityUnit(i) != null && session.CityUnit(i) == session.InHands) index = i;
            if (index < 0 && Physics.Raycast(eye.transform.position, eye.transform.forward, out var hit, 2.5f, Physics.DefaultRaycastLayers))
                for (var i = 0; i < lamps.Length; i++) if (hit.collider.transform == lamps[i]) index = i;
            ActionFeedback("power", session.TogglePower(index, out message)); Refresh();
        }
        private void Place()
        {
            DropHeld();
        }

        public bool DropHeld()
        {
            if (!Allowed || session.InHands == null && !session.IsCrateCarried) return false;
            var forward = Vector3.ProjectOnPlane(eye.transform.forward, Vector3.up).normalized;
            if (!Physics.Raycast(eye.transform.position, eye.transform.forward, out var hit, 2.5f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) || hit.normal.y < 0.85f)
            {
                var probe = eye.transform.position + forward * 1.4f;
                if (Physics.Linecast(eye.transform.position, probe, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ||
                    !Physics.Raycast(probe, Vector3.down, out hit, 4, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) || hit.normal.y < 0.85f)
                { message = "Перед тобой нет свободного места."; return false; }
            }
            var isCrate = session.IsCrateCarried;
            var half = isCrate ? new Vector3(0.625f, 0.175f, 0.325f) : new Vector3(0.16f, 0.24f, 0.14f);
            var position = hit.point + Vector3.up * (half.y + 0.015f);
            var rotation = Quaternion.Euler(0, eye.transform.eulerAngles.y, 0);
            if (Physics.Linecast(eye.transform.position, position + Vector3.up * half.y,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ||
                // Aiming ignores the walking capsule; placement occupancy must include it.
                Physics.CheckBox(position, half, rotation, ~0, QueryTriggerInteraction.Ignore))
            { message = "Недостаточно свободного места."; return false; }
            if (isCrate)
            {
                var oldPose = save.World.HasCratePose; var oldPosition = save.World.CratePosition; var oldYaw = save.World.CrateYaw;
                save.World.HasCratePose = true; save.World.CratePosition = position; save.World.CrateYaw = rotation.eulerAngles.y;
                var success = session.ToggleCrate(out message);
                if (!success)
                { save.World.HasCratePose = oldPose; save.World.CratePosition = oldPosition; save.World.CrateYaw = oldYaw; }
                ActionFeedback("place", success); Refresh(); return success;
            }
            for (var i = 0; i < lamps.Length; i++)
                if (session.CityUnit(i) == session.InHands)
                {
                    lamps[i].SetParent(root, true);
                    lamps[i].SetPositionAndRotation(position, rotation);
                    var success = session.PlaceHeld(out message);
                    ActionFeedback("place", success); Refresh(); return success;
                }
            return false;
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
            crate = Box("Ящик партии", root, SellerDock, new Vector3(1.25f, 0.35f, 0.65f), bodyMaterial);
            AddPoint(crate, CityTradeKind.Crate);
            if (vehicle != null && vehicle.CargoGate != null && vehicle.CargoFloor != null)
            {
                AddPoint(vehicle.CargoGate, CityTradeKind.Cargo);
            }
            for (var i = 0; i < lamps.Length; i++)
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
            BuildSolderingStation();
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
            if (kind == CityTradeKind.Seller) sellerCounter = target;
            AddPoint(target, kind);
            if (kind == CityTradeKind.Seller || kind == CityTradeKind.A || kind == CityTradeKind.B || kind == CityTradeKind.Workshop)
                target.Find("Visual").gameObject.SetActive(false);
            // The visible storefront card is also an E target; keep the lower counter target reachable.
            if (kind == CityTradeKind.A || kind == CityTradeKind.B)
            {
                var card = Board("ЗАКАЗ " + title, position + new Vector3(0, 0.8f, -0.65f), 0, new Vector2(3.5f, 0.55f));
                card.GetComponent<Collider>().enabled = true; AddPoint(card, kind);
            }
            else if (kind == CityTradeKind.Seller)
            {
                var card = Board("E — МАГАЗИН", position + new Vector3(0, 0.05f, 0.75f), 180, new Vector2(1.8f, 0.3f));
                card.GetComponent<Collider>().enabled = true; AddPoint(card, kind);
            }
            else Board(title, position + new Vector3(0, -0.35f, -0.2f), 0, new Vector2(2, 0.28f));
        }

        private void LateUpdate()
        {
            UpdateSafeArea();
            UpdatePauseUI();
            tradeBackdrop.SetActive(MenuOpen);
            // Failed startup loads still need the recovery/new-game controls.
            if (session == null)
            {
                hudPanel.gameObject.SetActive(false);
                bottomContext.gameObject.SetActive(false); prompt.text = "";
                return;
            }
            KeepMenuFocusVisible();
            canvasObject.SetActive(!save.IsDriving);
            hudPanel.gameObject.SetActive(!walk.IsPaused && !MenuOpen);
            prompt.text = !walk.IsPaused && !save.IsDriving ? raycast.FocusLabel : "";
            if (displayedMessage != message)
            { displayedMessage = message; messageUntil = Time.unscaledTime + 4; }
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
                input.OnTestItem -= TestFocused; input.OnRepairItem -= RepairFocused;
            }
            if (root != null) Destroy(root.gameObject);
            if (canvasObject != null) Destroy(canvasObject);
            if (ownedEventSystem != null) Destroy(ownedEventSystem);
            if (bodyMaterial != null) Destroy(bodyMaterial);
            if (accentMaterial != null) Destroy(accentMaterial);
            if (signMaterial != null) Destroy(signMaterial);
            if (workshopMaterial != null) Destroy(workshopMaterial);
            if (plasterMaterial != null) Destroy(plasterMaterial);
            if (yardMaterial != null) Destroy(yardMaterial);
            DisposeFeedback();
            DisposeUiArtwork();
        }
    }
}
