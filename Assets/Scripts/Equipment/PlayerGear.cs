using System;
using System.Collections.Generic;
using System.IO;
using CharacterEquipment;
using UnityEngine;
using CharEquip = CharacterEquipment.CharacterEquipment;

/// <summary>Что лежало в одном слоте: для стартового набора и для сохранения.</summary>
[Serializable]
public class GearSlotData
{
    public EquipmentSlot slot;
    public string itemId;
}

[Serializable]
public class GearSaveData
{
    public int version = 1;
    public List<GearSlotData> slots = new List<GearSlotData>();
}

/// <summary>
/// Снаряжение игрока. Инвентаря и быстрой панели нет: герой подбирает предметы
/// с земли сразу в руки, с ними же уходит в бой и в них же сражается.
///
/// Правила рук живут в CharacterEquipment: двуручное оружие и лук занимают обе кисти
/// и вытесняют щит, а щит вытесняет двуручное. Вытесненный предмет падает на землю,
/// потому что положить его некуда.
///
/// Этот компонент связывает надетое с боевыми системами: защита и штрафы уходят
/// в PlayerArmor, класс оружия — в PlayerWeapons, откуда их берёт PlayerCombat.
/// </summary>
public class PlayerGear : MonoBehaviour
{
    [Header("Данные")]
    [Tooltip("База всех предметов. По ней id из сохранения превращается в предмет")]
    [SerializeField] private EquipmentDatabase database;

    [Header("Подбор")]
    [Tooltip("На каком расстоянии игрок видит лежащее снаряжение, м")]
    [SerializeField, Min(0f)] private float pickupRadius = 2f;

    [Tooltip("Слои, на которых лежит снаряжение. Nothing — искать на всех")]
    [SerializeField] private LayerMask pickupMask = ~0;

    [Tooltip("Показывать подсказку 'E — взять' рядом с предметом")]
    [SerializeField] private bool showPrompt = true;

    [Header("Выброс вытесненного")]
    [Tooltip("Куда падает вытесненный предмет: сдвиг вперёд от игрока, м")]
    [SerializeField] private float dropForward = 0.8f;

    [Tooltip("Сколько предмет нельзя поднять после выброса, с. Иначе он сразу вернётся в руки")]
    [SerializeField, Min(0f)] private float dropPickupDelay = 1f;

    [Header("Стартовое снаряжение")]
    [Tooltip("Выдаётся, только если герой ещё ничего не носил")]
    [SerializeField] private List<GearSlotData> startingGear = new List<GearSlotData>();

    [Header("Сохранение")]
    [Tooltip("Снаряжение переносится между сценами и переживает перезапуск")]
    [SerializeField] private bool useSave = true;
    [SerializeField] private string saveFileName = "gear.json";

    [Header("Ссылки (пусто — найдутся на этом объекте)")]
    [SerializeField] private CharEquip equipment;
    [SerializeField] private InputHandler inputHandler;
    [SerializeField] private PlayerWeapons weapons;
    [SerializeField] private PlayerArmor armor;

    [Header("Статус (только чтение)")]
    [SerializeField] private string rightHand = "пусто";
    [SerializeField] private string leftHand = "пусто";
    [SerializeField] private string head = "пусто";
    [SerializeField] private string body = "пусто";

    // Надетое переносится между сценами: герой снарядился и вышел в бой в том же виде
    private static readonly Dictionary<EquipmentSlot, string> Carried = new Dictionary<EquipmentSlot, string>();
    private static bool carriedLoaded;

    // Порядок применения: руки последние, чтобы правила вытеснения считались от готового тела
    private static readonly EquipmentSlot[] ApplyOrder =
    {
        EquipmentSlot.Helmet, EquipmentSlot.Body, EquipmentSlot.Arms, EquipmentSlot.Legs,
        EquipmentSlot.RightHand, EquipmentSlot.LeftHand
    };

    private EquipmentPickup nearest;
    private bool applyingLoadout;
    private bool warnedLimbArmor;
    private bool dirty;

    /// <summary>Снаряжение в руках изменилось: подобрали, сменили или выбили.</summary>
    public event Action OnGearChanged;

    public EquipmentItem MainHand => equipment != null ? equipment.GetEquipped(EquipmentSlot.RightHand) : null;
    public EquipmentItem OffHand => equipment != null ? equipment.GetEquipped(EquipmentSlot.LeftHand) : null;

