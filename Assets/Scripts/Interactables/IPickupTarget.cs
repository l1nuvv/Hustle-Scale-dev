using UnityEngine;

namespace Interactables
{
    // Null means the current interaction cannot take an item. This query never mutates inventory.
    public interface IPickupTarget
    {
        Transform PickupTarget { get; }
    }
}
