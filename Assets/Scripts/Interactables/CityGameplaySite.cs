using UnityEngine;

namespace Interactables
{
    // Configuration only. All instances operate on the scene's existing WorkshopSession.
    public sealed class CityGameplaySite : MonoBehaviour
    {
        [SerializeField] private CityTradeKind role;
        [SerializeField] private Transform crateDock;
        [SerializeField] private Transform benchItem;
        [SerializeField] private CityTradeReaction reaction;
        public CityTradeKind Role => role;
        public Transform CrateDock => crateDock;
        public Transform BenchItem => benchItem;
        public CityTradeReaction Reaction => reaction;

        public bool ValidateConfiguration()
        {
            if ((role == CityTradeKind.Seller || role == CityTradeKind.Workshop) &&
                (crateDock == null || !crateDock.IsChildOf(transform)))
                return Error("Crate Dock must be a child of this prefab.");
            if (role == CityTradeKind.Workshop && (benchItem == null || !benchItem.IsChildOf(transform)))
                return Error("Bench Item must be a child of this prefab.");
            if (!System.Array.Exists(GetComponentsInChildren<CityTradePoint>(true), point => point.Kind == role))
                return Error("At least one CityTradePoint with a collider and matching Role is required.");
            return true;
        }

        private bool Error(string reason)
        {
            Debug.LogError($"City site '{name}': {reason}", this);
            return false;
        }
    }
}
