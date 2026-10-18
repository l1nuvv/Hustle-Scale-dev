using Inventory;
using Inventory.InventoryScriptableObjects;
using UnityEngine;

namespace Interactables
{
    public class ItemPickup : MonoBehaviour
    {
        public Item item;
        public int amount = 1;

        // Ссылка на InventoryManager для прямой коммуникации
        private InventoryManager _inventoryManager;

        private void Start()
        {
            // Проверяем, что предмет назначен
            if (item == null)
            {
                Debug.LogError($"ItemPickup на объекте {gameObject.name} не имеет назначенного предмета Item!");
                return;
            }

            // Находим InventoryManager в сцене
            _inventoryManager = FindFirstObjectByType<InventoryManager>();
            if (_inventoryManager == null)
            {
                Debug.LogWarning("InventoryManager не найден в сцене. Подбор предметов может не работать корректно.");
            }
        }

        // Вызывается при столкновении с коллайдером триггера
        private void OnTriggerEnter(Collider other)
        {
            // Проверяем, что столкнулись с игроком
            if (other.CompareTag("Player"))
            {
                Debug.Log($"Игрок вошел в триггер предмета {item?.name}");
                PickupItem(other.gameObject);
            }
        }

        // Публичный метод для подбора предмета через raycast
        public bool PickupItem(GameObject player)
        {
            if (item == null)
            {
                Debug.LogError(
                    $"Попытка подобрать предмет с ItemPickup без назначенного Item на объекте {gameObject.name}");
                return false;
            }

            Debug.Log($"Подбираем предмет: {item.name}");

            // Сначала пробуем использовать InventoryManager из кэша
            if (_inventoryManager != null)
            {
                _inventoryManager.AddItem(item, amount);
                Debug.Log($"Предмет {item.name} добавлен в инвентарь через кэшированный _inventoryManager");
                Destroy(gameObject);
                return true;
            }

            // Если InventoryManager не кэширован, ищем его в игроке
            var playerInventory = player.GetComponentInChildren<InventoryManager>();
            if (playerInventory != null)
            {
                playerInventory.AddItem(item, amount);
                Debug.Log($"Предмет {item.name} добавлен в инвентарь через playerInventory");
                Destroy(gameObject);
                return true;
            }

            // В крайнем случае ищем InventoryManager в сцене снова
            var sceneInventory = FindFirstObjectByType<InventoryManager>();
            if (sceneInventory != null)
            {
                sceneInventory.AddItem(item, amount);
                Debug.Log($"Предмет {item.name} добавлен в инвентарь через sceneInventory");
                Destroy(gameObject);
                return true;
            }

            Debug.LogError("Не удалось найти InventoryManager! Предмет не может быть добавлен в инвентарь.");
            return false;
        }
    }
}
