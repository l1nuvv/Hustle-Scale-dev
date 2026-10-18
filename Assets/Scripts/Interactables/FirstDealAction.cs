using UnityEngine;

namespace Interactables
{
    public enum FirstDealActionKind { Offers, Seller, DeliverA, DeliverB, DeliverRepeatB }

    public sealed class FirstDealAction : MonoBehaviour, IWorldInteraction
    {
        [SerializeField] private FirstDealView deal;
        [SerializeField] private FirstDealActionKind action;
        public string Prompt => action == FirstDealActionKind.Offers ? "E — Открыть заказы (время остановится)" :
            action == FirstDealActionKind.Seller ? "E — Продавец: партия $180 / батарея $50 / выкуп $60–35" :
            action == FirstDealActionKind.DeliverA ? "E — Передать A два проверенных исправных фонаря" :
            action == FirstDealActionKind.DeliverB ? "E — Передать Б один проверенный исправный фонарь" : "E — Выполнить повторный заказ Б";
        public void Interact() => deal.Perform(action);
    }
}
