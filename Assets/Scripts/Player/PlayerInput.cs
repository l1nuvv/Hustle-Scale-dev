using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Player
{
    [DefaultExecutionOrder(-100)]
    public class PlayerInput : MonoBehaviour
    {
        private InputActionMap _actions;
        private InputAction _move;
        private InputAction _sprint;
        private InputAction _interact;
        private InputAction _inventory;
        private Vector2 _moveInput;
        private Vector2 _mouseInput;
        public ref Vector2 MoveInput => ref _moveInput;
        public ref Vector2 MouseInput => ref _mouseInput;
        public bool IsSprinting { get; private set; }
        public bool MouseInputEnabled { get; set; } = true;
        public event Action OnInteract;
        public event Action OnToggleInventory;

        private void Awake()
        {
            // Own this map so scene PlayerInput components cannot dispatch a second E/Tab.
            _actions = new InputActionMap("Prototype Player");
            _move = _actions.AddAction("Movement", InputActionType.Value);
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            _sprint = _actions.AddAction("Sprint", InputActionType.Button, "<Keyboard>/leftShift");
            _interact = _actions.AddAction("Interact", InputActionType.Button, "<Keyboard>/e");
            _inventory = _actions.AddAction("ToggleInventory", InputActionType.Button, "<Keyboard>/tab");
        }
        private void OnEnable() => _actions.Enable();
        private void OnDisable()
        {
            _actions.Disable();
            _moveInput = _mouseInput = Vector2.zero;
            IsSprinting = false;
        }
        private void OnDestroy() => _actions.Dispose();

        private void Update()
        {
            _moveInput = _move.ReadValue<Vector2>();
            IsSprinting = _sprint.IsPressed();
            // Mouse delta is already per-frame; do not multiply by deltaTime.
            _mouseInput = MouseInputEnabled && Mouse.current != null
                ? Mouse.current.delta.ReadValue() : Vector2.zero;
            if (_inventory.WasPressedThisFrame()) OnToggleInventory?.Invoke();
            if (_interact.WasPressedThisFrame()) OnInteract?.Invoke();
        }
    }
}
