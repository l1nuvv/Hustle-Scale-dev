using System.Collections.Generic;
using Inventory.InventoryScriptableObjects;
using Player.Movement;
using UnityEngine;
using UnityEngine.InputSystem;
using PlayerInput = Player.PlayerInput;

namespace Inventory
{
    public class InventoryManager : MonoBehaviour
    {
        public GameObject uiPanel;
        public PlayerController playerController;
        public Transform inventoryPanel;
        public List<InventorySlot> slots = new();
        public bool isOpened;

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
        }

        void Start()
        {
            // Get all inventory slots
            if (inventoryPanel != null)
            {
                // Очистим список слотов перед добавлением
                slots.Clear();

                for (var i = 0; i < inventoryPanel.childCount; i++)
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

        private void Update()
        {
            // Keep the standalone prototype usable without a PlayerInput component.
            if (_playerInput == null && Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
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

        // Отдельный метод для поиска PlayerInput и подписки на события
        private void SetupPlayerInputEvents()
        {
            // A scene can retain a reference to a disabled prototype player.
            if (playerController == null || !playerController.isActiveAndEnabled)
            {
                playerController = null;
                foreach (var controller in FindObjectsByType<PlayerController>())
                {
                    if (controller.isActiveAndEnabled)
                    {
                        playerController = controller;
                        break;
                    }
                }
            }

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

        private void ToggleInventory()
        {
            isOpened = !isOpened;

            if (!isOpened)
            {
                if (uiPanel != null)
                {
                    uiPanel.SetActive(false);
                }

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

        public bool AddItem(Item item, int amount)
        {
            if (item == null || amount <= 0 || amount > item.maxStack || slots.Count == 0)
            {
                return false;
            }

            // Сначала ищем существующий стак
            foreach (var slot in slots)
            {
                if (slot == null)
                {
                    continue;
                }

                if (!slot.IsEmpty && slot.item == item && slot.amount <= item.maxStack - amount)
                {
                    slot.amount += amount;
                    if (slot.itemAmountText != null)
                    {
                        slot.itemAmountText.text = slot.amount.ToString();
                    }

                    return true;
                }
            }

            // Затем ищем пустой слот
            foreach (var slot in slots)
            {
                if (slot == null)
                {
                    continue;
                }

                if (slot.IsEmpty)
                {
                    slot.item = item;
                    slot.amount = amount;
                    slot.IsEmpty = false;
                    slot.SetIcon(item.icon);
                    if (slot.itemAmountText != null)
                    {
                        slot.itemAmountText.text = slot.amount.ToString();
                    }

                    // Если инвентарь открыт, сразу обновляем UI
                    if (isOpened && uiPanel != null && uiPanel.activeSelf)
                    {
                        UpdateInventoryUI();
                    }

                    return true;
                }
            }
            return false;
        }

        // Обновление UI инвентаря
        private void UpdateInventoryUI()
        {
            foreach (var slot in slots)
            {
                if (slot == null)
                {
                    continue;
                }

                if (!slot.IsEmpty && slot.item != null)
                {
                    // Если в слоте есть предмет, обновляем его отображение
                    slot.SetIcon(slot.item.icon);
                    if (slot.itemAmountText != null)
                    {
                        slot.itemAmountText.text = slot.amount.ToString();
                    }
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
