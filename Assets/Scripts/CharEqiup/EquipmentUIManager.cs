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

            foreach (var item in items)
            {
                var btn = Instantiate(itemButtonPrefab, itemGridContainer);
                btn.Setup(item, itemToggleGroup, () => OnItemSelected(item));
                spawnedButtons.Add(btn);
            }
        }

        private void OnItemSelected(EquipmentItem item)
        {
            characterEquipment.Equip(item);
            detailsPanel.Show(item);
        }
    }
}