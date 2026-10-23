using UnityEngine;

namespace Interactables
{
    public sealed class CitySharpeningSite : MonoBehaviour
    {
        public SharpeningTrial Station;
        public GameObject Preview, Plaque, KnifePrefab;
        public bool PlaceAvailable()
        {
            foreach (var hit in Physics.OverlapBox(transform.TransformPoint(new Vector3(0, .85f, 0)), new Vector3(.53f, .35f, .34f), transform.rotation, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform)) return false;
            return true;
        }
        public void Refresh(bool owned, bool unlocked)
        {
            Station.gameObject.SetActive(owned);
            Preview.SetActive(!owned && unlocked);
            Plaque.SetActive(owned && !Station.Active);
            if (owned) Station.RefreshCityKnife();
        }
    }
}
