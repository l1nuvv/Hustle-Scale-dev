using UnityEngine;

namespace Interactables
{
    // An interaction probe for T02; no inventory or trade rules live here.
    public class GrayboxInteractable : MonoBehaviour
    {
        [SerializeField] private Light indicator;
        private bool _isOn;
        public bool IsOn => _isOn;
        public int InteractionCount { get; private set; }
        public string Prompt => _isOn ? "E - Switch off test light" : "E - Switch on test light";
        public void Interact()
        {
            _isOn = !_isOn;
            InteractionCount++;
            if (indicator != null) indicator.enabled = _isOn;
        }
    }
}
