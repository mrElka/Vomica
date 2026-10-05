using System;
using System.Collections.Generic;
using UnityEngine;

namespace CharacterEquipment
{
    /// <summary>
    /// Куда крепить предмет, если на модели нет готового AttachmentPoint.
    /// Кость ищется по имени, поэтому новую модель достаточно назвать так же,
    /// как названы кости в риге: Hand.R, Hand.L, Head и т.д.
    /// </summary>
    [Serializable]
    public class BoneBinding
    {
        public AttachmentPointId id;

        [Tooltip("Имя кости в риге. Регистр важен")]
        public string boneName;

        [Tooltip("Сдвиг рукояти относительно кости")]
        public Vector3 localPosition;

        [Tooltip("Поворот предмета в кости, градусы")]
        public Vector3 localEuler;
    }

    /// <summary>
    /// Вешается на корень персонажа. Знает про точки крепления и правила экипировки:
    /// парные слоты (руки/ноги), две кисти, двуручное оружие.
    /// </summary>
    public class CharacterEquipment : MonoBehaviour
    {
        [Tooltip("Готовые точки на модели. Пусто — точки создадутся сами по именам костей ниже")]
        [SerializeField] private List<AttachmentPoint> attachmentPoints = new List<AttachmentPoint>();

        [Tooltip("Привязка слотов к костям рига. Используется для тех точек, " +
                 "которых нет в списке выше")]
        [SerializeField] private List<BoneBinding> boneBindings = new List<BoneBinding>
        {
            // Рукоять лежит в кисти под углом, иначе клинок смотрит вдоль предплечья
            new BoneBinding { id = AttachmentPointId.RightHand, boneName = "Hand.R",
                              localPosition = new Vector3(0f, -0.0025f, 0.005f),
                              localEuler = new Vector3(120f, 0f, 0f) },
            new BoneBinding { id = AttachmentPointId.LeftHand,  boneName = "Hand.L",
                              localPosition = new Vector3(0f, -0.0025f, 0.005f),
                              localEuler = new Vector3(120f, 0f, 0f) },
            new BoneBinding { id = AttachmentPointId.Head,      boneName = "Head" },
            new BoneBinding { id = AttachmentPointId.Body,      boneName = "Spine" },
            new BoneBinding { id = AttachmentPointId.LeftArm,   boneName = "LowerArm.L" },
            new BoneBinding { id = AttachmentPointId.RightArm,  boneName = "LowerArm.R" },
            new BoneBinding { id = AttachmentPointId.LeftLeg,   boneName = "LowerLeg.L" },
            new BoneBinding { id = AttachmentPointId.RightLeg,  boneName = "LowerLeg.R" }
        };

        private readonly Dictionary<AttachmentPointId, AttachmentPoint> points =
            new Dictionary<AttachmentPointId, AttachmentPoint>();

        private readonly Dictionary<EquipmentSlot, EquipmentItem> equipped =
            new Dictionary<EquipmentSlot, EquipmentItem>();

        // На один слот может быть несколько инстансов (парная броня = 2 модели)
        private readonly Dictionary<EquipmentSlot, List<GameObject>> spawned =
            new Dictionary<EquipmentSlot, List<GameObject>>();

        public event Action<EquipmentSlot, EquipmentItem> OnItemEquipped;
        public event Action<EquipmentSlot> OnItemUnequipped;

        /// <summary>
        /// Предмет вытеснен из слота, потому что на его место взяли другой: двуручное
        /// выбило щит, щит выбил двуручное, новое оружие заменило старое. Такой предмет
        /// некуда положить (инвентаря нет), поэтому его нужно выбросить в мир.
        /// </summary>
        public event Action<EquipmentItem> OnItemDisplaced;

        /// <summary> Любое изменение рук — на это подписывается контроллер поз. </summary>
        public event Action OnHandsChanged;

        private void Awake()
        {
            if (attachmentPoints.Count == 0)
                attachmentPoints.AddRange(GetComponentsInChildren<AttachmentPoint>(true));

            foreach (var p in attachmentPoints)
            {
                if (p == null) continue;

                if (points.ContainsKey(p.id))
                    Debug.LogWarning($"[CharacterEquipment] Дубликат точки {p.id} на '{p.name}'", p);
                else
                    points[p.id] = p;
            }

            BindBonesToMissingPoints();
        }

        /// <summary>
        /// Для слотов без готовой точки находим кость по имени и вешаем точку на неё.
        /// Так новая модель персонажа работает сразу: достаточно, чтобы кости
        /// назывались как в boneBindings.
        /// </summary>
        private void BindBonesToMissingPoints()
        {
            if (boneBindings == null || boneBindings.Count == 0) return;

            Transform[] all = GetComponentsInChildren<Transform>(true);

            foreach (BoneBinding binding in boneBindings)
            {
                if (binding == null || string.IsNullOrWhiteSpace(binding.boneName)) continue;
                if (points.ContainsKey(binding.id)) continue;

                Transform bone = FindByName(all, binding.boneName);
                if (bone == null) continue;

                var holder = new GameObject($"AP_{binding.id}");
                holder.transform.SetParent(bone, false);
                holder.transform.localPosition = binding.localPosition;
                holder.transform.localRotation = Quaternion.Euler(binding.localEuler);

                AttachmentPoint point = holder.AddComponent<AttachmentPoint>();
                point.id = binding.id;

                points[binding.id] = point;
                attachmentPoints.Add(point);
            }
        }

