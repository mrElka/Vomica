using UnityEngine;

public enum WeaponStaminaMode
{
    /// <summary>Использовать фиксированные стоимости из оружия.</summary>
    Fixed,
    /// <summary>Умножать базовые стоимости стамины на множители оружия.</summary>
    Multiplier,
    /// <summary>Не влиять на стамину вообще (например, кулаки).</summary>
    Ignore
}

[CreateAssetMenu(menuName = "Game/Weapon Data", fileName = "Weapon_")]
public class WeaponData : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string _displayName = "New Weapon";
    [Tooltip("Иконка для UI (необязательно)")]
    [SerializeField] private Sprite _icon;

    [Header("Combat")]
    [SerializeField] private int _damage = 20;
    [SerializeField] private float _attackCooldown = 0.45f;
    [SerializeField] private float _attackRange = 2f;
    [SerializeField] private float _attackDistance = 1.5f;

    [Header("Stamina")]
    [SerializeField] private WeaponStaminaMode _staminaMode = WeaponStaminaMode.Multiplier;

    [Tooltip("Множитель стоимости атаки (при Multiplier). 1 = как у базовой.")]
    [SerializeField, Range(0f, 5f)] private float _attackCostMultiplier = 1f;

    [Tooltip("Множитель стоимости рывка (при Multiplier).")]
    [SerializeField, Range(0f, 5f)] private float _dashCostMultiplier = 1f;

    [Tooltip("Множитель стоимости бега (при Multiplier).")]
    [SerializeField, Range(0f, 5f)] private float _runCostMultiplier = 1f;

    [Tooltip("Фиксированная стоимость атаки (при Fixed).")]
    [SerializeField] private float _flatAttackCost = 15f;

    [Tooltip("Фиксированная стоимость рывка (при Fixed).")]
    [SerializeField] private float _flatDashCost = 25f;

    [Tooltip("Фиксированная стоимость бега в секунду (при Fixed).")]
    [SerializeField] private float _flatRunCostPerSecond = 15f;

    // ==== Public API ====

    public string DisplayName => _displayName;
    public Sprite Icon => _icon;

    public int Damage => _damage;
    public float AttackCooldown => _attackCooldown;
    public float AttackRange => _attackRange;
    public float AttackDistance => _attackDistance;

    public WeaponStaminaMode StaminaMode => _staminaMode;

    /// <summary>
    /// Возвращает итоговую стоимость атаки с учётом режима оружия.
    /// baseCost — базовая стоимость из PlayerStamina (с учётом её модификаторов).
    /// </summary>
    public float GetAttackCost(float baseCost)
    {
        switch (_staminaMode)
        {
            case WeaponStaminaMode.Fixed: return _flatAttackCost;
            case WeaponStaminaMode.Ignore: return 0f;
            case WeaponStaminaMode.Multiplier:
            default: return baseCost * _attackCostMultiplier;
        }
    }

    public float GetDashCost(float baseCost)
    {
        switch (_staminaMode)
        {
            case WeaponStaminaMode.Fixed: return _flatDashCost;
            case WeaponStaminaMode.Ignore: return baseCost; 
            case WeaponStaminaMode.Multiplier:
            default: return baseCost * _dashCostMultiplier;
        }
    }

    public float GetRunCostPerSecond(float baseCost)
    {
        switch (_staminaMode)
        {
            case WeaponStaminaMode.Fixed: return _flatRunCostPerSecond;
            case WeaponStaminaMode.Ignore: return baseCost;
            case WeaponStaminaMode.Multiplier:
            default: return baseCost * _runCostMultiplier;
        }
    }
}