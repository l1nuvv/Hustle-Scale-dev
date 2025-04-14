using Inventory;
using Interactables;
using UnityEngine;

namespace Player
{
    public class PlayerRaycast : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private LayerMask _layerMask;
        [SerializeField] private float _raycastDistance = 5f; // Увеличиваем дистанцию с 2 до 5 метров

        [Header("Inventory")]
        [SerializeField] private InventoryManager _inventoryManager;
        
        [Header("Debug")]
        [SerializeField] private bool _showDebugRay = true;
        [SerializeField] private bool _verboseLogging = true;
        
        private PlayerInput _playerInput;
        
        private void Start()
        {
            // If camera is not assigned, get the main camera
            if (_camera == null)
            {
                _camera = Camera.main;
                if (_camera != null)
                {
                    Debug.Log("Камера успешно назначена для подбора предметов");
                }
                else
                {
                    Debug.LogError("Не найдена главная камера! Подбор предметов не будет работать.");
                }
            }
            
            // If inventory manager is not assigned, try to find it
            if (_inventoryManager == null)
            {
                _inventoryManager = FindFirstObjectByType<InventoryManager>();
                if (_inventoryManager != null)
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
            if (_layerMask.value == 0)
            {
                Debug.Log("LayerMask не настроен, будут проверяться все слои");
                _layerMask = ~0; // Установка всех битов в 1 (все слои)
            }
            else
            {
                // Выводим настройку маски слоев в более читаемом виде
                string layerNames = "";
                for (int i = 0; i < 32; i++) // Unity поддерживает 32 слоя
                {
                    if (((_layerMask.value >> i) & 1) != 0)
                    {
                        layerNames += LayerMask.LayerToName(i) + ", ";
                    }
                }
                Debug.Log($"Настроенные слои для взаимодействия: {layerNames}");
            }
        }
        
        private void OnDestroy()
        {
            if (_playerInput != null)
            {
                _playerInput.OnInteract -= HandleInteraction;
            }
        }
        
        private void HandleInteraction()
        {
            Debug.Log("Обработка взаимодействия... Расстояние луча: " + _raycastDistance);
            
            // Проверим текущую позицию и направление камеры
            if (_verboseLogging)
            {
                Debug.Log($"Позиция камеры: {_camera.transform.position}, направление: {_camera.transform.forward}");
            }
            
            RaycastHit hit;
            
            // Отладочный визуальный луч для проверки направления
            if (_showDebugRay)
            {
                Debug.DrawRay(_camera.transform.position, _camera.transform.forward * _raycastDistance, Color.red, 3f);
            }
            
            // Используем побитовое OR для проверки всех слоев, если _layerMask не настроен
            int layerMask = _layerMask.value == 0 ? ~0 : _layerMask.value;
            
            if (Physics.Raycast(_camera.transform.position, _camera.transform.forward, out hit, _raycastDistance, layerMask))
            {
                string objectInfo = $"Hit object: {hit.collider.gameObject.name}, distance: {hit.distance}, layer: {LayerMask.LayerToName(hit.collider.gameObject.layer)}";
                Debug.Log(objectInfo);
                
                // Check for InventorySlot
                if (hit.collider.TryGetComponent(out InventorySlot item))
                {
                    if (_inventoryManager != null)
                    {
                        _inventoryManager.AddItem(item.item, item.amount);
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
                        bool success = pickup.PickupItem(gameObject);
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
                
                Debug.Log($"Предмет {hit.collider.gameObject.name} не содержит компонентов InventorySlot или ItemPickup");
            }
            else
            {
                Debug.Log("Луч не попал ни в один объект в пределах дистанции " + _raycastDistance);
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
        
        // Визуализация луча в редакторе для отладки
        private void OnDrawGizmos()
        {
            if (_camera != null && _showDebugRay)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawRay(_camera.transform.position, _camera.transform.forward * _raycastDistance);
            }
        }
    }
}
