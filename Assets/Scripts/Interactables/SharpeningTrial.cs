using System.Collections.Generic;
using Player;
using Player.Movement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Interactables
{
    [DefaultExecutionOrder(200)]
    public sealed class SharpeningTrial : MonoBehaviour, IWorldInteraction
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private Camera view;
        [SerializeField] private Transform knife;
        [SerializeField] private Transform wheel;
        [SerializeField] private Transform wheelMark;
        [SerializeField] private Renderer[] edgeSections;
        [SerializeField] private Material unfinishedEdge;
        [SerializeField] private Material honedEdge;
        [SerializeField, Range(5, 40)] private float targetAngle = 20;
        [SerializeField, Range(1, 12)] private float angleTolerance = 5;
        [SerializeField, Min(0.001f)] private float requiredTravel = 0.04f;
        [SerializeField, Min(0.01f)] private float processingSpeed = 0.12f;
        private readonly SharpeningTrialState state = new SharpeningTrialState();
        private readonly List<Behaviour> suspended = new List<Behaviour>();
        private InputActionMap actions;
        private InputAction move, hold, zoom, tilt, motor, inspect, reset, leave;
        private Vector3 origin, savedCameraPosition;
        private Quaternion savedCameraRotation, wheelHome;
        private float angle = 35, savedFov, rotation;
        private CursorLockMode savedLock;
        private bool savedVisible, active, consumed, exitRequested, gripping, focused = true;
        private int enteredFrame;
        private ParticleSystem sparks;
        private AudioSource sound;
        private AudioClip soundClip;
        private Material sparkMaterial;
        private TextMesh title;
        private GUIStyle hudStyle;
        private string result = "";
        public string Prompt => "E — M6: ручная заточка ножа";
        public bool Active => active;
        public SharpeningTrialState State => state;
        public Vector3 EdgeOrigin => origin;
        public bool EffectsPlaying => sparks.isEmitting || sound.isPlaying;

        private void Awake()
        {
            actions = new InputActionMap("M6 sharpening");
            move = actions.AddAction("Guide", InputActionType.Value, "<Mouse>/delta");
            hold = actions.AddAction("Hold knife", InputActionType.Button, "<Mouse>/leftButton");
            zoom = actions.AddAction("Magnify", InputActionType.Button, "<Mouse>/rightButton");
            tilt = actions.AddAction("Angle", InputActionType.Value);
            tilt.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/q").With("Positive", "<Keyboard>/e");
            motor = actions.AddAction("Motor", InputActionType.Button, "<Keyboard>/space");
            inspect = actions.AddAction("Inspect", InputActionType.Button, "<Keyboard>/f");
            reset = actions.AddAction("Reset", InputActionType.Button, "<Keyboard>/r");
            leave = actions.AddAction("Exit", InputActionType.Button, "<Keyboard>/escape");
            wheelHome = wheel.localRotation;
            title = GetComponentInChildren<TextMesh>();
            BuildEffects();
            ResetTrial();
        }

        public void Interact()
        {
            if (active || !enabled || player == null || !player.CanInteract) return;
            active = true;
            if (title != null) title.gameObject.SetActive(false);
            savedCameraPosition = view.transform.position;
            savedCameraRotation = view.transform.rotation;
            savedFov = view.fieldOfView;
            savedLock = Cursor.lockState;
            savedVisible = Cursor.visible;
            Suspend(player.GetComponent<Player.PlayerInput>());
            Suspend(player.GetComponent<PlayerRaycast>());
            Suspend(player);
            Suspend(FindAnyObjectByType<FirstDealView>());
            Suspend(FindAnyObjectByType<DevelopmentSandbox>());
            foreach (var canvas in FindObjectsByType<Canvas>()) Suspend(canvas);
            player.SetCursorLocked(true);
            actions.Enable();
            enteredFrame = Time.frameCount;
            consumed = true;
            SetCamera(false);
        }

        private void Suspend(Behaviour item)
        {
            if (item == null || !item.enabled) return;
            suspended.Add(item);
            item.enabled = false;
        }

        public void Exit()
        {
            if (!active) return;
            active = false;
            gripping = false;
            if (title != null) title.gameObject.SetActive(true);
            exitRequested = false;
            actions.Disable();
            state.StopMotor();
            StopEffects();
            RefreshKnife();
            foreach (var item in suspended) if (item != null) item.enabled = true;
            suspended.Clear();
            if (view != null)
            {
                view.transform.SetPositionAndRotation(savedCameraPosition, savedCameraRotation);
                view.fieldOfView = savedFov;
            }
            if (player != null) player.SetCursorLocked(focused && savedLock == CursorLockMode.Locked);
            Cursor.lockState = focused ? savedLock : CursorLockMode.None;
            Cursor.visible = !focused || savedVisible;
        }

        public void ResetTrial()
        {
            state.Reset();
            origin = new Vector3(0, 1.27f, -0.14f);
            angle = 35;
            consumed = true;
            gripping = false;
            result = "";
            StopEffects();
            RefreshKnife();
        }

        private void Update()
        {
            if (!active || Time.frameCount == enteredFrame) return;
            if (leave.WasPressedThisFrame()) { exitRequested = true; return; }
            if (!focused || !Application.isFocused || Time.timeScale <= 0)
            {
                consumed = true;
                gripping = false;
                state.StopContact();
                StopEffects();
                RefreshKnife();
                SetCamera(false);
                return;
            }
            if (!hold.IsPressed()) consumed = false;
            if (reset.WasPressedThisFrame()) ResetTrial();
            if (!state.Placed && hold.WasPressedThisFrame() && !consumed)
            {
                state.Place();
                consumed = true;
            }
            if (motor.WasPressedThisFrame()) state.ToggleMotor();
            var working = state.Placed && hold.IsPressed() && !consumed;
            gripping = working;
            var previous = origin;
            if (working)
            {
                var delta = move.ReadValue<Vector2>();
                var displacement = Vector3.ClampMagnitude(new Vector3(delta.x * 0.0007f, 0, delta.y * 0.0005f), 0.025f);
                origin += displacement;
                origin.x = Mathf.Clamp(origin.x, -0.22f, 0.22f);
                origin.z = Mathf.Clamp(origin.z, -0.23f, 0.014f);
                angle = Mathf.Clamp(angle + tilt.ReadValue<float>() * 25 * Time.deltaTime, 0, 50);
                result = "";
            }
            state.Sweep(previous, origin, angle, Time.deltaTime, working,
                targetAngle, angleTolerance, requiredTravel, processingSpeed);
            if (inspect.WasPressedThisFrame())
                result = origin.z > -0.035f ? "Сначала отведите нож от круга движением мыши вниз." :
                    state.Complete ? "Проверка: вся кромка обработана. Нож готов. R — повторить." :
                    "Проверка: кромка обработана не полностью. Тёмные участки требуют прохода.";
            if (state.Running)
            {
                rotation = Mathf.Repeat(rotation + 900 * Time.deltaTime, 360);
                wheel.localRotation = Quaternion.AngleAxis(rotation, Vector3.right) * wheelHome;
                wheelMark.RotateAround(transform.TransformPoint(SharpeningTrialState.WheelCenter), transform.right, 900 * Time.deltaTime);
            }
            RefreshKnife();
            if (state.Contact && !state.Complete)
            {
                sparks.transform.position = transform.TransformPoint(state.ContactPoint);
                if (!sparks.isEmitting) sparks.Play();
            }
            else sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (state.Running)
            {
                sound.volume = state.Contact ? 0.16f : 0.045f;
                sound.pitch = state.Contact ? 0.74f : 1;
                if (!sound.isPlaying) sound.Play();
            }
            else sound.Stop();
            SetCamera(zoom.IsPressed());
        }

        private void LateUpdate()
        {
            if (exitRequested) { Exit(); return; }
            if (active) SetCamera(focused && zoom.IsPressed());
        }

        private void SetCamera(bool magnify)
        {
            view.transform.SetPositionAndRotation(transform.TransformPoint(new Vector3(0.10f, 1.72f, -0.57f)),
                transform.rotation * Quaternion.Euler(39, -7, 0));
            view.fieldOfView = savedFov * 0.8f * (magnify ? 0.78f : 1);
        }

        private void RefreshKnife()
        {
            knife.localPosition = gripping ? origin : new Vector3(state.Placed ? 0.05f : 0.28f, 1.04f, -0.18f);
            knife.localRotation = Quaternion.Euler(gripping ? angle : 0, 0, 0);
            for (var i = 0; i < edgeSections.Length; i++)
                edgeSections[i].sharedMaterial = state.Coverage(i) >= 1 ? honedEdge : unfinishedEdge;
        }

        private void BuildEffects()
        {
            var go = new GameObject("M6 contact sparks");
            go.transform.SetParent(transform, false);
            go.transform.localRotation = Quaternion.Euler(15, 180, 0);
            sparks = go.AddComponent<ParticleSystem>();
            sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = sparks.main;
            main.playOnAwake = false; main.loop = true; main.startLifetime = 0.15f;
            main.startSpeed = 0.45f; main.startSize = 0.004f;
            main.startColor = new Color(1, 0.65f, 0.18f); main.gravityModifier = 0.4f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = sparks.emission; emission.rateOverTime = 35;
            var shape = sparks.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 20; shape.radius = 0.002f;
            sparkMaterial = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            sparks.GetComponent<ParticleSystemRenderer>().sharedMaterial = sparkMaterial;
            sound = gameObject.AddComponent<AudioSource>();
            sound.playOnAwake = false; sound.loop = true; sound.spatialBlend = 0.5f;
            soundClip = AudioClip.Create("M6 motor and abrasive", 22050, 1, 22050, false);
            var samples = new float[22050];
            var random = new System.Random(6);
            for (var i = 0; i < samples.Length; i++) samples[i] =
                0.3f * Mathf.Sin(2 * Mathf.PI * 110 * i / 22050) + 0.15f * ((float)random.NextDouble() * 2 - 1);
            soundClip.SetData(samples, 0); sound.clip = soundClip;
        }

        private void StopEffects()
        {
            if (sparks != null) sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (sound != null) sound.Stop();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            focused = hasFocus;
            consumed = true;
            gripping = false;
            state.StopContact();
            StopEffects();
            if (active && !hasFocus) state.StopMotor();
            if (active) RefreshKnife();
            if (active) player.SetCursorLocked(hasFocus);
        }
        private void OnDisable() => Exit();
        private void OnDestroy()
        {
            Exit(); actions?.Dispose();
            if (soundClip != null) Destroy(soundClip);
            if (sparkMaterial != null) Destroy(sparkMaterial);
        }

        private void OnGUI()
        {
            if (!active) return;
            var matrix = GUI.matrix;
            var scale = Mathf.Clamp(Screen.height / 900f, 0.65f, 1.5f);
            GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
            if (hudStyle == null) hudStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 18, alignment = TextAnchor.UpperLeft, wordWrap = true,
                padding = new RectOffset(12, 12, 8, 8)
            };
            var width = Mathf.Min(940, Screen.width / scale - 30);
            var instruction = !state.Placed ? "ЛКМ — положить нож на рабочее место" :
                !state.Running ? "Пробел — включить станок" : state.Complete ? "Кромка готова: отведите нож и нажмите F" :
                !hold.IsPressed() ? "Удерживайте ЛКМ, чтобы взять нож и вести кромку" :
                !state.Contact ? "Нет контакта: мышь вверх — к кругу; влево/вправо — вдоль кромки" :
                Mathf.Abs(angle - targetAngle) > angleTolerance ? "Неверный угол: Q / E — наклонить нож" :
                !state.Cutting ? "Контакт есть: ведите кромку влево / вправо" : "Идёт обработка: проведите всю кромку";
            GUI.Box(new Rect(15, 14, width, 155), "M6 — ОДИН НОЖ / ИЗОЛИРОВАННАЯ ПРОБА\n" + instruction +
                $"\nУгол {angle:0}° | ориентир {targetAngle:0}° ± {angleTolerance:0}° | станок: {(state.Running ? "ВКЛ" : "ВЫКЛ")}" +
                "\nЛКМ держать + мышь — вести; Q/E — угол; ПКМ — ближе\nПробел — станок; F — проверить вдали от круга; R — сброс; Esc — выйти", hudStyle);
            if (result.Length > 0) GUI.Box(new Rect(15, 175, width, 65), result, hudStyle);
            GUI.matrix = matrix;
        }
    }
}
