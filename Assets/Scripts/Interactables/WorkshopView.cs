using System.Collections;
using Inventory;
using UnityEngine;
using UnityEngine.UI;

namespace Interactables
{
    // Scene adapter and presentation only; all mutations are validated by WorkshopSession.
    public class WorkshopView : MonoBehaviour
    {
        [SerializeField] private Transform hand;
        [SerializeField] private Transform crate;
        [SerializeField] private Transform dock;
        [SerializeField] private Transform bench;
        [SerializeField] private Transform[] lamps;
        [SerializeField] private Transform[] storageSlots;
        [SerializeField] private Light[] indicators;
        [SerializeField] private Text status;
        private WorkshopSession session;
        private string lastMessage = "Take the crate or select one lamp, then place it on the workbench.";
        private Coroutine feedback;
        public WorkshopSession Session => session;

        private void Awake() => session = new WorkshopSession();
        private void OnEnable() => session.Changed += Refresh;
        private void Start() => Refresh();
        private void OnDisable()
        {
            session.Changed -= Refresh;
            if (feedback != null) StopCoroutine(feedback);
            feedback = null;
            foreach (var light in indicators) if (light != null) light.enabled = false;
        }

        public string GetPrompt(WorkshopActionKind action, int index)
        {
            switch (action)
            {
                case WorkshopActionKind.Crate: return session.IsCrateCarried ? "E - Dock crate" : "E - Carry crate";
                case WorkshopActionKind.Unit:
                    var unit = session.Units[index];
                    return "E - Lamp " + (index + 1) + " | " + unit.KnownCondition +
                        (unit.Location == FlashlightLocation.Hands ? " | Return to crate" : " | Take from crate");
                case WorkshopActionKind.Bench: return session.OnBench == null ? "E - Place held lamp on bench" : "E - Retrieve bench lamp";
                case WorkshopActionKind.Test: return "E - Test bench lamp";
                case WorkshopActionKind.BuyBattery: return "E - Buy spare battery | $50";
                case WorkshopActionKind.Repair: return "E - Replace battery | 1 spare, +04:00 | Retest required";
                default: return "";
            }
        }

        public void Perform(WorkshopActionKind action, int index)
        {
            bool success;
            string message;
            switch (action)
            {
                case WorkshopActionKind.Crate: success = session.ToggleCrate(out message); break;
                case WorkshopActionKind.Unit: success = session.ToggleUnit(index, out message); break;
                case WorkshopActionKind.Bench: success = session.ToggleBench(out message); break;
                case WorkshopActionKind.Test: success = session.Test(out message); break;
                case WorkshopActionKind.BuyBattery: success = session.BuyBattery(out message); break;
                case WorkshopActionKind.Repair: success = session.Repair(out message); break;
                default: return;
            }
            lastMessage = (success ? "" : "Cannot: ") + message;
            Refresh();
            if (success && action == WorkshopActionKind.Test && !session.OnBench.LastTestPassed)
            {
                if (feedback != null) StopCoroutine(feedback);
                feedback = StartCoroutine(Blink(IndexOf(session.OnBench)));
            }
        }

        private int IndexOf(FlashlightUnit unit)
        {
            for (var i = 0; i < session.Units.Count; i++) if (session.Units[i] == unit) return i;
            return -1;
        }

        private void Refresh()
        {
            if (feedback != null) { StopCoroutine(feedback); feedback = null; }
            Attach(crate, session.IsCrateCarried ? hand : dock, session.IsCrateCarried ?
                new Vector3(0, -0.65f, 0.85f) : Vector3.zero);
            for (var i = 0; i < lamps.Length; i++)
            {
                var unit = session.Units[i];
                var parent = unit.Location == FlashlightLocation.Crate ? storageSlots[i] :
                    unit.Location == FlashlightLocation.Bench ? bench : hand;
                Attach(lamps[i], parent, unit.Location == FlashlightLocation.Hands ?
                    new Vector3(0.35f, -0.4f, 0.65f) : Vector3.zero);
                indicators[i].enabled = unit.IsTested && unit.LastTestPassed;
            }
            if (status == null) return;
            status.text = "T03 WORKSHOP | Post-purchase fixture: $120 and 3 owned lamps\n" +
                "Balance $" + session.Balance + " | Spare batteries " + session.SpareBatteries +
                " | Repair time added " + (session.RepairSeconds / 60).ToString("00") + ":00\n";
            for (var i = 0; i < session.Units.Count; i++)
                status.text += "Lamp " + (i + 1) + ": " + session.Units[i].KnownCondition +
                    " | " + session.Units[i].Location + "\n";
            status.text += lastMessage;
        }

        private static void Attach(Transform target, Transform parent, Vector3 offset)
        {
            target.SetParent(parent, false);
            target.localPosition = offset;
            target.localRotation = Quaternion.identity;
        }

        private IEnumerator Blink(int index)
        {
            for (var i = 0; i < 3; i++)
            {
                indicators[index].enabled = true;
                yield return new WaitForSeconds(0.12f);
                indicators[index].enabled = false;
                yield return new WaitForSeconds(0.12f);
            }
            feedback = null;
        }
    }
}
