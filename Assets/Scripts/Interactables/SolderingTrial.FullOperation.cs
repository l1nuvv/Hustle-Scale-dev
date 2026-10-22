using UnityEngine;

namespace Interactables
{
    public sealed partial class SolderingTrial
    {
        public enum RepairTarget { None, Flashlight, WorkMat, Cover, Tray, Fastener0, Fastener1, Power }
        private enum RepairMotion { None, TakeFlashlight, PlaceFlashlight, LiftCover, PutCover, TakeCover, ReplaceCover }
        private FlashlightRepairState repair;
        private Transform flashlight, cover, powerSwitch;
        private readonly Transform[] fasteners = new Transform[2];
        private readonly BoxCollider[] repairHits = new BoxCollider[8];
        private Light flashlightBeam;
        private Material lensMaterial;
        private RepairTarget repairHover;
        private RepairMotion repairMotion;
        private Transform movingPart;
        private Vector3 partStart, partEnd;
        private float partTime, fasteningTime;
        private int fasteningIndex = -1;
        private bool fasteningReleased = true;
        private bool workPaused;
        private static Vector3 SupplyPosition => Joint + new Vector3(0, 0, -0.38f);
        private static Vector3 HeldFlashlight => Joint + new Vector3(0.40f, 0.14f, -0.30f);
        private static Vector3 HeldCover => Joint + new Vector3(0.40f, 0.16f, -0.28f);
        private static Vector3 CoverTray => Joint + new Vector3(-0.56f, -0.04f, 0.35f);
        public bool IsFullOperation => fullOperation;
        public FlashlightRepairState Repair => repair;
        public bool RepairAnimating => repairMotion != RepairMotion.None;
        public RepairTarget HoveredRepairTarget => repairHover;
        public bool BeamEnabled => flashlightBeam != null && flashlightBeam.enabled;
        public bool WorkPaused => workPaused;
        public Vector2 RepairScreenPosition(RepairTarget target) =>
            view.WorldToScreenPoint(repairHits[(int)target].bounds.center);

        private Transform FindPart(string name)
        {
            foreach (var part in flashlight.GetComponentsInChildren<Transform>())
                if (part.name == name) return part;
            throw new System.InvalidOperationException("M3 authored part missing: " + name);
        }

        private BoxCollider RepairHit(RepairTarget target, Transform parent, Vector3 center, Vector3 size)
        {
            var hit = parent.gameObject.AddComponent<BoxCollider>();
            hit.isTrigger = true;
            hit.center = center;
            hit.size = size;
            repairHits[(int)target] = hit;
            return hit;
        }

