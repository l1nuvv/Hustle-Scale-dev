using Inventory;
using UnityEngine;

namespace Interactables
{
    public class InventoryPickup : MonoBehaviour
    {
        public Item item;

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                bool wasPickedUp = Inventory.Inventory.instance.AddItem(item);
                if (wasPickedUp) Destroy(gameObject);
            }
        }
    }
}
