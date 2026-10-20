using UnityEngine;

/// <summary>
/// Выносливость игрока. Тратится на атаку, рывок и бег, восстанавливается со временем.
/// Стоимости могут модифицироваться оружием (WeaponData) и баффами/дебаффами через публичные модификаторы.
/// </summary>
public class PlayerStamina : MonoBehaviour
{
    [Header("Stamina")]
    [SerializeField] private float _maxStamina = 100f;
    [SerializeField] private float _currentStamina = 100f;
    [SerializeField] private float _regenRate = 20f;
    [SerializeField] private float _regenDelay = 0.6f;

    [Header("Base Costs (без оружия)")]
    [SerializeField] private float _attackCost = 15f;
    [SerializeField] private float _dashCost = 25f;
    [SerializeField] private float _runCostPerSecond = 15f;

    [Header("Dynamic Modifiers (базовые множители)")]
    [SerializeField, Range(0f, 5f)] private float _attackCostModifier = 1f;
    [SerializeField, Range(0f, 5f)] private float _dashCostModifier = 1f;
    [SerializeField, Range(0f, 5f)] private float _runCostModifier = 1f;

    [Header("Weapon")]
    [Tooltip("Текущее оружие. Если null — используются базовые стоимости")]
    [SerializeField] private WeaponData _currentWeapon;

    [Header("UI")]
    [SerializeField] private StaminaBar staminaBar;

    private float _regenTimer;
    private bool _regenBlocked;


    public float MaxStamina => _maxStamina;

    public float CurrentStamina => _currentStamina;

    public float Normalized => _maxStamina > 0f ? _currentStamina / _maxStamina : 0f;

    public bool IsEmpty => _currentStamina <= 0.001f;

 
    public WeaponData CurrentWeapon
    {
        get => _currentWeapon;
        set => _currentWeapon = value;
    }

    private float BaseAttackCost => _attackCost * _attackCostModifier;

    private float BaseDashCost => _dashCost * _dashCostModifier;

    private float BaseRunCostPerSecond => _runCostPerSecond * _runCostModifier;

    public float AttackCost
    {
        get
        {
            float baseCost = BaseAttackCost;
            return _currentWeapon != null
                ? _currentWeapon.GetAttackCost(baseCost)
                : baseCost;
        }
    }

    /// <summary>Стоимость одного рывка. Учитывает базовый модификатор и модификатор оружия.</summary>
    public float DashCost
    {
        get
        {
            float baseCost = BaseDashCost;
            return _currentWeapon != null
                ? _currentWeapon.GetDashCost(baseCost)
                : baseCost;
        }
    }

    /// <summary>Стоимость бега в секунду. Учитывает базовый модификатор и модификатор оружия.</summary>
    public float RunCostPerSecond
    {
        get
        {
            float baseCost = BaseRunCostPerSecond;
            return _currentWeapon != null
                ? _currentWeapon.GetRunCostPerSecond(baseCost)
                : baseCost;
        }
    }

    /// <summary>Модификатор стоимости атаки (для баффов/дебаффов). 1 — базовое значение.</summary>
    public float AttackCostModifier
    {
        get => _attackCostModifier;
        set => _attackCostModifier = Mathf.Max(0f, value);
    }

    /// <summary>Модификатор стоимости рывка (для баффов/дебаффов). 1 — базовое значение.</summary>
    public float DashCostModifier
    {
        get => _dashCostModifier;
        set => _dashCostModifier = Mathf.Max(0f, value);
    }

    /// <summary>Модификатор стоимости бега (для баффов/дебаффов). 1 — базовое значение.</summary>
    public float RunCostModifier
    {
        get => _runCostModifier;
        set => _runCostModifier = Mathf.Max(0f, value);
    }

    private void Awake()
    {
        _currentStamina = Mathf.Clamp(_currentStamina, 0f, _maxStamina);

        if (staminaBar != null)
        {
            staminaBar.SetMaxStamina(_maxStamina);
            staminaBar.SetStamina(_currentStamina);
        }
    }

    private void Update()
    {
        HandleRegen();
        UpdateBar();
        SyncHud();
    }

    /// <summary>Восстанавливает выносливость после паузы или продолжает ждать окончания паузы.</summary>
    private void HandleRegen()
    {
        if (_regenBlocked)
        {
            _regenTimer -= Time.deltaTime;
            if (_regenTimer <= 0f)
                _regenBlocked = false;
            return;
        }

        if (_currentStamina < _maxStamina)
            _currentStamina = Mathf.Min(_maxStamina, _currentStamina + _regenRate * Time.deltaTime);
    }

    /// <summary>Обновляет полоску стамины на экране.</summary>
    private void UpdateBar()
    {
        if (staminaBar != null)
            staminaBar.SetStamina(_currentStamina);
    }

    /// <summary>Проверяет, хватает ли выносливости на указанную трату.</summary>
    public bool CanSpend(float amount) => _currentStamina >= amount;

    /// <summary>Пытается списать указанное количество выносливости. true — удалось.</summary>
    public bool TrySpend(float amount)
    {
        if (amount <= 0f) return true;
        if (_currentStamina < amount) return false;

        _currentStamina -= amount;
        BlockRegen();
        return true;
    }

    public bool TrySpendAttack() => TrySpend(AttackCost);

    public bool TrySpendDash() => TrySpend(DashCost);

    /// <summary>Расход выносливости за бег. Вызывать каждый кадр, пока игрок бежит.</summary>
    public void DrainRun(float deltaTime)
    {
        float cost = RunCostPerSecond * deltaTime;
        if (cost <= 0f) return;

        _currentStamina = Mathf.Max(0f, _currentStamina - cost);
        BlockRegen();
    }

    /// <summary>Восстановить указанное количество выносливости (например, зельем).</summary>
    public void Refill(float amount)
    {
        _currentStamina = Mathf.Min(_maxStamina, _currentStamina + Mathf.Max(0f, amount));
    }

    /// <summary>Блокирует регенерацию на время задержки после траты.</summary>
    private void BlockRegen()
    {
        _regenTimer = _regenDelay;
        _regenBlocked = true;
    }

    /// <summary>Передаёт текущее значение в HUDData.</summary>
    private void SyncHud()
    {
        if (HUDManager.Instance == null) return;

        HUDData data = HUDManager.Instance.GetHUDData();
        if (data == null) return;

        data.staminaPercent = Normalized;
    }
}