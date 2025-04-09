using UnityEngine;

namespace Player.Movement
{
    [RequireComponent(typeof(PlayerInput))] //Check PlayerInput Component exists
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float _baseMovementSpeed = 10f; // Standart movement speed of the player
        
        [Header("Sprint Settings")]
        [SerializeField, Range(1f, 5f)] private float _speedMultiplier = 2f; // Multiplier for sprinting speed

        private PlayerInput _playerInput; // Reference to input handler class
        private bool _isSprinting; // Current sprint state
        private float _currentMovementSpeed; // Actual speed after all modifiers

        private void Awake()
        {
            _playerInput = GetComponent<PlayerInput>(); // Get input component from PlayerInput.cs
            _currentMovementSpeed = _baseMovementSpeed; // Initialize speed
        }

        private void Update()
        {
            UpdateSprintState(); // Check for sprint changes
            MovePlayer(); // Move the player based on input
        }
    
        // Move player based on input vector
        public void MovePlayer()
        {
            Vector2 moveInput = _playerInput.MoveInput;
            Vector3 movement = new Vector3(moveInput.x, 0, moveInput.y).normalized; // Normalize movement vector for
            // consistent speed and convert to 3D
            if (movement.magnitude > 0)
            {
                // Apply movement with current speed
                transform.Translate(movement * (_currentMovementSpeed * Time.deltaTime), Space.World);
            }
        }

        private void UpdateSprintState()
        {
            bool shouldSprint = _playerInput.isSprinting; // Get current input state
        
            if (shouldSprint != _isSprinting) 
            {
                _isSprinting = shouldSprint;
                _currentMovementSpeed = _isSprinting 
                    ? _baseMovementSpeed * _speedMultiplier // Apply speed boost
                    : _baseMovementSpeed; // Back to normal speed
            }
        }
    }
}
