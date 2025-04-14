using System.Collections.Generic;
using Inventory.InventoryScriptableObjects;
using Player.Movement;
using UnityEngine;
using Player;

namespace Inventory
{
    public class InventoryManager : MonoBehaviour
    {
        public GameObject uiPanel;
        public PlayerController playerController;
        public Transform inventoryPanel;
        public List<InventorySlot> slots = new List<InventorySlot>();
        public bool isOpened;

        // Для прямого открытия инвентаря кнопкой
        [SerializeField] private KeyCode _toggleInventoryKey = KeyCode.Tab;
        
        private PlayerInput _playerInput;

        private void Awake()
        {
            // Make sure UI is initially active so we can find all child slots
            if (uiPanel != null)
            {
                uiPanel.SetActive(true);
            }
            else
            {
                return;
            }
            
            // Find the player controller if not assigned
            if (playerController == null)
            {
                // Находим компонент PlayerController в сцене
                playerController = FindFirstObjectByType<PlayerController>();
            }
        }

        void Start()
        {
            // Get all inventory slots
            if (inventoryPanel != null)
            {
                // Очистим список слотов перед добавлением
                slots.Clear();
                
                for (int i = 0; i < inventoryPanel.childCount; i++)
                {
                    if (inventoryPanel.GetChild(i).TryGetComponent<InventorySlot>(out var slot))
                    {
                        slots.Add(slot);
                    }
                }
            }
        
            // Hide the UI after collecting slots
            if (uiPanel != null)
            {
                uiPanel.SetActive(false);
            }
            isOpened = false;

            // Получаем компонент PlayerInput из PlayerController и подписываемся на события
            SetupPlayerInputEvents();
        }
        
        // Отдельный метод для поиска PlayerInput и подписки на события
        private void SetupPlayerInputEvents()
        {
            if (playerController != null)
            {
                // Важно: получаем PlayerInput из объекта, содержащего PlayerController
                _playerInput = playerController.GetComponent<PlayerInput>();
                
                if (_playerInput != null)
                {
                    // Отписываемся на всякий случай, чтобы избежать двойной подписки
                    _playerInput.OnToggleInventory -= ToggleInventory;
                    
                    // Подписываемся на событие
                    _playerInput.OnToggleInventory += ToggleInventory;
                }
            }
        }
        
        private void Update()
        {
            // Прямое управление инвентарем кнопкой Tab для отладки
            if (Input.GetKeyDown(_toggleInventoryKey))
            {
                ToggleInventoryManually();
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
            isOpened = !isOpened;
            
            if (!isOpened)
            {
                if (uiPanel != null) uiPanel.SetActive(false);
                
                // Используем метод в PlayerController для управления курсором, камерой и движением
                if (playerController != null)
                {
                    playerController.SetCursorLocked(true);
                    playerController.SetCameraControlEnabled(true);
                    playerController.SetMovementEnabled(true);
                    
                    // Включаем ввод мыши
                    if (_playerInput != null)
                    {
                        _playerInput.MouseInputEnabled = true;
                    }
                }
                else
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }
            else
            {
                if (uiPanel != null) 
                {
                    uiPanel.SetActive(true);
                    // Обновляем UI при открытии инвентаря
                    UpdateInventoryUI();
                }
                
                // Используем метод в PlayerController для управления курсором, камерой и движением
                if (playerController != null)
                {
                    playerController.SetCursorLocked(false);
                    playerController.SetCameraControlEnabled(false);
                    playerController.SetMovementEnabled(false);
                    
                    // Отключаем ввод мыши
                    if (_playerInput != null)
                    {
                        _playerInput.MouseInputEnabled = false;
                    }
                }
                else
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
            }
        }

        public void AddItem(Item _item, int _amount)
        {
            if (_item == null || slots.Count == 0)
            {
                return;
            }
            
            // Сначала ищем существующий стак
            foreach (InventorySlot slot in slots)
            {
                if (slot == null) continue;
                
                if (!slot.IsEmpty && slot.item == _item && (slot.amount + _amount) <= _item.maxStack)
                {
                    slot.amount += _amount;
                    if (slot.itemAmountText != null)
                        slot.itemAmountText.text = slot.amount.ToString();
                    return;
                }
            }

            // Затем ищем пустой слот
            foreach (InventorySlot slot in slots)
            {
                if (slot == null) continue;
                
                if (slot.IsEmpty)
                {
                    slot.item = _item;
                    slot.amount = _amount;
                    slot.IsEmpty = false;
                    slot.SetIcon(_item.icon);
                    if (slot.itemAmountText != null)
                        slot.itemAmountText.text = slot.amount.ToString();
                    
                    // Если инвентарь открыт, сразу обновляем UI
                    if (isOpened && uiPanel != null && uiPanel.activeSelf)
                    {
                        UpdateInventoryUI();
                    }
                    
                    return;
                }
            }
        }
        
        // Обновление UI инвентаря
        private void UpdateInventoryUI()
        {
            foreach (InventorySlot slot in slots)
            {
                if (slot == null) continue;
                
                if (!slot.IsEmpty && slot.item != null)
                {
                    // Если в слоте есть предмет, обновляем его отображение
                    slot.SetIcon(slot.item.icon);
                    if (slot.itemAmountText != null)
                        slot.itemAmountText.text = slot.amount.ToString();
                }
                else
                {
                    // Если слот пуст, очищаем его отображение
                    slot.ClearSlot();
                }
            }
        }
        
        // Manual toggle for UI button or testing
        // ReSharper disable Unity.PerformanceAnalysis
        public void ToggleInventoryManually()
        {
            ToggleInventory();
        }
    }
}
