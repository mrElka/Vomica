using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CharacterEquipment;
using UnityEngine;
using CharEquip = CharacterEquipment.CharacterEquipment;

public enum InventorySlotKind
{
    /// <summary>Надетый предмет: шлем, броня, руки, ноги, оружие, щит. Набор фиксированный.</summary>
    Equipment,
    /// <summary>Быстрая панель. Первые 9 ячеек доступны по цифрам 1..9.</summary>
    Hotbar,
    /// <summary>Рюкзак.</summary>
    Backpack
}

/// <summary>
/// Все id слотов. UI привязывается к слоту по этой строке, сохранение тоже хранит её.
/// Количество ячеек хотбара и рюкзака задаёт PlayerInventory, а не этот класс.
/// </summary>
public static class InventorySlotIds
{
    public const string None = "none";

    public const string Helmet = "helmet";
    public const string Armor = "armor";
    public const string Arms = "arms";
    public const string Legs = "legs";
    public const string Weapon = "weapon";
    public const string Shield = "shield";

    public const string HotbarPrefix = "hotbar_";
    public const string BackpackPrefix = "inv_";

    /// <summary>Сколько ячеек хотбара можно выбрать цифрами 1..9.</summary>
    public const int NumberKeySlots = 9;

    public static readonly string[] Equipment = { Helmet, Armor, Arms, Legs, Weapon, Shield };

    /// <summary>hotbar_0, hotbar_1, ...</summary>
    public static string Hotbar(int index) => HotbarPrefix + index;

    /// <summary>inv_0, inv_1, ...</summary>
    public static string Backpack(int index) => BackpackPrefix + index;

    /// <summary>
    /// В какой слот экипировки идёт предмет. Щит — отдельный слот,
    /// всё остальное оружие (одноручное, двуручное, дальнее) — в weapon.
    /// </summary>
    public static string ForItem(EquipmentItem item)
    {
        if (item == null) return None;

        switch (item.category)
        {
            case EquipmentCategory.Helmet: return Helmet;
            case EquipmentCategory.Armor: return Armor;
            case EquipmentCategory.Arms: return Arms;
            case EquipmentCategory.Legs: return Legs;
            default:
                return item.weaponSubCategory == WeaponSubCategory.Shield ? Shield : Weapon;
        }
    }
}

/// <summary>Содержимое одного слота: в сохранении и в стартовом наборе.</summary>
[Serializable]
public class InventorySlotData
{
    public string slotId;
    public string itemId;
    [Min(1)] public int count = 1;
}

[Serializable]
public class InventorySaveData
{
    public int version = 2;
    public int hotbarSize = -1;
    public int backpackSize = -1;
    public List<InventorySlotData> slots = new List<InventorySlotData>();
}

/// <summary>Строка статуса SelectedItems для инспектора.</summary>
[Serializable]
public class SelectedItemEntry
{
    public string slot;
    public string itemId;
}

public class InventorySlot
{
    public string SlotId { get; }
    public InventorySlotKind Kind { get; }

    public EquipmentItem Item { get; internal set; }
    public int Count { get; internal set; }

    public bool IsEmpty => Item == null || Count <= 0;
    public bool IsEquipment => Kind == InventorySlotKind.Equipment;

    /// <summary>itemId лежащего предмета или "none".</summary>
    public string ItemId => IsEmpty ? InventorySlotIds.None : Item.itemId;

    public InventorySlot(string slotId, InventorySlotKind kind)
    {
        SlotId = slotId;
        Kind = kind;
    }

    /// <summary>Можно ли положить сюда предмет. null (пусто) можно всегда.</summary>
    public bool Accepts(EquipmentItem item)
    {
        if (item == null || !IsEquipment) return true;
        return InventorySlotIds.ForItem(item) == SlotId;
    }
}

/// <summary>
/// Инвентарь игрока: слоты экипировки, хотбар и рюкзак. Количество ячеек хотбара
/// и рюкзака задаётся в инспекторе и меняется из кода (AddSlots / SetSize).
/// Предметы хранятся по itemId из EquipmentDatabase, состояние сохраняется в JSON.
/// Подробности для UI — в ReadmeInventory.md рядом со скриптом.
/// </summary>
public class PlayerInventory : MonoBehaviour
{
    public static PlayerInventory Instance { get; private set; }

