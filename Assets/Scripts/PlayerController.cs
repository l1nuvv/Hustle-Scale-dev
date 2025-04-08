using UnityEngine;
using UnityEngine.InputSystem;


public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float movementSpeed = 10f;
    [SerializeField] private float speedMultiplier = 2f;
    
    private Vector2 _moveInput;
    
    private void Update()
    {
        HandleInput();
        MovePlayer();
    }

    private void HandleInput()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;
        
        _moveInput = Vector2.zero;

        if (keyboard.wKey.isPressed) _moveInput.y += 1;
        if (keyboard.sKey.isPressed) _moveInput.y -= 1;
        if (keyboard.aKey.isPressed) _moveInput.x -= 1;
        if (keyboard.dKey.isPressed) _moveInput.x += 1;
        
        if(keyboard.leftShiftKey.wasPressedThisFrame) Sprint();
        if(keyboard.leftShiftKey.wasReleasedThisFrame) StopSprint();
    }
    
    private void MovePlayer()
    {
        Vector3 movement = new Vector3(_moveInput.x, 0, _moveInput.y).normalized;
        if (movement.magnitude > 0)
        {
            Vector3 moveDirection = movement * (movementSpeed * Time.deltaTime);
            transform.Translate(moveDirection, Space.World);
        }
    }

    private void Sprint()
    {
        movementSpeed *= speedMultiplier;
    }

    private void StopSprint()
    {
        movementSpeed /= speedMultiplier;
    }
}
