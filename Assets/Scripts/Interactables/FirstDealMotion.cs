using UnityEngine;

namespace Interactables
{
    // Presentation only. Ownership and trade settlement never wait for this motion.
    internal sealed class FirstDealMotion
    {
        private readonly Transform target;
        private Transform parent;
        private Vector3 rest, start;
        private Quaternion startRotation;
        private float elapsed, duration, lift, turns;
        private bool initialized, hiding;
        public bool Active => duration > 0;

        public FirstDealMotion(Transform target) => this.target = target;

        public void Attach(Transform destination, Vector3 offset, bool animate)
        {
            if (initialized && parent == destination && rest == offset && !hiding) return;
            var wasInitialized = initialized;
            parent = destination;
            rest = offset;
            initialized = true;
            hiding = false;
            target.SetParent(parent, wasInitialized && animate);
            Begin(animate && wasInitialized ? 0.32f : 0, 0.045f, 0);
        }

        public void React(bool repair)
        {
            if (!initialized || hiding) return;
            Begin(repair ? 1.1f : 0.55f, repair ? 0.07f : 0.04f, repair ? 3 : 1);
        }

        public void Sell(Transform hand)
        {
            if (hiding) return;
            hiding = true;
            parent = hand;
            target.SetParent(hand, true);
            rest = target.localPosition + new Vector3(0, 0.12f, 0.65f);
            Begin(0.45f, 0.08f, 0);
        }

        private void Begin(float seconds, float height, float oscillations)
        {
            start = target.localPosition;
            startRotation = target.localRotation;
            elapsed = 0;
            duration = seconds;
            lift = height;
            turns = oscillations;
            if (duration == 0) Settle();
        }

        public void Tick(float delta)
        {
            if (!Active) return;
            elapsed += delta;
            var t = Mathf.Clamp01(elapsed / duration);
            var eased = t * t * (3 - 2 * t);
            target.localPosition = Vector3.Lerp(start, rest, eased) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * lift);
            target.localRotation = Quaternion.Slerp(startRotation, Quaternion.identity, eased) *
                Quaternion.Euler(0, 0, Mathf.Sin(t * Mathf.PI * 2 * turns) * 12 * (1 - t));
            if (t >= 1) Settle();
        }

        public void Settle()
        {
            duration = 0;
            if (!initialized || target == null) return;
            target.localPosition = rest;
            target.localRotation = Quaternion.identity;
            if (hiding) target.gameObject.SetActive(false);
        }
    }
}
