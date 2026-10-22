using UnityEngine;

namespace Interactables
{
    public enum CityTradeKind { Seller, Battery, Buyback, A, B, Workshop, SellerDock, WorkshopDock, Crate, Lamp, Cargo }

    public sealed class CityTradePoint : MonoBehaviour, IWorldInteraction, IPickupTarget
    {
        public CityTradeView View { get; private set; }
        [SerializeField] private CityTradeKind kind;
        [SerializeField] private int index = -1;
        public CityTradeKind Kind => kind;
        public int Index => index;
        public Transform PickupTarget => View != null ? View.PickupTarget(this) : null;
        private string prompt;
        public string Prompt => View == null ? "City point is not connected" : Kind == CityTradeKind.Cargo ? View.CargoPrompt :
            Kind == CityTradeKind.Workshop || Kind == CityTradeKind.Lamp && View.Session != null &&
            View.Session.CityUnit(Index)?.Location == Inventory.FlashlightLocation.Bench ? View.WorkshopPrompt : prompt;
        public void Bind(CityTradeView view, CityTradeKind kind, int index = -1)
        {
            View = view; this.kind = kind; this.index = index;
            // The raycast reads this every frame. Static point text changes only on Bind.
            prompt = kind == CityTradeKind.Crate ? "E — взять ящик · Tab рядом — содержимое" :
                kind == CityTradeKind.Lamp ? "E — Подобрать фонарь " + (index + 1) : "E — " + CityTradeView.Title(kind);
        }
        public void Interact() { if (View != null) View.Open(this); }
    }
}
