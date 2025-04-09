using Player.Inventory;
using UnityEngine;

public class PlayerEquipment : MonoBehaviour
{
    public Transform handSlot;
    private GameObject currentItem;

    public void Equip(Item item)
    {
        if (currentItem != null) Destroy(currentItem);
        
        if (item.heldPrefab != null) currentItem = Instantiate(item.heldPrefab, handSlot);
    }
}
