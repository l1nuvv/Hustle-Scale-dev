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
        private bool consumed;

        private void Start()
        {
            // Проверяем, что предмет назначен
            if (item == null)
            {
                Debug.LogError($"ItemPickup на объекте {gameObject.name} не имеет назначенного предмета Item!");
                return;
            }

            // Находим InventoryManager в сцене
            _inventoryManager = FindAnyObjectByType<InventoryManager>();
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
            if (consumed || !gameObject.activeInHierarchy) return false;
            if (item == null)
            {
                Debug.LogError(
                    $"Попытка подобрать предмет с ItemPickup без назначенного Item на объекте {gameObject.name}");
                return false;
            }

            Debug.Log($"Подбираем предмет: {item.name}");

            var destination = _inventoryManager;
            if (destination == null && player != null) destination = player.GetComponentInChildren<InventoryManager>();
            if (destination == null) destination = FindAnyObjectByType<InventoryManager>();
            if (destination == null)
            {
                Debug.LogError("Не удалось найти InventoryManager! Предмет не может быть добавлен в инвентарь.");
                return false;
            }
            if (!destination.AddItem(item, amount)) return false;

            // Destroy is deferred until the end of the frame; consume the interaction now.
            consumed = true;
            gameObject.SetActive(false);
            Destroy(gameObject);
            return true;
        }
    }
}
