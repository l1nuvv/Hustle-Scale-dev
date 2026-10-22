using Interactables;
using Inventory;
using Player.Movement;
using UnityEngine;
using UnityEngine.Serialization;

namespace Player
{
    [RequireComponent(typeof(PlayerInput), typeof(PlayerController))]
    [DefaultExecutionOrder(50)]
    public class PlayerRaycast : MonoBehaviour
    {
        [FormerlySerializedAs("_camera"), SerializeField] private Camera camera;
        [FormerlySerializedAs("_layerMask"), SerializeField] private LayerMask layerMask = ~0;
        [FormerlySerializedAs("_raycastDistance"), SerializeField, Min(0.1f)] private float raycastDistance = 2.5f;
        [FormerlySerializedAs("_inventoryManager"), SerializeField] private InventoryManager inventoryManager;
        [FormerlySerializedAs("_showDebugRay"), SerializeField] private bool showDebugRay;
        private PlayerInput _input;
        private PlayerController _controller;
        private bool _interactionRequested;
        private PickupOutline outline;
        public Transform PickupFocus => outline != null ? outline.Target : null;
        public string FocusLabel { get; private set; } = "";
        public void Configure(Camera source, LayerMask mask) { camera = source; layerMask = mask; }

        private void Awake()
        {
            _input = GetComponent<PlayerInput>();
            _controller = GetComponent<PlayerController>();
            if (camera == null) camera = Camera.main;
            outline = gameObject.AddComponent<PickupOutline>();
        }
        private void Start()
        {
            if (inventoryManager == null) inventoryManager = FindAnyObjectByType<InventoryManager>();
        }
        private void OnEnable() => _input.OnInteract += HandleInteraction;
        private void OnDisable()
        {
            _input.OnInteract -= HandleInteraction;
            _interactionRequested = false;
            FocusLabel = "";
            if (outline != null) outline.Focus(null, camera);
        }
        private bool TryHit(out RaycastHit hit)
        {
            hit = default;
            return _controller.CanInteract && camera != null &&
                (inventoryManager == null || !inventoryManager.isOpened) &&
                Physics.Raycast(camera.transform.position, camera.transform.forward, out hit,
                    raycastDistance, layerMask.value == 0 ? ~0 : layerMask.value, QueryTriggerInteraction.Collide);
        }
        private void LateUpdate()
        {
            // PlayerInput samples before movement; use the camera after its LateUpdate.
            if (_interactionRequested)
            {
                _interactionRequested = false;
                InteractWithHit();
            }
            FocusLabel = "";
            if (!TryHit(out var hit)) { outline.Focus(null, camera); return; }
            var interaction = hit.collider.GetComponentInParent<IWorldInteraction>();
            if (interaction != null)
            {
                outline.Focus((interaction as IPickupTarget)?.PickupTarget, camera);
                FocusLabel = interaction.Prompt;
                return;
            }
            var pickup = hit.collider.GetComponentInParent<ItemPickup>();
            var target = hit.collider.GetComponentInParent<GrayboxInteractable>();
            if (target != null) { outline.Focus(null, camera); FocusLabel = target.Prompt; }
            else if (pickup != null) { outline.Focus(pickup.PickupTarget, camera); FocusLabel = "E - Pick up"; }
            else
            {
                var slot = hit.collider.GetComponent<InventorySlot>();
                outline.Focus(slot != null && inventoryManager != null && inventoryManager.CanAddItem(slot.item, slot.amount) ? slot.transform : null, camera);
            }
        }
        private void HandleInteraction()
        {
            // Remember the input context now, before Escape can close a modal UI
            // later in Update. The actual ray still uses the final camera pose.
            if (_controller.CanInteract) _interactionRequested = true;
        }
        private void InteractWithHit()
        {
            // The nearest solid hit blocks interaction, even if it is not interactable.
            if (!TryHit(out var hit)) return;
            var interaction = hit.collider.GetComponentInParent<IWorldInteraction>();
            if (interaction != null) { interaction.Interact(); return; }
            var target = hit.collider.GetComponentInParent<GrayboxInteractable>();
            if (target != null) { target.Interact(); return; }
            var pickup = hit.collider.GetComponentInParent<ItemPickup>();
            if (pickup != null) { pickup.PickupItem(gameObject); return; }
            if (inventoryManager != null && hit.collider.TryGetComponent<InventorySlot>(out var slot) && slot.item != null)
            {
                if (!inventoryManager.AddItem(slot.item, slot.amount)) return;
                hit.collider.enabled = false;
                slot.gameObject.SetActive(false);
                Destroy(slot.gameObject);
            }
        }
        private void OnDrawGizmosSelected()
        {
            if (camera == null || !showDebugRay) return;
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(camera.transform.position, camera.transform.forward * raycastDistance);
        }
    }
}
