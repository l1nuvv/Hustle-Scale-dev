namespace Interactables
{
    // Manual operation state shared by the M3 trial and the city session; no money or item creation.
    public sealed class FlashlightRepairState
    {
        public event System.Action Changed;
        public System.Func<bool> CanAct { get; set; }
        public enum ItemPlace { Supply, Held, Bench }
        public enum CoverPlace { Attached, Held, Tray }
        private readonly SolderingTrialState soldering;
        private readonly bool[] secured = { true, true };
        public ItemPlace Flashlight { get; private set; }
        public CoverPlace Cover { get; private set; }
        public bool ReturningCover { get; private set; }
        public bool LightOn { get; private set; }
        public bool Tested { get; private set; }
        public int CompletionCount { get; private set; }
        public bool Complete => CompletionCount == 1;
        public bool BothLoose => !secured[0] && !secured[1];
        public bool Assembled => Flashlight == ItemPlace.Bench && Cover == CoverPlace.Attached && secured[0] && secured[1];
        public bool HandsFree => Flashlight != ItemPlace.Held && Cover != CoverPlace.Held;
        public bool CanUseTools => Flashlight == ItemPlace.Bench && Cover == CoverPlace.Tray;
        public bool CanLiftCover => Flashlight == ItemPlace.Bench && Cover == CoverPlace.Attached && BothLoose;
        public bool IsSecured(int index) => index >= 0 && index < 2 && secured[index];

        public FlashlightRepairState(SolderingTrialState soldering)
        {
            this.soldering = soldering;
            Reset();
        }

        private bool EmptyTool => soldering.HeldTool == SolderingTrialState.Tool.None && (CanAct == null || CanAct());

        public bool TakeFlashlight()
        {
            if (Flashlight != ItemPlace.Supply || !EmptyTool) return false;
            Flashlight = ItemPlace.Held;
            Changed?.Invoke();
            return true;
        }

        public bool PlaceFlashlight()
        {
            if (Flashlight != ItemPlace.Held || !EmptyTool) return false;
            Flashlight = ItemPlace.Bench;
            Changed?.Invoke();
            return true;
        }

        public bool TurnFastener(int index)
        {
            if (index < 0 || index > 1 || Flashlight != ItemPlace.Bench ||
                Cover != CoverPlace.Attached || !EmptyTool) return false;
            secured[index] = !secured[index];
            LightOn = Tested = false;
            Changed?.Invoke();
            return true;
        }

        public bool LiftCover()
        {
            if (!CanLiftCover || !EmptyTool) return false;
            ReturningCover = false;
            Cover = CoverPlace.Held;
            LightOn = Tested = false;
            UpdateAccess();
            Changed?.Invoke();
            return true;
        }

        public bool PutCoverInTray()
        {
            if (Cover != CoverPlace.Held || !EmptyTool) return false;
            Cover = CoverPlace.Tray;
            UpdateAccess();
            Changed?.Invoke();
            return true;
        }

        public bool TakeCover()
        {
            if (Cover != CoverPlace.Tray || !EmptyTool) return false;
            ReturningCover = true;
            Cover = CoverPlace.Held;
            UpdateAccess();
            Changed?.Invoke();
            return true;
        }

        public bool ReplaceCover()
        {
            if (Cover != CoverPlace.Held || Flashlight != ItemPlace.Bench || !EmptyTool) return false;
            Cover = CoverPlace.Attached;
            UpdateAccess();
            Changed?.Invoke();
            return true;
        }

        public bool TogglePower()
        {
            if (!Assembled || !EmptyTool) return false;
            Tested = true;
            LightOn = soldering.Complete && !LightOn;
            if (LightOn && CompletionCount == 0) CompletionCount = 1;
            Changed?.Invoke();
            return true;
        }

        public void Restore(bool onBench, bool coverAttached, bool firstSecured, bool secondSecured, bool tested)
        {
            Flashlight = onBench ? ItemPlace.Bench : ItemPlace.Supply;
            Cover = coverAttached ? CoverPlace.Attached : CoverPlace.Tray;
            secured[0] = firstSecured; secured[1] = secondSecured;
            Tested = tested;
            LightOn = ReturningCover = false;
            CompletionCount = tested && soldering.Complete ? 1 : 0;
            UpdateAccess();
        }

        public void Normalize()
        {
            if (Flashlight == ItemPlace.Held) Flashlight = ItemPlace.Bench;
            if (Cover == CoverPlace.Held) Cover = CoverPlace.Tray;
            soldering.SelectTool(SolderingTrialState.Tool.None);
            LightOn = false;
            UpdateAccess();
            Changed?.Invoke();
        }

        private void UpdateAccess() => soldering.SetAccessible(CanUseTools);

        public void Reset()
        {
            soldering.Reset();
            Flashlight = ItemPlace.Supply;
            Cover = CoverPlace.Attached;
            secured[0] = secured[1] = true;
            ReturningCover = false;
            LightOn = Tested = false;
            CompletionCount = 0;
            UpdateAccess();
        }
    }
}
