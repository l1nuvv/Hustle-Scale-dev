using UnityEngine;

namespace Inventory.InventoryScriptableObjects
{
    public enum ItemType
    {
        Default,
        Food,
        Weapon
    }

    public class Item : ScriptableObject
    {
        public int id;
        // Serialized item display name, independent of the Unity asset name.
        public new string name;
        public Sprite icon; // For UI
        public GameObject worldPrefab; // Item prefab in the world
        public GameObject heldPrefab; // Prefab in the player hand
        public int maxStack = 1;
        [TextArea] public string description;
        public ItemType itemType;
    }
}
