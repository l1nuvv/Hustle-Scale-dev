using Inventory.InventoryScriptableObjects;
using UnityEngine;

namespace Inventory
{
    public class InventoryUI : MonoBehaviour
    {
        public Transform slotsParent;
        public GameObject slotPrefab;

        private void Start()
        {
            Inventory.Instance.AddItem(ScriptableObject.CreateInstance<Item>(), 3);
            UpdateUI();
        }

        public void UpdateUI()
        {
            foreach (Transform child in slotsParent)
            {
                Destroy(child.gameObject);
            }

            foreach (var slot in Inventory.Instance.slots)
            {
                var slotUI = Instantiate(slotPrefab, slotsParent).GetComponent<InventorySlotUI>();
                slotUI.Setup(slot);
            }
        }
    }
}
