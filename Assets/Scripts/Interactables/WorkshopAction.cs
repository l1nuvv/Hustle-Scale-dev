using UnityEngine;

namespace Interactables
{
    public enum WorkshopActionKind { Crate, Unit, Bench, Test, BuyBattery, Repair }

    public class WorkshopAction : MonoBehaviour, IWorldInteraction
    {
        [SerializeField] private WorkshopView workshop;
        [SerializeField] private WorkshopActionKind action;
        [SerializeField] private int unitIndex;

        public string Prompt => workshop.GetPrompt(action, unitIndex);
        public void Interact() => workshop.Perform(action, unitIndex);
    }
}
