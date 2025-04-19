using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Player
{
    public class PlayerInput : MonoBehaviour
    {
        // Для отладки состояния инвентаря
        private bool _isInventoryOpen;
        // reference access

        // Новые свойства для ввода мыши
        private Vector2 _mouseInput;
        private Vector2 _moveInput; // Stores the player's movement input

        // For ToggleSprint()
        public bool IsSprinting { get; private set; } // Indicates whether the player is currently sprinting
        public ref Vector2 MoveInput => ref _moveInput; // Public property to use movement input with
        public ref Vector2 MouseInput => ref _mouseInput;

        public bool MouseInputEnabled { get; set; } = true;

        private void Awake()
        {
            // Убедимся, что обработчики событий не null
            if (OnToggleInventory == null)
            {
                Debug.LogWarning("OnToggleInventory is null in Awake");
            }
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            MoveInput = Vector2.zero; // Reset movement input

            // Process WASD input for movement
            if (keyboard.wKey.isPressed)
            {
                MoveInput.y += 1;
            }

            if (keyboard.sKey.isPressed)
            {
                MoveInput.y -= 1;
            }

            if (keyboard.aKey.isPressed)
            {
                MoveInput.x -= 1;
            }

            if (keyboard.dKey.isPressed)
            {
                MoveInput.x += 1;
            }

            // Process LShift input for sprinting
            IsSprinting = keyboard.leftShiftKey.isPressed;

            // Fallback input for inventory toggle (if the new input system doesn't work)
            if (keyboard.tabKey.wasPressedThisFrame)
            {
                Debug.Log("Tab key pressed as fallback");
                ToggleInventoryEvent();
            }

            if (MouseInputEnabled)
            {
                _mouseInput = new Vector2(
                    Input.GetAxis("Mouse X"),
                    Input.GetAxis("Mouse Y")
                );
            }
            else
            {
                _mouseInput = Vector2.zero;
            }
        }

        private void OnEnable()
        {
            // Subscribe to the new Input System events
            var playerInput = GetComponent<UnityEngine.InputSystem.PlayerInput>();
            if (playerInput != null)
            {
                playerInput.actions["Interact"].performed += OnInteractPerformed;
                playerInput.actions["ToggleInventory"].performed += OnToggleInventoryPerformed;
                Debug.Log("Subscribed to PlayerInput actions");
            }
            else
            {
                Debug.LogWarning("PlayerInput component not found! Using fallback input system.");
            }
        }

        private void OnDisable()
        {
            // Unsubscribe from the new Input System events
            var playerInput = GetComponent<UnityEngine.InputSystem.PlayerInput>();
            if (playerInput != null)
            {
                playerInput.actions["Interact"].performed -= OnInteractPerformed;
                playerInput.actions["ToggleInventory"].performed -= OnToggleInventoryPerformed;
            }
        }

        // Events for different actions
        public event Action OnInteract;
        public event Action OnToggleInventory;

        private void OnInteractPerformed(InputAction.CallbackContext context)
        {
            Debug.Log("Interact performed");
            OnInteract?.Invoke();
        }

        private void OnToggleInventoryPerformed(InputAction.CallbackContext context)
        {
            Debug.Log("Toggle Inventory performed через Input System");
            ToggleInventoryEvent();
        }

        // Отдельный метод для вызова события
        private void ToggleInventoryEvent()
        {
            if (OnToggleInventory != null)
            {
                _isInventoryOpen = !_isInventoryOpen;
                OnToggleInventory.Invoke();
                Debug.Log($"Событие OnToggleInventory вызвано. Новое состояние: {_isInventoryOpen}");
            }
            else
            {
                Debug.LogError("Событие OnToggleInventory не имеет подписчиков!");
            }
        }
    }
}
