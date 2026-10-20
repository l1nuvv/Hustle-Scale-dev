using Player.Movement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Player
{
    // One city vehicle. Seating owns the two camera/input contexts; wheel physics
    // remains in FixedUpdate. This does not participate in the trade prototype.
    [RequireComponent(typeof(Rigidbody))]
    public sealed class CityUtilityVehicle : MonoBehaviour
    {
        [SerializeField] private PlayerController walker;
        [SerializeField] private CityGreyboxWalk walkOverlay;
        [SerializeField] private Camera walkingCamera, drivingCamera;
        [SerializeField] private WheelCollider[] wheels;
        [SerializeField] private Transform[] wheelVisuals;
        [SerializeField] private Transform steeringWheel;
        [SerializeField] private TextMesh speedDisplay;
        [SerializeField] private float maximumSpeed = 11.1f;
        [SerializeField] private float motorTorque = 330;
        [SerializeField, Range(0, 1)] private float cameraMotion = 0.6f;
        private Rigidbody body;
        private CharacterController capsule;
        private PlayerInput walkingInput;
        private InputActionMap actions;
        private InputAction movement, handbrake, interact, pause, comfort, look;
        private Vector2 drive;
        private bool braking, paused, nearDoor;
        private float steer, yaw, pitch, impact, longitudinalAcceleration;
        private Vector3 previousVelocity, cameraRest, cameraOffset, cameraOffsetVelocity;
        private string message = "", speedText = "0";
        private int displayedSpeed = -1;
        private AudioSource motor, rolling;
        private AudioClip motorClip, rollingClip;
        public bool IsDriving { get; private set; }
        public bool IsPaused => paused;
        public float Speed => body == null ? 0 : body.linearVelocity.magnitude;
        public float SignedSpeed => body == null ? 0 : Vector3.Dot(body.linearVelocity, transform.forward);
        public float Steering => steer;
        public int GroundedWheels { get; private set; }

#if UNITY_EDITOR
        private bool verificationInput;
        public void VerifyControls(float throttle, float steering, bool brake)
        {
            verificationInput = true;
            drive = new Vector2(Mathf.Clamp(steering, -1, 1), Mathf.Clamp(throttle, -1, 1));
            braking = brake;
        }
        public void EndVerification() { verificationInput = false; drive = Vector2.zero; braking = true; }
#endif

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            capsule = walker.GetComponent<CharacterController>();
            walkingInput = walker.GetComponent<PlayerInput>();
            cameraRest = drivingCamera.transform.localPosition;
            actions = new InputActionMap("City utility vehicle");
            movement = actions.AddAction("Drive", InputActionType.Value);
            movement.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            handbrake = actions.AddAction("Brake", InputActionType.Button, "<Keyboard>/space");
            interact = actions.AddAction("EnterExit", InputActionType.Button, "<Keyboard>/e");
            pause = actions.AddAction("Pause", InputActionType.Button, "<Keyboard>/escape");
            comfort = actions.AddAction("CameraMotion", InputActionType.Button, "<Keyboard>/v");
            look = actions.AddAction("Look", InputActionType.Value, "<Mouse>/delta");
            actions.Enable();
            wheels[0].ConfigureVehicleSubsteps(5, 12, 15);
            CreateAudio();
        }

        private void Update()
        {
            if (!IsDriving)
            {
                var door = transform.TransformPoint(new Vector3(-1.1f, 1, 0.4f));
                var delta = door - walkingCamera.transform.position;
                nearDoor = walker.CanInteract && delta.magnitude < 3 &&
                    Vector3.Dot(walkingCamera.transform.forward, delta.normalized) > 0.25f;
                if (nearDoor && interact.WasPressedThisFrame()) TryEnter();
                return;
            }
            if (pause.WasPressedThisFrame()) SetPaused(!paused);
            if (comfort.WasPressedThisFrame()) cameraMotion = cameraMotion > 0 ? 0 : 0.6f;
            if (paused) return;
#if UNITY_EDITOR
            if (!verificationInput)
#endif
            {
                drive = movement.ReadValue<Vector2>();
                braking = handbrake.IsPressed();
            }
            var mouse = look.ReadValue<Vector2>() * 0.1f;
            yaw = Mathf.Clamp(yaw + mouse.x, -100, 100);
            pitch = Mathf.Clamp(pitch - mouse.y, -35, 40);
            if (interact.WasPressedThisFrame()) TryExit();
        }

        private void FixedUpdate()
        {
            if (paused) return;
            var speed = SignedSpeed;
            var movingAgainstInput = drive.y * speed < -0.4f;
            var pedalBrake = !IsDriving || braking || movingAgainstInput;
            var desiredSteer = IsDriving ? drive.x * Mathf.Lerp(34, 12, Mathf.Clamp01(Mathf.Abs(speed) / maximumSpeed)) : 0;
            steer = Mathf.MoveTowards(steer, desiredSteer, 85 * Time.fixedDeltaTime);
            GroundedWheels = 0;
            for (var i = 0; i < wheels.Length; i++)
            {
                var wheel = wheels[i];
                if (wheel.isGrounded) GroundedWheels++;
                wheel.steerAngle = i < 2 ? steer : 0;
                var limit = drive.y < 0 ? 3.5f : maximumSpeed;
                var torque = IsDriving && !pedalBrake && i >= 2
                    ? drive.y * motorTorque * Mathf.Clamp01((limit - Mathf.Abs(speed)) / 2) : 0;
                wheel.motorTorque = torque;
                wheel.brakeTorque = pedalBrake ? 1300 : Mathf.Abs(drive.y) < 0.05f ? 25 : 0;
            }
            AntiRoll(0, 1);
            AntiRoll(2, 3);
            // Air/rolling resistance acts on the body, never by writing its position.
            body.AddForce(-body.linearVelocity * Speed * 0.45f);
            longitudinalAcceleration = Vector3.Dot((body.linearVelocity - previousVelocity) / Time.fixedDeltaTime, transform.forward);
            previousVelocity = body.linearVelocity;
        }

        private void AntiRoll(int left, int right)
        {
            var a = wheels[left];
            var b = wheels[right];
            var onA = a.GetGroundHit(out var hitA);
            var onB = b.GetGroundHit(out var hitB);
            var travelA = onA ? (-a.transform.InverseTransformPoint(hitA.point).y - a.radius) / a.suspensionDistance : 1;
            var travelB = onB ? (-b.transform.InverseTransformPoint(hitB.point).y - b.radius) / b.suspensionDistance : 1;
            var force = (travelA - travelB) * 1800;
            if (onA) body.AddForceAtPosition(-a.transform.up * force, a.transform.position);
            if (onB) body.AddForceAtPosition(b.transform.up * force, b.transform.position);
        }

        private void LateUpdate()
        {
            for (var i = 0; i < wheels.Length; i++)
            {
                wheels[i].GetWorldPose(out var position, out var rotation);
                wheelVisuals[i].SetPositionAndRotation(position, rotation * Quaternion.Euler(0, 0, 90));
            }
            steeringWheel.localRotation = Quaternion.Euler(20, 0, -steer * 5);
            var kmh = Mathf.RoundToInt(Speed * 3.6f);
            if (kmh != displayedSpeed)
            {
                displayedSpeed = kmh;
                speedText = kmh.ToString("00");
                speedDisplay.text = speedText + " km/h";
            }
            if (!IsDriving || paused) return;
            impact = Mathf.MoveTowards(impact, 0, Time.deltaTime * 3);
            var ground = GroundedWheels > 0 ? Mathf.Clamp01(Speed / maximumSpeed) : 0;
            var vibration = Mathf.Sin(Time.time * 49) * ground * 0.003f + Mathf.Sin(Time.time * 21) * impact * 0.015f;
            var targetOffset = new Vector3(-steer * Speed * 0.00008f, vibration, Mathf.Clamp(-longitudinalAcceleration * 0.004f, -0.025f, 0.025f)) * cameraMotion;
            cameraOffset = Vector3.SmoothDamp(cameraOffset, targetOffset, ref cameraOffsetVelocity, 0.07f);
            drivingCamera.transform.localPosition = cameraRest + cameraOffset;
            drivingCamera.transform.localRotation = Quaternion.Euler(pitch + Mathf.Clamp(longitudinalAcceleration * 0.13f, -1.5f, 1.5f) * cameraMotion, yaw, -steer * Speed * 0.003f * cameraMotion);
            drivingCamera.fieldOfView = Mathf.Lerp(68, 74, Mathf.Clamp01(Speed / maximumSpeed));
            motor.pitch = Mathf.Lerp(0.65f, 2.2f, Speed / maximumSpeed) + Mathf.Abs(drive.y) * 0.12f;
            motor.volume = 0.035f + Mathf.Abs(drive.y) * 0.055f;
            rolling.volume = ground * 0.075f;
            rolling.pitch = Mathf.Lerp(0.6f, 1.6f, ground);
        }

        public bool TryEnter()
        {
            if (IsDriving || Speed > 0.8f) return false;
            var door = transform.TransformPoint(new Vector3(-1.1f, 1, 0.4f));
            var approach = door - walkingCamera.transform.position;
            if (approach.magnitude > 3) return false;
            foreach (var obstruction in Physics.RaycastAll(walkingCamera.transform.position, approach.normalized,
                         approach.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                if (obstruction.collider != capsule && obstruction.collider.attachedRigidbody != body) return false;
            IsDriving = true;
            nearDoor = false;
            message = "";
            if (walkOverlay != null) walkOverlay.enabled = false;
            walkingInput.enabled = false;
            walker.enabled = false;
            capsule.enabled = false;
            walkingCamera.gameObject.SetActive(false);
            drivingCamera.gameObject.SetActive(true);
            yaw = pitch = 0;
            drive = Vector2.zero;
            braking = false;
            SetPaused(false);
            motor.Play();
            rolling.Play();
            return true;
        }

        public bool TryExit()
        {
            if (!IsDriving) return false;
            if (Speed > 0.8f) { message = "Stop before leaving the vehicle."; return false; }
            foreach (var side in new[] { -1, 1 })
            {
                var position = transform.TransformPoint(new Vector3(side * 1.55f, 1, 0.3f));
                if (!Physics.Raycast(position + Vector3.up * 2, Vector3.down, out var ground, 5,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ||
                    ground.collider.attachedRigidbody == body || ground.normal.y < 0.75f ||
                    Mathf.Abs(ground.point.y - transform.position.y) > 0.8f) continue;
                position = ground.point + Vector3.up * 0.06f;
                if (Physics.CheckCapsule(position + Vector3.up * 0.3f, position + Vector3.up * 1.5f,
                        0.33f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                IsDriving = false;
                SetPaused(false);
                drive = Vector2.zero;
                braking = true;
                drivingCamera.gameObject.SetActive(false);
                walker.transform.SetPositionAndRotation(position, Quaternion.Euler(0, transform.eulerAngles.y, 0));
                capsule.enabled = true;
                walker.enabled = true;
                walkingInput.enabled = true;
                if (walkOverlay != null) walkOverlay.enabled = true;
                walkingCamera.gameObject.SetActive(true);
                if (walkOverlay != null) walkOverlay.SetPaused(false);
                else
                {
                    walker.SetMovementEnabled(true);
                    walker.SetCameraControlEnabled(true);
                    walker.SetCursorLocked(true);
                }
                motor.Stop();
                rolling.Stop();
                message = "";
                return true;
            }
            message = "Both exits are blocked. Move to an open space.";
            return false;
        }

        public void SetPaused(bool value)
        {
            paused = value;
            if (value && !body.isKinematic)
            {
                drive = Vector2.zero;
                braking = true;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            body.isKinematic = value;
            Cursor.lockState = value ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = value;
            if (motor != null) { if (value) { motor.Pause(); rolling.Pause(); } else { motor.UnPause(); rolling.UnPause(); } }
        }

        private void OnApplicationFocus(bool focused) { if (!focused && IsDriving) SetPaused(true); }
        private void OnCollisionEnter(Collision collision) => impact = Mathf.Clamp01(collision.impulse.magnitude / 1800);

        private void CreateAudio()
        {
            const int rate = 22050;
            var tone = new float[rate];
            var noise = new float[rate];
            var random = new System.Random(841);
            var low = 0f;
            for (var i = 0; i < rate; i++)
            {
                var t = (float)i / rate;
                tone[i] = Mathf.Sin(t * 2 * Mathf.PI * 90) * 0.35f + Mathf.Sin(t * 2 * Mathf.PI * 180) * 0.12f;
                low = Mathf.Lerp(low, (float)random.NextDouble() * 2 - 1, 0.12f);
                noise[i] = low * 0.5f;
            }
            motorClip = AudioClip.Create("Utility EV motor - original synthesis", rate, 1, rate, false);
            motorClip.SetData(tone, 0);
            rollingClip = AudioClip.Create("Utility tyres - original synthesis", rate, 1, rate, false);
            rollingClip.SetData(noise, 0);
            motor = gameObject.AddComponent<AudioSource>();
            rolling = gameObject.AddComponent<AudioSource>();
            motor.clip = motorClip;
            rolling.clip = rollingClip;
            motor.loop = rolling.loop = true;
            motor.playOnAwake = rolling.playOnAwake = false;
            motor.volume = rolling.volume = 0;
            motor.dopplerLevel = rolling.dopplerLevel = 0;
        }

        private void OnGUI()
        {
            if (!IsDriving && !nearDoor) return;
            var scale = Mathf.Clamp(Screen.height / 900f, 0.8f, 1.5f);
            var old = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
            var width = Screen.width / scale;
            var height = Screen.height / scale;
            GUI.Box(new Rect((width - 520) / 2, height - 105, 520, 90), IsDriving
                ? "WORKSHOP ELECTRIC UTILITY | " + speedText + " km/h" +
                  "\nW/S: drive / brake / reverse | A/D: steer | Space: brake" +
                  "\nMouse: look | E: leave when stopped | Esc: pause | V: camera motion" +
                  "\n" + (paused ? "PAUSED — Esc to resume" : message)
                : "E — drive workshop utility\nWASD: drive | Space: brake | E: leave when stopped");
            GUI.matrix = old;
        }

        private void OnDestroy()
        {
            actions?.Dispose();
            if (motorClip != null) Destroy(motorClip);
            if (rollingClip != null) Destroy(rollingClip);
        }
    }
}
