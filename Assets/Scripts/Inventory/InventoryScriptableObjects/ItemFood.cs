using UnityEngine;

namespace Inventory.InventoryScriptableObjects
{
    [CreateAssetMenu(fileName = "Item Food", menuName = "Inventory/Items/New Item Food")]
    public class ItemFood : Item
    {
        public int healAmount;
    }
}
