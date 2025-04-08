using UnityEngine;
using UnityEngine.InputSystem;


public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float movementSpeed = 10f; // Base movement speed of the player
    [Header("Sprint Settings")]
    [SerializeField, Range(1f, 5f)] private float speedMultiplier = 2f; // Multiplier for sprinting speed
    
    // For ToggleSprint()
    private bool _isSprinting = false; // Indicates whether the player is currently sprinting
    private Vector2 _moveInput; // Stores the player's movement input
    
    private void Update()
    {
        HandleInput(); // Process input from the player
        MovePlayer(); // Move the player based on input
    }

    private void HandleInput()
    {
        // Init keyboard input
        var keyboard = Keyboard.current;
        if (keyboard == null) return;
        
        _moveInput = Vector2.zero; // Reset movement input

        // Process WASD input for movement 
        if (keyboard.wKey.isPressed) _moveInput.y += 1;
        if (keyboard.sKey.isPressed) _moveInput.y -= 1;
        if (keyboard.aKey.isPressed) _moveInput.x -= 1;
        if (keyboard.dKey.isPressed) _moveInput.x += 1;
        // Process LShift input for sprinting 
        if(keyboard.leftShiftKey.wasPressedThisFrame) ToggleSprint(true);
        if(keyboard.leftShiftKey.wasReleasedThisFrame) ToggleSprint(false);
    }
    
    // Move player based on input vector
    private void MovePlayer()
    {
        Vector3 movement = new Vector3(_moveInput.x, 0, _moveInput.y).normalized; // Normalize movement
                                                                                  // vector for consistent speed
        if (movement.magnitude > 0)
        {
            Vector3 moveDirection = movement * (movementSpeed * Time.deltaTime);
            transform.Translate(moveDirection, Space.World); 
            // Move the player in the world space
        }
    }

    // Adjusts the player's movement speed based on sprinting state
    private void ToggleSprint(bool sprinting)
    {
        if (sprinting && !_isSprinting)
        {
            movementSpeed *= speedMultiplier; // Increase speed when sprinting
            _isSprinting = true; // Set sprinting state
        }
        else if (!sprinting && _isSprinting)
        {
            movementSpeed /= speedMultiplier; // Reset to original speed when sprinting is deactivated
            _isSprinting = false; // Reset sprinting state
        }
    }
}
