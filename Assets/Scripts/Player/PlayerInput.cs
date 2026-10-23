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
        private InputAction _sprint, _jump;
        public bool JumpPressed { get; private set; }
        private InputAction _interact;
        private InputAction _inventory;
        private InputAction _light, _place, _stow, _test, _repair;
        private readonly InputAction[] _slots = new InputAction[4];
        private Vector2 _moveInput;
        private Vector2 _mouseInput;
        public ref Vector2 MoveInput => ref _moveInput;
        public ref Vector2 MouseInput => ref _mouseInput;
        public bool IsSprinting { get; private set; }
        public bool MouseInputEnabled { get; set; } = true;
        public event Action OnInteract;
        public event Action OnToggleInventory;
        public event Action OnToggleLight;
        public event Action OnPlaceItem;
        public event Action OnStowItem;
        public event Action OnTestItem;
        public event Action OnRepairItem;
        public event Action<int> OnSelectSlot;

        private void Awake()
        {
            // Own this map so scene PlayerInput components cannot dispatch a second E/Tab.
            _actions = new InputActionMap("Prototype Player");
            _move = _actions.AddAction("Movement", InputActionType.Value);
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            _sprint = _actions.AddAction("Sprint", InputActionType.Button, "<Keyboard>/leftShift");
            _jump = _actions.AddAction("Jump", InputActionType.Button, "<Keyboard>/space");
            _interact = _actions.AddAction("Interact", InputActionType.Button, "<Keyboard>/e");
            _inventory = _actions.AddAction("ToggleInventory", InputActionType.Button, "<Keyboard>/tab");
            _light = _actions.AddAction("ToggleLight", InputActionType.Button, "<Keyboard>/f");
            _place = _actions.AddAction("PlaceItem", InputActionType.Button, "<Keyboard>/q");
            _stow = _actions.AddAction("StowItem", InputActionType.Button, "<Keyboard>/g");
            _test = _actions.AddAction("TestItem", InputActionType.Button, "<Keyboard>/t");
            _repair = _actions.AddAction("RepairItem", InputActionType.Button, "<Keyboard>/r");
            for (var i = 0; i < _slots.Length; i++)
                _slots[i] = _actions.AddAction("InventorySlot" + (i + 1), InputActionType.Button, "<Keyboard>/" + (i + 1));
        }
        private void OnEnable() => _actions.Enable();
        private void OnDisable()
        {
            _actions.Disable();
            _moveInput = _mouseInput = Vector2.zero;
            IsSprinting = false; JumpPressed = false;
        }
        private void OnDestroy() => _actions.Dispose();

        private void Update()
        {
            JumpPressed = _jump.WasPressedThisFrame() && !_inventory.WasPressedThisFrame();
            _moveInput = _move.ReadValue<Vector2>();
            IsSprinting = _sprint.IsPressed();
            // Mouse delta is already per-frame; do not multiply by deltaTime.
            _mouseInput = MouseInputEnabled && Mouse.current != null
                ? Mouse.current.delta.ReadValue() : Vector2.zero;
            // A menu transition consumes this frame's gameplay shortcuts, including
            // when the menu closes and immediately restores the controller.
            if (_inventory.WasPressedThisFrame())
            {
                OnToggleInventory?.Invoke();
                return;
            }
            if (_interact.WasPressedThisFrame()) OnInteract?.Invoke();
            if (_light.WasPressedThisFrame()) OnToggleLight?.Invoke();
            if (_place.WasPressedThisFrame()) OnPlaceItem?.Invoke();
            if (_stow.WasPressedThisFrame()) OnStowItem?.Invoke();
            if (_test.WasPressedThisFrame()) OnTestItem?.Invoke();
            if (_repair.WasPressedThisFrame()) OnRepairItem?.Invoke();
            for (var i = 0; i < _slots.Length; i++)
                if (_slots[i].WasPressedThisFrame()) OnSelectSlot?.Invoke(i);
        }
    }
}
