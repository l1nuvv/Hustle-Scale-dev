using UnityEngine;
using UnityEngine.InputSystem;
using System;

namespace Player
{
        public class PlayerInput : MonoBehaviour
        {
        
                // For ToggleSprint()
                public bool isSprinting { get; private set; } // Indicates whether the player is currently sprinting
                private Vector2 _moveInput; // Stores the player's movement input
                public ref Vector2 MoveInput => ref _moveInput;  // Public property to use movement input with
                                                                 // reference access

                // Events for different actions
                public event Action OnInteract;
                public event Action OnToggleInventory;

                private void OnEnable()
                {
                        // Subscribe to the new Input System events
                        var playerInput = GetComponent<UnityEngine.InputSystem.PlayerInput>();
                        if (playerInput != null)
                        {
                                playerInput.actions["Interact"].performed += OnInteractPerformed;
                                playerInput.actions["ToggleInventory"].performed += OnToggleInventoryPerformed;
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

                private void OnInteractPerformed(InputAction.CallbackContext context)
                {
                        OnInteract?.Invoke();
                }

                private void OnToggleInventoryPerformed(InputAction.CallbackContext context)
                {
                        OnToggleInventory?.Invoke();
                }

                private void Update()
                {
                        var keyboard = Keyboard.current;
                        if (keyboard == null) return;
        
                        MoveInput = Vector2.zero; // Reset movement input

                        // Process WASD input for movement 
                        if (keyboard.wKey.isPressed) MoveInput.y += 1;
                        if (keyboard.sKey.isPressed) MoveInput.y -= 1;
                        if (keyboard.aKey.isPressed) MoveInput.x -= 1;
                        if (keyboard.dKey.isPressed) MoveInput.x += 1;
        
                        // Process LShift input for sprinting 
                        isSprinting = keyboard.leftShiftKey.isPressed;
                }
        }
}
