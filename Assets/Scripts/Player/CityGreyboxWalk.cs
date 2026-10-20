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
        public bool ConfirmNewGame => _confirmNewGame;

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
            if (!paused) _confirmNewGame = false;
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

        public void PauseAction(string action)
        {
            if (!_paused || _trade.MenuOpen) return;
            switch (action)
            {
                case "resume": SetPaused(false); break;
                case "save" when !_confirmNewGame: _save.Save(); break;
                case "load" when !_confirmNewGame: _save.Load(false); break;
                case "backup" when !_confirmNewGame && _save.HasBackup: _save.Load(true); break;
                case "new": _confirmNewGame = true; break;
                case "cancel": _confirmNewGame = false; break;
                case "confirm" when _confirmNewGame: _confirmNewGame = false; _save.NewGame(); break;
            }
        }
    }
}
