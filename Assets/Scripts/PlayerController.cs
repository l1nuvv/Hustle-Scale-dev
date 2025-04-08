using UnityEngine;
using UnityEngine.InputSystem;


public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float movementSpeed = 10f;
    [SerializeField] private float speedMultiplier = 2f;
    
    private Vector2 _moveInput;
    private float _horizontalInput;
    private float _verticalInput;
    
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
        
        _moveInput = _moveInput.normalized;
    }
    
    private void MovePlayer()
    {
        _verticalInput = Input.GetAxis("Vertical");
        _horizontalInput = Input.GetAxis("Horizontal");
        
        Vector3 movement = new Vector3(_horizontalInput, 0, _verticalInput).normalized;
        if (movement.magnitude > 0)
        {
            Vector3 moveDirection = movement * (movementSpeed * Time.deltaTime);
            transform.Translate(moveDirection, Space.World);
        }
    }

    /*private void Sprint()
    {
        movementSpeed *= speedMultiplier;
    }

    private void StopSprint()
    {
        movementSpeed /= speedMultiplier;
    }*/
}