    /// <summary>Оружие в руках занимает обе кисти, значит щит взять нельзя.</summary>
    public bool HandsFull => MainHand != null && MainHand.IsTwoHanded;

    public bool HasShield => OffHand != null && OffHand.IsOffHand;

    public string SavePath => Path.Combine(Application.persistentDataPath, saveFileName);

    // ================= ЖИЗНЕННЫЙ ЦИКЛ =================

    private void Awake()
    {
        if (equipment == null) equipment = GetComponent<CharEquip>();
        if (inputHandler == null) inputHandler = GetComponent<InputHandler>();
        if (weapons == null) weapons = GetComponent<PlayerWeapons>();

        if (equipment == null)
        {
            Debug.LogError("[Gear] Нужен CharacterEquipment на игроке — без него нечего держать в руках", this);
            return;
        }

        equipment.OnItemEquipped += HandleEquipped;
        equipment.OnItemUnequipped += HandleUnequipped;
        equipment.OnItemDisplaced += HandleDisplaced;
    }

    private void OnDestroy()
    {
        if (equipment == null) return;

        equipment.OnItemEquipped -= HandleEquipped;
        equipment.OnItemUnequipped -= HandleUnequipped;
        equipment.OnItemDisplaced -= HandleDisplaced;
    }

    private void Start()
    {
        // PlayerArmor создаёт PlayerHealth в своём Awake, поэтому ссылку добираем здесь
        if (armor == null) armor = GetComponent<PlayerArmor>();

        // Оружие меняется подбором, а не цифрами: иначе статы разойдутся с тем, что в руках
        if (weapons != null)
        {
            weapons.SelectWithNumberKeys = false;

            // Модель на кости руки — одна; PlayerWeapons не дублирует её на корне игрока
            if (equipment != null && equipment.HasPoint(AttachmentPointId.RightHand))
                weapons.ShowModel = false;
        }

        if (database == null)
            Debug.LogError("[Gear] Не назначена EquipmentDatabase — снаряжение не загрузится", this);

        if (equipment != null) ApplyCarried();
    }

    private void Update()
    {
        nearest = FindNearestPickup();

        if (nearest != null && inputHandler != null && inputHandler.InteractPressed)
            Pickup(nearest);
    }

    private void LateUpdate()
    {
        // Один подбор меняет несколько слотов, поэтому пишем файл один раз за кадр
        if (!dirty) return;

        dirty = false;
        Save();
    }

    private void OnApplicationQuit()
    {
        if (dirty) Save();
    }

    // ================= ПОДБОР =================

    /// <summary>Взять предмет с земли сразу в руки.</summary>
    public void Pickup(EquipmentPickup pickup)
    {
        if (pickup == null || !pickup.IsReady || equipment == null) return;

        EquipmentItem item = pickup.Item;

        equipment.Equip(item);
        pickup.Consume();

        Debug.Log($"[Gear] Взял: {Describe(item)}");
    }

    /// <summary>Выбросить предмет из слота на землю.</summary>
    public void DropSlot(EquipmentSlot slot)
    {
        if (equipment == null) return;

        EquipmentItem removed = equipment.Unequip(slot);
        if (removed != null) DropToGround(removed);
    }

    private EquipmentPickup FindNearestPickup()
    {
        if (pickupRadius <= 0f) return null;

        Collider[] hits = Physics.OverlapSphere(transform.position, pickupRadius, pickupMask,
                                                QueryTriggerInteraction.Collide);

        EquipmentPickup best = null;
        float bestDistance = float.MaxValue;

        foreach (Collider hit in hits)
        {
            var pickup = hit.GetComponentInParent<EquipmentPickup>();
            if (pickup == null || !pickup.IsReady) continue;

            float distance = (pickup.transform.position - transform.position).sqrMagnitude;
            if (distance >= bestDistance) continue;

            bestDistance = distance;
            best = pickup;
        }

        return best;
    }

    private void DropToGround(EquipmentItem item)
    {
        Vector3 position = transform.position + transform.forward * dropForward;

        // Кладём на землю, чтобы предмет не висел в воздухе и не падал под пол
        if (Physics.Raycast(position + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 8f))
            position = hit.point + Vector3.up * 0.08f;

        EquipmentPickup.Drop(item, position, dropPickupDelay);
        Debug.Log($"[Gear] Вытеснено и брошено: {item.displayName}");
    }

    // ================= РЕАКЦИЯ НА СМЕНУ СНАРЯЖЕНИЯ =================

