using UnityEngine;

namespace Interactables
{
    public enum FirstDealActionKind { Offers, Seller, DeliverA, DeliverB, DeliverRepeatB }

    public sealed class FirstDealAction : MonoBehaviour, IWorldInteraction
    {
        [SerializeField] private FirstDealView deal;
        [SerializeField] private FirstDealActionKind action;
        public string Prompt => deal.GetPrompt(action);
        public void Interact() => deal.Perform(action);
    }
}
