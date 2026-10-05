using UnityEngine;

namespace CharacterEquipment
{
    /// <summary>
    /// Снаряжение, лежащее в мире. Игрок подходит и забирает его сразу в руки —
    /// ни инвентаря, ни быстрой панели нет. Такие же объекты появляются сами,
    /// когда предмет вытесняют из рук.
    /// </summary>
    public class EquipmentPickup : MonoBehaviour
    {
        [Tooltip("Что лежит. Модель берётся из предмета, если её ещё нет на объекте")]
        [SerializeField] private EquipmentItem item;

        [Tooltip("Спавнить модель предмета дочерним объектом при старте")]
        [SerializeField] private bool spawnModel = true;

        [Tooltip("Сдвиг модели относительно этого объекта")]
        [SerializeField] private Vector3 modelOffset = Vector3.zero;

        [Tooltip("Вращать лежащий предмет, чтобы его было заметно. 0 — не вращать")]
        [SerializeField] private float spinSpeed = 45f;

        [Tooltip("Пауза перед тем как предмет можно поднять. Нужна выброшенным из рук")]
        [SerializeField, Min(0f)] private float pickupDelay = 0f;

        private GameObject model;
        private float readyAt;

        public EquipmentItem Item => item;

        /// <summary>Готов ли предмет к подбору: только что выброшенный ещё нельзя.</summary>
        public bool IsReady => item != null && Time.time >= readyAt;

        /// <summary>Название для подсказки на экране.</summary>
        public string DisplayName => item != null ? item.displayName : "пусто";

        private void Start()
        {
            readyAt = Time.time + pickupDelay;

            if (item == null)
            {
                Debug.LogWarning($"[Pickup] На '{name}' не назначен предмет", this);
                return;
            }

            if (spawnModel && model == null && item.modelPrefab != null)
            {
                model = Instantiate(item.modelPrefab, transform);
                model.transform.localPosition = modelOffset;
                model.transform.localRotation = Quaternion.identity;
            }
        }

        private void Update()
        {
            if (spinSpeed != 0f)
                transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
        }

        /// <summary>Предмет забрали — объект больше не нужен.</summary>
        public void Consume() => Destroy(gameObject);

        /// <summary>
        /// Положить предмет в мир: так выбрасывается снаряжение, вытесненное из рук.
        /// Коллайдер нужен, чтобы игрок мог найти предмет рядом с собой.
        /// </summary>
        public static EquipmentPickup Drop(EquipmentItem item, Vector3 position, float pickupDelay = 1f)
        {
            if (item == null) return null;

            var go = new GameObject($"Pickup_{item.itemId}");
            go.transform.position = position;

            var collider = go.AddComponent<SphereCollider>();
            collider.radius = 0.35f;
            collider.isTrigger = true;

            var pickup = go.AddComponent<EquipmentPickup>();
            pickup.item = item;
            pickup.spawnModel = true;
            pickup.pickupDelay = pickupDelay;
            pickup.readyAt = Time.time + pickupDelay;

            return pickup;
        }
    }
}
