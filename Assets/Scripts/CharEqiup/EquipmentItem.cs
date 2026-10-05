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

        [Header("Инвентарь")]
        [Tooltip("Сколько штук помещается в один слот. Для экипировки — 1")]
        [Min(1)] public int maxStack = 1;

        [Header("Баланс v0.1: оружие")]
        [Tooltip("Класс оружия из документа баланса: задаёт урон, дальность, цикл и выносливость")]
        public WeaponClass weaponClass = WeaponClass.Unarmed;

        [Header("Баланс v0.1: броня")]
        [Tooltip("Базовая защита A. 0 — предмет не защищает по формуле документа")]
        [Min(0f)] public float armorA = 0f;
        public ArmorProfile armorProfile = ArmorProfile.None;
        [Range(0f, 0.5f)] public float movementPenalty = 0f;
        [Range(0f, 0.5f)] public float attackPenalty = 0f;

        [Header("Статы (выводятся в панели информации в виде полосок)")]
        [Tooltip("Например для оружия: Урон / Скорость / Дальность. Для брони: Класс брони / Сила брони.")]
        public List<EquipmentStat> stats = new List<EquipmentStat>();

        // ---------- Вычисляемое из подкатегории ----------

        public bool IsWeapon => category == EquipmentCategory.Weapon;

        /// <summary> Двуручное оружие и дальнее (лук) занимают обе кисти. </summary>
        public bool IsTwoHanded =>
            IsWeapon && (weaponSubCategory == WeaponSubCategory.TwoHanded ||
                         weaponSubCategory == WeaponSubCategory.Ranged);

        /// <summary>
        /// В какой кисти будет модель по умолчанию:
        /// щит и дальнее (лук) — в левой, всё остальное — в правой.
        /// </summary>
        public PreferredHand PreferredHand =>
            weaponSubCategory == WeaponSubCategory.Shield || weaponSubCategory == WeaponSubCategory.Ranged
                ? PreferredHand.Left
                : PreferredHand.Right;
    }
}