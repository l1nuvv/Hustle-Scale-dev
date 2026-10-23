using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace Player.Movement
{
    [RequireComponent(typeof(PlayerInput))]
    public class PlayerController : MonoBehaviour
    {
        [FormerlySerializedAs("_baseMovementSpeed"), SerializeField, Min(0.1f)]
        private float baseMovementSpeed = 4f;
        [FormerlySerializedAs("_speedMultiplier"), SerializeField, Range(1f, 5f)]
        private float speedMultiplier = 2f;
        [SerializeField, Min(0)] private float jumpHeight = 0.8f;
        [FormerlySerializedAs("_cameraTransform"), SerializeField]
        private Transform cameraTransform;
        [FormerlySerializedAs("_cameraOffset"), SerializeField]
        private Vector3 cameraOffset = new Vector3(0, 1.65f, 0);
        [FormerlySerializedAs("_mouseSensitivity"), SerializeField, Min(0.01f)]
        private float mouseSensitivity = 0.12f;
        private PlayerInput _input;
        private CharacterController _body;
        private bool _movementEnabled = true;
        private bool _cameraControlEnabled = true;
        private bool _cursorLocked;
        private float _pitch;
        private float _verticalSpeed;
        public bool CanInteract => _movementEnabled && _cameraControlEnabled && _cursorLocked;
        public float CameraPitch => _pitch;
        public float CheatSpeedMultiplier { get; private set; } = 1;
        public void SetCheatSpeed(float multiplier) => CheatSpeedMultiplier =
            float.IsNaN(multiplier) || float.IsInfinity(multiplier) ? 1 : Mathf.Clamp(multiplier, 1, 5);

        public void RestorePose(Vector3 position, float yaw, float pitch)
        {
            var wasEnabled = _body.enabled;
            _body.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            _pitch = Mathf.Clamp(pitch, -85, 85);
            _verticalSpeed = 0;
            _body.enabled = wasEnabled;
            LateUpdate();
        }

        private void Awake()
        {
            _input = GetComponent<PlayerInput>();
            _body = GetComponent<CharacterController>();
            // Existing prototype instances predate this requirement.
            if (_body == null)
            {
                _body = gameObject.AddComponent<CharacterController>();
                _body.height = 1.8f;
                _body.radius = 0.3f;
                _body.center = Vector3.up * 0.9f;
            }
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
            if (cameraTransform == null)
            {
                Debug.LogError("PlayerController requires a camera.", this);
                enabled = false;
                return;
            }
            SetCursorLocked(true);
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                SetCursorLocked(false);
            if (!_cursorLocked && _movementEnabled && _cameraControlEnabled &&
                Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                SetCursorLocked(true);
            if (_cursorLocked && _cameraControlEnabled)
            {
                transform.Rotate(0, _input.MouseInput.x * mouseSensitivity, 0);
                _pitch = Mathf.Clamp(_pitch - _input.MouseInput.y * mouseSensitivity, -85f, 85f);
            }
            MovePlayer();
        }

        private void LateUpdate()
        {
            if (cameraTransform == null) return;
            cameraTransform.SetPositionAndRotation(transform.TransformPoint(cameraOffset),
                transform.rotation * Quaternion.Euler(_pitch, 0, 0));
        }

        public void MovePlayer()
        {
            if (!_movementEnabled || _body == null || !_body.enabled) return;
            var input = _cursorLocked ? Vector2.ClampMagnitude(_input.MoveInput, 1f) : Vector2.zero;
            var speed = baseMovementSpeed * (_input.IsSprinting ? speedMultiplier : 1f) * CheatSpeedMultiplier;
            var velocity = (transform.forward * input.y + transform.right * input.x) * speed;
            if (_body.isGrounded)
            {
                if (_verticalSpeed < 0) _verticalSpeed = -2f;
                if (_cursorLocked && _input.JumpPressed && jumpHeight > 0)
                    _verticalSpeed = Mathf.Sqrt(2f * -Physics.gravity.y * jumpHeight);
            }
            _verticalSpeed = Mathf.Max(_verticalSpeed + Physics.gravity.y * Time.deltaTime, -40f);
            velocity.y = _verticalSpeed;
            _body.Move(velocity * Time.deltaTime);
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) SetCursorLocked(false);
        }
        private void OnDisable() => SetCursorLocked(false);
        public void SetCursorLocked(bool locked)
        {
            _cursorLocked = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
        public void SetCameraControlEnabled(bool cameraEnabled) => _cameraControlEnabled = cameraEnabled;
        public void SetMovementEnabled(bool movementEnabled) => _movementEnabled = movementEnabled;
    }
}
