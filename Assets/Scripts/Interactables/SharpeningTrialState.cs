using System;
using UnityEngine;

namespace Interactables
{
    // Experiment only: coverage of a straight edge, independent of orders and saves.
    public sealed class SharpeningTrialState
    {
        public const int Sections = 12;
        public const float EdgeLength = 0.28f;
        public static readonly Vector3 WheelCenter = new Vector3(0, 1.25f, 0.14f);
        public const float WheelRadius = 0.13f;
        private readonly float[] passes = new float[Sections];
        public bool Placed { get; private set; }
        public bool Running { get; private set; }
        public bool Complete
        {
            get { for (var i = 0; i < Sections; i++) if (passes[i] < 1) return false; return true; }
        }
        public bool Contact { get; private set; }
        public bool Cutting { get; private set; }
        public Vector3 ContactPoint { get; private set; }
        public float Coverage(int section) => passes[section];
        public void Place() => Placed = true;
        public void ToggleMotor() { if (Placed) Running = !Running; StopContact(); }
        public void StopMotor() { Running = false; StopContact(); }
        public void StopContact() { Contact = false; Cutting = false; }
        public void Reset() { Array.Clear(passes, 0, passes.Length); Placed = false; StopMotor(); }

        public void Sweep(Vector3 from, Vector3 to, float angle, float seconds, bool held,
            float targetAngle, float tolerance, float requiredTravel, float processingSpeed)
        {
            StopContact();
            if (!Placed || !Running || !held || seconds <= 0) return;
            var travel = Mathf.Abs(to.x - from.x);
            // Teleports/re-entry cannot process an unseen path.
            if (Vector3.Distance(from, to) > 0.08f) return;
            var steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(from, to) / 0.002f));
            var credit = Mathf.Min(travel, Mathf.Max(0, processingSpeed) * seconds) / steps;
            for (var step = 1; step <= steps; step++)
            {
                var origin = Vector3.Lerp(from, to, (float)step / steps);
                for (var section = 0; section < Sections; section++)
                {
                    var point = origin + Vector3.right * ((section + 0.5f) / Sections - 0.5f) * EdgeLength;
                    var radial = new Vector2(point.y - WheelCenter.y, point.z - WheelCenter.z);
                    if (Mathf.Abs(point.x) > 0.0225f || Mathf.Abs(radial.magnitude - WheelRadius) > 0.006f || point.z >= WheelCenter.z) continue;
                    Contact = true;
                    ContactPoint = point;
                    if (Mathf.Abs(angle - targetAngle) > tolerance || credit <= 0 || Complete) continue;
                    passes[section] = Mathf.Clamp01(passes[section] + credit / Mathf.Max(0.001f, requiredTravel));
                    Cutting = true;
                }
            }
        }
    }
}
