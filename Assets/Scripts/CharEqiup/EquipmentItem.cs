using System.Collections.Generic;
using UnityEngine;

namespace CharacterEquipment
{
    /// <summary>
    /// Описание одного предмета экипировки как объект в ассетах. Создаётся через
    /// Assets -> Create -> Equipment -> Equipment Item
    /// </summary>
    /// 
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
                 "Shield - физически отдельный слот, но в UI лежит вкладкой внутри оружия.")]
        public WeaponSubCategory weaponSubCategory = WeaponSubCategory.None;

        [Header("Визуал")]
        [Tooltip("Префаб модели, который будет прикреплён к точке крепления персонажа")]
        public GameObject modelPrefab;

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

        /// <summary>
        /// Привязка к поинтам на персонаже
        /// 
        /// МБ позже будет привязка к позе
        /// </summary>
        public AttachmentSlotType GetAttachmentSlotType()
        {
            switch (category)
            {
                case EquipmentCategory.Helmet:
                    return AttachmentSlotType.Helmet;
                case EquipmentCategory.Armor:
                    return AttachmentSlotType.Armor;
                case EquipmentCategory.Weapon:
                    return weaponSubCategory == WeaponSubCategory.Shield
                        ? AttachmentSlotType.Shield
                        : AttachmentSlotType.Weapon;
                default:
                    return AttachmentSlotType.Weapon;
            }
        }
    }
}
