using System;
using UnityEngine;

namespace Interactables
{
    // Experiment only: coverage of the kitchen knife's cutting curve, independent of orders/saves.
    public sealed class SharpeningTrialState
    {
        public const int Sections = 12;
        public const float EdgeLength = 0.28f;
        public static readonly Vector3 WheelCenter = new Vector3(0, 1.25f, 0.14f);
        public const float WheelRadius = 0.13f;
        private const int SamplesPerSection = 8;
        // Blender's authored edge and marker positions are verified against this curve.
        public static Vector3 EdgePoint(float along) => new Vector3((along - .5f) * EdgeLength, 0, -.03f * Mathf.Pow(1 - along, 4));
        public static Vector3 EdgeSample(int section) => EdgePoint((section + .5f) / Sections);
        public static bool HasWheelClearance(Vector3 origin, float angle)
        {
            var rotation = Quaternion.Euler(angle, 0, 0);
            for (var i = 0; i <= Sections * SamplesPerSection; i++)
            {
                var point = origin + rotation * EdgePoint((float)i / (Sections * SamplesPerSection));
                var radial = new Vector2(point.y - WheelCenter.y, point.z - WheelCenter.z).magnitude - WheelRadius;
                var axial = Mathf.Abs(point.x) - .0225f;
                var outside = new Vector2(Mathf.Max(axial, 0), Mathf.Max(radial, 0)).magnitude;
                var distance = outside + Mathf.Min(Mathf.Max(axial, radial), 0);
                if (distance < .015f) return false;
            }
            return true;
        }
        public static Vector3 ConstrainOrigin(Vector3 origin, float angle)
        {
            var rotation = Quaternion.Euler(angle, 0, 0);
            var maxDepth = float.PositiveInfinity;
            for (var i = 0; i <= Sections * SamplesPerSection; i++)
            {
                var offset = rotation * EdgePoint((float)i / (Sections * SamplesPerSection));
                var point = origin + offset;
                if (Mathf.Abs(point.x) > .0225f) continue;
                var height = point.y - WheelCenter.y;
                if (Mathf.Abs(height) >= WheelRadius) continue;
                var surface = WheelCenter.z - Mathf.Sqrt(WheelRadius * WheelRadius - height * height);
                maxDepth = Mathf.Min(maxDepth, surface - offset.z);
            }
            origin.z = Mathf.Min(origin.z, maxDepth);
            return origin;
        }
        private readonly float[] passes = new float[Sections];
        private readonly bool[] contacts = new bool[Sections];
        public bool Placed { get; private set; }
        public bool Running { get; private set; }
        public bool Complete
        {
            get { for (var i = 0; i < Sections; i++) if (passes[i] < 1) return false; return true; }
        }
        public bool Contact { get; private set; }
        public bool Cutting { get; private set; }
        public Vector3 ContactPoint { get; private set; }
        public bool ContactNeedsWork { get; private set; }
        public int FinishedSections
        {
            get { var count = 0; for (var i = 0; i < Sections; i++) if (passes[i] >= 1) count++; return count; }
        }
        public float Progress
        {
            get { var sum = 0f; for (var i = 0; i < Sections; i++) sum += passes[i]; return sum / Sections; }
        }
        public float Coverage(int section) => passes[section];
        public bool SectionContact(int section) => contacts[section];
        public void Place() => Placed = true;
        public void ToggleMotor() { if (Placed) Running = !Running; StopContact(); }
        public void StopMotor() { Running = false; StopContact(); }
        public void StopContact()
        {
            Contact = false; Cutting = false; ContactNeedsWork = false;
            Array.Clear(contacts, 0, contacts.Length);
        }
        public void Reset() { Array.Clear(passes, 0, passes.Length); Placed = false; StopMotor(); }

        public void Sweep(Vector3 from, Vector3 to, float angle, float seconds, bool held,
            float targetAngle, float tolerance, float requiredTravel, float processingSpeed)
        {
            StopContact();
            if (!Placed || !Running || !held || seconds <= 0) return;
            var travel = Mathf.Abs(to.x - from.x);
            var edgeRotation = Quaternion.Euler(angle, 0, 0);
            // Teleports/re-entry cannot process an unseen path.
            if (Vector3.Distance(from, to) > 0.08f) return;
            var steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(from, to) / 0.002f));
            var credit = Mathf.Min(travel, Mathf.Max(0, processingSpeed) * seconds) / steps;
            for (var step = 1; step <= steps; step++)
            {
                var origin = Vector3.Lerp(from, to, (float)step / steps);
                for (var section = 0; section < Sections; section++)
                {
                    var touching = false;
                    var point = Vector3.zero;
                    for (var sample = 0; sample < SamplesPerSection; sample++)
                    {
                        point = origin + edgeRotation * EdgePoint((section + (sample + .5f) / SamplesPerSection) / Sections);
                        var radial = new Vector2(point.y - WheelCenter.y, point.z - WheelCenter.z);
                        if (Mathf.Abs(point.x) <= .0225f && Mathf.Abs(radial.magnitude - WheelRadius) <= .006f && point.z < WheelCenter.z)
                        { touching = true; break; }
                    }
                    if (!touching) continue;
                    Contact = true;
                    contacts[section] = true;
                    ContactPoint = point;
                    if (Mathf.Abs(angle - targetAngle) > tolerance || credit <= 0 || Complete) continue;
                    var previous = passes[section];
                    passes[section] = Mathf.Clamp01(previous + credit / Mathf.Max(0.001f, requiredTravel));
                    if (passes[section] > previous) Cutting = true;
                }
            }
            for (var i = 0; i < Sections; i++)
                if (contacts[i] && passes[i] < 1) ContactNeedsWork = true;
        }
    }
}
