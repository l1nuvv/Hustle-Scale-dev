using UnityEngine;
using UnityEngine.Serialization;

namespace Player.Movement
{
    [RequireComponent(typeof(PlayerInput))] //Check PlayerInput Component exists
    public class PlayerController : MonoBehaviour
    {
        [FormerlySerializedAs("_baseMovementSpeed")] [Header("Movement Settings")] [SerializeField]
        private float baseMovementSpeed = 10f; // Standart movement speed of the player

        [FormerlySerializedAs("_speedMultiplier")] [Header("Sprint Settings")] [SerializeField] [Range(1f, 5f)]
        private float speedMultiplier = 2f; // Multiplier for sprinting speed

        [FormerlySerializedAs("_cameraTransform")] [Header("Camera Settings")] [SerializeField]
        private Transform cameraTransform;

        [FormerlySerializedAs("_cameraOffset")] [SerializeField] private Vector3 cameraOffset = new(0, 1.8f, 0.05f); // Позиция глаз персонажа (поднята выше)
        [FormerlySerializedAs("_mouseSensitivity")] [SerializeField] private float mouseSensitivity = 2f;
        private bool _cameraControlEnabled = true;
        private float _currentMovementSpeed; // Actual speed after all modifiers
        private bool _cursorLocked = true;
        private bool _isSprinting; // Current sprint state
        private bool _movementEnabled = true; // Новый флаг для управления движением

        private PlayerInput _playerInput; // Reference to input handler class

        private float _verticalLookRotation;

        private void Awake()
        {
            _playerInput = GetComponent<PlayerInput>(); // Get input component from PlayerInput.cs
            _currentMovementSpeed = baseMovementSpeed; // Initialize speed

            // If camera transform is not assigned, try to find main camera
            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
                Debug.Log("Камера найдена и привязана к персонажу");
            }
            else if (cameraTransform == null)
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
            if (cameraTransform != null)
            {
                UpdateCameraPosition();
                Debug.Log($"Начальная позиция камеры: {cameraTransform.position}, смещение: {cameraOffset}");
            }

            // Установим начальное вращение камеры немного вниз
            _verticalLookRotation = 0f;
            UpdateCameraRotation();
        }

        private void Update()
        {
            if (!enabled)
            {
                return;
            }

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
            if (!_movementEnabled)
            {
                return;
            }

            var moveInput = _playerInput.MoveInput;
            // Используем направление камеры для движения вперед
            var forward = new Vector3(cameraTransform.forward.x, 0, cameraTransform.forward.z).normalized;
            var right = new Vector3(cameraTransform.right.x, 0, cameraTransform.right.z).normalized;

            var movement = (forward * moveInput.y + right * moveInput.x).normalized;

            if (movement.magnitude > 0)
            {
                // Apply movement with current speed
                transform.Translate(movement * (_currentMovementSpeed * Time.deltaTime), Space.World);
            }
        }

        private void UpdateSprintState()
        {
            var shouldSprint = _playerInput.IsSprinting; // Get current input state

            if (shouldSprint != _isSprinting)
            {
                _isSprinting = shouldSprint;
                _currentMovementSpeed = _isSprinting
                    ? baseMovementSpeed * speedMultiplier // Apply speed boost
                    : baseMovementSpeed; // Back to normal speed
            }
        }

        private void UpdateCameraPosition()
        {
            if (cameraTransform != null)
            {
                // Position the camera at the player position plus the offset (на уровне глаз персонажа)
                cameraTransform.position = transform.position + cameraOffset;
            }
        }

        private void UpdateCameraRotation()
        {
            if (!_cursorLocked || cameraTransform == null || !_cameraControlEnabled)
            {
                return;
            }

            // Получение ввода мыши через PlayerInput
            var mouseInput = _playerInput.MouseInput;
            var mouseX = mouseInput.x * mouseSensitivity;
            var mouseY = mouseInput.y * mouseSensitivity;

            // Поворот персонажа вокруг Y оси (горизонтальное вращение)
            transform.Rotate(Vector3.up, mouseX);

            // Вращение камеры вокруг X оси (вертикальное вращение)
            _verticalLookRotation -= mouseY;
            // Ограничение угла наклона камеры
            _verticalLookRotation = Mathf.Clamp(_verticalLookRotation, -90f, 90f);
            cameraTransform.localRotation = Quaternion.Euler(_verticalLookRotation, 0f, 0f);
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
