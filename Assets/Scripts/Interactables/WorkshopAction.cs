using UnityEngine;

namespace Interactables
{
    public enum WorkshopActionKind { Crate, Unit, Bench, Test, BuyBattery, Repair }

    public class WorkshopAction : MonoBehaviour, IWorldInteraction, IPickupTarget
    {
        [SerializeField] private WorkshopView workshop;
        [SerializeField] private WorkshopActionKind action;
        [SerializeField] private int unitIndex;
        public WorkshopActionKind Kind => action;
        public Transform PickupTarget => workshop != null ? workshop.PickupTarget(action, unitIndex) : null;

        public string Prompt => workshop.GetPrompt(action, unitIndex);
        public void Interact() => workshop.Perform(action, unitIndex);
    }
}
