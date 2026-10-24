using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Одно оружие в арсенале бота. Задаётся в инспекторе префаба.</summary>
[Serializable]
public class BotWeaponItem
{
    [Tooltip("Id предмета для идентификации. Можно оставить пустым")]
    public string itemId = "";

    [Tooltip("Название для логов. Пусто — возьмётся название класса")]
    public string displayName = "";

    [Tooltip("Класс задаёт урон и его тип, дальность, цикл, замах и выносливость")]
    public WeaponClass weaponClass = WeaponClass.Sword;

    [Tooltip("Префаб или FBX модели оружия")]
    public GameObject model;

    [Header("Позиция в руке")]
    public Vector3 handPosition = Vector3.zero;
    public Vector3 handEulerRotation = Vector3.zero;
    public Vector3 handScale = Vector3.one;

    public string ResolvedName => string.IsNullOrWhiteSpace(displayName)
        ? VomicaBalance.GetWeapon(weaponClass).DisplayName
        : displayName;

    public WeaponClassStats Stats => VomicaBalance.GetWeapon(weaponClass);
}

/// <summary>
/// Арсенал бота: список оружия, выбор активного, спавн модели в точке крепления.
/// Все показатели — из VomicaBalance по классу. Штраф атаки — из BotArmor.
/// </summary>
public class BotWeapons : MonoBehaviour
{
    [Header("Арсенал")]
    [Tooltip("Список оружия бота. Настраивается в инспекторе префаба")]
    [SerializeField] private List<BotWeaponItem> _weapons = new List<BotWeaponItem>();

    [Tooltip("Индекс выбранного оружия. -1 — с пустыми руками")]
    [SerializeField] private int _selectedItem = 0;

    [Header("Модель")]
    [Tooltip("Куда крепить модель оружия (кость руки). Пусто — корень бота")]
    [SerializeField] private Transform _modelAttachPoint;

    [Tooltip("Спавнить модель из этого компонента. Если модель уже висит на кости — выключи")]
    [SerializeField] private bool _showModel = true;

    [Header("Ссылки")]
    [SerializeField] private BotArmor _armor;
    [SerializeField] private BotStamina _stamina;

    private GameObject _spawnedModel;
    private int _appliedItem = int.MinValue;

    public IReadOnlyList<BotWeaponItem> Weapons => _weapons;
    public int SelectedIndex => _selectedItem;

    /// <summary>Выбранное оружие или null, если руки пусты.</summary>
    public BotWeaponItem SelectedItem => IsValidIndex(_selectedItem) ? _weapons[_selectedItem] : null;

    public bool HasWeapon => SelectedItem != null && SelectedItem.weaponClass != WeaponClass.Unarmed;
    public WeaponClass SelectedClass => SelectedItem != null ? SelectedItem.weaponClass : WeaponClass.Unarmed;
    public string SelectedName => SelectedItem != null ? SelectedItem.ResolvedName : "пусто";

    public WeaponClassStats Stats => VomicaBalance.GetWeapon(SelectedClass);
    public DamagePacket Damage => Stats.Damage;
    public float TotalDamage => Stats.Damage.Total;
    public float Range => Stats.Range;
    public float StaminaCost => Stats.StaminaCost;
    public int Hands => Stats.Hands;

    /// <summary>Сумма штрафов частоты атак от надетой брони, доля.</summary>
    public float AttackPenalty => _armor != null ? _armor.AttackSpeedPenalty01 : 0f;

    /// <summary>Цикл атаки с учётом штрафа брони.</summary>
    public float Cycle => VomicaBalance.ApplyAttackPenalty(Stats.Cycle, AttackPenalty);

    public float Windup => VomicaBalance.ApplyAttackPenalty(Stats.Windup, AttackPenalty);
    public float ActivePhase => VomicaBalance.ApplyAttackPenalty(VomicaBalance.WeaponActivePhase, AttackPenalty);
    public float AttacksPerSecond => Cycle > 0f ? 1f / Cycle : 0f;

    public bool ShowModel
    {
        get => _showModel;
        set
        {
            if (_showModel == value) return;
            _showModel = value;
            ApplySelection();
        }
    }

    private void Awake()
    {
        if (_armor == null) _armor = GetComponent<BotArmor>();
        if (_stamina == null) _stamina = GetComponent<BotStamina>();
    }

    private void Start()
    {
        if (_armor == null) _armor = GetComponent<BotArmor>();
        ApplySelection();
    }

    private void Update()
    {
        if (_appliedItem != _selectedItem)
            ApplySelection();
    }

    public void SelectWeapon(int index)
    {
        _selectedItem = _weapons.Count == 0 ? -1 : Mathf.Clamp(index, -1, _weapons.Count - 1);
        ApplySelection();
    }

    public void SelectWeapon(WeaponClass weaponClass)
    {
        for (int i = 0; i < _weapons.Count; i++)
        {
            if (_weapons[i] != null && _weapons[i].weaponClass == weaponClass)
            {
                SelectWeapon(i);
                return;
            }
        }
    }

    public void Unequip() => SelectWeapon(-1);

    private bool IsValidIndex(int index) => index >= 0 && index < _weapons.Count && _weapons[index] != null;

    /// <summary>Спавнит модель выбранного оружия в точке крепления.</summary>
    private void ApplySelection()
    {
        _appliedItem = _selectedItem;

        if (_spawnedModel != null)
        {
            Destroy(_spawnedModel);
            _spawnedModel = null;
        }

        BotWeaponItem item = SelectedItem;

        if (_showModel && item != null && item.model != null)
        {
            Transform parent = _modelAttachPoint != null ? _modelAttachPoint : transform;

            _spawnedModel = Instantiate(item.model, parent);
            _spawnedModel.transform.localPosition = item.handPosition;
            _spawnedModel.transform.localRotation = Quaternion.Euler(item.handEulerRotation);

            Vector3 parentScale = parent.lossyScale;
            Vector3 compensation = new Vector3(
                parentScale.x != 0f ? 1f / parentScale.x : 1f,
                parentScale.y != 0f ? 1f / parentScale.y : 1f,
                parentScale.z != 0f ? 1f / parentScale.z : 1f
            );

            _spawnedModel.transform.localScale = Vector3.Scale(compensation, item.handScale);

            // Модель не должна ловить коллайдеры и попадать под удары
            foreach (var c in _spawnedModel.GetComponentsInChildren<Collider>(true))
                c.enabled = false;
        }

        if (_stamina != null)
            _stamina.SetWeaponStats(Stats);
    }
}