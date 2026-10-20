using UnityEngine;

namespace Interactables
{
    // Visual acknowledgement is deliberately independent of money/order transitions.
    public sealed class CityTradeReaction : MonoBehaviour
    {
        private CityTradeView view;
        private Animator animator;
        private Transform head;
        private Quaternion rest;
        private float remaining;
        private bool positive;
        public bool Reacting => remaining > 0;

        public void Bind(CityTradeView owner)
        {
            view = owner; animator = GetComponentInChildren<Animator>();
            foreach (var bone in GetComponentsInChildren<Transform>())
                if (bone.name == "Head") { head = bone; break; }
            if (head != null) rest = head.localRotation;
        }

        public void React(bool success) { positive = success; remaining = 0.9f; }

        private void LateUpdate()
        {
            if (view == null || view.Session == null) return;
            var paused = view.Session.IsPaused;
            if (animator != null) animator.speed = paused ? 0 : 1;
            if (paused || remaining <= 0) return;
            remaining = Mathf.Max(0, remaining - Time.deltaTime);
            var angle = Mathf.Sin((0.9f - remaining) / 0.9f * Mathf.PI * 2) * 9;
            if (head != null) head.localRotation = rest * Quaternion.Euler(positive ? angle : 0, positive ? 0 : angle, 0);
        }

        private void OnDisable()
        {
            remaining = 0;
            if (head != null) head.localRotation = rest;
            if (animator != null) animator.speed = 1;
        }
    }
}