        private void BuildFullOperation()
        {
            repair = new FlashlightRepairState(state);
            cover = FindPart("M3_Cover");
            fasteners[0] = FindPart("M3_Fastener0");
            fasteners[1] = FindPart("M3_Fastener1");
            powerSwitch = FindPart("M3_PowerSwitch");
            cover.SetParent(transform, true);
            RepairHit(RepairTarget.Flashlight, flashlight, new Vector3(0, -0.01f, 0), new Vector3(0.74f, 0.18f, 0.36f));
            RepairHit(RepairTarget.Cover, cover, new Vector3(-0.025f, 0.036f, 0), new Vector3(0.56f, 0.04f, 0.28f));
            for (var i = 0; i < 2; i++)
                RepairHit((RepairTarget)((int)RepairTarget.Fastener0 + i), fasteners[i], Vector3.zero, new Vector3(0.085f, 0.06f, 0.085f));
            RepairHit(RepairTarget.Power, powerSwitch, Vector3.zero, new Vector3(0.085f, 0.05f, 0.075f));
            var mat = Part("M3 placement mat", transform, PrimitiveType.Cube, new Vector3(0, 0.998f, 0),
                new Vector3(0.79f, 0.008f, 0.34f), Material(new Color(0.12f, 0.22f, 0.25f)));
            RepairHit(RepairTarget.WorkMat, mat, Vector3.zero, new Vector3(1, 1, 1));
            var tray = Part("M3 cover tray", transform, PrimitiveType.Cube, new Vector3(-0.56f, 0.998f, 0.35f),
                new Vector3(0.65f, 0.012f, 0.34f), Material(new Color(0.15f, 0.24f, 0.28f)));
            RepairHit(RepairTarget.Tray, tray, Vector3.zero, Vector3.one);
            Label("КРЫШКА", new Vector3(-0.56f, 1.015f, 0.54f), 0.006f);
            Label("МЕСТО РЕМОНТА", new Vector3(0, 1.015f, 0.23f), 0.005f);
            Part("M3 beam target", transform, PrimitiveType.Cube, new Vector3(0.93f, 1.22f, 0),
                new Vector3(0.035f, 0.43f, 0.42f), Material(new Color(0.68f, 0.70f, 0.65f)));
            var beam = new GameObject("M3 working flashlight beam");
            beam.transform.SetParent(flashlight, false);
            beam.transform.localPosition = new Vector3(0.34f, -0.01f, 0);
            beam.transform.localRotation = Quaternion.LookRotation(Vector3.right);
            flashlightBeam = beam.AddComponent<Light>();
            flashlightBeam.type = LightType.Spot;
            flashlightBeam.range = 3;
            flashlightBeam.spotAngle = 48;
            flashlightBeam.intensity = 7;
            flashlightBeam.color = new Color(1, 0.91f, 0.71f);
            flashlightBeam.enabled = false;
            var lens = FindPart("M3Body_Opaque lens diffuser").GetComponent<MeshRenderer>();
            lensMaterial = new Material(lens.sharedMaterial);
            resources.Add(lensMaterial);
            lensMaterial.EnableKeyword("_EMISSION");
            lens.sharedMaterial = lensMaterial;
            SyncRepairParts();
        }

        private bool TargetAvailable(RepairTarget target)
        {
            if (RepairAnimating || IsToolAnimating || state.HeldTool != SolderingTrialState.Tool.None) return false;
            switch (target)
            {
                case RepairTarget.Flashlight: return repair.Flashlight == FlashlightRepairState.ItemPlace.Supply;
                case RepairTarget.WorkMat: return repair.Flashlight == FlashlightRepairState.ItemPlace.Held || repair.Cover == FlashlightRepairState.CoverPlace.Held;
                case RepairTarget.Cover: return repair.CanLiftCover || repair.Cover == FlashlightRepairState.CoverPlace.Tray;
                case RepairTarget.Tray: return repair.Cover == FlashlightRepairState.CoverPlace.Held;
                case RepairTarget.Fastener0:
                case RepairTarget.Fastener1: return repair.Flashlight == FlashlightRepairState.ItemPlace.Bench && repair.Cover == FlashlightRepairState.CoverPlace.Attached;
                case RepairTarget.Power: return repair.Assembled;
                default: return false;
            }
        }

        private void UpdateRepairInput(Ray ray, float seconds)
        {
            TickRepairMotion(seconds);
            repairHover = RepairTarget.None;
            var distance = float.MaxValue;
            for (var i = 1; i < repairHits.Length; i++)
            {
                var target = (RepairTarget)i;
                if (!TargetAvailable(target) || !repairHits[i].Raycast(ray, out var hit, 3) || hit.distance >= distance) continue;
                repairHover = target;
                distance = hit.distance;
            }
            TickFastener(seconds, lower.IsPressed());
            if (repairHover == RepairTarget.Power && lower.WasPressedThisFrame()) repair.TogglePower();
        }

        private bool HandleRepairTake()
        {
            if (state.HeldTool != SolderingTrialState.Tool.None) return false;
            switch (repairHover)
            {
                case RepairTarget.Flashlight: StartRepairMotion(RepairMotion.TakeFlashlight, flashlight, HeldFlashlight); break;
                case RepairTarget.WorkMat:
                    if (repair.Flashlight == FlashlightRepairState.ItemPlace.Held)
                        StartRepairMotion(RepairMotion.PlaceFlashlight, flashlight, Joint);
                    else StartRepairMotion(RepairMotion.ReplaceCover, cover, Joint);
                    break;
                case RepairTarget.Cover:
                    StartRepairMotion(repair.CanLiftCover ? RepairMotion.LiftCover : RepairMotion.TakeCover, cover, HeldCover);
                    break;
                case RepairTarget.Tray: StartRepairMotion(RepairMotion.PutCover, cover, CoverTray); break;
            }
            return !repair.CanUseTools || repairHover != RepairTarget.None;
        }