    private void HandleEquipped(EquipmentSlot slot, EquipmentItem item)
    {
        Carried[slot] = item.itemId;
        ApplySlot(slot, item);
        AfterGearChanged();
    }

    private void HandleUnequipped(EquipmentSlot slot)
    {
        Carried.Remove(slot);
        ApplySlot(slot, null);
        AfterGearChanged();
    }

    private void HandleDisplaced(EquipmentItem item)
    {
        // При выдаче стартового набора вытеснять нечего, так что бросать тоже нечего
        if (applyingLoadout) return;

        DropToGround(item);
    }

    private void AfterGearChanged()
    {
        RefreshStatus();

        if (applyingLoadout) return;

        dirty = true;
        OnGearChanged?.Invoke();
    }

    /// <summary>Разложить предмет слота по боевым системам.</summary>
    private void ApplySlot(EquipmentSlot slot, EquipmentItem item)
    {
        switch (slot)
        {
            case EquipmentSlot.Helmet:
                ApplyArmorSlot(armor != null ? armor.Helmet : null, item);
                break;

            case EquipmentSlot.Body:
                ApplyArmorSlot(armor != null ? armor.Body : null, item);
                break;

            case EquipmentSlot.RightHand:
                ApplyWeapon(item);
                break;

            case EquipmentSlot.Arms:
            case EquipmentSlot.Legs:
                WarnLimbArmor(slot, item);
                break;
        }
    }

    private static void ApplyArmorSlot(ArmorSlot armorSlot, EquipmentItem item)
    {
        if (armorSlot == null) return;

        if (item == null || !item.HasArmorStats)
            armorSlot.TakeOff();
        else
            armorSlot.Wear(item.displayName, item.ArmorA, item.ArmorProfile,
                           item.MovementPenalty, item.AttackPenalty);
    }

    private void ApplyWeapon(EquipmentItem item)
    {
        if (weapons == null) return;

        if (item == null)
        {
            weapons.Unequip();
            return;
        }

        if (weapons.SelectWeaponById(item.itemId)) return;

        // Запись без id, но того же класса — её модель и положение в руке уже настроены
        for (int i = 0; i < weapons.Weapons.Count; i++)
        {
            WeaponItem entry = weapons.Weapons[i];
            if (entry == null || !string.IsNullOrEmpty(entry.itemId) ||
                entry.weaponClass != item.weaponClass) continue;

            entry.itemId = item.itemId;
            weapons.SelectWeapon(i);
            return;
        }

        // Если на модели есть кисть, модель предмета вешает CharacterEquipment,
        // и дублировать её через PlayerWeapons не нужно
        bool handPointExists = equipment != null && equipment.HasPoint(AttachmentPointId.RightHand);

        weapons.SelectWeapon(weapons.AddWeapon(new WeaponItem
        {
            itemId = item.itemId,
            displayName = item.displayName,
            weaponClass = item.weaponClass,
            model = handPointExists ? null : item.modelPrefab
        }));
    }

    private void WarnLimbArmor(EquipmentSlot slot, EquipmentItem item)
    {
        if (item == null || !item.HasArmorStats || warnedLimbArmor) return;

        warnedLimbArmor = true;
        Debug.LogWarning($"[Gear] Защита {slot} пока не учитывается: у PlayerArmor есть только " +
                         "корпус и шлем. Предмет надевается, но защиту не даёт.", this);
    }

    // ================= ПЕРЕНОС МЕЖДУ СЦЕНАМИ И СОХРАНЕНИЕ =================

    private void ApplyCarried()
    {
        if (!carriedLoaded)
        {
            carriedLoaded = true;
            if (!Load()) TakeStartingGear();
        }

        applyingLoadout = true;

        foreach (EquipmentSlot slot in ApplyOrder)
        {
            if (!Carried.TryGetValue(slot, out string itemId)) continue;

            EquipmentItem item = Resolve(itemId);
            if (item == null) continue;

            // Защита от правленого руками сохранения: щит и двуручное вместе не живут
            if (slot == EquipmentSlot.LeftHand && HandsFull)
            {
                Debug.LogWarning($"[Gear] '{item.displayName}' не взят: в руках двуручное оружие", this);
                continue;
            }

            equipment.Equip(item, slot == EquipmentSlot.LeftHand ? PreferredHand.Left : (PreferredHand?)null);
        }

        // Приводим список к тому, что реально надето: сохранение могли править руками,
        // да и предмет мог уехать в другую руку по своей подкатегории
        Carried.Clear();
        foreach (EquipmentSlot slot in ApplyOrder)
        {
            EquipmentItem worn = equipment.GetEquipped(slot);
            if (worn != null) Carried[slot] = worn.itemId;
        }

        applyingLoadout = false;

        RefreshStatus();
        OnGearChanged?.Invoke();
    }

