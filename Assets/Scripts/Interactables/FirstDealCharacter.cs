using Inventory;
using UnityEngine;

namespace Interactables
{
    // Read-only presentation of the existing session; never owns trade rules.
    public sealed class FirstDealCharacter : MonoBehaviour
    {
        public enum Role { Seller, BuyerA, BuyerB }
        [SerializeField] private WorkshopView workshop;
        [SerializeField] private Role role;
        [SerializeField] private Transform head;
        [SerializeField] private Transform arm;
        private WorkshopSession session;
        private Quaternion headRest, armRest;
        private DealOrderState state;
        private bool purchased;
        private int batteries, boughtBack;
        private float reaction;

        private void Awake()
        {
            headRest = head.localRotation;
            armRest = arm.localRotation;
        }

        private void OnEnable() => Bind();
        private void Start() => Bind(); // All scene Awake callbacks have now run.

        private void Bind()
        {
            if (session != null || workshop == null || workshop.Session == null) return;
            session = workshop.Session;
            Snapshot();
            session.Changed += Refresh;
            Pose();
        }

        private DealOrderState CurrentState => role == Role.BuyerA ? session.Order(DealOrderId.A).State :
            session.Order(session.Order(DealOrderId.B).State == DealOrderState.Completed ? DealOrderId.RepeatB : DealOrderId.B).State;

        private int BoughtBackCount()
        {
            var count = 0;
            foreach (var unit in session.Units) if (unit.Location == FlashlightLocation.BoughtBack) count++;
            return count;
        }

        private void Snapshot()
        {
            state = CurrentState;
            purchased = session.LotPurchased;
            batteries = session.SpareBatteries;
            boughtBack = BoughtBackCount();
        }

        private void Refresh()
        {
            var changed = role == Role.Seller ? purchased != session.LotPurchased ||
                session.SpareBatteries > batteries || BoughtBackCount() > boughtBack : state != CurrentState;
            Snapshot();
            if (changed && !session.ShiftEnded) reaction = 0.8f;
            if (session.ShiftEnded) reaction = 0;
            Pose();
        }

        private void Update()
        {
            if (session == null || session.IsPaused || session.ShiftEnded || reaction <= 0) return;
            reaction = Mathf.Max(0, reaction - Time.deltaTime);
            Pose();
        }

        private void Pose()
        {
            var closed = role != Role.Seller && state != DealOrderState.Available && state != DealOrderState.Accepted;
            var pulse = Mathf.Sin((1 - reaction / 0.8f) * Mathf.PI);
            head.localRotation = headRest * Quaternion.Euler((closed ? 7 : 0) + pulse * 12, 0, 0);
            arm.localRotation = armRest * Quaternion.Euler(closed ? 12 : state == DealOrderState.Accepted && role != Role.Seller ? -12 : 0, 0, -pulse * 15);
        }

        private void OnDisable()
        {
            if (session != null) session.Changed -= Refresh;
            session = null;
            reaction = 0;
            head.localRotation = headRest;
            arm.localRotation = armRest;
        }
    }
}
