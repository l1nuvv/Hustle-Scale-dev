using Inventory.InventoryScriptableObjects;
using UnityEngine;

namespace Player
{
    public class PlayerEquipment : MonoBehaviour
    {
        public Transform handSlot;
        private GameObject _currentItem;

        public void Equip(Item item)
        {
            if (_currentItem != null)
            {
                Destroy(_currentItem);
            }

            if (item.heldPrefab != null)
            {
                _currentItem = Instantiate(item.heldPrefab, handSlot);
            }
        }
    }
}
