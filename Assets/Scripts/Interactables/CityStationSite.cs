using UnityEngine;

namespace Interactables
{
    public sealed class CityStationSite : MonoBehaviour
    {
        [SerializeField] private SolderingTrial station;
        [SerializeField] private GameObject preview;
        [SerializeField] private GameObject plaque;
        [SerializeField] private Transform clearance;
        [SerializeField] private Vector3 clearanceHalfExtents = new Vector3(.49f, .44f, .36f);
        public SolderingTrial Station => station;

        public bool ValidateConfiguration()
        {
            if (station != null && preview != null && plaque != null && clearance != null &&
                station.transform.IsChildOf(transform) && preview.transform.IsChildOf(transform) &&
                plaque.transform.IsChildOf(transform) && clearance.IsChildOf(transform) &&
                clearanceHalfExtents.x > 0 && clearanceHalfExtents.y > 0 && clearanceHalfExtents.z > 0)
                return true;
            Debug.LogError($"City station '{name}': assign local Station, Preview, Plaque and Clearance; extents must be positive.", this);
            return false;
        }

        public bool PlaceAvailable()
        {
            var extents = Vector3.Scale(clearanceHalfExtents, clearance.lossyScale);
            foreach (var hit in Physics.OverlapBox(clearance.position, extents, clearance.rotation, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform)) return false;
            return true;
        }

        public void Refresh(bool owned, bool canBuy)
        {
            station.gameObject.SetActive(owned);
            preview.SetActive(false);
            plaque.SetActive(owned);
        }

        private void OnDrawGizmosSelected()
        {
            if (clearance == null) return;
            Gizmos.color = Color.yellow;
            Gizmos.matrix = clearance.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, clearanceHalfExtents * 2);
        }
    }
}
