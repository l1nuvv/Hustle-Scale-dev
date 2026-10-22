using UnityEngine;

namespace Interactables
{
    // M2 experiment only. No inventory, money or persistent save state.
    public sealed class SolderingTrialState
    {
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
            HeldTool = tool;
            if (tool == Tool.Iron) Powered = true;
            Working = false;
        }

        public void SetAccessible(bool accessible)
        {
            Accessible = accessible;
            if (!accessible) Working = false;
        }

        public void Tick(float seconds, bool contact, bool operating)
        {
            Working = false;
            if (!operating || seconds <= 0 || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            // Read readiness before heating: crossing the threshold cannot credit a cold interval.
            var wasReady = Ready;
            if (Powered) Heat = Mathf.Min(1f, Heat + seconds / HeatSeconds);
            if (!Accessible || !contact || Complete) return;
            if (HeldTool == Tool.Solder) HasSolder = true;
            Working = HeldTool == Tool.Iron && wasReady && HasSolder;
            if (!Working) return;
            Progress = Mathf.Min(1f, Progress + seconds / ContactSeconds);
            if (!Complete) return;
            CompletionCount++;
            Working = false;
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
