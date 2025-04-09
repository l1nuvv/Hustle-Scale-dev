namespace Player.Inventory
{
    [System.Serializable]
    public class InventorySlot
    {
        public Item item;
        public int amount;

        public bool IsEmpty => item == null;
        public bool CanAddStack(Item itemToAdd) => !IsEmpty && item.id == itemToAdd.id && amount < item.maxStack;
    }
}
