using UnityEngine.UI;

namespace Inventory
{
    public class InventorySlotUI
    {
        public Image icon;
        public Text amountText;

        public void Setup(InventorySlot slot)
        {
            icon.sprite = slot.item.icon;
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
