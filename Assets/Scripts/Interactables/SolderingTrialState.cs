using UnityEngine;

namespace Interactables
{
    // Contact/heat state shared with the city session. The city supply callback authorizes material use.
    public sealed class SolderingTrialState
    {
        public event System.Action Changed;
        public System.Func<bool> Supply { get; set; }
        public System.Func<bool> OperatingAllowed { get; set; }
        public enum Tool { None, Iron, Solder }
        public const float HeatSeconds = 4f;
        public const float ContactSeconds = 2.5f;
        public Tool HeldTool { get; private set; }
        public float Heat { get; private set; }
        public float Progress { get; private set; }
        public bool HasSolder { get; private set; }
        public bool Powered { get; private set; }
        public bool Working { get; private set; }
        public bool Accessible { get; private set; } = true;
        public bool Ready => Heat >= 1f;
        public bool Complete => Progress >= 1f;
        public int CompletionCount { get; private set; }

        public void SelectTool(Tool tool)
        {
            var stopped = Working;
            HeldTool = tool;
            if (tool == Tool.Iron) Powered = true;
            Working = false;
            if (stopped) Changed?.Invoke();
        }

        public void SetAccessible(bool accessible)
        {
            Accessible = accessible;
            if (!accessible) Working = false;
        }

        public void Tick(float seconds, bool contact, bool operating)
        {
            var wasWorking = Working;
            Working = false;
            if (!operating || OperatingAllowed != null && !OperatingAllowed() || seconds <= 0 || float.IsNaN(seconds) || float.IsInfinity(seconds))
            { if (wasWorking) Changed?.Invoke(); return; }
            // Read readiness before heating: crossing the threshold cannot credit a cold interval.
            var wasReady = Ready;
            if (Powered) Heat = Mathf.Min(1f, Heat + seconds / HeatSeconds);
            if (!Accessible || !contact || Complete) { if (wasWorking) Changed?.Invoke(); return; }
            if (HeldTool == Tool.Solder && !HasSolder && (Supply == null || Supply()))
            { HasSolder = true; Changed?.Invoke(); }
            Working = HeldTool == Tool.Iron && wasReady && HasSolder;
            if (!Working) { if (wasWorking) Changed?.Invoke(); return; }
            Progress = Mathf.Min(1f, Progress + seconds / ContactSeconds);
            if (!Complete) return;
            CompletionCount++;
            Working = false;
            Changed?.Invoke();
        }

        public void Restore(bool hasSolder, float progress)
        {
            Reset();
            HasSolder = hasSolder;
            Progress = progress;
            CompletionCount = Complete ? 1 : 0;
        }

        public void Reset()
        {
            HeldTool = Tool.None;
            Heat = Progress = 0;
            HasSolder = Powered = Working = false;
            CompletionCount = 0;
        }
    }
}
