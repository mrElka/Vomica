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