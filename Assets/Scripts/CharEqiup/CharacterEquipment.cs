using System;
using System.Collections.Generic;
using UnityEngine;

namespace CharacterEquipment
{
    /// <summary>
    /// Вешается на корень персонажа. Знает про точки крепления и правила экипировки:
    /// парные слоты (руки/ноги), две кисти, двуручное оружие.
    /// </summary>
    public class CharacterEquipment : MonoBehaviour
    {
        [SerializeField] private List<AttachmentPoint> attachmentPoints = new List<AttachmentPoint>();

        private readonly Dictionary<AttachmentPointId, AttachmentPoint> points =
            new Dictionary<AttachmentPointId, AttachmentPoint>();

        private readonly Dictionary<EquipmentSlot, EquipmentItem> equipped =
            new Dictionary<EquipmentSlot, EquipmentItem>();

        // На один слот может быть несколько инстансов (парная броня = 2 модели)
        private readonly Dictionary<EquipmentSlot, List<GameObject>> spawned =
            new Dictionary<EquipmentSlot, List<GameObject>>();

        public event Action<EquipmentSlot, EquipmentItem> OnItemEquipped;
        public event Action<EquipmentSlot> OnItemUnequipped;
        /// <summary> Любое изменение рук — на это подписывается контроллер поз. </summary>
        public event Action OnHandsChanged;

        private void Awake()
        {
            if (attachmentPoints.Count == 0)
                attachmentPoints.AddRange(GetComponentsInChildren<AttachmentPoint>(true));

            foreach (var p in attachmentPoints)
            {
                if (points.ContainsKey(p.id))
                    Debug.LogWarning($"[CharacterEquipment] Дубликат точки {p.id} на '{p.name}'", p);
                else
                    points[p.id] = p;
            }
        }

        // ---------- Публичный API ----------

        /// <summary>
        /// Надеть предмет. Для Hand-предметов рука выбирается автоматически
        /// по item.preferredHand, либо передайте её явно.
        /// </summary>
        public void Equip(EquipmentItem item, PreferredHand? handOverride = null)
        {
            if (item == null) return;

            switch (item.category)
            {
                case EquipmentCategory.Helmet:
                    EquipSingle(EquipmentSlot.Helmet, AttachmentPointId.Head, item);
                    break;
                case EquipmentCategory.Armor:
                    EquipSingle(EquipmentSlot.Body, AttachmentPointId.Body, item);
                    break;
                case EquipmentCategory.Arms:
                    EquipPair(EquipmentSlot.Arms, AttachmentPointId.LeftArm, AttachmentPointId.RightArm, item);
                    break;
                case EquipmentCategory.Legs:
                    EquipPair(EquipmentSlot.Legs, AttachmentPointId.LeftLeg, AttachmentPointId.RightLeg, item);
                    break;
                case EquipmentCategory.Weapon:
                    EquipHand(item, handOverride ?? item.PreferredHand);
                    break;
            }
        }

        public void Unequip(EquipmentSlot slot)
        {
            bool had = equipped.ContainsKey(slot);

            if (spawned.TryGetValue(slot, out var list))
            {
                foreach (var go in list)
                    if (go != null) Destroy(go);
                spawned.Remove(slot);
            }
            equipped.Remove(slot);

            if (!had) return;

            OnItemUnequipped?.Invoke(slot);
            if (IsHandSlot(slot)) OnHandsChanged?.Invoke();
        }

        public EquipmentItem GetEquipped(EquipmentSlot slot)
        {
            equipped.TryGetValue(slot, out var item);
            return item;
        }

        // ---------- Логика ----------

        private void EquipSingle(EquipmentSlot slot, AttachmentPointId pointId, EquipmentItem item)
        {
            if (!TryGetPoint(pointId, item, out var point)) return;

            Unequip(slot);
            Register(slot, item, Spawn(item.modelPrefab, point, mirror: false));
            OnItemEquipped?.Invoke(slot, item);
        }

        private void EquipPair(EquipmentSlot slot, AttachmentPointId leftId, AttachmentPointId rightId,
                               EquipmentItem item)
        {
            if (!TryGetPoint(leftId, item, out var left) || !TryGetPoint(rightId, item, out var right))
                return;

            Unequip(slot);

            var right_go = Spawn(item.modelPrefab, right, mirror: false);
            GameObject left_go = item.modelPrefabLeft != null
                ? Spawn(item.modelPrefabLeft, left, mirror: false)
                : Spawn(item.modelPrefab, left, mirror: true);   // зеркалим основную модель

            Register(slot, item, right_go, left_go);
            OnItemEquipped?.Invoke(slot, item);
        }

        private void EquipHand(EquipmentItem item, PreferredHand hand)
        {
            var slot = hand == PreferredHand.Right ? EquipmentSlot.RightHand : EquipmentSlot.LeftHand;
            var otherSlot = hand == PreferredHand.Right ? EquipmentSlot.LeftHand : EquipmentSlot.RightHand;
            var pointId = hand == PreferredHand.Right ? AttachmentPointId.RightHand : AttachmentPointId.LeftHand;

            if (!TryGetPoint(pointId, item, out var point)) return;

            // Двуручный занимает обе кисти — освобождаем вторую.
            // И наоборот: если во второй руке двуручка, а мы берём что-то в эту — двуручка снимается.
            if (item.IsTwoHanded)
            {
                Unequip(otherSlot);
            }
            else if (equipped.TryGetValue(otherSlot, out var otherItem) && otherItem.IsTwoHanded)
            {
                Unequip(otherSlot);
            }

            Unequip(slot);
            Register(slot, item, Spawn(item.modelPrefab, point, mirror: false));

            OnItemEquipped?.Invoke(slot, item);
            OnHandsChanged?.Invoke();
        }

        // ---------- Хелперы ----------

        private bool TryGetPoint(AttachmentPointId id, EquipmentItem item, out AttachmentPoint point)
        {
            if (points.TryGetValue(id, out point) && point != null) return true;

            Debug.LogWarning($"[CharacterEquipment] Нет точки крепления {id} для '{item.displayName}'");
            return false;
        }

        private static GameObject Spawn(GameObject prefab, AttachmentPoint point, bool mirror)
        {
            if (prefab == null) return null;

            var go = Instantiate(prefab, point.transform);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            if (mirror)
            {
                var s = go.transform.localScale;
                go.transform.localScale = new Vector3(-s.x, s.y, s.z);
            }
            return go;
        }

        private void Register(EquipmentSlot slot, EquipmentItem item, params GameObject[] instances)
        {
            equipped[slot] = item;
            var list = new List<GameObject>();
            foreach (var go in instances)
                if (go != null) list.Add(go);
            spawned[slot] = list;
        }

        private static bool IsHandSlot(EquipmentSlot s) =>
            s == EquipmentSlot.LeftHand || s == EquipmentSlot.RightHand;
    }
}