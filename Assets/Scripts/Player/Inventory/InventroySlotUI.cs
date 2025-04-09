using UnityEngine;
using UnityEngine.UI;

namespace Player.Inventory
{
    public class InventorySlotUI : MonoBehaviour
    {
        public Image icon;
        public Text amountText;

        public void Setup(InventorySlot slot)
        {
            icon.sprite = slot.item.sprite;
            icon.enabled = true;
            amountText.text = slot.amount > 1 ? slot.amount.ToString() : "";
        }

        public void Clear()
        {
            icon.sprite = null;
            icon.enabled = false;
            amountText.text = "";
        }
    }
}
