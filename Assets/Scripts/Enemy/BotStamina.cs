using UnityEngine;

/// <summary>
/// Выносливость бота: тратится на атаку, рывок и бег, восстанавливается со временем.
/// Стоимость атаки берётся из класса активного оружия (BotWeapons выставляет её через SetWeaponStats).
/// </summary>
public class BotStamina : MonoBehaviour
{
    [Header("Stamina")]
    [SerializeField] private float _maxStamina = 100f;
    [SerializeField] private float _currentStamina = 100f;
    [SerializeField] private float _regenRate = 20f;
    [SerializeField] private float _regenDelay = 0.6f;

    [Header("Base Costs (если нет оружия)")]
    [SerializeField] private float _attackCost = 15f;
    [SerializeField] private float _dashCost = 25f;
    [SerializeField] private float _runCostPerSecond = 15f;

    private WeaponClassStats _weaponStats;
    private float _regenTimer;
    private bool _regenBlocked;

    public float MaxStamina => _maxStamina;
    public float CurrentStamina => _currentStamina;
    public float Normalized => _maxStamina > 0f ? _currentStamina / _maxStamina : 0f;
    public bool IsEmpty => _currentStamina <= 0.001f;

    public float AttackCost => _weaponStats.StaminaCost > 0f ? _weaponStats.StaminaCost : _attackCost;
    public float DashCost => _dashCost;
    public float RunCostPerSecond => _runCostPerSecond;

    /// <summary>Обновить показатели оружия — вызывает BotWeapons при смене.</summary>
    public void SetWeaponStats(WeaponClassStats stats) => _weaponStats = stats;

    private void Awake()
    {
        _currentStamina = Mathf.Clamp(_currentStamina, 0f, _maxStamina);
    }

    private void Update()
    {
        if (_regenBlocked)
        {
            _regenTimer -= Time.deltaTime;
            if (_regenTimer <= 0f) _regenBlocked = false;
            return;
        }

        if (_currentStamina < _maxStamina)
            _currentStamina = Mathf.Min(_maxStamina, _currentStamina + _regenRate * Time.deltaTime);
    }

    public bool CanSpend(float amount) => _currentStamina >= amount;

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

    /// <summary>Расход выносливости за бег. Вызывать каждый кадр, пока бот бежит.</summary>
    public void DrainRun(float deltaTime)
    {
        float cost = RunCostPerSecond * deltaTime;
        if (cost <= 0f) return;

        _currentStamina = Mathf.Max(0f, _currentStamina - cost);
        BlockRegen();
    }

    private void BlockRegen()
    {
        _regenTimer = _regenDelay;
        _regenBlocked = true;
    }
}