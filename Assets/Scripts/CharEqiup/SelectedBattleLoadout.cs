using UnityEngine;

namespace CharacterEquipment
{
    /// <summary>
    /// Набор для следующего боя. Хранит только ID выбранных на экране экипировки
    /// шлема и оружия, поэтому между сценами не передаются ссылки на объекты.
    /// </summary>
    public static class SelectedBattleLoadout
    {
        private const string HasSelectionKey = "battle-loadout.selected";
        private const string HelmetIdKey = "battle-loadout.helmet-id";
        private const string WeaponIdKey = "battle-loadout.weapon-id";

        public static bool HasSelection => PlayerPrefs.GetInt(HasSelectionKey, 0) == 1;
        public static string HelmetId => PlayerPrefs.GetString(HelmetIdKey, string.Empty);
        public static string WeaponId => PlayerPrefs.GetString(WeaponIdKey, string.Empty);

        public static void SetItem(EquipmentItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.itemId)) return;

            switch (item.category)
            {
                case EquipmentCategory.Helmet:
                    PlayerPrefs.SetString(HelmetIdKey, item.itemId);
                    break;
                case EquipmentCategory.Weapon:
                    PlayerPrefs.SetString(WeaponIdKey, item.itemId);
                    break;
                default:
                    return;
            }

            PlayerPrefs.SetInt(HasSelectionKey, 1);
            PlayerPrefs.Save();
        }

        /// <summary>Копирует выбранные ID в экипировку боевого персонажа.</summary>
        public static void ApplyTo(System.Collections.Generic.IDictionary<EquipmentSlot, string> target)
        {
            target.Clear();

            if (!HasSelection) return;
            if (!string.IsNullOrEmpty(HelmetId)) target[EquipmentSlot.Helmet] = HelmetId;
        }
    }
}
