using Interactables;
using Inventory;
using UnityEngine;
using UnityEngine.Serialization;

namespace Player
{
    public class PlayerRaycast : MonoBehaviour
    {
        [FormerlySerializedAs("_camera")] [SerializeField] private Camera camera;
        [FormerlySerializedAs("_layerMask")] [SerializeField] private LayerMask layerMask;
        [FormerlySerializedAs("_raycastDistance")] [SerializeField] private float raycastDistance = 5f; // Увеличиваем дистанцию с 2 до 5 метров

        [FormerlySerializedAs("_inventoryManager")] [Header("Inventory")] [SerializeField] private InventoryManager inventoryManager;

        [FormerlySerializedAs("_showDebugRay")] [Header("Debug")] [SerializeField] private bool showDebugRay = true;

        [FormerlySerializedAs("_verboseLogging")] [SerializeField] private bool verboseLogging = true;

        private PlayerInput _playerInput;

        private void Start()
        {
            // If camera is not assigned, get the main camera
            if (camera == null)
            {
                camera = Camera.main;
                if (camera != null)
                {
                    Debug.Log("Камера успешно назначена для подбора предметов");
                }
                else
                {
                    Debug.LogError("Не найдена главная камера! Подбор предметов не будет работать.");
                }
            }

            // If inventory manager is not assigned, try to find it
            if (inventoryManager == null)
            {
                inventoryManager = FindFirstObjectByType<InventoryManager>();
                if (inventoryManager != null)
                {
                    Debug.Log("InventoryManager успешно найден для PlayerRaycast");
                }
                else
                {
                    Debug.LogError("InventoryManager не найден! Предметы не будут добавляться в инвентарь.");
                }
            }

            // Get player input component
            _playerInput = GetComponent<PlayerInput>();
            if (_playerInput != null)
            {
                _playerInput.OnInteract += HandleInteraction;
                Debug.Log("PlayerRaycast успешно подписался на событие OnInteract");
            }
            else
            {
                Debug.LogError("PlayerInput не найден на игроке! Взаимодействие не будет работать.");
            }

            // Если маска слоев не настроена, проверять все слои
            if (layerMask.value == 0)
            {
                Debug.Log("LayerMask не настроен, будут проверяться все слои");
                layerMask = ~0; // Установка всех битов в 1 (все слои)
            }
            else
            {
                // Выводим настройку маски слоев в более читаемом виде
                var layerNames = "";
                for (var i = 0; i < 32; i++) // Unity поддерживает 32 слоя
                {
                    if (((layerMask.value >> i) & 1) != 0)
                    {
                        layerNames += LayerMask.LayerToName(i) + ", ";
                    }
                }

                Debug.Log($"Настроенные слои для взаимодействия: {layerNames}");
            }
        }

        void Update()
        {
            // For backward compatibility - can remove once the input system is fully working
            if (Input.GetKeyDown(KeyCode.E))
            {
                HandleInteraction();
            }
        }

        private void OnDestroy()
        {
            if (_playerInput != null)
            {
                _playerInput.OnInteract -= HandleInteraction;
            }
        }

        // Визуализация луча в редакторе для отладки
        private void OnDrawGizmos()
        {
            if (camera != null && showDebugRay)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawRay(camera.transform.position, camera.transform.forward * raycastDistance);
            }
        }

        private void HandleInteraction()
        {
            Debug.Log("Обработка взаимодействия... Расстояние луча: " + raycastDistance);

            // Проверим текущую позицию и направление камеры
            if (verboseLogging)
            {
                Debug.Log($"Позиция камеры: {camera.transform.position}, направление: {camera.transform.forward}");
            }

            RaycastHit hit;

            // Отладочный визуальный луч для проверки направления
            if (showDebugRay)
            {
                Debug.DrawRay(camera.transform.position, camera.transform.forward * raycastDistance, Color.red, 3f);
            }

            // Используем побитовое OR для проверки всех слоев, если _layerMask не настроен
            var layerMask = this.layerMask.value == 0 ? ~0 : this.layerMask.value;

            if (Physics.Raycast(camera.transform.position, camera.transform.forward, out hit, raycastDistance,
                    layerMask))
            {
                var objectInfo =
                    $"Hit object: {hit.collider.gameObject.name}, distance: {hit.distance}, layer: {LayerMask.LayerToName(hit.collider.gameObject.layer)}";
                Debug.Log(objectInfo);

                // Check for InventorySlot
                if (hit.collider.TryGetComponent(out InventorySlot item))
                {
                    if (inventoryManager != null)
                    {
                        inventoryManager.AddItem(item.item, item.amount);
                        Destroy(item.gameObject);
                        Debug.Log($"Подобран предмет из слота: {item.item?.name}");
                    }

                    return;
                }

                // Check for ItemPickup - используем улучшенный метод PickupItem
                if (hit.collider.TryGetComponent(out ItemPickup pickup))
                {
                    if (pickup.item != null)
                    {
                        var success = pickup.PickupItem(gameObject);
                        if (success)
                        {
                            Debug.Log($"Успешно подобран предмет: {pickup.item.name}");
                        }
                        else
                        {
                            Debug.LogError($"Не удалось подобрать предмет из ItemPickup: {pickup.name}");
                        }
                    }
                    else
                    {
                        Debug.LogError($"Item в компоненте ItemPickup на объекте {pickup.name} не назначен!");
                    }

                    return;
                }

                Debug.Log(
                    $"Предмет {hit.collider.gameObject.name} не содержит компонентов InventorySlot или ItemPickup");
            }
            else
            {
                Debug.Log("Луч не попал ни в один объект в пределах дистанции " + raycastDistance);
            }
        }
    }
}
