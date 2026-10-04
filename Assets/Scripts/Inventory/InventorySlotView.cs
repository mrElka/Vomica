using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Ячейка инвентаря в UI. Вешается на слот на макете, в поле slotId пишется id
/// слота: helmet, armor, weapon, shield, hotbar_N, inv_N. Ячейки хотбара и рюкзака
/// обычно создаёт InventoryGridView и сам выставляет им id.
/// ЛКМ — взять предмет, ЛКМ по другому слоту — положить или поменять местами.
/// Shift+ЛКМ — надеть или снять. ПКМ — отменить выбор.
/// </summary>
public class InventorySlotView : MonoBehaviour, IPointerClickHandler
{
    [Tooltip("Id слота из PlayerInventory. Для ячеек из InventoryGridView оставь пустым")]
    [SerializeField] private string slotId = "";

    [Header("Отображение")]
    [Tooltip("Иконка предмета. Берётся из EquipmentItem.icon")]
    [SerializeField] private Image icon;
    [Tooltip("Картинка пустого слота. Пусто — иконка скрывается")]
    [SerializeField] private Sprite emptySprite;
    [Tooltip("Количество в стопке, показывается при 2 и больше")]
    [SerializeField] private Text countText;
    [Tooltip("Подсветка, когда предмет взят из этого слота")]
    [SerializeField] private GameObject pickedHighlight;

    private static InventorySlotView picked;
    private PlayerInventory inventory;

    /// <summary>Можно менять из кода, например при спавне сетки рюкзака из префаба.</summary>
    public string SlotId
    {
        get => slotId;
        set
        {
            slotId = value;
            Refresh();
        }
    }
    public InventorySlot Slot => inventory != null ? inventory.GetSlot(slotId) : null;

    private void OnEnable()
    {
        TryBind();
        SetHighlight(false);
    }

    private void OnDisable()
    {
        Unbind();
        if (picked == this) picked = null;
    }

    private void Update()
    {
        // Инвентарь может появиться позже UI: например, после загрузки сцены
        if (inventory == null) TryBind();
    }

    /// <summary>Перерисовать ячейку по данным инвентаря.</summary>
    public void Refresh()
    {
        InventorySlot slot = Slot;
        bool hasItem = slot != null && !slot.IsEmpty;

        if (icon != null)
        {
            Sprite sprite = hasItem ? slot.Item.icon : emptySprite;
            icon.sprite = sprite;
            icon.enabled = sprite != null;
        }

        if (countText != null)
            countText.text = hasItem && slot.Count > 1 ? slot.Count.ToString() : "";
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (inventory == null) return;

        if (eventData.button == PointerEventData.InputButton.Right)
        {
            ClearPicked();
            return;
        }

        if (eventData.button != PointerEventData.InputButton.Left) return;

        Keyboard keyboard = Keyboard.current;
        bool shift = keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);

        if (shift)
        {
            ClearPicked();
            inventory.QuickEquip(slotId);
            return;
        }

        if (picked == null)
        {
            InventorySlot slot = Slot;
            if (slot == null || slot.IsEmpty) return;

            picked = this;
            SetHighlight(true);
            return;
        }

        InventorySlotView from = picked;
        ClearPicked();

        if (from != this)
            inventory.Move(from.slotId, slotId);
    }

    private void TryBind()
    {
        if (inventory != null || PlayerInventory.Instance == null) return;

        inventory = PlayerInventory.Instance;
        inventory.OnSlotChanged += HandleSlotChanged;
        inventory.OnInventoryChanged += Refresh;

        if (!string.IsNullOrEmpty(slotId) && inventory.GetSlot(slotId) == null)
            Debug.LogWarning($"[InventorySlotView] Нет слота с id '{slotId}' на '{name}'", this);

        Refresh();
    }

    private void Unbind()
    {
        if (inventory == null) return;

        inventory.OnSlotChanged -= HandleSlotChanged;
        inventory.OnInventoryChanged -= Refresh;
        inventory = null;
    }

    private void HandleSlotChanged(string changedSlotId)
    {
        if (changedSlotId == slotId) Refresh();
    }

    private static void ClearPicked()
    {
        if (picked != null) picked.SetHighlight(false);
        picked = null;
    }

    private void SetHighlight(bool value)
    {
        if (pickedHighlight != null) pickedHighlight.SetActive(value);
    }
}
