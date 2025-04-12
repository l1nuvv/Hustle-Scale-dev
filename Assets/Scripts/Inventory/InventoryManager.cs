using System.Collections.Generic;
using Inventory.InventoryScriptableObjects;
using Player.Movement;
using UnityEngine;

namespace Inventory
{
    public class InventoryManager : MonoBehaviour
    {
        public GameObject uiPanel;
        public PlayerController playerController;
        public Transform inventoryPanel;
        public List<InventorySlot> slots = new List<InventorySlot>();
        public bool isOpened;

        private Player.PlayerInput _playerInput;

        private void Awake()
        {
            uiPanel.SetActive(true);
        }

        void Start()
        {
            for (int i = 0; i < inventoryPanel.childCount; i++)
            {
                slots.Add(inventoryPanel.GetChild(i).GetComponent<InventorySlot>());
            }
        
            uiPanel.SetActive(false);

            _playerInput = playerController.GetComponent<Player.PlayerInput>();
            if (_playerInput != null)
            {
                _playerInput.OnToggleInventory += ToggleInventory;
            }
        }

        private void OnDestroy()
        {
            if (_playerInput != null)
            {
                _playerInput.OnToggleInventory -= ToggleInventory;
            }
        }

        private void ToggleInventory()
        {
            if (isOpened)
            {
                uiPanel.SetActive(false);
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                playerController.enabled = true;
            }
            else
            {
                uiPanel.SetActive(true);
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                playerController.enabled = false;
            }
            
            isOpened = !isOpened;
        }

        public void AddItem(Item _item, int _amount)
        {
            foreach (InventorySlot slot in slots)
            {
                if (slot.item == _item && (slot.amount + _amount) <= _item.maxStack)
                {
                    slot.amount += _amount;
                    slot.itemAmountText.text = slot.amount.ToString();
                    return;
                }
            }

            foreach (InventorySlot slot in slots)
            {
                if (slot.IsEmpty)
                {
                    slot.item = _item;
                    slot.amount = _amount;
                    slot.IsEmpty = false;
                    slot.SetIcon(_item.icon);
                    slot.itemAmountText.text = slot.amount.ToString();
                    return;
                }
            }
        }
    }
}
