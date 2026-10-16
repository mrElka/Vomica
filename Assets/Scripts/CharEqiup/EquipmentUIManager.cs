using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CharacterEquipment
{
    /// <summary>
    /// Главный контроллер экрана экипировки.
    /// Верх: 3 главные вкладки (Шлема / Броня / Оружие).
    /// Внутри Оружия: 4 подвкладки (Одноручное / Двуручное / Дальний бой / Щиты).
    /// Низ: сетка предметов + панель информации о выбранном предмете.
    /// </summary>
    public class EquipmentUIManager : MonoBehaviour
    {
        [Header("Ссылки")]
        [SerializeField] private CharacterEquipment characterEquipment;
        [SerializeField] private EquipmentDatabase database;

        [Header("Главные вкладки (ToggleGroup должен быть общий у всех трёх)")]
        [SerializeField] private Toggle helmetTab;
        [SerializeField] private Toggle armorTab;
        [SerializeField] private Toggle weaponTab;

        [Header("Подвкладки оружия")]
        [SerializeField] private GameObject weaponSubTabsPanel;
        [SerializeField] private Toggle oneHandedTab;
        [SerializeField] private Toggle twoHandedTab;
        [SerializeField] private Toggle rangedTab;
        [SerializeField] private Toggle shieldTab;

        [Header("Сетка предметов")]
        [Tooltip("Content внутри ScrollView, куда спавнятся кнопки предметов")]
        [SerializeField] private Transform itemGridContainer;
        [SerializeField] private EquipmentSlotButton itemButtonPrefab;
        [Tooltip("ToggleGroup сетки предметов — чтобы одновременно был выбран только один предмет")]
        [SerializeField] private ToggleGroup itemToggleGroup;

        [Header("Панель информации о предмете")]
        [SerializeField] private EquipmentDetailsPanel detailsPanel;

        private readonly List<EquipmentSlotButton> spawnedButtons = new List<EquipmentSlotButton>();
        private void Awake()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Start()
        {
            helmetTab.onValueChanged.AddListener(isOn => { if (isOn) ShowMainCategory(EquipmentCategory.Helmet); });
            armorTab.onValueChanged.AddListener(isOn => { if (isOn) ShowMainCategory(EquipmentCategory.Armor); });
            weaponTab.onValueChanged.AddListener(isOn => { if (isOn) ShowMainCategory(EquipmentCategory.Weapon); });

            oneHandedTab.onValueChanged.AddListener(isOn => { if (isOn) ShowWeaponSubCategory(WeaponSubCategory.OneHanded); });
            twoHandedTab.onValueChanged.AddListener(isOn => { if (isOn) ShowWeaponSubCategory(WeaponSubCategory.TwoHanded); });
            rangedTab.onValueChanged.AddListener(isOn => { if (isOn) ShowWeaponSubCategory(WeaponSubCategory.Ranged); });
            shieldTab.onValueChanged.AddListener(isOn => { if (isOn) ShowWeaponSubCategory(WeaponSubCategory.Shield); });

            // Вкладка по умолчанию
            helmetTab.isOn = true;
            ShowMainCategory(EquipmentCategory.Helmet);
        }

        private void ShowMainCategory(EquipmentCategory category)
        {
            detailsPanel.Clear();

            if (category == EquipmentCategory.Weapon)
            {
                weaponSubTabsPanel.SetActive(true);
                // При открытии вкладки оружия по умолчанию показываем "Одноручное"
                if (!oneHandedTab.isOn) oneHandedTab.isOn = true;
                else ShowWeaponSubCategory(WeaponSubCategory.OneHanded);
            }
            else
            {
                weaponSubTabsPanel.SetActive(false);
                PopulateGrid(database.GetByCategory(category));
            }
        }

        private void ShowWeaponSubCategory(WeaponSubCategory subCategory)
        {
            detailsPanel.Clear();
            PopulateGrid(database.GetWeaponsBySubCategory(subCategory));
        }

        private void PopulateGrid(List<EquipmentItem> items)
        {
            foreach (var btn in spawnedButtons)
            {
                if (btn != null) Destroy(btn.gameObject);
            }
            spawnedButtons.Clear();

            EquipmentItem equippedItem = FindEquippedItem(items);

            foreach (var item in items)
            {
                var btn = Instantiate(itemButtonPrefab, itemGridContainer);
                btn.Setup(item, itemToggleGroup, () => OnItemSelected(item));
                btn.SetSelectedWithoutNotify(item == equippedItem);
                spawnedButtons.Add(btn);
            }

            if (equippedItem != null)
                detailsPanel.Show(equippedItem);
        }

        /// <summary>
        /// Кнопки сетки создаются заново при переключении вкладок. Поэтому их
        /// состояние берём из экипировки персонажа, а не из старых Toggle.
        /// </summary>
        private EquipmentItem FindEquippedItem(List<EquipmentItem> items)
        {
            if (characterEquipment == null || items == null) return null;

            foreach (EquipmentItem item in items)
            {
                if (item == null) continue;

                EquipmentItem equipped = null;
                switch (item.category)
                {
                    case EquipmentCategory.Helmet:
                        equipped = characterEquipment.GetEquipped(EquipmentSlot.Helmet);
                        break;
                    case EquipmentCategory.Armor:
                        equipped = characterEquipment.GetEquipped(EquipmentSlot.Body);
                        break;
                    case EquipmentCategory.Arms:
                        equipped = characterEquipment.GetEquipped(EquipmentSlot.Arms);
                        break;
                    case EquipmentCategory.Legs:
                        equipped = characterEquipment.GetEquipped(EquipmentSlot.Legs);
                        break;
                    case EquipmentCategory.Weapon:
                        EquipmentItem rightHand = characterEquipment.GetEquipped(EquipmentSlot.RightHand);
                        EquipmentItem leftHand = characterEquipment.GetEquipped(EquipmentSlot.LeftHand);
                        if (rightHand == item || leftHand == item) return item;
                        continue;
                }

                if (equipped == item) return item;
            }

            return null;
        }

        private void OnItemSelected(EquipmentItem item)
        {
            characterEquipment.Equip(item);
            SelectedBattleLoadout.SetItem(item);
            detailsPanel.Show(item);
        }
    }
}
