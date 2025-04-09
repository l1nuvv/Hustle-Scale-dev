using UnityEngine;

namespace Player.Inventory
{
    public class Item
    {
        public int id;
        public string name;
        public Sprite sprite; // For UI
        public GameObject worldPrefab; // Item prefab in the world
        public GameObject heldPrefab; // Prefab in the player hand
        public int maxStack = 1;
        [TextArea] public string description;
    }
}
