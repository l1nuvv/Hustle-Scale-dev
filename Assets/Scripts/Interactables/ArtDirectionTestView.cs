using Inventory;
using Player.Movement;
using UnityEngine;
using UnityEngine.UI;

namespace Interactables
{
    // Presentation fixture: the existing session owns identity, knowledge and item mutations.
    // No save, trade, repair shortcut, or alternate economy is introduced here.
    public sealed class ArtDirectionTestView : MonoBehaviour
    {
        [SerializeField] private Player.PlayerInput input;
        [SerializeField] private PlayerController controller;
        [SerializeField] private Player.PlayerRaycast raycast;
        [SerializeField] private Camera eye;
        [SerializeField] private Transform bench;
        [SerializeField] private Transform[] lamps;
        [SerializeField] private Transform[] slots;
        [SerializeField] private Renderer[] lenses;
        [SerializeField] private Light[] beams;
        [SerializeField] private Text stateLabel;
        [SerializeField] private Text resultLabel;
        [SerializeField] private Text promptLabel;
        [SerializeField] private RectTransform safeArea;
        private WorkshopSession session;
        private BoxCollider[] colliders;
        private MaterialPropertyBlock properties;
        private int selected;
        private string previousPrompt;
        private Rect previousSafeArea;
        private bool wasInteractive;

        public WorkshopSession Session => session;

        private void Start() => controller.RestorePose(controller.transform.position, controller.transform.eulerAngles.y, 13);

        private void Awake()
        {
            session = new WorkshopSession();
            properties = new MaterialPropertyBlock();
            colliders = new BoxCollider[lamps.Length];
            for (var i = 0; i < lamps.Length; i++) colliders[i] = lamps[i].GetComponent<BoxCollider>();
        }

        private void OnEnable()
        {
            session.Changed += Refresh;
            input.OnToggleLight += TogglePower;
            input.OnPlaceItem += Place;
            Refresh();
        }

        private void OnDisable()
        {
            session.Changed -= Refresh;
            input.OnToggleLight -= TogglePower;
            input.OnPlaceItem -= Place;
        }

        private void Update()
        {
            var interactive = controller.CanInteract;
            var prompt = interactive ? raycast.FocusLabel : "Щёлкни по игре — продолжить · Esc — освободить курсор";
            if (prompt != previousPrompt)
            {
                previousPrompt = prompt;
                promptLabel.text = prompt;
            }
            if (interactive != wasInteractive)
            {
                wasInteractive = interactive;
                session.SetPaused(!interactive);
            }
            if (Screen.safeArea != previousSafeArea)
            {
                previousSafeArea = Screen.safeArea;
                safeArea.anchorMin = previousSafeArea.position / new Vector2(Screen.width, Screen.height);
                safeArea.anchorMax = (previousSafeArea.position + previousSafeArea.size) / new Vector2(Screen.width, Screen.height);
            }
        }

        public string Prompt(ArtTestInteraction.ActionKind action, int index)
        {
            switch (action)
            {
                case ArtTestInteraction.ActionKind.Lamp:
                    if (session.Units[index].Location == FlashlightLocation.Bench)
                        return "E — забрать фонарь со стенда · F — свет";
                    return "E — взять фонарь " + (index + 1).ToString("00") + " · " + Known(session.Units[index]);
                case ArtTestInteraction.ActionKind.Test: return "E — проверить фонарь на стенде";
                case ArtTestInteraction.ActionKind.Crate: return "E — вернуть фонарь из рук в ящик";
                default: return session.OnBench == null ? "E / Q — поставить фонарь на стенд" : "E — забрать фонарь со стенда · F — свет";
            }
        }

