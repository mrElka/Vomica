using UnityEngine;
using UnityEngine.UI;
[System.Serializable]
public class ArmorSlot
{
    [SerializeField] private string _name = "Slot";
    [SerializeField, Range(0f, 100f)] private float _damageReductionPercent = 0f;
    [SerializeField] private bool _isEquipped = true;

    [Header("Balance v0.1")]
    [Tooltip("Базовая защита A из таблицы набора брони. 0 — работает старая модель в процентах")]
    [SerializeField] private float _armorA = 0f;

    [Tooltip("Профиль материала: меняет защиту A против рубящего, колющего и дробящего")]
    [SerializeField] private ArmorProfile _profile = ArmorProfile.None;

    [Tooltip("Штраф движения за этот предмет, доля: 0.02 = -2%")]
    [SerializeField, Range(0f, 0.5f)] private float _movementPenalty = 0f;

    [Tooltip("Штраф частоты атак за этот предмет, доля")]
    [SerializeField, Range(0f, 0.5f)] private float _attackPenalty = 0f;

    public string Name => _name;
    public bool IsEquipped => _isEquipped;

    /// <summary>Базовая защита A. Если слот не надет — 0.</summary>
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

    /// <summary>Взять значения из таблицы набора брони (раздел 5 документа).</summary>
    public void ApplyPreset(ArmorItemPreset preset)
    {
        _name = preset.DisplayName;
        _armorA = preset.ArmorA;
        _profile = preset.Profile;
        _movementPenalty = preset.MovementPenalty;
        _attackPenalty = preset.AttackPenalty;
    }

    /// <summary>Надеть предмет из инвентаря с параметрами из документа баланса.</summary>
    public void Wear(string name, float armorA, ArmorProfile profile, float movementPenalty, float attackPenalty)
    {
        _name = name;
        _armorA = Mathf.Max(0f, armorA);
        _profile = profile;
        _movementPenalty = movementPenalty;
        _attackPenalty = attackPenalty;
        _isEquipped = true;
    }

    /// <summary>Снять предмет: слот перестаёт защищать и штрафовать.</summary>
    public void TakeOff()
    {
        _armorA = 0f;
        _profile = ArmorProfile.None;
        _movementPenalty = 0f;
        _attackPenalty = 0f;
        _isEquipped = false;
    }

    /// <summary>Снижение урона в процентах (0..100). Если слот не надет — 0.</summary>
    public float DamageReductionPercent
    {
        get => _isEquipped ? _damageReductionPercent : 0f;
        set => _damageReductionPercent = Mathf.Clamp(value, 0f, 100f);
    }

    /// <summary>Снижение урона как доля (0..1).</summary>
    public float Reduction01 => DamageReductionPercent / 100f;

    public void SetEquipped(bool value) => _isEquipped = value;
}

public class PlayerArmor : MonoBehaviour
{
    [Header("Body Armor (броня)")]
    [SerializeField] private ArmorSlot _body = new ArmorSlot();

    [Header("Helmet (шлем)")]
    [SerializeField] private ArmorSlot _helmet = new ArmorSlot();

    [Header("Settings")]
    [Tooltip("Вкл — мультипликативно: (1-body)*(1-helmet). Выкл — аддитивно: body+helmet.")]
    [SerializeField] private bool _multiplicative = true;

    [Tooltip("Максимальное суммарное снижение урона (в %), чтобы нельзя было стать бессмертным")]
    [SerializeField, Range(0f, 100f)] private float _maxTotalReductionPercent = 90f;

    [Header("UI")]
    [SerializeField] private ArmorBar _bodyBar;
    [SerializeField] private ArmorBar _helmetBar;

    public ArmorSlot Body => _body;
    public ArmorSlot Helmet => _helmet;

    public float BodyReduction => _body.Reduction01;
    public float HelmetReduction => _helmet.Reduction01;

    /// <summary>Итоговое снижение урона как доля (0..1).</summary>
    public float TotalReduction
    {
        get
        {
            float r;
            if (_multiplicative)
                r = 1f - (1f - BodyReduction) * (1f - HelmetReduction);
            else
                r = BodyReduction + HelmetReduction;

            return Mathf.Clamp(r, 0f, _maxTotalReductionPercent / 100f);
        }
    }