    private void TakeStartingGear()
    {
        foreach (GearSlotData entry in startingGear)
        {
            if (entry == null || Resolve(entry.itemId) == null) continue;

            Carried[entry.slot] = entry.itemId;
        }
    }

    private EquipmentItem Resolve(string itemId)
    {
        if (database == null || string.IsNullOrEmpty(itemId)) return null;

        EquipmentItem item = database.GetById(itemId);
        if (item == null)
            Debug.LogWarning($"[Gear] Предмета '{itemId}' нет в базе", this);

        return item;
    }

    public void Save()
    {
        if (!useSave) return;

        var data = new GearSaveData();
        foreach (var pair in Carried)
            data.slots.Add(new GearSlotData { slot = pair.Key, itemId = pair.Value });

        try
        {
            File.WriteAllText(SavePath, JsonUtility.ToJson(data, true));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Gear] Не удалось сохранить в {SavePath}: {e.Message}");
        }
    }

    /// <summary>Загрузить снаряжение из файла. false — файла нет или он повреждён.</summary>
    public bool Load()
    {
        if (!useSave || !File.Exists(SavePath)) return false;

        GearSaveData data;
        try
        {
            data = JsonUtility.FromJson<GearSaveData>(File.ReadAllText(SavePath));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Gear] Сохранение повреждено, выдаю стартовое снаряжение: {e.Message}");
            return false;
        }

        if (data == null || data.slots.Count == 0) return false;

        Carried.Clear();
        foreach (GearSlotData entry in data.slots)
        {
            if (entry != null && !string.IsNullOrEmpty(entry.itemId))
                Carried[entry.slot] = entry.itemId;
        }

        return true;
    }

    /// <summary>Забыть снаряжение и начать со стартового набора.</summary>
    [ContextMenu("Сбросить снаряжение")]
    public void ResetGear()
    {
        if (File.Exists(SavePath)) File.Delete(SavePath);

        Carried.Clear();
        carriedLoaded = false;

        if (!Application.isPlaying || equipment == null) return;

        foreach (EquipmentSlot slot in ApplyOrder)
            equipment.Unequip(slot);

        ApplyCarried();
    }

    // ================= ОТОБРАЖЕНИЕ =================

    private void RefreshStatus()
    {
        rightHand = Describe(equipment != null ? equipment.GetEquipped(EquipmentSlot.RightHand) : null);
        leftHand = Describe(equipment != null ? equipment.GetEquipped(EquipmentSlot.LeftHand) : null);
        head = Describe(equipment != null ? equipment.GetEquipped(EquipmentSlot.Helmet) : null);
        body = Describe(equipment != null ? equipment.GetEquipped(EquipmentSlot.Body) : null);
    }

    private static string Describe(EquipmentItem item)
    {
        if (item == null) return "пусто";

        if (item.HasWeaponStats)
        {
            WeaponClassStats s = item.WeaponStats;
            string hands = item.IsTwoHanded ? "двуручное" : item.IsOffHand ? "вторая рука" : "одноручное";
            return $"{item.displayName} ({hands}): {s.Damage}, {s.Range:0.##} м, цикл {s.Cycle:0.00} с";
        }

        if (item.HasArmorStats)
            return $"{item.displayName}: защита {item.ArmorA:0.#} {item.ArmorProfile}";

        return item.displayName;
    }

    private void OnGUI()
    {
        if (!showPrompt || nearest == null) return;

        var style = new GUIStyle(GUI.skin.label)
        {
            richText = true,
            fontSize = 18,
            alignment = TextAnchor.MiddleCenter
        };

        string warning = nearest.Item != null && nearest.Item.IsOffHand && HandsFull
            ? "  <color=orange>(двуручное уйдёт на землю)</color>"
            : "";

        GUI.Label(new Rect(0f, Screen.height * 0.62f, Screen.width, 30f),
                  $"<color=yellow>E</color> — взять: {nearest.DisplayName}{warning}", style);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, pickupRadius);
    }
}
