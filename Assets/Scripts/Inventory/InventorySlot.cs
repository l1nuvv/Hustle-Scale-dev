namespace Inventory
{
    [System.Serializable]
    public class InventorySlot
    {
        public IItem item;
        public int amount;

        public bool IsEmpty => item == null;
        public bool CanAddStack(IItem itemToAdd) => !IsEmpty && item.Id == itemToAdd.Id && amount < item.MaxStack;
    }
}
