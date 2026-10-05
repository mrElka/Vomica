using System.Collections.Generic;
using UnityEngine;

namespace CharacterEquipment
{
    /// <summary>
    /// Описание одного предмета экипировки как объект в ассетах. Создаётся через
    /// Assets -> Create -> Equipment -> Equipment Item
    /// </summary>
    [CreateAssetMenu(fileName = "NewEquipmentItem", menuName = "Equipment/Equipment Item")]
    public class EquipmentItem : ScriptableObject
    {
        [Header("Основная информация")]
        public string itemId;
        public string displayName;
        [TextArea] public string description;
        public Sprite icon;

        [Header("Категория для UI")]
        public EquipmentCategory category;

        [Tooltip("Заполняется только если category == Weapon. " +
                 "Определяет, в какую руку и сколькими руками предмет держится.")]
        public WeaponSubCategory weaponSubCategory = WeaponSubCategory.None;

        [Header("Визуал")]
        [Tooltip("Префаб модели. Для парной брони (Arms/Legs) — модель ПРАВОЙ стороны.")]
        public GameObject modelPrefab;

        [Tooltip("Только для парной брони (Arms/Legs): модель ЛЕВОЙ стороны. " +
                 "Если пусто — основная модель будет отзеркалена по X.")]
        public GameObject modelPrefabLeft;

        [Header("Поза (только для оружия)")]
        [Tooltip("Поза, которая включится в аниматоре при экипировке.")]
        public HoldPose holdPose = HoldPose.None;

        [Header("Характеристики: оружие")]
        [Tooltip("Класс из документа баланса. Задаёт урон и его тип, дальность, цикл, " +
                 "замах, расход выносливости и количество рук. Отдельных чисел у предмета нет.")]
        public WeaponClass weaponClass = WeaponClass.Unarmed;

        [Header("Характеристики: броня")]
        [Tooltip("Предмет из набора брони. Задаёт защиту A, профиль материала и штрафы. " +
                 "None — предмет не защищает.")]
        public ArmorPiece armorPiece = ArmorPiece.None;

        [Header("Дополнительные статы для панели информации")]
        [Tooltip("Полоски сверх тех, что считаются из таблиц баланса автоматически.")]
        public List<EquipmentStat> extraStats = new List<EquipmentStat>();

        // ---------- Оружие ----------

        public bool IsWeapon => category == EquipmentCategory.Weapon;

        /// <summary> Табличные показатели класса: урон, дальность, цикл, замах, выносливость. </summary>
        public WeaponClassStats WeaponStats => VomicaBalance.GetWeapon(weaponClass);

        public bool HasWeaponStats => IsWeapon && weaponClass != WeaponClass.Unarmed;

        /// <summary>
        /// Сколько кистей занимает предмет: 2 — двуручное, 0 — только левая, 1 — одна рука.
        /// Главный источник — класс оружия из документа. Для лука класса в документе нет,
        /// поэтому там решает подкатегория.
        /// </summary>
        public int Hands
        {
            get
            {
                if (!IsWeapon) return 0;
                if (HasWeaponStats) return WeaponStats.Hands;

                switch (weaponSubCategory)
                {
                    case WeaponSubCategory.TwoHanded:
                    case WeaponSubCategory.Ranged:
                        return 2;
                    case WeaponSubCategory.Shield:
                        return 0;
                    default:
                        return 1;
                }
            }
        }

        /// <summary> Двуручное оружие и лук занимают обе кисти и вытесняют щит. </summary>
        public bool IsTwoHanded => Hands >= 2;

        /// <summary> Предмет для второй руки: щит, баклер. </summary>
        public bool IsOffHand => IsWeapon && Hands == 0;

        /// <summary>
        /// В какой кисти будет модель по умолчанию:
        /// щит и лук — в левой, всё остальное — в правой.
        /// </summary>
        public PreferredHand PreferredHand =>
            IsOffHand || weaponSubCategory == WeaponSubCategory.Shield || weaponSubCategory == WeaponSubCategory.Ranged
                ? PreferredHand.Left
                : PreferredHand.Right;

        // ---------- Броня ----------

        public bool HasArmorStats => armorPiece != ArmorPiece.None;

        private ArmorItemPreset ArmorPreset
        {
            get
            {
                VomicaBalance.TryGetArmorPreset(armorPiece, out ArmorItemPreset preset);
                return preset;
            }
        }

        /// <summary> Базовая защита A до коэффициента материала. </summary>
        public float ArmorA => ArmorPreset.ArmorA;

        public ArmorProfile ArmorProfile => ArmorPreset.Profile;

        /// <summary> Штраф скорости движения за этот предмет, доля. </summary>
        public float MovementPenalty => ArmorPreset.MovementPenalty;

        /// <summary> Штраф частоты атак за этот предмет, доля. </summary>
        public float AttackPenalty => ArmorPreset.AttackPenalty;

        // ---------- Панель информации ----------

        /// <summary>
        /// Полоски для панели информации. Считаются из таблиц баланса, поэтому
        /// их не нужно заполнять руками у каждого предмета.
        /// </summary>
        public List<EquipmentStat> GetDisplayStats()
        {
            var result = new List<EquipmentStat>();

            if (HasWeaponStats)
            {
                WeaponClassStats s = WeaponStats;

                result.Add(new EquipmentStat { label = "Урон", value = s.Damage.Total, maxValue = 40f });
                result.Add(new EquipmentStat { label = "Дальность", value = s.Range, maxValue = 3f });
                result.Add(new EquipmentStat { label = "Скорость", value = s.AttacksPerSecond, maxValue = 2f });
                result.Add(new EquipmentStat { label = "Выносливость", value = s.StaminaCost, maxValue = 26f });
            }

            if (HasArmorStats)
            {
                ArmorItemPreset p = ArmorPreset;

                result.Add(new EquipmentStat { label = "Защита", value = p.ArmorA, maxValue = 16f });
                result.Add(new EquipmentStat { label = "Штраф скорости", value = p.MovementPenalty, maxValue = 0.06f });
            }

            result.AddRange(extraStats);
            return result;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!HasWeaponStats) return;

            // Руки считаются по классу оружия, поэтому подкатегория не должна ему противоречить
            bool subSaysTwoHanded = weaponSubCategory == WeaponSubCategory.TwoHanded ||
                                    weaponSubCategory == WeaponSubCategory.Ranged;

            if (subSaysTwoHanded != WeaponStats.IsTwoHanded)
            {
                Debug.LogWarning(
                    $"[{name}] '{WeaponStats.DisplayName}' по документу занимает {WeaponStats.Hands} " +
                    $"кисти, а подкатегория стоит {weaponSubCategory}. Руки берутся из класса оружия.", this);
            }
        }
#endif
    }
}