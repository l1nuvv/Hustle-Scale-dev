using UnityEngine.UI;

namespace Inventory
{
    public class InventorySlotUI
    {
        public Text AmountText;
        public Image Icon;

        public void Setup(InventorySlot slot)
        {
            Icon.sprite = slot.item.icon;
            Icon.enabled = true;
            AmountText.text = slot.amount > 1 ? slot.amount.ToString() : "";
        }

        public void Clear()
        {
            Icon.sprite = null;
            Icon.enabled = false;
            AmountText.text = "";
        }
    }
}
