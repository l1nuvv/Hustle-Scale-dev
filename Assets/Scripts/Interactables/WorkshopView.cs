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
        [SerializeField] private bool firstDeal;
        [SerializeField] private Transform sellerDock;
        private WorkshopSession session;
        private string lastMessage = "Take the crate or select one lamp, then place it on the workbench.";
        private Coroutine feedback;
        private int blinkIndex = -1;
        public WorkshopSession Session => session;
        public bool IsFirstDeal => firstDeal;

        private void Awake() => session = firstDeal ? WorkshopSession.CreateFirstDeal() : new WorkshopSession();
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
            if (firstDeal && (action == WorkshopActionKind.Crate || action == WorkshopActionKind.Unit) &&
                !session.IsCrateCarried && Vector3.Distance(hand.position, crate.position) > 2.5f)
            {
                ShowMessage("Bring the crate to this station first.");
                return;
            }
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
                blinkIndex = IndexOf(session.OnBench);
                feedback = StartCoroutine(Blink(blinkIndex));
            }
        }

        private int IndexOf(FlashlightUnit unit)
        {
            for (var i = 0; i < session.Units.Count; i++) if (session.Units[i] == unit) return i;
            return -1;
        }

        public void ShowMessage(string message)
        {
            lastMessage = message;
            Refresh();
        }

        private void Refresh()
        {
            if (feedback != null && (blinkIndex < 0 || session.OnBench != session.Units[blinkIndex] ||
                !session.OnBench.IsTested || session.OnBench.LastTestPassed))
            { StopCoroutine(feedback); feedback = null; blinkIndex = -1; }
            var restingDock = firstDeal && !session.HasTransportedCrate ? sellerDock : dock;
            Attach(crate, session.IsCrateCarried ? hand : restingDock, session.IsCrateCarried ?
                new Vector3(0, -0.65f, 0.85f) : Vector3.zero);
            if (firstDeal)
                foreach (var collider in crate.GetComponentsInChildren<Collider>()) collider.enabled = !session.IsCrateCarried;
            for (var i = 0; i < lamps.Length; i++)
            {
                var unit = session.Units[i];
                var sold = unit.Location == FlashlightLocation.BuyerA || unit.Location == FlashlightLocation.BuyerB;
                lamps[i].gameObject.SetActive(!sold);
                if (sold) { indicators[i].enabled = false; continue; }
                var parent = unit.Location == FlashlightLocation.Crate || unit.Location == FlashlightLocation.Seller ? storageSlots[i] :
                    unit.Location == FlashlightLocation.Bench ? bench : hand;
                Attach(lamps[i], parent, unit.Location == FlashlightLocation.Hands ?
                    new Vector3(0.35f, -0.4f, 0.65f) : Vector3.zero);
                if (feedback == null || i != blinkIndex) indicators[i].enabled = unit.IsTested && unit.LastTestPassed;
            }
            if (status == null) return;
            status.text = (firstDeal ? "FIRST DEAL | $300 start | One lot: 3 lamps for $180\n" :
                "T03 WORKSHOP | Post-purchase fixture: $120 and 3 owned lamps\n") +
                "Balance $" + session.Balance + " | Spare batteries " + session.SpareBatteries +
                " | Repair time added " + (session.RepairSeconds / 60).ToString("00") + ":00\n";
            if (firstDeal)
            {
                status.text += "Shift " + Clock(session.ElapsedSeconds) +
                    (!session.ShiftStarted ? " | Starts on first exit from workshop" : session.IsPaused ? " | PAUSED" : " | Running") + "\n";
                foreach (DealOrderId id in System.Enum.GetValues(typeof(DealOrderId)))
                {
                    var order = session.Order(id);
                    status.text += id + ": " + order.State + " | " + order.Quantity + " tested working | $" + order.Payment +
                        " | before " + Clock(order.DeadlineSeconds) + " | left " + Clock(System.Math.Max(0, order.DeadlineSeconds - session.ElapsedSeconds)) + "\n";
                }
            }
            for (var i = 0; i < session.Units.Count; i++)
                status.text += "Lamp " + (i + 1) + ": " + session.Units[i].KnownCondition +
                    " | " + session.Units[i].Location + "\n";
            status.text += lastMessage;
        }

        public static string Clock(double seconds) => ((int)seconds / 60).ToString("00") + ":" + ((int)seconds % 60).ToString("00");

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
            blinkIndex = -1;
        }
    }
}
