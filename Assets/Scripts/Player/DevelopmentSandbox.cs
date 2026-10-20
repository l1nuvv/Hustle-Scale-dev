using Interactables;
using Player.Movement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Player
{
    // Scene-local development controls; the existing workshop still owns all trades.
    [DefaultExecutionOrder(-300)]
    public sealed class DevelopmentSandbox : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private CityUtilityVehicle vehicle;
        [SerializeField] private FirstDealView deal;
        [SerializeField] private WorkshopView workshop;
        [SerializeField] private Transform[] destinations;
        private bool panelOpen, driving;
        private Canvas[] canvases;
        private bool[] canvasStates;
        public bool ClockHeld => deal.ClockSuspended;

        private void Awake() => deal.ClockSuspended = true;

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f2Key.wasPressedThisFrame && !vehicle.IsDriving)
                SetPanel(!panelOpen);
        }

        private void LateUpdate()
        {
            if (panelOpen && !deal.MenuOpen) panelOpen = false;
            if (driving == vehicle.IsDriving) return;
            driving = vehicle.IsDriving;
            if (driving)
            {
                deal.enabled = false;
                workshop.Session.SetPaused(true);
                canvases = FindObjectsByType<Canvas>();
                canvasStates = new bool[canvases.Length];
                for (var i = 0; i < canvases.Length; i++)
                {
                    canvasStates[i] = canvases[i].enabled;
                    canvases[i].enabled = false;
                }
            }
            else
            {
                for (var i = 0; i < canvases.Length; i++)
                    if (canvases[i] != null) canvases[i].enabled = canvasStates[i];
                deal.enabled = true;
                deal.CloseMenu();
            }
        }

        public void SetPanel(bool open)
        {
            if (vehicle.IsDriving) return;
            panelOpen = open;
            if (open) deal.OpenMenu(false, true);
            else deal.CloseMenu();
        }

        public bool GoToZone(int index)
        {
            if (vehicle.IsDriving || index < 0 || index >= destinations.Length) return false;
            var body = player.GetComponent<CharacterController>();
            body.enabled = false;
            player.transform.SetPositionAndRotation(destinations[index].position, destinations[index].rotation);
            body.enabled = true;
            SetPanel(false);
            return true;
        }

        public void ToggleClock() => deal.ClockSuspended = !deal.ClockSuspended;

        public void RestartScene()
        {
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(gameObject.scene.path,
                new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(gameObject.scene.path);
#endif
        }

        private void OnGUI()
        {
            var old = GUI.matrix;
            var scale = Mathf.Clamp(Screen.height / 900f, 0.65f, 1.5f);
            GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
            var width = Screen.width / scale;
            if (!vehicle.IsDriving)
                GUI.Box(new Rect(width - 355, 12, 340, 55),
                    "ПОЛИГОН 260 × 220 м | F2 — зоны\nЧасы сделки: " + (ClockHeld ? "ОСТАНОВЛЕНЫ" : "ИДУТ"));
            if (panelOpen)
            {
                var rect = new Rect(width - 375, 80, 360, 420);
                GUI.Box(rect, "ПОЛИГОН РАЗРАБОТКИ");
                for (var i = 0; i < destinations.Length; i++)
                    if (GUI.Button(new Rect(rect.x + 15, rect.y + 35 + i * 38, 330, 32), destinations[i].name)) GoToZone(i);
                if (GUI.Button(new Rect(rect.x + 15, rect.y + 305, 330, 32), "Часы: остановить / запустить")) ToggleClock();
                if (GUI.Button(new Rect(rect.x + 15, rect.y + 343, 330, 32), "Сбросить полигон и сделку")) RestartScene();
                if (GUI.Button(new Rect(rect.x + 15, rect.y + 381, 330, 28), "Закрыть — F2")) SetPanel(false);
            }
            GUI.matrix = old;
        }
    }
}
