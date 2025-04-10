using UnityEngine;

namespace Inventory
{
    public interface IItem
    {
        int Id { get; }
        string Name { get; }
        Sprite Icon { get; }
        GameObject WorldPrefab { get; } // Item prefab in the world
        GameObject HeldPrefab { get; } // Prefab in the player hand
        int MaxStack { get; }
        string Description { get; }
    }
}
