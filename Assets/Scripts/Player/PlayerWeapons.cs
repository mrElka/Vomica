using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Одно оружие в арсенале. Числа здесь не хранятся: урон, дальность, цикл
/// и расход выносливости приходят из таблицы по классу (VomicaBalance).
/// </summary>
[Serializable]
public class WeaponItem
{
    [Tooltip("Название для UI и логов. Пусто — возьмётся название класса")]
    public string displayName = "Новое оружие";

    [Tooltip("Класс задаёт урон и его тип, дальность, цикл, замах и выносливость")]
    public WeaponClass weaponClass = WeaponClass.Sword;

    [Tooltip("Префаб модели. Спавнится в точке крепления при выборе этого оружия")]
    public GameObject model;

    public string ResolvedName => string.IsNullOrWhiteSpace(displayName)
        ? VomicaBalance.GetWeapon(weaponClass).DisplayName
        : displayName;

    public WeaponClassStats Stats => VomicaBalance.GetWeapon(weaponClass);
}

/// <summary>
/// Арсенал игрока. Вешается на игрока: в списке weapons задаются название,
/// класс и модель, а selectedItem выбирает активное оружие. Все показатели
/// считаются от класса, штрафы частоты атак берутся из надетой брони.
/// </summary>
public class PlayerWeapons : MonoBehaviour
{
    [Header("Арсенал")]
    [Tooltip("Список оружия: название, класс, модель")]
    [SerializeField] private List<WeaponItem> weapons = new List<WeaponItem>();

    [Header("Выбор")]
    [Tooltip("Индекс выбранного оружия в списке weapons. -1 — с пустыми руками")]
    [SerializeField] private int selectedItem = 0;

    [Tooltip("Переключение оружия цифрами 1..9")]
    [SerializeField] private bool selectWithNumberKeys = true;

    [Header("Модель")]
    [Tooltip("Куда крепить модель оружия. Пусто — корень игрока")]
    [SerializeField] private Transform modelAttachPoint;

    [Header("Ссылки")]
    [SerializeField] private InputHandler inputHandler;
    [SerializeField] private PlayerArmor armor;

    [Header("Отладка")]
    [SerializeField] private bool logWeaponChange = true;

    private GameObject spawnedModel;
    private int appliedItem = int.MinValue;

    /// <summary>Сработало при смене оружия. null в аргументе — руки пусты.</summary>
    public event Action<WeaponItem> OnWeaponChanged;

    // ================= ВЫБОР =================

    public IReadOnlyList<WeaponItem> Weapons => weapons;

    public int SelectedIndex => selectedItem;

    /// <summary>Выбранное оружие или null, если руки пусты.</summary>
    public WeaponItem SelectedItem => IsValidIndex(selectedItem) ? weapons[selectedItem] : null;

    public bool HasWeapon => SelectedItem != null && SelectedItem.weaponClass != WeaponClass.Unarmed;

    public WeaponClass SelectedClass => SelectedItem != null ? SelectedItem.weaponClass : WeaponClass.Unarmed;

    public string SelectedName => SelectedItem != null ? SelectedItem.ResolvedName : "пусто";

    // ================= ПОКАЗАТЕЛИ ИЗ КЛАССА =================

    /// <summary>Табличные показатели класса до штрафов снаряжения.</summary>
    public WeaponClassStats Stats => VomicaBalance.GetWeapon(SelectedClass);

    /// <summary>Урон одного попадания, разложенный по типам.</summary>
    public DamagePacket Damage => Stats.Damage;

    public float TotalDamage => Stats.Damage.Total;

    /// <summary>Максимальная дальность удара от центра персонажа, м.</summary>
    public float Range => Stats.Range;

    public float StaminaCost => Stats.StaminaCost;

    /// <summary>1 — одноручное, 2 — двуручное, 0 — предмет левой руки.</summary>
    public int Hands => Stats.Hands;

    /// <summary>Сумма штрафов частоты атак от надетой брони, доля.</summary>
    public float AttackPenalty => armor != null ? armor.AttackSpeedPenalty01 : 0f;

    /// <summary>Цикл атаки с учётом штрафа брони: базовый / (1 − штраф).</summary>
    public float Cycle => VomicaBalance.ApplyAttackPenalty(Stats.Cycle, AttackPenalty);