        private void StartRepairMotion(RepairMotion motion, Transform part, Vector3 end)
        {
            if (part == cover) cover.SetParent(transform, true);
            repairMotion = motion;
            movingPart = part;
            partStart = part.localPosition;
            partEnd = end;
            partTime = 0;
            hoveredTool = SolderingTrialState.Tool.None;
            InterruptRepairAction();
        }

        private void TickRepairMotion(float seconds)
        {
            if (!RepairAnimating) return;
            partTime += seconds;
            var t = Mathf.Clamp01(partTime / 0.34f);
            movingPart.localPosition = Vector3.Lerp(partStart, partEnd, Mathf.SmoothStep(0, 1, t)) +
                Vector3.up * (Mathf.Sin(t * Mathf.PI) * 0.09f);
            movingPart.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * Mathf.PI) * 7);
            if (movingPart == flashlight && repair.Cover == FlashlightRepairState.CoverPlace.Attached)
                cover.SetPositionAndRotation(flashlight.position, flashlight.rotation);
            if (t < 1) return;
            switch (repairMotion)
            {
                case RepairMotion.TakeFlashlight: repair.TakeFlashlight(); break;
                case RepairMotion.PlaceFlashlight: repair.PlaceFlashlight(); break;
                case RepairMotion.LiftCover: repair.LiftCover(); break;
                case RepairMotion.PutCover: repair.PutCoverInTray(); break;
                case RepairMotion.TakeCover: repair.TakeCover(); break;
                case RepairMotion.ReplaceCover: repair.ReplaceCover(); break;
            }
            repairMotion = RepairMotion.None;
            movingPart = null;
            SyncRepairParts();
        }

        private void TickFastener(float seconds, bool pressing)
        {
            if (!pressing) { fasteningReleased = true; InterruptRepairAction(); return; }
            var index = repairHover == RepairTarget.Fastener0 ? 0 : repairHover == RepairTarget.Fastener1 ? 1 : -1;
            if (index < 0 || !fasteningReleased) { InterruptRepairAction(); return; }
            if (fasteningIndex != index) { InterruptRepairAction(); fasteningIndex = index; }
            fasteningTime += seconds;
            var t = Mathf.Clamp01(fasteningTime / 0.5f);
            var start = repair.IsSecured(index) ? 0 : 90;
            fasteners[index].localRotation = Quaternion.Euler(0, Mathf.Lerp(start, 90 - start, t), 0);
            if (t < 1) return;
            repair.TurnFastener(index);
            fasteningReleased = false; // Held button cannot undo/repeat the same turn.
            InterruptRepairAction();
        }

        private void InterruptRepairAction()
        {
            fasteningTime = 0;
            fasteningIndex = -1;
            if (repair == null) return;
            for (var i = 0; i < 2; i++)
                fasteners[i].localRotation = Quaternion.Euler(0, repair.IsSecured(i) ? 0 : 90, 0);
        }

        private void CancelRepairMotion()
        {
            repairMotion = RepairMotion.None;
            movingPart = null;
            repairHover = RepairTarget.None;
            workPaused = false;
            fasteningReleased = false;
            InterruptRepairAction();
            SyncRepairParts();
        }

        private void SyncRepairParts()
        {
            if (RepairAnimating) return;
            flashlight.localPosition = repair.Flashlight == FlashlightRepairState.ItemPlace.Supply ? SupplyPosition :
                repair.Flashlight == FlashlightRepairState.ItemPlace.Held ? HeldFlashlight : Joint;
            flashlight.localRotation = Quaternion.identity;
            var parent = repair.Cover == FlashlightRepairState.CoverPlace.Attached ? flashlight : transform;
            if (cover.parent != parent) cover.SetParent(parent, true);
            cover.localPosition = repair.Cover == FlashlightRepairState.CoverPlace.Attached ? Vector3.zero :
                repair.Cover == FlashlightRepairState.CoverPlace.Tray ? CoverTray : HeldCover;
            cover.localRotation = Quaternion.identity;
        }

        private void ResetRepair()
        {
            repair.Reset();
            CancelRepairMotion();
        }

        private void RefreshRepairFeedback()
        {
            SyncRepairParts();
            flashlightBeam.enabled = repair.LightOn;
            lensMaterial.SetColor("_EmissionColor", repair.LightOn ? new Color(1, 0.88f, 0.58f) * 3 : Color.black);
            powerSwitch.localRotation = Quaternion.Euler(0, 0, repair.LightOn ? -12 : 12);
            // Amber outlines are reserved for objects that can actually be picked up.
            if (hoveredTool == SolderingTrialState.Tool.None)
                outline?.Focus(active && focused && Application.isFocused && Time.timeScale > 0 && !workPaused && !RepairAnimating &&
                    (repairHover == RepairTarget.Flashlight || repairHover == RepairTarget.Cover) ?
                    repairHover == RepairTarget.Flashlight ? flashlight : cover : null, view);
            pointer.gameObject.SetActive(active && repair.CanUseTools && !state.Complete &&
                state.HeldTool != SolderingTrialState.Tool.None && !IsToolAnimating);
            if (!state.Accessible)
            {
                placedSolder.gameObject.SetActive(false);
                bead.gameObject.SetActive(false);
            }
        }

        private string RepairInstruction()
        {
            if (workPaused) return "Пауза · P — продолжить · Esc — отойти";
            if (RepairAnimating) return repairMotion == RepairMotion.LiftCover ? "Снимаем крышку…" :
                repairMotion == RepairMotion.ReplaceCover ? "Возвращаем крышку…" : "Переносим деталь…";
            if (IsToolAnimating) return null;
            if (fasteningIndex >= 0) return repair.IsSecured(fasteningIndex) ? "Поворачиваем крепление…" : "Закрепляем корпус…";
            if (repairHover == RepairTarget.Power) return repair.Tested && !state.Complete
                ? "Света нет: соединение повреждено. Откройте корпус" :
                repair.LightOn ? "Свет работает ✓ · ЛКМ — выключить" : "ЛКМ — включить фонарик";
            if (repairHover == RepairTarget.Fastener0 || repairHover == RepairTarget.Fastener1)
                return "Удерживайте ЛКМ — повернуть крепление на ¼";
            if (repair.Flashlight == FlashlightRepairState.ItemPlace.Supply)
                return "E — взять фонарик · сначала припой, затем паяльник";
            if (repair.Flashlight == FlashlightRepairState.ItemPlace.Held) return "Наведите на коврик — E поставить фонарик";
            if (repair.Cover == FlashlightRepairState.CoverPlace.Held)
                return repairHover == RepairTarget.WorkMat ? "E — вернуть крышку на корпус" : "E над лотком слева — положить крышку";
            if (repair.Cover == FlashlightRepairState.CoverPlace.Attached)
            {
                if (repair.Complete && repair.Assembled) return "Фонарик проверен: свет работает · R — повторить";
                if (repair.Tested && !state.Complete) return "Света нет: откройте корпус и запаяйте соединение";
                if (repair.Assembled) return state.Complete ? "ЛКМ по выключателю — проверить свет" : "Ослабьте два крепления — удерживайте ЛКМ";
                if (repair.BothLoose && !state.Complete) return "E по крышке — снять; внутри сначала припой";
                if (repair.BothLoose) return "Закрепите оба крепления или снимите крышку — E";
                return state.Complete ? "Закрепите второе крепление — ЛКМ" : "Ослабьте второе крепление — ЛКМ";
            }
            if (state.Complete) return state.HeldTool != SolderingTrialState.Tool.None
                ? "Пайка готова. Положите инструмент — E" : "E — взять крышку из лотка слева";
            if (repairHover == RepairTarget.Cover) return "E — взять крышку (соединение ещё повреждено)";
            return null; // Existing M2 material / heat / physical-contact instructions.
        }
    }
}
