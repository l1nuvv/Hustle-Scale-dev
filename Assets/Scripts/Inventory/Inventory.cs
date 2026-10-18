using System.Collections.Generic;
using Inventory.InventoryScriptableObjects;
using UnityEngine;

namespace Inventory
{
    public class Inventory : MonoBehaviour
    {
        public static Inventory Instance;
        public List<InventorySlot> slots = new();
        public int maxSlots = 10;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public bool AddItem(Item item, int amount = 1)
        {
            foreach (var slot in slots)
            {
                if (slot.CanAddStack(item))
                {
                    slot.amount += amount;
                    return true;
                }
            }

            if (slots.Count < maxSlots)
            {
                slots.Add(gameObject.AddComponent<InventorySlot>());
                return true;
            }

            Debug.LogWarning("Inventory is full!");
            return false;
        }

        public void RemoveItem(int slotIndex, int amount = 1)
        {
            if (slotIndex < 0 || slotIndex >= slots.Count)
            {
                return;
            }

            slots[slotIndex].amount -= amount;
            if (slots[slotIndex].amount <= 0)
            {
                slots.RemoveAt(slotIndex);
            }
        }
    }
}
