using Interactables;
using Inventory;
using Player.Movement;
using UnityEngine;
using UnityEngine.Serialization;

namespace Player
{
    [RequireComponent(typeof(PlayerInput), typeof(PlayerController))]
    public class PlayerRaycast : MonoBehaviour
    {
        [FormerlySerializedAs("_camera"), SerializeField] private Camera camera;
        [FormerlySerializedAs("_layerMask"), SerializeField] private LayerMask layerMask = ~0;
        [FormerlySerializedAs("_raycastDistance"), SerializeField, Min(0.1f)] private float raycastDistance = 2.5f;
        [FormerlySerializedAs("_inventoryManager"), SerializeField] private InventoryManager inventoryManager;
        [FormerlySerializedAs("_showDebugRay"), SerializeField] private bool showDebugRay;
        private PlayerInput _input;
        private PlayerController _controller;
        public string FocusLabel { get; private set; } = "";

        private void Awake()
        {
            _input = GetComponent<PlayerInput>();
            _controller = GetComponent<PlayerController>();
            if (camera == null) camera = Camera.main;
        }
        private void Start()
        {
            if (inventoryManager == null) inventoryManager = FindAnyObjectByType<InventoryManager>();
        }
        private void OnEnable() => _input.OnInteract += HandleInteraction;
        private void OnDisable()
        {
            _input.OnInteract -= HandleInteraction;
            FocusLabel = "";
        }
        private bool TryHit(out RaycastHit hit)
        {
            hit = default;
            return _controller.CanInteract && camera != null &&
                (inventoryManager == null || !inventoryManager.isOpened) &&
                Physics.Raycast(camera.transform.position, camera.transform.forward, out hit,
                    raycastDistance, layerMask.value == 0 ? ~0 : layerMask.value, QueryTriggerInteraction.Collide);
        }
        private void Update()
        {
            FocusLabel = "";
            if (!TryHit(out var hit)) return;
            var interaction = hit.collider.GetComponentInParent<IWorldInteraction>();
            if (interaction != null) { FocusLabel = interaction.Prompt; return; }
            var target = hit.collider.GetComponentInParent<GrayboxInteractable>();
            if (target != null) FocusLabel = target.Prompt;
            else if (hit.collider.GetComponentInParent<ItemPickup>() != null) FocusLabel = "E - Pick up";
        }
        private void HandleInteraction()
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
                inventoryManager.AddItem(slot.item, slot.amount);
                hit.collider.enabled = false;
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
