using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Сетка ячеек одного вида (хотбар или рюкзак). Создаёт по ячейке из префаба на
/// каждый слот инвентаря и пересобирается, когда слоты добавляются или убираются.
/// Раскладку задаёт GridLayoutGroup / HorizontalLayoutGroup на контейнере.
/// </summary>
public class InventoryGridView : MonoBehaviour
{
    [Tooltip("Какие ячейки показывать")]
    [SerializeField] private InventorySlotKind kind = InventorySlotKind.Backpack;

    [Tooltip("Префаб ячейки с компонентом InventorySlotView, Slot Id в нём пустой")]
    [SerializeField] private InventorySlotView slotPrefab;

    [Tooltip("Куда спавнить ячейки. Пусто — этот объект")]
    [SerializeField] private Transform container;

    private readonly List<InventorySlotView> spawned = new List<InventorySlotView>();
    private PlayerInventory inventory;

    public InventorySlotKind Kind => kind;
    public IReadOnlyList<InventorySlotView> Cells => spawned;

    private void OnEnable() => TryBind();

    private void OnDisable()
    {
        if (inventory != null) inventory.OnLayoutChanged -= Rebuild;
        inventory = null;
    }

    private void Update()
    {
        if (inventory == null) TryBind();
    }

    /// <summary>Удалить ячейки и создать заново по текущему числу слотов.</summary>
    public void Rebuild()
    {
        foreach (var cell in spawned)
            if (cell != null) Destroy(cell.gameObject);
        spawned.Clear();

        if (inventory == null || slotPrefab == null) return;

        Transform parent = container != null ? container : transform;

        foreach (string id in inventory.GetSlotIds(kind))
        {
            InventorySlotView cell = Instantiate(slotPrefab, parent);
            cell.SlotId = id;
            cell.name = id;
            spawned.Add(cell);
        }
    }

    private void TryBind()
    {
        if (inventory != null || PlayerInventory.Instance == null) return;

        if (slotPrefab == null)
            Debug.LogWarning($"[InventoryGridView] Не назначен префаб ячейки на '{name}'", this);

        inventory = PlayerInventory.Instance;
        inventory.OnLayoutChanged += Rebuild;
        Rebuild();
    }
}
