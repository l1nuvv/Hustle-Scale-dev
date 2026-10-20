using Player.Movement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Player
{
    // Only the city owns this exploration overlay. No workshop session is started.
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(PlayerController))]
    public sealed class CityGreyboxWalk : MonoBehaviour
    {
        private PlayerController _player;
        private bool _paused;
        private static readonly string[] Headings = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };

        private void Awake() => _player = GetComponent<PlayerController>();

        private void Update()
        {
            if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;
            SetPaused(!_paused);
        }

        public void SetPaused(bool paused)
        {
            _paused = paused;
            _player.SetMovementEnabled(!paused);
            _player.SetCameraControlEnabled(!paused);
            _player.SetCursorLocked(!paused);
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) SetPaused(true);
        }

        private void OnGUI()
        {
            var p = transform.position;
            var district = p.z >= 96 ? (p.x < 0 ? "RESIDENTIAL" : "WAREHOUSES") :
                p.z <= -96 ? (p.x < 0 ? "GARAGES" : "PARK / OUTSKIRTS") : "MARKET";
            var heading = Headings[Mathf.RoundToInt(transform.eulerAngles.y / 45) % Headings.Length];
            var baseDistance = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(-65, 30));
            GUI.Box(new Rect(12, 12, 460, 110), "CITY GREYBOX | " + district +
                "\nWASD / Mouse / Shift | Esc: pause / resume" +
                "\nTrade signs are placeholders. No active deals." +
                "\nX " + p.x.ToString("F0") + "   Z " + p.z.ToString("F0") + "   North = +Z" +
                "\nFacing " + heading + " | BASE " + baseDistance.ToString("F0") + "m straight line");
            if (_paused)
                GUI.Box(new Rect(Screen.width / 2f - 180, Screen.height / 2f - 40, 360, 80),
                    "PAUSED\nEsc to resume and capture mouse");
        }
    }
}
