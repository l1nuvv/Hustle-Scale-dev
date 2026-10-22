using System.Collections.Generic;
using Player;
using Player.Movement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Interactables
{
    // Only placed in DevelopmentSandbox. All temporary geometry/audio is owned here.
    [DefaultExecutionOrder(200)]
    public sealed partial class SolderingTrial : MonoBehaviour, IWorldInteraction
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private Camera view;
        [SerializeField] private FirstDealView deal;
        [SerializeField] private DevelopmentSandbox sandbox;
        [SerializeField] private GameObject ironVisualPrefab;
        [SerializeField] private GameObject solderVisualPrefab;
        [SerializeField] private GameObject flashlightVisualPrefab;
        [SerializeField] private Texture2D smokeTexture;
        [SerializeField] private bool fullOperation;
        [SerializeField] private GameObject solderingMatPrefab;
        private readonly SolderingTrialState state = new SolderingTrialState();
        private readonly List<Object> resources = new List<Object>();
        private readonly List<Behaviour> suspended = new List<Behaviour>();
        private InputActionMap actions;
        private InputAction move, lower, take, reset, leave, pauseWork;
        private Transform iron, solder, tip, wireEnd, pointer, bead, placedSolder;
        private Material hotMetal;
        private ParticleSystem smoke;
        private AudioSource hiss;
        private TextMesh titleLabel;
        private Vector2 aim;
        private Vector3 savedPosition;
        private float savedYaw, savedPitch, depth;
        private int enteredFrame;
        private bool active, focused = true, exitRequested;
        private GUIStyle hudStyle;
        private string instruction;
        private PickupOutline outline;
        private BoxCollider ironHit, solderHit;
        private Vector2 cursorViewport;
        private SolderingTrialState.Tool hoveredTool;
        private Transform movingTool;
        private Vector3 motionStart;
        private Quaternion motionRotation;
        private float motionTime;
        private bool returningTool;
        private Transform workMat, repairAnchor, ironAnchor, solderAnchor, coverAnchor, supplyAnchor, dockedSolderFeed;
        private Vector3 LocalAnchor(Transform anchor) => transform.InverseTransformPoint(anchor.position);
        private Vector3 Joint => repairAnchor != null ? LocalAnchor(repairAnchor) : new Vector3(0, 1.075f, 0);
        private Vector3 IronDock => ironAnchor != null ? LocalAnchor(ironAnchor) : new Vector3(0.56f, 1.115f, 0);
        private Vector3 SolderDock => solderAnchor != null ? LocalAnchor(solderAnchor) : new Vector3(-0.56f, 1.115f, 0);
        private Quaternion DockRotation(Transform anchor) => anchor != null ? Quaternion.Inverse(transform.rotation) * anchor.rotation : Quaternion.identity;
        public Transform WorkMat => workMat;
        public Vector3 ToolHomePosition(SolderingTrialState.Tool tool) => transform.TransformPoint(tool == SolderingTrialState.Tool.Iron ? IronDock : SolderDock);
        public string Prompt => fullOperation ? "E — M3: полный ремонт фонарика" : "E — M2: проба пайки (отдельный эксперимент)";
        public SolderingTrialState State => state;
        public bool Active => active;
        public bool HasContact { get; private set; }
        public bool SmokePlaying => smoke.isEmitting;
        public bool SoundPlaying => hiss.isPlaying;
        public Vector3 TipPosition => state.HeldTool == SolderingTrialState.Tool.Solder ? wireEnd.position : tip.position;
        public string Instruction => instruction;
        public bool UsesAuthoredVisuals => ironVisualPrefab != null && solderVisualPrefab != null && flashlightVisualPrefab != null;
        public bool IsToolAnimating => movingTool != null;
        public SolderingTrialState.Tool HoveredTool => hoveredTool;
        public Vector2 CursorScreenPosition => view.ViewportToScreenPoint(cursorViewport);
        public Vector2 JointScreenPosition => view.WorldToScreenPoint(transform.TransformPoint(Joint));
        public Vector2 ToolScreenPosition(SolderingTrialState.Tool tool) =>
            view.WorldToScreenPoint((tool == SolderingTrialState.Tool.Iron ? ironHit : solderHit).bounds.center);

        private void Awake()
        {
            actions = new InputActionMap("M2 workbench");
            move = actions.AddAction("Guide tool", InputActionType.Value, "<Mouse>/delta");
            lower = actions.AddAction("Lower tool", InputActionType.Button, "<Mouse>/leftButton");
            take = actions.AddAction("Take or return", InputActionType.Button, "<Keyboard>/e");
            reset = actions.AddAction("Reset trial", InputActionType.Button, "<Keyboard>/r");
            leave = actions.AddAction("Leave bench", InputActionType.Button, "<Keyboard>/escape");
            if (fullOperation) pauseWork = actions.AddAction("Pause operation", InputActionType.Button, "<Keyboard>/p");
            BuildWorkplace();
            outline = gameObject.AddComponent<PickupOutline>();
            ironHit = ToolCollider(iron);
            solderHit = ToolCollider(solder);
            if (fullOperation) BuildFullOperation();
            RefreshFeedback();
        }

        public void Interact()
        {
            if (active || !enabled || player == null || !player.CanInteract) return;
            active = true;
            titleLabel.gameObject.SetActive(false);
            enteredFrame = Time.frameCount;
            savedPosition = player.transform.position;
            savedYaw = player.transform.eulerAngles.y;
            savedPitch = player.CameraPitch;
            Suspend(player.GetComponent<Player.PlayerInput>());
            Suspend(player.GetComponent<PlayerRaycast>());
            Suspend(deal);
            Suspend(sandbox);
            foreach (var canvas in FindObjectsByType<Canvas>()) Suspend(canvas);
            player.SetMovementEnabled(false);
            player.SetCameraControlEnabled(false);
            player.SetCursorLocked(true);
            aim = Vector2.zero;
            depth = 0;
            SetWorkCamera();
            cursorViewport = view.WorldToViewportPoint(transform.TransformPoint(Joint));
            actions.Enable();
        }

        private void Suspend(Behaviour behaviour)
        {
            if (behaviour == null || !behaviour.enabled) return;
            suspended.Add(behaviour);
            behaviour.enabled = false;
        }

        public void Exit()
        {
            if (!active) return;
            active = false;
            titleLabel.gameObject.SetActive(true);
            actions.Disable();
            CancelToolMotion();
            if (fullOperation) CancelRepairMotion();
            hoveredTool = SolderingTrialState.Tool.None;
            outline.Focus(null, view);
            state.SelectTool(SolderingTrialState.Tool.None);
            AdvanceTool(0, false, false);
            if (player != null)
            {
                player.RestorePose(savedPosition, savedYaw, savedPitch);
                player.SetMovementEnabled(true);
                player.SetCameraControlEnabled(true);
                player.SetCursorLocked(focused);
            }
            foreach (var behaviour in suspended)
                if (behaviour != null) behaviour.enabled = true;
            suspended.Clear();
        }

        private void Update()
        {
            if (!active || Time.frameCount == enteredFrame) return;
            if (leave.WasPressedThisFrame()) { exitRequested = true; return; }
            if (focused && Application.isFocused && reset.WasPressedThisFrame()) ResetTrial();
            if (fullOperation && focused && Application.isFocused && pauseWork.WasPressedThisFrame()) workPaused = !workPaused;
            if (focused && Application.isFocused && Time.timeScale > 0 && !(fullOperation && workPaused))
            {
                var delta = move.ReadValue<Vector2>();
                cursorViewport += new Vector2(delta.x / Screen.width, delta.y / Screen.height);
                cursorViewport.x = Mathf.Clamp(cursorViewport.x, 0.05f, 0.95f);
                cursorViewport.y = Mathf.Clamp(cursorViewport.y, 0.08f, 0.88f);
                var ray = view.ViewportPointToRay(cursorViewport);
                var plane = new Plane(transform.up, transform.TransformPoint(Joint));
                if (plane.Raycast(ray, out var distance))
                {
                    var point = transform.InverseTransformPoint(ray.GetPoint(distance));
                    aim = new Vector2(point.x, point.z);
                }
                aim.x = Mathf.Clamp(aim.x, -0.79f, 0.79f);
                aim.y = Mathf.Clamp(aim.y, -0.30f, 0.30f);
                hoveredTool = ToolUnderCursor(ray);
                if (fullOperation) UpdateRepairInput(ray, Time.deltaTime);
                if (take.WasPressedThisFrame() && !IsToolAnimating) TakeOrReturnTool();
                TickToolMotion(Time.deltaTime);
                AdvanceTool(Time.deltaTime, lower.IsPressed(), true);
            }
            else
            {
                hoveredTool = SolderingTrialState.Tool.None;
                if (fullOperation) { repairHover = RepairTarget.None; InterruptRepairAction(); }
                AdvanceTool(0, false, false);
            }
        }

        private void LateUpdate()
        {
            // Restore after the existing Escape handlers, avoiding a second menu transition.
            if (exitRequested) { exitRequested = false; Exit(); return; }
            if (!active) return;
            SetWorkCamera();
        }

        private void SetWorkCamera()
        {
            view.transform.SetPositionAndRotation(transform.TransformPoint(new Vector3(0, 1.78f, -0.9f)),
                transform.rotation * Quaternion.Euler(40, 0, 0));
        }

        private BoxCollider ToolCollider(Transform tool)
        {
            var bounds = new Bounds();
            var first = true;
            foreach (var filter in tool.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = filter.sharedMesh.bounds;
                for (var corner = 0; corner < 8; corner++)
                {
                    var point = mesh.center + Vector3.Scale(mesh.extents, new Vector3(
                        (corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                    point = tool.InverseTransformPoint(filter.transform.TransformPoint(point));
                    if (first) { bounds = new Bounds(point, Vector3.zero); first = false; }
                    else bounds.Encapsulate(point);
                }
            }
            var collider = tool.gameObject.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.center = bounds.center;
            collider.size = bounds.size;
            return collider;
        }

        private SolderingTrialState.Tool ToolUnderCursor(Ray ray)
        {
            if (state.HeldTool != SolderingTrialState.Tool.None || IsToolAnimating ||
                (fullOperation && (!repair.CanUseTools || RepairAnimating))) return SolderingTrialState.Tool.None;
            var hitIron = ironHit.Raycast(ray, out var a, 3);
            var hitSolder = solderHit.Raycast(ray, out var b, 3);
            return hitIron && (!hitSolder || a.distance < b.distance) ? SolderingTrialState.Tool.Iron :
                hitSolder ? SolderingTrialState.Tool.Solder : SolderingTrialState.Tool.None;
        }

        private void TakeOrReturnTool()
        {
            if (fullOperation && (RepairAnimating || HandleRepairTake())) return;
            var held = state.HeldTool;
            returningTool = held != SolderingTrialState.Tool.None;
            var chosen = returningTool ? held : hoveredTool;
            if (chosen == SolderingTrialState.Tool.None) return;
            movingTool = chosen == SolderingTrialState.Tool.Iron ? iron : solder;
            motionStart = movingTool.localPosition;
            motionRotation = movingTool.localRotation;
            motionTime = 0;
            depth = 0;
            state.SelectTool(returningTool ? SolderingTrialState.Tool.None : chosen);
            hoveredTool = SolderingTrialState.Tool.None;
            if (!returningTool)
            {
                aim = new Vector2(chosen == SolderingTrialState.Tool.Iron ? 0.18f : -0.18f, -0.16f);
                cursorViewport = view.WorldToViewportPoint(transform.TransformPoint(Joint + new Vector3(aim.x, 0, aim.y)));
            }
        }

        private void TickToolMotion(float seconds)
        {
            if (!IsToolAnimating) return;
            motionTime += seconds;
            var t = Mathf.Clamp01(motionTime / 0.24f);
            var eased = Mathf.SmoothStep(0, 1, t);
            var end = returningTool ? (movingTool == iron ? IronDock : SolderDock) :
                Joint + new Vector3(aim.x, 0.18f, aim.y);
            movingTool.localPosition = Vector3.Lerp(motionStart, end, eased) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 0.065f);
            var endRotation = returningTool ? DockRotation(movingTool == iron ? ironAnchor : solderAnchor) : Quaternion.identity;
            movingTool.localRotation = Quaternion.Slerp(motionRotation, endRotation, eased) *
                Quaternion.Euler(0, 0, Mathf.Sin(t * Mathf.PI) * (returningTool ? -8 : 8));
            if (t >= 1) { movingTool.localRotation = endRotation; movingTool = null; }
        }

        private void CancelToolMotion()
        {
            movingTool = null;
        }

        private void AdvanceTool(float seconds, bool pressing, bool operating)
        {
            var held = state.HeldTool;
            depth = Mathf.MoveTowards(depth, pressing && held != SolderingTrialState.Tool.None ? 1 : 0, seconds * 5);
            if (!operating) depth = 0;
            var workingEnd = Joint + new Vector3(aim.x, Mathf.Lerp(0.18f, 0, Mathf.SmoothStep(0, 1, depth)), aim.y);
            if (movingTool != iron)
            {
                iron.localPosition = held == SolderingTrialState.Tool.Iron ? workingEnd : IronDock;
                iron.localRotation = held == SolderingTrialState.Tool.Iron ? Quaternion.identity : DockRotation(ironAnchor);
            }
            if (movingTool != solder)
            {
                solder.localPosition = held == SolderingTrialState.Tool.Solder ? workingEnd : SolderDock;
                solder.localRotation = held == SolderingTrialState.Tool.Solder ? Quaternion.identity : DockRotation(solderAnchor);
            }
            if (dockedSolderFeed != null) dockedSolderFeed.gameObject.SetActive(held == SolderingTrialState.Tool.Solder || movingTool == solder);
            pointer.localPosition = Joint + new Vector3(aim.x, 0.006f, aim.y);
            var actualEnd = held == SolderingTrialState.Tool.Solder ? wireEnd : tip;
            // Geometry gates progress: lateral aim alone or the button alone cannot solder.
            HasContact = operating && pressing && state.Accessible && !IsToolAnimating && held != SolderingTrialState.Tool.None &&
                Vector3.Distance(actualEnd.position, transform.TransformPoint(Joint)) <= 0.038f;
            state.Tick(seconds, HasContact, operating);
            RefreshFeedback();
        }

        public void ResetTrial()
        {
            state.Reset();
            if (fullOperation) ResetRepair();
            CancelToolMotion();
            hoveredTool = SolderingTrialState.Tool.None;
            depth = 0;
            HasContact = false;
            smoke.Clear();
            AdvanceTool(0, false, false);
        }

        private void RefreshFeedback()
        {
            instruction = NextInstruction();
            outline?.Focus(active && focused && Time.timeScale > 0 && hoveredTool != SolderingTrialState.Tool.None ?
                hoveredTool == SolderingTrialState.Tool.Iron ? iron : solder : null, view);
            pointer.gameObject.SetActive(active && !state.Complete && state.HeldTool != SolderingTrialState.Tool.None && !IsToolAnimating);
            hotMetal.color = Color.Lerp(new Color(0.35f, 0.38f, 0.40f), new Color(1, 0.12f, 0.025f), state.Heat);
            hotMetal.SetColor("_EmissionColor", new Color(1, 0.04f, 0) * state.Heat * 0.6f);
            placedSolder.gameObject.SetActive(state.HasSolder && !state.Complete);
            placedSolder.localScale = new Vector3(0.012f, 0.065f * (1 - state.Progress), 0.012f);
            bead.gameObject.SetActive(state.Progress > 0);
            bead.localScale = new Vector3(Mathf.Lerp(0.015f, 0.15f, state.Progress), 0.028f, 0.065f);
            if (state.Working && active && focused)
            {
                if (!smoke.isEmitting) smoke.Play();
                if (!hiss.isPlaying) hiss.Play();
            }
            else
            {
                smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                if (hiss.isPlaying) hiss.Stop();
            }
            if (fullOperation) RefreshRepairFeedback();
        }

        private string NextInstruction()
        {
            if (fullOperation)
            {
                var next = RepairInstruction();
                if (next != null) return next;
            }
            var held = state.HeldTool;
            if (IsToolAnimating) return returningTool ? "Кладём инструмент…" : "Берём инструмент…";
            if (held == SolderingTrialState.Tool.None && hoveredTool != SolderingTrialState.Tool.None)
                return hoveredTool == SolderingTrialState.Tool.Iron ? "E — взять паяльник" : "E — взять припой";
            if (state.Complete) return held == SolderingTrialState.Tool.Iron
                ? "Пайка готова — отпустите ЛКМ и отведите жало" : "Соединение готово · R — новая проба";
            if (state.Working) return "Держите жало на соединении…";
            if (!state.HasSolder)
            {
                if (held == SolderingTrialState.Tool.Iron) return "Сначала нужен припой. Положите паяльник — E";
                if (held == SolderingTrialState.Tool.Solder) return aim.magnitude <= 0.038f
                    ? "Удерживайте ЛКМ — уложить припой" : "Подведите конец припоя к зазору";
                return "Возьмите припой слева — E";
            }
            if (held == SolderingTrialState.Tool.Solder) return "Припой уложен. Положите катушку — E";
            if (held == SolderingTrialState.Tool.None) return "Возьмите паяльник справа — E";
            if (!state.Ready) return "Дождитесь нагрева жала";
            return aim.magnitude <= 0.038f ? "Удерживайте ЛКМ — коснуться припоя" : "Подведите кончик жала к припою";
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            focused = hasFocus;
            if (!hasFocus) hoveredTool = SolderingTrialState.Tool.None;
            if (!hasFocus && fullOperation) InterruptRepairAction();
            if (active && hasFocus) player.SetCursorLocked(true);
            if (active) AdvanceTool(0, false, false);
        }

        private void OnDisable() => Exit();
        private void OnDestroy()
        {
            actions?.Dispose();
            foreach (var resource in resources) if (resource != null) Destroy(resource);
        }

        private void BuildWorkplace()
        {
            var dark = Material(new Color(0.045f, 0.065f, 0.085f));
            var wood = Material(new Color(0.34f, 0.25f, 0.17f));
            var copper = Material(new Color(0.85f, 0.42f, 0.12f));
            var metal = Material(new Color(0.7f, 0.76f, 0.80f));
            metal.SetFloat("_Metallic", 0.8f);
            metal.SetFloat("_Smoothness", 0.45f);
            var green = Material(new Color(0.08f, 0.35f, 0.22f));
            var white = Material(new Color(0.88f, 0.93f, 0.94f));
            hotMetal = Material(Color.gray);
            hotMetal.EnableKeyword("_EMISSION");
            Part("Trial table", transform, PrimitiveType.Cube, new Vector3(0, 0.94f, 0), new Vector3(1.9f, 0.1f, fullOperation ? 1.4f : 1), wood, true);
            foreach (var x in new[] { -0.8f, 0.8f })
                Part("Leg", transform, PrimitiveType.Cube, new Vector3(x, 0.45f, 0), new Vector3(0.09f, 0.9f, 0.65f), dark);
            if (solderingMatPrefab != null)
            {
                workMat = Visual(solderingMatPrefab, transform, new Vector3(0, 0.99f, 0));
                if (!fullOperation) workMat.localScale = new Vector3(1, 1, 0.74f);
                repairAnchor = workMat.Find("RepairOrigin");
                ironAnchor = workMat.Find("IronDock");
                solderAnchor = workMat.Find("SolderDock");
                coverAnchor = workMat.Find("CoverTray");
                supplyAnchor = workMat.Find("Supply");
            }
            if (flashlightVisualPrefab != null) flashlight = Visual(flashlightVisualPrefab, transform, Joint);
            else
            {
                Part("Open flashlight lower shell", transform, PrimitiveType.Cube, new Vector3(0, 1.005f, 0), new Vector3(0.7f, 0.055f, 0.32f), dark);
                Part("Exposed circuit board", transform, PrimitiveType.Cube, new Vector3(0, 1.04f, 0), new Vector3(0.53f, 0.03f, 0.23f), green);
                foreach (var x in new[] { -0.08f, 0.08f })
                    Part("Broken copper contact", transform, PrimitiveType.Cube, new Vector3(x, 1.068f, 0), new Vector3(0.085f, 0.025f, 0.075f), copper);
                Part("Loose red wire", transform, PrimitiveType.Cube, new Vector3(-0.19f, 1.065f, 0.04f), new Vector3(0.18f, 0.024f, 0.025f), Material(new Color(0.65f, 0.035f, 0.025f)));
                Part("Battery", transform, PrimitiveType.Cylinder, new Vector3(0, 1.075f, 0.092f), new Vector3(0.045f, 0.12f, 0.045f), metal).localRotation = Quaternion.Euler(0, 0, 90);
                Part("Flashlight reflector", transform, PrimitiveType.Cylinder, new Vector3(0.31f, 1.065f, 0), new Vector3(0.25f, 0.04f, 0.25f), metal).localRotation = Quaternion.Euler(0, 0, 90);
                Part("Lens", transform, PrimitiveType.Cylinder, new Vector3(0.355f, 1.065f, 0), new Vector3(0.19f, 0.012f, 0.19f), white).localRotation = Quaternion.Euler(0, 0, 90);
            }
            bead = Part("Growing solder bridge", transform, PrimitiveType.Sphere, Joint, Vector3.one * 0.02f, metal);
            placedSolder = Part("Unmelted solder wire", transform, PrimitiveType.Cylinder,
                Joint + Vector3.up * 0.007f, new Vector3(0.012f, 0.065f, 0.012f), metal);
            placedSolder.localRotation = Quaternion.Euler(0, 0, 90);
            iron = new GameObject("Soldering iron - tip origin").transform;
            iron.SetParent(transform, false);
            if (ironVisualPrefab != null)
            {
                var visual = Visual(ironVisualPrefab, iron, Vector3.zero);
                foreach (var renderer in visual.GetComponentsInChildren<MeshRenderer>())
                    if (renderer.name == "HotTip") renderer.sharedMaterial = hotMetal;
                tip = new GameObject("Iron working end").transform;
                tip.SetParent(iron, false);
            }
            else
            {
                tip = Part("Hot tip", iron, PrimitiveType.Sphere, Vector3.zero, new Vector3(0.025f, 0.025f, 0.05f), hotMetal);
                var shaftEnd = new Vector3(0.16f, 0.12f, -0.16f);
                var shaft = Part("Shaft", iron, PrimitiveType.Cylinder, shaftEnd / 2,
                    new Vector3(0.025f, shaftEnd.magnitude / 2, 0.025f), metal);
                shaft.localRotation = Quaternion.FromToRotation(Vector3.up, shaftEnd);
                var grip = Part("Iron grip", iron, PrimitiveType.Capsule, shaftEnd * 1.4f,
                    new Vector3(0.065f, 0.10f, 0.065f), dark);
                grip.localRotation = shaft.localRotation;
            }
            solder = new GameObject("Solder spool - wire end origin").transform;
            solder.SetParent(transform, false);
            if (solderVisualPrefab != null)
            {
                Visual(solderVisualPrefab, solder, Vector3.zero);
                if (workMat != null)
                    foreach (var part in solder.GetComponentsInChildren<Transform>())
                        if (part.name == "Solder feed wire") { dockedSolderFeed = part; part.gameObject.SetActive(false); }
                wireEnd = new GameObject("Solder working end").transform;
                wireEnd.SetParent(solder, false);
            }
            else
            {
                wireEnd = Part("Solder wire end", solder, PrimitiveType.Sphere, Vector3.zero,
                    Vector3.one * 0.013f, metal);
                var spoolPosition = new Vector3(-0.08f, 0.14f, -0.07f);
                var wire = Part("Wire from spool", solder, PrimitiveType.Cylinder, spoolPosition / 2,
                    new Vector3(0.012f, spoolPosition.magnitude / 2, 0.012f), metal);
                wire.localRotation = Quaternion.FromToRotation(Vector3.up, spoolPosition);
                Part("Coiled solder", solder, PrimitiveType.Cylinder, spoolPosition,
                    new Vector3(0.115f, 0.035f, 0.115f), metal);
                foreach (var y in new[] { -0.038f, 0.038f })
                    Part("Spool rim", solder, PrimitiveType.Cylinder, spoolPosition + Vector3.up * y,
                        new Vector3(0.145f, 0.008f, 0.145f), dark);
            }
            pointer = new GameObject("Tool projection ring").transform;
            pointer.SetParent(transform, false);
            var ring = pointer.gameObject.AddComponent<LineRenderer>();
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.widthMultiplier = 0.0025f;
            ring.positionCount = 32;
            var ringMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = Color.white };
            resources.Add(ringMaterial);
            ring.sharedMaterial = ringMaterial;
            for (var i = 0; i < ring.positionCount; i++)
            {
                var angle = i * Mathf.PI * 2 / ring.positionCount;
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 0.024f);
            }
            iron.localPosition = IronDock;
            iron.localRotation = DockRotation(ironAnchor);
            solder.localPosition = SolderDock;
            titleLabel = Label(fullOperation ? "M3 · ПОЛНАЯ ОПЕРАЦИЯ\nE — приступить" : "M2 · ПРОБА ПАЙКИ\nE — приступить", new Vector3(0, 1.24f, fullOperation ? 0.49f : 0.39f), 0.012f);
            if (workMat == null)
            {
                Label("ПРИПОЙ", SolderDock + new Vector3(0, -0.10f, -0.22f), 0.007f);
                Label("ПАЯЛЬНИК", IronDock + new Vector3(0, -0.10f, -0.22f), 0.007f);
            }
            var smokeObject = new GameObject("Light contact smoke");
            smokeObject.transform.SetParent(transform, false);
            smokeObject.transform.localPosition = Joint + Vector3.up * 0.02f;
            smokeObject.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            smoke = smokeObject.AddComponent<ParticleSystem>();
            smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = smoke.main;
            main.playOnAwake = false;
            main.startLifetime = 0.65f;
            main.startSpeed = 0.12f;
            main.startSize = 0.028f;
            main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            main.startColor = new Color(0.78f, 0.82f, 0.85f, 0.23f);
            main.maxParticles = 24;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var expansion = smoke.sizeOverLifetime;
            expansion.enabled = true;
            expansion.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 0.45f, 1, 1.8f));
            var emission = smoke.emission;
            emission.rateOverTime = 18;
            var shape = smoke.shape;
            shape.angle = 16;
            shape.radius = 0.012f;
            var smokeMaterial = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            smokeMaterial.SetFloat("_Surface", 1);
            smokeMaterial.SetFloat("_Blend", 0);
            smokeMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            smokeMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            smokeMaterial.SetFloat("_ZWrite", 0);
            if (smokeTexture != null) smokeMaterial.SetTexture("_BaseMap", smokeTexture);
            smokeMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            smokeMaterial.renderQueue = 3000;
            resources.Add(smokeMaterial);
            smoke.GetComponent<ParticleSystemRenderer>().sharedMaterial = smokeMaterial;
            var fade = smoke.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0.6f, 0), new GradientAlphaKey(0, 1) });
            fade.color = gradient;
            hiss = smokeObject.AddComponent<AudioSource>();
            hiss.playOnAwake = false;
            hiss.loop = true;
            hiss.spatialBlend = 1;
            hiss.minDistance = 0.5f;
            hiss.maxDistance = 4;
            hiss.volume = 0.17f;
            var clip = AudioClip.Create("M2 generated solder hiss", 22050, 1, 22050, false);
            var samples = new float[22050];
            var noise = new System.Random(22);
            for (var i = 0; i < samples.Length; i++) samples[i] = ((float)noise.NextDouble() * 2 - 1) * 0.3f;
            clip.SetData(samples, 0);
            hiss.clip = clip;
            resources.Add(clip);
        }

        private Material Material(Color color)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color };
            resources.Add(material);
            return material;
        }

        private Transform Visual(GameObject prefab, Transform parent, Vector3 position)
        {
            var visual = Instantiate(prefab, parent).transform;
            visual.localPosition = position;
            visual.localRotation = Quaternion.identity;
            visual.localScale = Vector3.one;
            return visual;
        }

        private Transform Part(string name, Transform parent, PrimitiveType type, Vector3 position,
            Vector3 scale, Material material, bool solid = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid) { go.GetComponent<Collider>().enabled = false; Destroy(go.GetComponent<Collider>()); }
            return go.transform;
        }

        private TextMesh Label(string text, Vector3 position, float size)
        {
            var label = new GameObject("M2 label").AddComponent<TextMesh>();
            label.transform.SetParent(transform, false);
            label.transform.localPosition = position;
            label.transform.localRotation = Quaternion.Euler(40, 0, 0);
            label.text = text;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.GetComponent<MeshRenderer>().sharedMaterial = label.font.material;
            label.characterSize = size;
            label.fontSize = 48;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.color = Color.white;
            return label;
        }

        private void OnGUI()
        {
            if (!active) return;
            if (hudStyle == null) hudStyle = new GUIStyle(GUI.skin.label) { fontSize = 19, alignment = TextAnchor.MiddleCenter };
            var scale = Mathf.Clamp(Screen.height / 900f, 0.65f, 1.5f);
            var old = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
            var width = Screen.width / scale;
            var hintWidth = Mathf.Min(570, width - 30);
            GUI.Box(new Rect((width - hintWidth) / 2, 18, hintWidth, 34), "");
            GUI.Label(new Rect((width - hintWidth) / 2 + 5, 18, hintWidth - 10, 34), instruction, hudStyle);
            if (state.HeldTool == SolderingTrialState.Tool.None && !IsToolAnimating)
            {
                var previous = GUI.color;
                GUI.color = hoveredTool != SolderingTrialState.Tool.None || (fullOperation && repairHover != RepairTarget.None) ? new Color(1, 0.72f, 0.20f) : Color.white;
                var point = new Vector2(cursorViewport.x * width, (1 - cursorViewport.y) * Screen.height / scale);
                GUI.DrawTexture(new Rect(point.x - 6, point.y - 1, 12, 2), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(point.x - 1, point.y - 6, 2, 12), Texture2D.whiteTexture);
                GUI.color = previous;
            }
            if (state.Powered && (!fullOperation || state.HeldTool == SolderingTrialState.Tool.Iron))
            {
                GUI.Label(new Rect(width / 2 - 120, 58, 240, 28), state.Ready ? "Жало готово ✓" : "Жало нагревается…", hudStyle);
                GUI.Box(new Rect(width / 2 - 75, 87, 150, 8), "");
                var previousColor = GUI.color;
                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(width / 2 - 73, 89, 146 * state.Heat, 4), Texture2D.whiteTexture);
                GUI.color = previousColor;
            }
            var held = state.HeldTool != SolderingTrialState.Tool.None;
            GUI.Label(new Rect(15, Screen.height / scale - 48, width - 30, 32),
                (fullOperation ? "Мышь — навести · E — взять/положить · ЛКМ — действие · P — пауза · R — сброс · Esc — отойти" :
                "Мышь — вести  ·  E — " + (held ? "положить" : "взять под меткой") +
                "  ·  ЛКМ — опустить  ·  R — заново  ·  Esc — отойти"), hudStyle);
            GUI.matrix = old;
        }
    }
}