    [Header("Данные")]
    [Tooltip("База всех предметов. По ней id из сохранения превращается в предмет")]
    [SerializeField] private EquipmentDatabase database;

    [Header("Размер")]
    [Tooltip("Ячеек хотбара при первом запуске. Из кода — AddSlots / SetSize")]
    [SerializeField, Min(0)] private int hotbarSize = 9;
    [Tooltip("Ячеек рюкзака при первом запуске. Из кода — AddSlots / SetSize")]
    [SerializeField, Min(0)] private int backpackSize = 27;

    [Header("Стартовый набор")]
    [Tooltip("Выдаётся, только если сохранения ещё нет")]
    [SerializeField] private List<InventorySlotData> startingItems = new List<InventorySlotData>();

    [Header("Сохранение")]
    [SerializeField] private bool useSave = true;
    [SerializeField] private string saveFileName = "inventory.json";

    [Header("Управление")]
    [Tooltip("Цифры 1..9 надевают предмет из ячейки хотбара с этим номером")]
    [SerializeField] private bool hotbarNumberKeys = true;

    [Header("Ссылки (пусто — найдутся на этом объекте)")]
    [SerializeField] private InputHandler inputHandler;
    [SerializeField] private PlayerWeapons weapons;
    [SerializeField] private PlayerArmor armor;
    [SerializeField] private CharEquip characterEquipment;

    [Header("Статус (только чтение)")]
    [SerializeField] private List<SelectedItemEntry> selectedItems = new List<SelectedItemEntry>();
    [SerializeField, TextArea(2, 4)] private string selectedItemsJson;

    private readonly Dictionary<string, InventorySlot> slots = new Dictionary<string, InventorySlot>();
    private readonly List<string> equipmentIds = new List<string>();
    private readonly List<string> hotbarIds = new List<string>();
    private readonly List<string> backpackIds = new List<string>();
    private readonly List<string> slotOrder = new List<string>();
    private bool dirty;
    private bool bulkUpdate;

    /// <summary>Содержимое слота изменилось. Аргумент — slotId.</summary>
    public event Action<string> OnSlotChanged;

    /// <summary>Инвентарь загружен или изменён целиком.</summary>
    public event Action OnInventoryChanged;

    /// <summary>Добавились или убрались ячейки. UI должен пересобрать сетку.</summary>
    public event Action OnLayoutChanged;

    /// <summary>Изменилось что-то из надетого. Аргумент — JSON статуса SelectedItems.</summary>
    public event Action<string> OnSelectedItemsChanged;

    public EquipmentDatabase Database => database;

    /// <summary>Все id: экипировка, потом хотбар, потом рюкзак.</summary>
    public IReadOnlyList<string> AllSlotIds => slotOrder;

    public int HotbarSize => hotbarIds.Count;
    public int BackpackSize => backpackIds.Count;

    public string SavePath => Path.Combine(Application.persistentDataPath, saveFileName);

    // ================= ЖИЗНЕННЫЙ ЦИКЛ =================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;

        foreach (string id in InventorySlotIds.Equipment)
            RegisterSlot(new InventorySlot(id, InventorySlotKind.Equipment), equipmentIds);

        GrowTo(InventorySlotKind.Hotbar, hotbarSize);
        GrowTo(InventorySlotKind.Backpack, backpackSize);
        RebuildOrder();

