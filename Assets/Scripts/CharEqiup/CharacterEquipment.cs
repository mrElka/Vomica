using System;
using System.Collections.Generic;
using UnityEngine;

namespace CharacterEquipment
{
    /// <summary>
    /// Вешается на корень персонажа. Хранит список точек крепления и умеет
    /// экипировать/снимать предметы, спавня их префабы в нужных точках.
    /// </summary>
    public class CharacterEquipment : MonoBehaviour
    {
        [Tooltip("Все точки крепления персонажа. Если оставить пустым — будут найдены " +
                 "автоматически среди дочерних объектов при старте.")]
        [SerializeField] private List<AttachmentPoint> attachmentPoints = new List<AttachmentPoint>();

        // Заспавненные инстансы моделей по слотам (чтобы можно было снять/заменить)
        private readonly Dictionary<AttachmentSlotType, GameObject> spawnedModels =
            new Dictionary<AttachmentSlotType, GameObject>();

        // Текущие надетые предметы по слотам
        private readonly Dictionary<AttachmentSlotType, EquipmentItem> equippedItems =
            new Dictionary<AttachmentSlotType, EquipmentItem>();

        public event Action<AttachmentSlotType, EquipmentItem> OnItemEquipped;
        public event Action<AttachmentSlotType> OnItemUnequipped;

        private void Awake()
        {
            if (attachmentPoints.Count == 0)
            {
                attachmentPoints.AddRange(GetComponentsInChildren<AttachmentPoint>(true));
            }
        }

        /// <summary>
        /// Надеть предмет. Если в этом слоте уже что-то надето — снимает старое.
        /// </summary>
        public void Equip(EquipmentItem item)
        {
            if (item == null) return;

            AttachmentSlotType slot = item.GetAttachmentSlotType();
            AttachmentPoint point = FindPointFor(item, slot);
            if (point == null)
            {
                Debug.LogWarning($"[CharacterEquipment] Не найдена точка крепления для " +
                                  $"'{item.displayName}' (slot: {slot}, weapon sub: {item.weaponSubCategory})");
                return;
            }

            Unequip(slot);

            GameObject instance = null;
            if (item.modelPrefab != null)
            {
                instance = Instantiate(item.modelPrefab, point.transform);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
            }

            spawnedModels[slot] = instance;
            equippedItems[slot] = item;

            OnItemEquipped?.Invoke(slot, item);
        }

        /// <summary>
        /// Снять предмет из указанного слота (если там что-то надето).
        /// </summary>
        public void Unequip(AttachmentSlotType slotType)
        {
            if (spawnedModels.TryGetValue(slotType, out var oldInstance) && oldInstance != null)
            {
                Destroy(oldInstance);
            }

            spawnedModels.Remove(slotType);
            equippedItems.Remove(slotType);

            OnItemUnequipped?.Invoke(slotType);
        }

        public EquipmentItem GetEquipped(AttachmentSlotType slotType)
        {
            equippedItems.TryGetValue(slotType, out var item);
            return item;
        }

        private AttachmentPoint FindPointFor(EquipmentItem item, AttachmentSlotType slot)
        {
            foreach (var point in attachmentPoints)
            {
                if (point.slotType != slot) continue;

                if (slot == AttachmentSlotType.Weapon)
                {
                    // Точка либо универсальная (None), либо совпадает по подкатегории
                    if (point.weaponSubCategory == WeaponSubCategory.None ||
                        point.weaponSubCategory == item.weaponSubCategory)
                    {
                        return point;
                    }
                }
                else
                {
                    return point;
                }
            }
            return null;
        }
    }
}