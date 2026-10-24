using UnityEngine;

/// <summary>Один слот брони бота (тело или шлем). Настраивается в инспекторе префаба.</summary>
[System.Serializable]
public class BotArmorSlot
{
    [SerializeField] private string _name = "Slot";
    [SerializeField] private bool _isEquipped = true;

    [Header("Balance v0.1")]
    [Tooltip("Базовая защита A из таблицы набора брони")]
    [SerializeField] private float _armorA = 0f;

    [Tooltip("Профиль материала: меняет защиту A против рубящего, колющего и дробящего")]
    [SerializeField] private ArmorProfile _profile = ArmorProfile.None;

    [Tooltip("Штраф движения за этот предмет, доля: 0.02 = -2%")]
    [SerializeField, Range(0f, 0.5f)] private float _movementPenalty = 0f;

    [Tooltip("Штраф частоты атак за этот предмет, доля")]
    [SerializeField, Range(0f, 0.5f)] private float _attackPenalty = 0f;

    public string Name => _name;
    public bool IsEquipped => _isEquipped;

    public float ArmorA
    {
        get => _isEquipped ? Mathf.Max(0f, _armorA) : 0f;
        set => _armorA = Mathf.Max(0f, value);
    }

    public ArmorProfile Profile
    {
        get => _profile;
        set => _profile = value;
    }

    public float MovementPenalty => _isEquipped ? _movementPenalty : 0f;
    public float AttackPenalty => _isEquipped ? _attackPenalty : 0f;
}

/// <summary>
/// Броня бота: два слота (тело/шлем), профиль материала, штрафы движения и атаки.
/// Считает урон по той же формуле VomicaBalance, что и игрок.
/// Настраивается целиком в инспекторе префаба.
/// </summary>
public class BotArmor : MonoBehaviour
{
    [Header("Body Armor")]
    [SerializeField] private BotArmorSlot _body = new BotArmorSlot();

    [Header("Helmet")]
    [SerializeField] private BotArmorSlot _helmet = new BotArmorSlot();

    public BotArmorSlot Body => _body;
    public BotArmorSlot Helmet => _helmet;

    /// <summary>Суммарный штраф движения, доля 0..1.</summary>
    public float MovementPenalty01 => Mathf.Clamp01(_body.MovementPenalty + _helmet.MovementPenalty);

    /// <summary>Суммарный штраф частоты атак, доля 0..1.</summary>
    public float AttackSpeedPenalty01 => Mathf.Clamp01(_body.AttackPenalty + _helmet.AttackPenalty);

    /// <summary>Слот, который защищает указанную зону. Голова — шлем, всё остальное — тело.</summary>
    public BotArmorSlot GetSlot(BodyZone zone) => zone == BodyZone.Head ? _helmet : _body;

    /// <summary>Потеря HP от попадания с учётом типов урона и зоны.</summary>
    public float ApplyArmor(DamagePacket damage, BodyZone zone = BodyZone.Torso)
    {
        float total = damage.Total;
        if (total <= 0f) return 0f;

        BotArmorSlot slot = GetSlot(zone);

        if (slot.ArmorA > 0f)
            return VomicaBalance.ComputeHpLoss(damage, slot.ArmorA, slot.Profile);

        return total;
    }
}