    /// <summary>Замах растягивается тем же штрафом, что и цикл.</summary>
    public float Windup => VomicaBalance.ApplyAttackPenalty(Stats.Windup, AttackPenalty);

    /// <summary>Активная фаза, когда оружие может нанести урон.</summary>
    public float ActivePhase => VomicaBalance.ApplyAttackPenalty(VomicaBalance.WeaponActivePhase, AttackPenalty);

    public float AttacksPerSecond => Cycle > 0f ? 1f / Cycle : 0f;

    // ================= ЖИЗНЕННЫЙ ЦИКЛ =================

    private void Awake()
    {
        if (inputHandler == null) inputHandler = GetComponent<InputHandler>();
        if (armor == null) armor = GetComponent<PlayerArmor>();
    }

    private void Start()
    {
        // PlayerArmor может быть добавлена в Awake другим скриптом, поэтому ищем её здесь
        if (armor == null) armor = GetComponent<PlayerArmor>();

        ApplySelection();
    }

    private void Update()
    {
        if (selectWithNumberKeys && inputHandler != null)
        {
            int slot = inputHandler.WeaponSlotPressed;
            if (slot >= 0 && slot < weapons.Count)
                SelectWeapon(slot);
        }

        // Правка selectedItem прямо в инспекторе во время игры тоже должна сработать
        if (appliedItem != selectedItem)
            ApplySelection();
    }

    // ================= API =================

    /// <summary>Выбрать оружие по индексу в списке. -1 — убрать оружие.</summary>
    public void SelectWeapon(int index)
    {
        selectedItem = weapons.Count == 0 ? -1 : Mathf.Clamp(index, -1, weapons.Count - 1);
        ApplySelection();
    }

    /// <summary>Выбрать первое оружие указанного класса. false — такого в списке нет.</summary>
    public bool SelectWeapon(WeaponClass weaponClass)
    {
        for (int i = 0; i < weapons.Count; i++)
        {
            if (weapons[i] != null && weapons[i].weaponClass == weaponClass)
            {
                SelectWeapon(i);
                return true;
            }
        }

        return false;
    }

    /// <summary>Убрать оружие из рук.</summary>
    public void Unequip() => SelectWeapon(-1);

    /// <summary>Добавить оружие в список в рантайме. Возвращает его индекс.</summary>
    public int AddWeapon(WeaponItem item)
    {
        if (item == null) return -1;

        weapons.Add(item);
        return weapons.Count - 1;
    }

    /// <summary>Строка с показателями выбранного оружия — для логов и отладочного HUD.</summary>
    public string GetStatsSummary()
    {
        if (!HasWeapon) return "руки пусты";

        WeaponClassStats stats = Stats;

        return $"{SelectedName} [{stats.DisplayName}]: {stats.Damage} | " +
               $"дальность {stats.Range:0.##} м | цикл {Cycle:0.00} с | замах {Windup:0.00} с | " +
               $"{AttacksPerSecond:0.00} уд/с | выносливость {stats.StaminaCost:0.#} | руки: {HandsLabel}";
    }

    private string HandsLabel => Hands == 0 ? "левая" : Hands.ToString();

    // ================= ВНУТРЕННЕЕ =================

    private bool IsValidIndex(int index) => index >= 0 && index < weapons.Count && weapons[index] != null;

    private void ApplySelection()
    {
        appliedItem = selectedItem;

        if (spawnedModel != null)
        {
            Destroy(spawnedModel);
            spawnedModel = null;
        }

        WeaponItem item = SelectedItem;

        if (item != null && item.model != null)
        {
            Transform parent = modelAttachPoint != null ? modelAttachPoint : transform;

            spawnedModel = Instantiate(item.model, parent);
            spawnedModel.transform.localPosition = Vector3.zero;
            spawnedModel.transform.localRotation = Quaternion.identity;
        }

        if (logWeaponChange)
            Debug.Log($"[Weapon] {GetStatsSummary()}");

        OnWeaponChanged?.Invoke(item);
    }

    private void OnValidate()
    {
        if (weapons.Count == 0)
            selectedItem = -1;
        else
            selectedItem = Mathf.Clamp(selectedItem, -1, weapons.Count - 1);
    }
}
