using UnityEngine;

namespace Interactables
{
    public enum CityTradeKind { Seller, Battery, Buyback, A, B, Workshop, SellerDock, WorkshopDock, Crate, Lamp, Cargo }

    public sealed class CityTradePoint : MonoBehaviour, IWorldInteraction
    {
        public CityTradeView View { get; private set; }
        public CityTradeKind Kind { get; private set; }
        public int Index { get; private set; }
        private string prompt;
        public string Prompt => Kind == CityTradeKind.Cargo ? View.CargoPrompt :
            Kind == CityTradeKind.Workshop || Kind == CityTradeKind.Lamp && View.Session != null &&
            View.Session.Units[Index].Location == Inventory.FlashlightLocation.Bench ? View.WorkshopPrompt : prompt;
        public void Bind(CityTradeView view, CityTradeKind kind, int index = -1)
        {
            View = view; Kind = kind; Index = index;
            // The raycast reads this every frame. Static point text changes only on Bind.
            prompt = kind == CityTradeKind.Lamp ? "E — Подобрать фонарь " + (index + 1) : "E — " + CityTradeView.Title(kind);
        }
        public void Interact() => View.Open(this);
    }
}
