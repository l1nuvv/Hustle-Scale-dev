using Player.Movement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Player
{
    // City persistence is independent of the closed FIRST DEAL presentation.
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(PlayerController))]
    public sealed class CityGreyboxWalk : MonoBehaviour
    {
        [SerializeField] private GameObject worklightPrefab;
        [SerializeField] private GameObject workerPrefab, workbenchPrefab, testerPrefab;
        private PlayerController _player;
        private bool _paused;
        private bool _confirmNewGame;
        private CitySaveController _save;
        private Interactables.CityTradeView _trade;
        public bool IsPaused => _paused;
        public GameObject WorklightPrefab => worklightPrefab;
        public GameObject WorkerPrefab => workerPrefab;
        public GameObject WorkbenchPrefab => workbenchPrefab;
        public GameObject TesterPrefab => testerPrefab;

        private void Awake()
        {
            _player = GetComponent<PlayerController>();
            _save = GetComponent<CitySaveController>();
            if (_save == null) _save = gameObject.AddComponent<CitySaveController>();
            _trade = gameObject.AddComponent<Interactables.CityTradeView>();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (_trade.MenuOpen) { _trade.CloseMenu(); return; }
                if (_confirmNewGame) _confirmNewGame = false;
                else SetPaused(!_paused);
                return;
            }
            if (!_paused) return;
            if (_confirmNewGame)
            {
                if (keyboard.enterKey.wasPressedThisFrame) { _confirmNewGame = false; _save.NewGame(); }
                return;
            }
            if (keyboard.f5Key.wasPressedThisFrame) _save.Save();
            else if (keyboard.f9Key.wasPressedThisFrame) _save.Load(false);
            else if (keyboard.bKey.wasPressedThisFrame) _save.Load(true);
            else if (keyboard.nKey.wasPressedThisFrame) _confirmNewGame = true;
        }

        public void SetPaused(bool paused)
        {
            if (!paused && _save != null && _save.Session == null) paused = true;
            var changed = _paused != paused;
            _paused = paused;
            if (_save != null && _save.Session != null) _save.Session.SetPaused(paused);
            _player.SetMovementEnabled(!paused);
            _player.SetCameraControlEnabled(!paused);
            _player.SetCursorLocked(!paused);
            if (changed && paused && _save != null) _save.SaveOnPause();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) SetPaused(true);
        }

        private void OnGUI()
        {
            if (_trade.MenuOpen) return;
            if (!_paused) return;
            var width = Mathf.Min(600, Screen.width - 24);
            var height = Mathf.Min(310, Screen.height - 24);
            GUILayout.BeginArea(new Rect((Screen.width - width) / 2, (Screen.height - height) / 2, width, height), GUI.skin.box);
            GUILayout.Label("ГОРОД · ПАУЗА");
            GUILayout.Label("WASD — идти · мышь — осмотр · Shift — бег · E — взаимодействие · Tab — карта и имущество");
            if (_confirmNewGame)
            {
                GUILayout.Label("Начать новую игру? Деньги, предметы и сделки текущей игры будут сброшены.");
                if (GUILayout.Button("Подтвердить новую игру · Enter")) { _confirmNewGame = false; _save.NewGame(); }
                if (GUILayout.Button("Назад · Esc")) _confirmNewGame = false;
            }
            else
            {
                GUI.enabled = _save.Session != null;
                if (GUILayout.Button("Продолжить · Esc")) SetPaused(false);
                if (GUILayout.Button("Сохранить · F5")) _save.Save();
                GUI.enabled = true;
                if (GUILayout.Button("Загрузить основной файл · F9")) _save.Load(false);
                GUI.enabled = _save.HasBackup;
                if (GUILayout.Button("Загрузить резервную копию · B")) _save.Load(true);
                GUI.enabled = true;
                if (GUILayout.Button("Новая игра… · N")) _confirmNewGame = true;
            }
            GUILayout.Label(_save.Message, new GUIStyle(GUI.skin.label) { wordWrap = true });
            GUILayout.EndArea();
        }
    }
}