        if (inputHandler == null) inputHandler = GetComponent<InputHandler>();
        if (weapons == null) weapons = GetComponent<PlayerWeapons>();
        if (characterEquipment == null) characterEquipment = GetComponent<CharEquip>();
    }

    private void Start()
    {
        // PlayerArmor добавляет PlayerHealth в своём Awake
        if (armor == null) armor = GetComponent<PlayerArmor>();

        // Оружием управляет инвентарь, чтобы цифры не переключали его в обход слотов
        if (weapons != null && hotbarNumberKeys) weapons.SelectWithNumberKeys = false;

        if (database == null)
            Debug.LogError("[Inventory] Не назначена EquipmentDatabase — предметы не загрузятся");

        bulkUpdate = true;
        bool loaded = Load();
        if (!loaded) PutStartingItems();
        bulkUpdate = false;

        ApplyAllEquipment();
        RefreshStatus();
        dirty = !loaded;

        OnLayoutChanged?.Invoke();
        OnInventoryChanged?.Invoke();
    }

    private void Update()
    {
        if (!hotbarNumberKeys || inputHandler == null) return;

        int index = inputHandler.WeaponSlotPressed;
        if (index >= 0 && index < hotbarIds.Count)
            QuickEquip(hotbarIds[index]);
    }

    private void LateUpdate()
    {
        if (dirty) Save();
    }

    private void OnApplicationQuit()
    {
        if (dirty) Save();
    }

    private void OnDestroy()
    {
        if (Instance != this) return;

        if (dirty) Save();
        Instance = null;
    }

    // ================= ЧТЕНИЕ =================

    public InventorySlot GetSlot(string slotId)
    {
        if (string.IsNullOrEmpty(slotId)) return null;
        slots.TryGetValue(slotId, out var slot);
        return slot;
    }

    public EquipmentItem GetItem(string slotId) => GetSlot(slotId)?.Item;

    /// <summary>Id ячеек одного вида по порядку. По нему UI строит сетку.</summary>
    public IReadOnlyList<string> GetSlotIds(InventorySlotKind kind) => IdsOf(kind);

    public int GetSize(InventorySlotKind kind) => IdsOf(kind).Count;

    /// <summary>itemId надетого предмета в слоте экипировки или "none".</summary>
    public string GetSelected(string equipmentSlotId) => GetSlot(equipmentSlotId)?.ItemId ?? InventorySlotIds.None;

    /// <summary>Статус надетого: helmet / armor / arms / legs / weapon / shield -> itemId или "none".</summary>
    public IReadOnlyDictionary<string, string> SelectedItems
    {
        get
        {
            var result = new Dictionary<string, string>();
            foreach (string id in InventorySlotIds.Equipment)
                result[id] = GetSelected(id);
            return result;
        }
    }

    /// <summary>Тот же статус строкой: {"helmet":"none","armor":"none",...,"weapon":"weapon_2","shield":"none"}</summary>
    public string SelectedItemsJson => selectedItemsJson;

    /// <summary>Можно ли положить предмет в слот: шлем только в helmet и т.д.</summary>
    public bool CanPlace(string slotId, EquipmentItem item)
    {
        var slot = GetSlot(slotId);
        return slot != null && slot.Accepts(item);
    }

    // ================= РАЗМЕР =================

    /// <summary>
    /// Добавить ячейки в конец хотбара или рюкзака. Возвращает id новых ячеек.
    /// Слоты экипировки так не добавляются.
    /// </summary>
    public IReadOnlyList<string> AddSlots(InventorySlotKind kind, int count = 1)
    {
        var added = new List<string>();
        if (kind == InventorySlotKind.Equipment)
        {
            Debug.LogWarning("[Inventory] Слоты экипировки фиксированы, добавлять можно только Hotbar и Backpack");
            return added;
        }

        if (count <= 0) return added;

        added.AddRange(GrowTo(kind, GetSize(kind) + count));
        RebuildOrder();
        dirty = true;
        OnLayoutChanged?.Invoke();
        return added;
    }

    /// <summary>
    /// Задать количество ячеек хотбара или рюкзака. При уменьшении предметы из убираемых
    /// ячеек переезжают в свободные. false — им некуда переехать, размер не изменён.
    /// </summary>
    public bool SetSize(InventorySlotKind kind, int size)
    {
        if (kind == InventorySlotKind.Equipment) return false;

        size = Mathf.Max(0, size);
        int current = GetSize(kind);

        if (size > current)
        {
            AddSlots(kind, size - current);
            return true;
        }

        if (size == current) return true;

        List<string> ids = IdsOf(kind);
        var removed = ids.GetRange(size, current - size);

        // Свободные ячейки, которые останутся после уменьшения
        var free = new List<InventorySlot>();
        foreach (string id in slotOrder)
        {
            var slot = slots[id];
            if (!slot.IsEquipment && slot.IsEmpty && !removed.Contains(id))
                free.Add(slot);
        }

        int needed = 0;
        foreach (string id in removed)
            if (!slots[id].IsEmpty) needed++;

        if (needed > free.Count)
        {
            Debug.LogWarning($"[Inventory] Нельзя уменьшить {kind} до {size}: предметам некуда переехать");
            return false;
        }

        int freeIndex = 0;
        foreach (string id in removed)
        {
            var slot = slots[id];
            if (slot.IsEmpty) continue;

            var target = free[freeIndex++];
            SetSlot(target, slot.Item, slot.Count);
        }

        foreach (string id in removed)
            slots.Remove(id);
        ids.RemoveRange(size, current - size);

        RebuildOrder();
        dirty = true;
        OnLayoutChanged?.Invoke();
        return true;
    }

    // ================= ИЗМЕНЕНИЕ =================

    /// <summary>Положить предмет в первый подходящий слот хотбара, затем рюкзака.</summary>
    public bool AddItem(string itemId, int count = 1)
    {
        var item = database != null ? database.GetById(itemId) : null;
        if (item == null)
        {
            Debug.LogWarning($"[Inventory] Нет предмета с id '{itemId}' в базе");
            return false;
        }

        return AddItem(item, count);
    }

    public bool AddItem(EquipmentItem item, int count = 1)
    {
        if (item == null || count <= 0) return false;

        int maxStack = Mathf.Max(1, item.maxStack);

        // Сначала докладываем в неполные стопки того же предмета
        foreach (string id in slotOrder)
        {
            if (count <= 0) break;

            var slot = slots[id];
            if (slot.IsEquipment || slot.IsEmpty || slot.Item != item || slot.Count >= maxStack) continue;

            int add = Mathf.Min(count, maxStack - slot.Count);
            SetSlot(slot, item, slot.Count + add);
            count -= add;
        }

        foreach (string id in slotOrder)
        {
            if (count <= 0) break;

            var slot = slots[id];
            if (slot.IsEquipment || !slot.IsEmpty) continue;

            int add = Mathf.Min(count, maxStack);
            SetSlot(slot, item, add);
            count -= add;
        }

        if (count > 0)
            Debug.LogWarning($"[Inventory] Нет места для '{item.displayName}', не влезло: {count}");

        return count == 0;
    }

    /// <summary>Убрать предметы из слота. По умолчанию — всю стопку.</summary>
    public bool RemoveItem(string slotId, int count = int.MaxValue)
    {
        var slot = GetSlot(slotId);
        if (slot == null || slot.IsEmpty || count <= 0) return false;

        int left = slot.Count - count;
        SetSlot(slot, left > 0 ? slot.Item : null, Mathf.Max(0, left));
        return true;
    }

    /// <summary>
    /// Перенести предмет из одного слота в другой (drag and drop). Если цель занята —
    /// слоты меняются местами, одинаковые предметы складываются в стопку.
    /// </summary>
    public bool Move(string fromSlotId, string toSlotId)
    {
        var from = GetSlot(fromSlotId);
        var to = GetSlot(toSlotId);
        if (from == null || to == null || from == to || from.IsEmpty) return false;

        int maxStack = Mathf.Max(1, from.Item.maxStack);

        if (!to.IsEmpty && to.Item == from.Item && maxStack > 1 && !to.IsEquipment)
        {
            int moved = Mathf.Min(from.Count, maxStack - to.Count);
            if (moved <= 0) return false;

            SetSlot(to, to.Item, to.Count + moved);
            SetSlot(from, from.Count - moved > 0 ? from.Item : null, from.Count - moved);
            return true;
        }

        if (!to.Accepts(from.Item) || !from.Accepts(to.Item)) return false;

        EquipmentItem toItem = to.Item;
        int toCount = to.Count;

        SetSlot(to, from.Item, from.Count);
        SetSlot(from, toItem, toCount);
        return true;
    }

    /// <summary>
    /// Предмет из хотбара или рюкзака надевается в свой слот экипировки (старый встаёт
    /// на его место), надетый предмет снимается в первую свободную ячейку.
    /// </summary>
    public bool QuickEquip(string slotId)
    {
        var slot = GetSlot(slotId);
        if (slot == null || slot.IsEmpty) return false;

        if (slot.IsEquipment)
            return Unequip(slotId);

        string target = InventorySlotIds.ForItem(slot.Item);
        return Move(slotId, target);
    }

    /// <summary>Снять предмет из слота экипировки в первую свободную ячейку хотбара или рюкзака.</summary>
    public bool Unequip(string equipmentSlotId)
    {
        var slot = GetSlot(equipmentSlotId);
        if (slot == null || !slot.IsEquipment || slot.IsEmpty) return false;

        foreach (string id in slotOrder)
        {
            var free = slots[id];
            if (!free.IsEquipment && free.IsEmpty)
                return Move(equipmentSlotId, id);
        }

        Debug.LogWarning("[Inventory] Некуда снять предмет: инвентарь полон");
        return false;
    }

    /// <summary>Очистить все слоты.</summary>
    public void Clear()
    {
        bulkUpdate = true;
        foreach (var slot in slots.Values)
            SetSlot(slot, null, 0);
        bulkUpdate = false;

        ApplyAllEquipment();
        RefreshStatus();
        dirty = true;
        OnInventoryChanged?.Invoke();
    }

    // ================= СОХРАНЕНИЕ =================

    public void Save()
    {
        dirty = false;
        if (!useSave) return;

        var data = new InventorySaveData
        {
            hotbarSize = hotbarIds.Count,
            backpackSize = backpackIds.Count
        };

        foreach (string id in slotOrder)
        {
            var slot = slots[id];
            if (slot.IsEmpty) continue;

            data.slots.Add(new InventorySlotData { slotId = id, itemId = slot.Item.itemId, count = slot.Count });
        }

        try
        {
            File.WriteAllText(SavePath, JsonUtility.ToJson(data, true));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Inventory] Не удалось сохранить в {SavePath}: {e.Message}");
        }
    }

    /// <summary>Загрузить из файла. false — файла нет или он повреждён.</summary>
    public bool Load()
    {
        if (!useSave || !File.Exists(SavePath)) return false;

        InventorySaveData data;
        try
        {
            data = JsonUtility.FromJson<InventorySaveData>(File.ReadAllText(SavePath));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Inventory] Сохранение повреждено, выдаю стартовый набор: {e.Message}");
            return false;
        }

        if (data == null) return false;

        // Ячейки, добавленные из кода во время игры, восстанавливаются из сохранения
        GrowTo(InventorySlotKind.Hotbar, data.hotbarSize);
        GrowTo(InventorySlotKind.Backpack, data.backpackSize);
        RebuildOrder();

        foreach (var slot in slots.Values)
            SetSlot(slot, null, 0);

        foreach (var entry in data.slots)
            PutEntry(entry, "сохранении");

        Debug.Log($"[Inventory] Загружено из {SavePath}");
        return true;
    }

    /// <summary>Удалить файл сохранения и вернуть стартовый набор.</summary>
    [ContextMenu("Сбросить сохранение инвентаря")]
    public void ResetSave()
    {
        if (File.Exists(SavePath)) File.Delete(SavePath);

        if (!Application.isPlaying) return;

        bulkUpdate = true;
        foreach (var slot in slots.Values)
            SetSlot(slot, null, 0);
        bulkUpdate = false;

        // Ячейки пустые, поэтому уменьшение всегда пройдёт
        SetSize(InventorySlotKind.Hotbar, hotbarSize);
        SetSize(InventorySlotKind.Backpack, backpackSize);

        bulkUpdate = true;
        PutStartingItems();
        bulkUpdate = false;

        ApplyAllEquipment();
        RefreshStatus();
        dirty = true;
        OnInventoryChanged?.Invoke();
    }

    // ================= ВНУТРЕННЕЕ =================

    private List<string> IdsOf(InventorySlotKind kind)
    {
        switch (kind)
        {
            case InventorySlotKind.Equipment: return equipmentIds;
            case InventorySlotKind.Hotbar: return hotbarIds;
            default: return backpackIds;
        }
    }

    /// <summary>Досоздать ячейки до нужного количества. Лишние не удаляет.</summary>
    private List<string> GrowTo(InventorySlotKind kind, int size)
    {
        var added = new List<string>();
        List<string> ids = IdsOf(kind);

        while (ids.Count < size)
        {
            int index = ids.Count;
            string id = kind == InventorySlotKind.Hotbar ? InventorySlotIds.Hotbar(index) : InventorySlotIds.Backpack(index);

            RegisterSlot(new InventorySlot(id, kind), ids);
            added.Add(id);
        }

        return added;
    }

    private void RegisterSlot(InventorySlot slot, List<string> ids)
    {
        slots.Add(slot.SlotId, slot);
        ids.Add(slot.SlotId);
    }

    private void RebuildOrder()
    {
        slotOrder.Clear();
        slotOrder.AddRange(equipmentIds);
        slotOrder.AddRange(hotbarIds);
        slotOrder.AddRange(backpackIds);
    }

    private void PutStartingItems()
    {
        foreach (var entry in startingItems)
            PutEntry(entry, "стартовом наборе");
    }

    private void PutEntry(InventorySlotData entry, string source)
    {
        if (entry == null) return;

        var slot = GetSlot(entry.slotId);
        var item = database != null ? database.GetById(entry.itemId) : null;

        if (slot == null || item == null)
        {
            Debug.LogWarning($"[Inventory] Пропущено в {source}: слот '{entry.slotId}', предмет '{entry.itemId}'");
            return;
        }

        if (!slot.Accepts(item))
        {
            Debug.LogWarning($"[Inventory] '{entry.itemId}' нельзя положить в слот '{entry.slotId}'");
            return;
        }

        SetSlot(slot, item, Mathf.Clamp(entry.count, 1, Mathf.Max(1, item.maxStack)));
    }

    private void SetSlot(InventorySlot slot, EquipmentItem item, int count)
    {
        slot.Item = count > 0 ? item : null;
        slot.Count = slot.Item != null ? count : 0;

        if (bulkUpdate) return;

        dirty = true;

        if (slot.IsEquipment)
        {
            ApplyEquipment(slot);
            ResolveHandConflict(slot);
            RefreshStatus();
        }

        OnSlotChanged?.Invoke(slot.SlotId);
    }

    /// <summary>
    /// Двуручное (и дальнее) оружие занимает обе кисти, поэтому щит и такое оружие
    /// не могут быть надеты одновременно. Тот, кто пришёл последним, вытесняет другого
    /// в свободную ячейку. Если места нет — вытеснение не происходит, остаётся предупреждение.
    /// </summary>
    private void ResolveHandConflict(InventorySlot changed)
    {
        var weaponSlot = slots[InventorySlotIds.Weapon];
        var shieldSlot = slots[InventorySlotIds.Shield];

        if (weaponSlot.IsEmpty || shieldSlot.IsEmpty || !weaponSlot.Item.IsTwoHanded) return;

        // Кого убирать: того, кто не менялся
        string toRemove = changed == shieldSlot ? InventorySlotIds.Weapon : InventorySlotIds.Shield;
        Unequip(toRemove);
    }

    private void ApplyAllEquipment()
    {
        // Сначала разрешаем конфликты рук по данным (например, из сохранения),
        // чтобы визуально ничего не затиралось
        ResolveHandConflict(slots[InventorySlotIds.Weapon]);

        foreach (string id in equipmentIds)
            ApplyEquipment(slots[id]);
    }

    private void ApplyEquipment(InventorySlot slot)
    {
        EquipmentItem item = slot.Item;

        switch (slot.SlotId)
        {
            case InventorySlotIds.Helmet:
                ApplyArmor(armor != null ? armor.Helmet : null, item);
                break;
            case InventorySlotIds.Armor:
                ApplyArmor(armor != null ? armor.Body : null, item);
                break;
            case InventorySlotIds.Weapon:
                ApplyWeapon(item);
                break;
                // arms / legs / shield: в PlayerArmor и PlayerWeapons для них нет слотов,
                // они влияют только на внешний вид (CharacterEquipment)
        }

        if (characterEquipment != null)
            SyncVisual(slot.SlotId, item);
    }

    /// <summary>Показать или убрать модель на персонаже.</summary>
    private void SyncVisual(string slotId, EquipmentItem item)
    {
        if (item != null)
        {
            // Нужная рука и освобождение второй — внутри CharacterEquipment
            characterEquipment.Equip(item);
            return;
        }

        switch (slotId)
        {
            case InventorySlotIds.Helmet:
                characterEquipment.Unequip(EquipmentSlot.Helmet);
                break;
            case InventorySlotIds.Armor:
                characterEquipment.Unequip(EquipmentSlot.Body);
                break;
            case InventorySlotIds.Arms:
                characterEquipment.Unequip(EquipmentSlot.Arms);
                break;
            case InventorySlotIds.Legs:
                characterEquipment.Unequip(EquipmentSlot.Legs);
                break;

            case InventorySlotIds.Weapon:
                {
                    characterEquipment.Unequip(EquipmentSlot.RightHand);

                    // Лук лежит в левой кисти — его тоже надо убрать, но не щит
                    var left = characterEquipment.GetEquipped(EquipmentSlot.LeftHand);
                    if (left != null && left.weaponSubCategory != WeaponSubCategory.Shield)
                        characterEquipment.Unequip(EquipmentSlot.LeftHand);
                    break;
                }

            case InventorySlotIds.Shield:
                {
                    // В левой кисти может быть лук из слота weapon — его не трогаем
                    var left = characterEquipment.GetEquipped(EquipmentSlot.LeftHand);
                    if (left != null && left.weaponSubCategory == WeaponSubCategory.Shield)
                        characterEquipment.Unequip(EquipmentSlot.LeftHand);
                    break;
                }
        }
    }

    private static void ApplyArmor(ArmorSlot armorSlot, EquipmentItem item)
    {
        if (armorSlot == null) return;

        if (item == null)
            armorSlot.TakeOff();
        else
            armorSlot.Wear(item.displayName, item.armorA, item.armorProfile, item.movementPenalty, item.attackPenalty);
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

        // Запись без id, но того же класса — её модель и позиция в руке уже настроены
        for (int i = 0; i < weapons.Weapons.Count; i++)
        {
            WeaponItem entry = weapons.Weapons[i];
            if (entry == null || !string.IsNullOrEmpty(entry.itemId) || entry.weaponClass != item.weaponClass) continue;

            entry.itemId = item.itemId;
            weapons.SelectWeapon(i);
            return;
        }

        // Модель крепит CharacterEquipment, если он есть, иначе PlayerWeapons
        var weapon = new WeaponItem
        {
            itemId = item.itemId,
            displayName = item.displayName,
            weaponClass = item.weaponClass,
            model = characterEquipment != null ? null : item.modelPrefab
        };

        weapons.SelectWeapon(weapons.AddWeapon(weapon));
    }

    private void RefreshStatus()
    {
        selectedItems.Clear();

        var json = new StringBuilder("{");
        for (int i = 0; i < equipmentIds.Count; i++)
        {
            string id = equipmentIds[i];
            string itemId = GetSelected(id);

            selectedItems.Add(new SelectedItemEntry { slot = id, itemId = itemId });

            if (i > 0) json.Append(',');
            json.Append('"').Append(id).Append("\":\"").Append(itemId.Replace("\"", "\\\"")).Append('"');
        }
        json.Append('}');

        string newJson = json.ToString();
        if (newJson == selectedItemsJson) return;

        selectedItemsJson = newJson;
        OnSelectedItemsChanged?.Invoke(selectedItemsJson);
    }
}