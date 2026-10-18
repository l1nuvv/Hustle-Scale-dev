using UnityEngine;

namespace Interactables
{
    public enum FirstDealActionKind { Offers, Seller, DeliverA, DeliverB, DeliverRepeatB }

    public sealed class FirstDealAction : MonoBehaviour, IWorldInteraction
    {
        [SerializeField] private FirstDealView deal;
        [SerializeField] private FirstDealActionKind action;
        public string Prompt => action == FirstDealActionKind.Offers ? "E - Read offers (pauses shift)" :
            action == FirstDealActionKind.Seller ? "E - Seller | 3 lamps $180 | Battery $50" :
            "E - " + action + " | Bring tested working lamps here";
        public void Interact() => deal.Perform(action);
    }
}