    /// <summary>Применяет броню+шлем к входящему урону.</summary>
    public int ApplyArmor(int incomingDamage)
    {
        if (incomingDamage <= 0) return 0;

        float final = incomingDamage * (1f - TotalReduction);
        return Mathf.Max(0, Mathf.RoundToInt(final));
    }

    // ==== Модель из документа: A + профиль материала, защита только поражённой зоны ====

    /// <summary>Сумма штрафов движения от надетых предметов, доля.</summary>
    public float MovementPenalty01 => Mathf.Clamp01(_body.MovementPenalty + _helmet.MovementPenalty);

    /// <summary>Сумма штрафов частоты атак от надетых предметов, доля.</summary>
    public float AttackSpeedPenalty01 => Mathf.Clamp01(_body.AttackPenalty + _helmet.AttackPenalty);

    /// <summary>true — хотя бы у одного слота задана защита A, значит работает модель из документа.</summary>
    public bool UsesBalanceModel => _body.ArmorA > 0f || _helmet.ArmorA > 0f;

    /// <summary>Слот, который защищает указанную зону. Броня разных зон не складывается.</summary>
    public ArmorSlot GetSlot(BodyZone zone) => zone == BodyZone.Head ? _helmet : _body;

    /// <summary>
    /// Потеря HP от попадания с учётом типов урона. Если у задетой зоны задана защита A,
    /// считается по формуле документа, иначе остаётся старая модель в процентах.
    /// </summary>
    public float ApplyArmor(DamagePacket damage, BodyZone zone = BodyZone.Torso)
    {
        float total = damage.Total;
        if (total <= 0f) return 0f;

        ArmorSlot slot = GetSlot(zone);

        if (slot.ArmorA > 0f)
            return VomicaBalance.ComputeHpLoss(damage, slot.ArmorA, slot.Profile);

        return total * (1f - TotalReduction);
    }

    // ==== Рантайм-API ====

    public void SetBodyReduction(float percent) => _body.DamageReductionPercent = percent;
    public void SetHelmetReduction(float percent) => _helmet.DamageReductionPercent = percent;

    public void EquipBody(bool equipped) => _body.SetEquipped(equipped);
    public void EquipHelmet(bool equipped) => _helmet.SetEquipped(equipped);

    private void Awake()
    {
        if (_bodyBar == null || _helmetBar == null)
            CreateBars();
    }

    private void CreateBars()
    {
        GameObject canvasGo = new GameObject("PlayerArmorCanvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGo.AddComponent<CanvasScaler>();
        canvasGo.AddComponent<GraphicRaycaster>();

        _bodyBar = ArmorBar.CreateBar(
            canvasGo.transform,
            "BodyArmorBar",
            new Vector2(24f, -76f),
            new Vector2(260f, 14f),
            new Color(0.35f, 0.65f, 1f, 1f),   
            new Color(0.15f, 0.30f, 0.50f, 1f), 
            "Броня"
        );

        _helmetBar = ArmorBar.CreateBar(
            canvasGo.transform,
            "HelmetArmorBar",
            new Vector2(24f, -98f),
            new Vector2(260f, 14f),
            new Color(0.7f, 0.45f, 1f, 1f),   
            new Color(0.30f, 0.15f, 0.50f, 1f),
            "Шлем"
        );

        _bodyBar.SetMaxValue(100f);
        _helmetBar.SetMaxValue(100f);
        _bodyBar.SetValue(_body.DamageReductionPercent);
        _helmetBar.SetValue(_helmet.DamageReductionPercent);
    }

    private void Update()
    {
        if (_bodyBar != null)
            _bodyBar.SetValue(_body.DamageReductionPercent);

        if (_helmetBar != null)
            _helmetBar.SetValue(_helmet.DamageReductionPercent);

        SyncHud();
    }

    private void SyncHud()
    {
        if (HUDManager.Instance == null) return;

        HUDData data = HUDManager.Instance.GetHUDData();
        if (data == null) return;

        data.armorPercent = BodyReduction;    
        data.helmetPercent = HelmetReduction; 
    }
}