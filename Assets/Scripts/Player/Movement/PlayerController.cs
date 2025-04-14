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
        
        [Header("Camera Settings")]
        [SerializeField] private Transform _cameraTransform;
        [SerializeField] private Vector3 _cameraOffset = new Vector3(0, 1.8f, 0.05f); // Позиция глаз персонажа (поднята выше)
        [SerializeField] private float _mouseSensitivity = 2f;
        
        private float _verticalLookRotation;
        private bool _cursorLocked = true;
        private bool _cameraControlEnabled = true;
        private bool _movementEnabled = true; // Новый флаг для управления движением

        private PlayerInput _playerInput; // Reference to input handler class
        private bool _isSprinting; // Current sprint state
        private float _currentMovementSpeed; // Actual speed after all modifiers

        private void Awake()
        {
            _playerInput = GetComponent<PlayerInput>(); // Get input component from PlayerInput.cs
            _currentMovementSpeed = _baseMovementSpeed; // Initialize speed
            
            // If camera transform is not assigned, try to find main camera
            if (_cameraTransform == null && Camera.main != null)
            {
                _cameraTransform = Camera.main.transform;
                Debug.Log("Камера найдена и привязана к персонажу");
            }
            else if (_cameraTransform == null)
            {
                Debug.LogError("Камера не найдена! Невозможно настроить вид от первого лица.");
            }
            
            // Заблокировать и скрыть курсор в начале для FPS управления
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Start()
        {
            // Убедимся, что камера правильно позиционирована в начале игры
            if (_cameraTransform != null)
            {
                UpdateCameraPosition();
                Debug.Log($"Начальная позиция камеры: {_cameraTransform.position}, смещение: {_cameraOffset}");
            }
            
            // Установим начальное вращение камеры немного вниз
            _verticalLookRotation = 0f;
            UpdateCameraRotation();
        }

        private void Update()
        {
            if (!enabled) return;
            
            if (_movementEnabled)
            {
                UpdateSprintState(); // Check for sprint changes
                MovePlayer(); // Move the player based on input
            }
            
            UpdateCameraRotation(); // Вращение камеры мышью
            UpdateCameraPosition(); // Update the camera position to follow player
        }
    
        // Move player based on input vector
        public void MovePlayer()
        {
            if (!_movementEnabled) return;
            
            Vector2 moveInput = _playerInput.MoveInput;
            // Используем направление камеры для движения вперед
            Vector3 forward = new Vector3(_cameraTransform.forward.x, 0, _cameraTransform.forward.z).normalized;
            Vector3 right = new Vector3(_cameraTransform.right.x, 0, _cameraTransform.right.z).normalized;
            
            Vector3 movement = (forward * moveInput.y + right * moveInput.x).normalized;
            
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
        
        private void UpdateCameraPosition()
        {
            if (_cameraTransform != null)
            {
                // Position the camera at the player position plus the offset (на уровне глаз персонажа)
                _cameraTransform.position = transform.position + _cameraOffset;
            }
        }
        
        private void UpdateCameraRotation()
        {
            if (!_cursorLocked || _cameraTransform == null || !_cameraControlEnabled) return;
            
            // Получение ввода мыши через PlayerInput
            Vector2 mouseInput = _playerInput.MouseInput;
            float mouseX = mouseInput.x * _mouseSensitivity;
            float mouseY = mouseInput.y * _mouseSensitivity;
            
            // Поворот персонажа вокруг Y оси (горизонтальное вращение)
            transform.Rotate(Vector3.up, mouseX);
            
            // Вращение камеры вокруг X оси (вертикальное вращение)
            _verticalLookRotation -= mouseY;
            // Ограничение угла наклона камеры
            _verticalLookRotation = Mathf.Clamp(_verticalLookRotation, -90f, 90f);
            _cameraTransform.localRotation = Quaternion.Euler(_verticalLookRotation, 0f, 0f);
        }
        
        // Метод для управления блокировкой курсора (можно вызывать при открытии/закрытии инвентаря)
        public void SetCursorLocked(bool locked)
        {
            _cursorLocked = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        // Новый метод для управления камерой
        public void SetCameraControlEnabled(bool cameraEnabled)
        {
            _cameraControlEnabled = cameraEnabled;
        }

        // Новый метод для управления движением
        public void SetMovementEnabled(bool cameraEnabled)
        {
            _movementEnabled = cameraEnabled;
        }
    }
}