        // The same spatial gate is used for E, Q and verification calls; walls block reach.
        private bool Reaches(ArtTestInteraction.ActionKind action, int index)
        {
            if (!controller.CanInteract || !Physics.Raycast(eye.transform.position, eye.transform.forward,
                    out var hit, 2.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return false;
            var target = hit.collider.GetComponentInParent<ArtTestInteraction>();
            return target != null && target.Kind == action && (action != ArtTestInteraction.ActionKind.Lamp || target.Index == index);
        }

        public void Act(ArtTestInteraction.ActionKind action, int index)
        {
            if (!Reaches(action, index)) return;
            string message;
            bool success;
            switch (action)
            {
                case ArtTestInteraction.ActionKind.Lamp:
                    selected = index;
                    success = session.Units[index].Location == FlashlightLocation.Bench ?
                        session.ToggleBench(out message) : session.ToggleUnit(index, out message);
                    break;
                case ArtTestInteraction.ActionKind.Test:
                    success = session.Test(out message);
                    if (success) selected = IndexOf(session.OnBench);
                    break;
                case ArtTestInteraction.ActionKind.Crate:
                    if (session.InHands == null) { resultLabel.text = "В руках нет фонаря."; return; }
                    selected = IndexOf(session.InHands);
                    success = session.ToggleUnit(selected, out message);
                    break;
                default:
                    if (session.InHands != null) selected = IndexOf(session.InHands);
                    else if (session.OnBench != null) selected = IndexOf(session.OnBench);
                    success = session.ToggleBench(out message);
                    break;
            }
            resultLabel.text = success ? action == ArtTestInteraction.ActionKind.Test ?
                "ПРОВЕРКА · " + Known(session.OnBench) : "ДЕЙСТВИЕ · " + Translate(message) : "НЕДОСТУПНО · " + Translate(message);
            Refresh();
        }

        private void Place()
        {
            if (session.InHands != null && session.OnBench == null) Act(ArtTestInteraction.ActionKind.Bench, 0);
        }

        private void TogglePower()
        {
            if (!controller.CanInteract) return;
            var unit = session.InHands;
            if (unit == null && session.OnBench != null && (Reaches(ArtTestInteraction.ActionKind.Bench, 0) ||
                Reaches(ArtTestInteraction.ActionKind.Lamp, IndexOf(session.OnBench)))) unit = session.OnBench;
            if (unit == null) return;
            selected = IndexOf(unit);
            var success = session.TogglePower(selected, out var message);
            resultLabel.text = success ? "СВЕТ · " + (unit.IsPowered ?
                unit.IsTested ? "включён" : "включён. Диагностика ещё требуется." : "выключен") : FirstDealText.Message(message);
            Refresh();
        }

        private int IndexOf(FlashlightUnit unit)
        {
            for (var i = 0; i < session.Units.Count; i++) if (session.Units[i] == unit) return i;
            return 0;
        }

        private void Refresh()
        {
            for (var i = 0; i < lamps.Length; i++)
            {
                var unit = session.Units[i];
                var held = unit.Location == FlashlightLocation.Hands;
                var parent = held ? eye.transform : unit.Location == FlashlightLocation.Bench ? bench : slots[i];
                lamps[i].SetParent(parent, false);
                lamps[i].localPosition = held ? new Vector3(0.35f, -0.4f, 0.65f) : Vector3.zero;
                lamps[i].localRotation = held ? Quaternion.Euler(0, 180, 0) : Quaternion.identity;
                colliders[i].enabled = unit.Location == FlashlightLocation.Crate || unit.Location == FlashlightLocation.Bench;
                beams[i].enabled = unit.IsPowered;
                properties.SetColor("_EmissionColor", unit.IsPowered ? new Color(1.6f, 1.3f, 0.8f) : Color.black);
                lenses[i].SetPropertyBlock(properties);
            }
            var current = session.Units[selected];
            stateLabel.text = "ФОНАРЬ " + (selected + 1).ToString("00") + "   /   " + Known(current) +
                "\n" + (current.Location == FlashlightLocation.Hands ? "В руках" : current.Location == FlashlightLocation.Bench ? "На стенде" : "В транспортном ящике");
        }

        private static string Known(FlashlightUnit unit) => !unit.IsTested ? "Не проверен" :
            unit.LastTestPassed ? "Исправен" : "Дефект аккумулятора";

        private static string Translate(string message)
        {
            switch (message)
            {
                case "Lamp in hands: Not tested": return "Фонарь в руках. F — свет, E / Q у стенда — поставить.";
                case "Lamp placed on bench. Run the test.": return "Фонарь на стенде. E у прибора — проверить.";
                case "Lamp retrieved from bench.": return "Фонарь в руках.";
                case "Lamp returned to the crate.": return "Фонарь возвращён в ящик.";
                case "Place a lamp on the bench first.": return "Сначала поставь фонарь на стенд.";
                case "Hands occupied. Store or place that lamp first.": return "Руки заняты. Сначала поставь фонарь.";
                case "Hands occupied; bench already contains a lamp.": return "Стенд и руки заняты.";
                case "Take a lamp from the crate first.": return "Сначала возьми фонарь из ящика.";
                default: return FirstDealText.Message(message);
            }
        }
    }
}
