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
        [SerializeField] private Renderer coating;
        [SerializeField] private Transform[] edgeSamples;
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
        private bool inspectionPending;
        private int enteredFrame;
        private ParticleSystem sparks;
        private AudioSource sound;
        private AudioClip soundClip;
        private Material sparkMaterial;
        private MaterialPropertyBlock edgeProperties;
        private readonly System.Random coatingRandom = new System.Random();
        private Vector4 coatingPattern;
        private static readonly Color CoatingColor = new Color(0.04f, 0.78f, 1f);
        private float lastCutTime = float.NegativeInfinity;
        private TextMesh title;
        private GUIStyle hudStyle;
        private string result = "";
        public string Prompt => "E — M6: ручная заточка ножа";
        public bool Active => active;
        public SharpeningTrialState State => state;
        public Vector3 EdgeOrigin => origin;
        public bool EffectsPlaying => sparks.isEmitting || sound.isPlaying;
        public string Instruction { get; private set; } = "";
        public bool CanInspect => state.Placed && (!gripping || SharpeningTrialState.HasWheelClearance(origin, angle));

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
            edgeProperties = new MaterialPropertyBlock();
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
            RefreshInstruction();
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
            inspectionPending = false;
            gripping = false;
            lastCutTime = float.NegativeInfinity;
            if (title != null) title.gameObject.SetActive(true);
            exitRequested = false;
            actions.Disable();
            state.StopMotor();
            StopEffects();
            RefreshKnife();
            RefreshInstruction();
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
            lastCutTime = float.NegativeInfinity;
            result = "";
            inspectionPending = false;
            coatingPattern = new Vector4((float)coatingRandom.NextDouble(), (float)coatingRandom.NextDouble(),
                .85f + (float)coatingRandom.NextDouble() * .4f, .8f + (float)coatingRandom.NextDouble() * .4f);
            StopEffects();
            RefreshKnife();
            RefreshInstruction();
        }

        private void Update()
        {
            if (!active || Time.frameCount == enteredFrame) return;
            if (leave.WasPressedThisFrame()) { exitRequested = true; return; }
            if (!focused || !Application.isFocused || Time.timeScale <= 0)
            {
                consumed = true;
                gripping = false;
                lastCutTime = float.NegativeInfinity;
                state.StopContact();
                StopEffects();
                RefreshKnife();
                SetCamera(false);
                RefreshInstruction();
                return;
            }
            if (!hold.IsPressed()) consumed = false;
            if (reset.WasPressedThisFrame()) ResetTrial();
            if (!state.Placed && hold.WasPressedThisFrame() && !consumed)
            {
                state.Place();
                consumed = true;
            }
            if (motor.WasPressedThisFrame()) { state.ToggleMotor(); result = ""; inspectionPending = false; }
            var working = state.Placed && hold.IsPressed() && !consumed;
            gripping = working;
            var previous = origin;
            if (working)
            {
                var delta = move.ReadValue<Vector2>();
                var displacement = Vector3.ClampMagnitude(new Vector3(delta.x * 0.0007f, 0, delta.y * 0.0005f), 0.025f);
                origin += displacement;
                origin.x = Mathf.Clamp(origin.x, -0.22f, 0.22f);
                origin.z = Mathf.Clamp(origin.z, -0.23f, 0.05f);
                angle = Mathf.Clamp(angle + tilt.ReadValue<float>() * 25 * Time.deltaTime, 0, 50);
                origin = SharpeningTrialState.ConstrainOrigin(origin, angle);
                if (delta.sqrMagnitude > 0.01f || Mathf.Abs(tilt.ReadValue<float>()) > 0.01f) result = "";
            }
            state.Sweep(previous, origin, angle, Time.deltaTime, working,
                targetAngle, angleTolerance, requiredTravel, processingSpeed);
            if (state.Cutting) { lastCutTime = Time.unscaledTime; inspectionPending = false; }
            if (!working || !state.Contact || Mathf.Abs(angle - targetAngle) > angleTolerance)
                lastCutTime = float.NegativeInfinity;
            if (inspect.WasPressedThisFrame())
            {
                inspectionPending = state.Placed && !CanInspect;
                result = !state.Placed ? "Сначала положите нож на рабочее место." : inspectionPending ? "" :
                    state.Complete ? "Проверка: вся кромка обработана. Нож готов. R — повторить." :
                    "Кромка обработана не полностью. Проведите светящиеся участки через круг.";
            }
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
            RefreshInstruction();
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
            edgeProperties.SetVector("_Pattern", coatingPattern);
            edgeProperties.SetVector("_Coverage0", new Vector4(state.Coverage(0), state.Coverage(1), state.Coverage(2), state.Coverage(3)));
            edgeProperties.SetVector("_Coverage1", new Vector4(state.Coverage(4), state.Coverage(5), state.Coverage(6), state.Coverage(7)));
            edgeProperties.SetVector("_Coverage2", new Vector4(state.Coverage(8), state.Coverage(9), state.Coverage(10), state.Coverage(11)));
            coating.SetPropertyBlock(edgeProperties);
        }

        private void RefreshInstruction()
        {
            Instruction = result.Length > 0 ? result : !state.Placed ? "ЛКМ — положить нож" :
                inspectionPending || state.Complete ? CanInspect ? "Нож отведён — F: проверить кромку" :
                    "Удерживайте ЛКМ и отведите нож от круга мышью вниз" :
                !state.Running ? "Пробел — включить станок" :
                !gripping ? "Удерживайте ЛКМ — возьмите нож" :
                !state.Contact ? "Мышь вверх — подведите светящуюся кромку к кругу" :
                Mathf.Abs(angle - targetAngle) > angleTolerance ? "Контакт есть · Q / E — выставьте угол" :
                !state.ContactNeedsWork ? "Этот участок готов — подведите к кругу оставшееся покрытие" :
                Time.unscaledTime - lastCutTime <= 0.25f ? "Обрабатываем кромку — покрытие снимается" :
                "Контакт есть · ведите светящуюся кромку влево / вправо";
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
            lastCutTime = float.NegativeInfinity;
            state.StopContact();
            StopEffects();
            if (active && !hasFocus) state.StopMotor();
            if (active) { RefreshKnife(); RefreshInstruction(); }
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
            if (hudStyle == null) hudStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 19, alignment = TextAnchor.MiddleCenter, wordWrap = true
            };
            var width = Screen.width / scale;
            var height = Screen.height / scale;
            var hintWidth = Mathf.Min(730, width - 30);
            var center = width / 2;
            // The M3 layout: one contextual hint above, compact readings, controls along the bottom.
            GUI.Box(new Rect(center - hintWidth / 2, 18, hintWidth, 48), "");
            GUI.Label(new Rect(center - hintWidth / 2 + 8, 18, hintWidth - 16, 48), Instruction, hudStyle);
            if (state.Placed)
            {
                var color = GUI.color;
                GUI.color = Mathf.Abs(angle - targetAngle) <= angleTolerance ? new Color(.65f, 1, .75f) : new Color(1, .78f, .4f);
                GUI.Label(new Rect(center - 270, 72, 540, 28),
                    $"Угол {angle:0}° / {targetAngle:0}° ± {angleTolerance:0}° · Станок {(state.Running ? "включён" : "выключен")}", hudStyle);
                GUI.color = color;
                GUI.Label(new Rect(center - 270, 103, 540, 28),
                    $"Кромка: {state.Progress:P0} · Готово {state.FinishedSections} из {SharpeningTrialState.Sections} участков", hudStyle);
                var mapWidth = Mathf.Min(420, width - 40);
                var cellWidth = mapWidth / SharpeningTrialState.Sections;
                for (var i = 0; i < SharpeningTrialState.Sections; i++)
                {
                    var rect = new Rect(center - mapWidth / 2 + i * cellWidth, 137, cellWidth - 3, 14);
                    GUI.color = state.SectionContact(i) ? Color.white : new Color(.22f, .28f, .30f);
                    GUI.DrawTexture(rect, Texture2D.whiteTexture);
                    GUI.color = state.Coverage(i) >= 1 ? new Color(.72f, .84f, .80f) : CoatingColor;
                    GUI.DrawTexture(new Rect(rect.x + 2, rect.y + 2, rect.width - 4, rect.height - 4), Texture2D.whiteTexture);
                    GUI.color = new Color(.72f, .84f, .80f);
                    GUI.DrawTexture(new Rect(rect.x + 2, rect.y + 2, (rect.width - 4) * state.Coverage(i), rect.height - 4), Texture2D.whiteTexture);
                }
                GUI.color = color;
                GUI.Label(new Rect(center - 310, 154, 620, 25), "Голубое покрытие — ещё обработать · Под кругом — белая рамка", hudStyle);
            }
            GUI.Label(new Rect(15, height - 83, width - 30, 30),
                "ЛКМ удерживать + мышь — вести нож · Q/E — угол · ПКМ — ближе", hudStyle);
            GUI.Label(new Rect(15, height - 51, width - 30, 30),
                "Пробел — станок · F — проверить · R — заново · Esc — отойти", hudStyle);
            GUI.matrix = matrix;
        }
    }
}
