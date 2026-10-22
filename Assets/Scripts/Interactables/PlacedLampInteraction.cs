using UnityEngine;

namespace Interactables
{
    public sealed class PlacedLampInteraction : MonoBehaviour, IWorldInteraction, IPickupTarget
    {
        private WorkshopView workshop;
        public int Index { get; private set; }
        public Transform PickupTarget => workshop != null && workshop.Session.CanPickUpPlaced(Index) ? transform : null;
        public string Prompt => "E — Подобрать фонарь " + (Index + 1) + " · F — свет";
        public void Bind(WorkshopView view, int index) { workshop = view; Index = index; }
        public void Interact()
        {
            workshop.Session.PickUpPlaced(Index, out var message);
            workshop.ShowMessage(message);
        }
    }
}
