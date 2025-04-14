using System;
using Inventory.InventoryScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Inventory
{
    [Serializable]
    public class InventorySlot : MonoBehaviour
    {
        public Item item;
        public int amount;
        public GameObject iconGameObject;
        public TMP_Text itemAmountText;
        public Image iconImage;
        
        private bool _isEmpty = true; // Состояние слота
        
        public bool IsEmpty
        {
            get => _isEmpty || item == null;
            set => _isEmpty = value;
        }

        private void Awake()
        {
            // Находим дочерние объекты для UI элементов слота
            try
            {
                if (transform.childCount >= 2)
                {
                    // Получаем объект иконки
                    iconGameObject = transform.GetChild(0).gameObject;
                    if (iconGameObject != null)
                    {
                        iconImage = iconGameObject.GetComponent<Image>();
                        if (iconImage == null)
                        {
                            Debug.LogError($"Слот {name}: Компонент Image не найден на дочернем объекте иконки!");
                        }
                    }
                    else
                    {
                        Debug.LogError($"Слот {name}: Дочерний объект иконки не найден!");
                    }
                    
                    // Получаем текстовое поле для количества
                    GameObject textObj = transform.GetChild(1).gameObject;
                    if (textObj != null)
                    {
                        itemAmountText = textObj.GetComponent<TMP_Text>();
                        if (itemAmountText == null)
                        {
                            Debug.LogError($"Слот {name}: Компонент TMP_Text не найден на дочернем объекте текста!");
                        }
                    }
                    else
                    {
                        Debug.LogError($"Слот {name}: Дочерний объект текста не найден!");
                    }
                }
                else
                {
                    Debug.LogError($"Слот {name}: Недостаточно дочерних объектов! Ожидается минимум 2, найдено {transform.childCount}");
                }
                
                // Инициализируем пустой слот
                ClearSlot();
            }
            catch (Exception e)
            {
                Debug.LogError($"Ошибка инициализации слота {name}: {e.Message}");
            }
        }

        public void SetIcon(Sprite icon)
        {
            if (iconImage != null && icon != null)
            {
                iconImage.color = new Color(1, 1, 1, 1);
                iconImage.sprite = icon;
            }
            else if (iconGameObject != null && iconGameObject.GetComponent<Image>() != null)
            {
                // Запасной вариант, если iconImage не был установлен
                iconImage = iconGameObject.GetComponent<Image>();
                iconImage.color = new Color(1, 1, 1, 1);
                iconImage.sprite = icon;
            }
            else
            {
                Debug.LogError($"Не удалось установить иконку для слота {name}");
            }
        }
        
        public void ClearSlot()
        {
            // Очищаем слот
            item = null;
            amount = 0;
            IsEmpty = true;
            
            // Сбрасываем отображение
            if (iconImage != null)
            {
                iconImage.sprite = null;
                iconImage.color = new Color(1, 1, 1, 0); // Полностью прозрачный
            }
            
            if (itemAmountText != null)
            {
                itemAmountText.text = "";
            }
        }
        
        public bool CanAddStack(Item itemToAdd)
        {
            if (itemToAdd == null) return false;
            if (IsEmpty) return false;
            if (item == null) return false;
            
            return item.id == itemToAdd.id && amount < item.maxStack;
        }
    }
}