        private static Transform FindByName(Transform[] all, string boneName)
        {
            Transform exact = null;
            Transform suffix = null;

            foreach (Transform t in all)
            {
                if (t == null) continue;

                if (t.name == boneName)
                {
                    exact = t;
                    break;
                }

                // На случай префикса в экспорте (Armature/Hand.R)
                if (suffix == null && t.name.EndsWith(boneName, System.StringComparison.Ordinal))
                    suffix = t;
            }

            return exact != null ? exact : suffix;
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

        /// <summary> Снять предмет со слота. Возвращает снятый предмет или null. </summary>
        public EquipmentItem Unequip(EquipmentSlot slot)
        {
            equipped.TryGetValue(slot, out var removed);

            if (spawned.TryGetValue(slot, out var list))
            {
                foreach (var go in list)
                    if (go != null) Destroy(go);
                spawned.Remove(slot);
            }
            equipped.Remove(slot);

            if (removed == null) return null;

            OnItemUnequipped?.Invoke(slot);
            if (IsHandSlot(slot)) OnHandsChanged?.Invoke();

            return removed;
        }

        /// <summary> Освободить слот под новый предмет и сообщить о вытесненном. </summary>
        private void Displace(EquipmentSlot slot)
        {
            var removed = Unequip(slot);
            if (removed != null) OnItemDisplaced?.Invoke(removed);
        }

        public EquipmentItem GetEquipped(EquipmentSlot slot)
        {
            equipped.TryGetValue(slot, out var item);
            return item;
        }

        /// <summary>
        /// Есть ли на модели такая точка крепления. По ней видно, кто будет показывать
        /// модель предмета: эта система или тот, кто вешает её сам.
        /// </summary>
        public bool HasPoint(AttachmentPointId id) =>
            points.TryGetValue(id, out var point) && point != null;

        // ---------- Логика ----------

        private void EquipSingle(EquipmentSlot slot, AttachmentPointId pointId, EquipmentItem item)
        {
            var point = GetPoint(pointId, item);

            Displace(slot);
            Register(slot, item, Spawn(item.modelPrefab, point, mirror: false));
            OnItemEquipped?.Invoke(slot, item);
        }

        private void EquipPair(EquipmentSlot slot, AttachmentPointId leftId, AttachmentPointId rightId,
                               EquipmentItem item)
        {
            var left = GetPoint(leftId, item);
            var right = GetPoint(rightId, item);

            Displace(slot);

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

            var point = GetPoint(pointId, item);

            // Двуручное занимает обе кисти — освобождаем вторую.
            // И наоборот: если во второй руке двуручное, а мы берём что-то в эту — оно снимается.
            if (item.IsTwoHanded)
            {
                Displace(otherSlot);
            }
            else if (equipped.TryGetValue(otherSlot, out var otherItem) && otherItem.IsTwoHanded)
            {
                Displace(otherSlot);
            }

            Displace(slot);
            Register(slot, item, Spawn(item.modelPrefab, point, mirror: false));

            OnItemEquipped?.Invoke(slot, item);
            OnHandsChanged?.Invoke();
        }

        // ---------- Хелперы ----------

        /// <summary>
        /// Точка крепления или null, если её нет на модели. Предмет всё равно считается
        /// надетым: правила рук и боевые статы не должны зависеть от того,
        /// прикручены ли к модели точки крепления.
        /// </summary>
        private AttachmentPoint GetPoint(AttachmentPointId id, EquipmentItem item)
        {
            if (points.TryGetValue(id, out var point) && point != null) return point;

            Debug.LogWarning($"[CharacterEquipment] Нет точки крепления {id} для " +
                             $"'{item.displayName}' — предмет надет, но модель не появится");
            return null;
        }

        private static GameObject Spawn(GameObject prefab, AttachmentPoint point, bool mirror)
        {
            if (prefab == null || point == null) return null;

            var go = Instantiate(prefab, point.transform);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;

            // Кости рига приходят со своим масштабом — гасим его,
            // иначе предмет в руке раздувается или сплющивается
            Vector3 bone = point.transform.lossyScale;
            Vector3 scale = prefab.transform.localScale;
            scale = new Vector3(
                bone.x != 0f ? scale.x / bone.x : scale.x,
                bone.y != 0f ? scale.y / bone.y : scale.y,
                bone.z != 0f ? scale.z / bone.z : scale.z);

            if (mirror) scale.x = -scale.x;

            go.transform.localScale = scale;
            EquipmentPickup.MakeVisualOnly(go);
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