using System.Collections.Generic;
using UnityEngine;

namespace CharacterEquipment
{
    /// <summary>
    /// Единый список всех предметов экипировки в игре. Создаётся через
    /// Assets -> Create -> Equipment -> Equipment Database.
    /// UI берёт данные отсюда, чтобы наполнить вкладки.
    /// </summary>
    [CreateAssetMenu(fileName = "EquipmentDatabase", menuName = "Equipment/Equipment Database")]
    public class EquipmentDatabase : ScriptableObject
    {
        public List<EquipmentItem> allItems = new List<EquipmentItem>();

        private Dictionary<string, EquipmentItem> byId;

        /// <summary>Предмет по itemId или null. Инвентарь и сохранения хранят только id.</summary>
        public EquipmentItem GetById(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;

            if (byId == null)
            {
                byId = new Dictionary<string, EquipmentItem>();
                foreach (var item in allItems)
                {
                    if (item == null || string.IsNullOrEmpty(item.itemId)) continue;

                    if (byId.ContainsKey(item.itemId))
                        Debug.LogWarning($"[EquipmentDatabase] Повтор itemId '{item.itemId}' у '{item.name}'");
                    else
                        byId.Add(item.itemId, item);
                }
            }

            byId.TryGetValue(itemId, out var found);
            return found;
        }

        private void OnEnable() => byId = null;
        private void OnValidate() => byId = null;

        /// <summary>Для главных вкладок: Шлема / Броня. Для Оружия используй GetWeaponsBySubCategory.</summary>
        public List<EquipmentItem> GetByCategory(EquipmentCategory category)
        {
            return allItems.FindAll(i => i.category == category);
        }

        /// <summary>Для подвкладок оружия: Одноручное / Двуручное / Дальний бой / Щиты.</summary>
        public List<EquipmentItem> GetWeaponsBySubCategory(WeaponSubCategory subCategory)
        {
            return allItems.FindAll(i => i.category == EquipmentCategory.Weapon &&
                                          i.weaponSubCategory == subCategory);
        }
    }
}