using System;
using Inventory.InventoryScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Inventory
{
    [System.Serializable]
    public class InventorySlot : MonoBehaviour
    {
        public Item item;
        public int amount;
        public GameObject iconGameObject;
        public TMP_Text itemAmountText;
        public bool IsEmpty
        {
            get => item == null;
            set => throw new NotImplementedException();
        }

        private void Awake()
        {
            iconGameObject = transform.GetChild(0).gameObject;
            itemAmountText = transform.GetChild(1).gameObject.GetComponent<TMP_Text>();
        }

        public void SetIcon(Sprite icon)
        {
            iconGameObject.GetComponent<Image>().color = new Color(1, 1, 1, 1);
            iconGameObject.GetComponent<Image>().sprite = icon;
        }
        public bool CanAddStack(Item itemToAdd) => !IsEmpty && item.id == itemToAdd.id && amount < item.maxStack;
    }
}
