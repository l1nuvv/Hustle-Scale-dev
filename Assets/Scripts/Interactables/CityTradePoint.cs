using UnityEngine;

namespace Interactables
{
    public enum CityTradeKind { Seller, Battery, Buyback, A, B, Workshop, SellerDock, WorkshopDock, Crate, Lamp }

    public sealed class CityTradePoint : MonoBehaviour, IWorldInteraction
    {
        public CityTradeView View { get; private set; }
        public CityTradeKind Kind { get; private set; }
        public int Index { get; private set; }
        public string Prompt => Kind == CityTradeKind.Lamp ? "E — Подобрать фонарь " + (Index + 1) :
            "E — " + CityTradeView.Title(Kind);
        public void Bind(CityTradeView view, CityTradeKind kind, int index = -1)
        { View = view; Kind = kind; Index = index; }
        public void Interact() => View.Open(this);
    }
}
