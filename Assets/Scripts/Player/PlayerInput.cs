using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInput : MonoBehaviour
{
        
        // For ToggleSprint()
        public bool isSprinting { get; private set; } // Indicates whether the player is currently sprinting
        private Vector2 _moveInput; // Stores the player's movement input
        public ref Vector2 MoveInput => ref _moveInput;  // Public property to use movement input with reference access
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
