using UnityEngine;

namespace Inventory
{
    public class Item : IItem
    {
        public int id;
        public string name;
        public Sprite icon; // For UI
        public GameObject worldPrefab; // Item prefab in the world
        public GameObject heldPrefab; // Prefab in the player hand
        public int maxStack = 1;
        [TextArea] public string description;
        
        // 
        public int Id => id;
        public string Name => name;
        public Sprite Icon => icon;
        public GameObject WorldPrefab => worldPrefab;
        public GameObject HeldPrefab => heldPrefab;
        public int MaxStack => maxStack;
        public string Description => description;
    }
}
