using UnityEngine;
using UnityEngine.UI;
/// <summary>
// СЛОТ БРОНИ

// Один слот защиты: тело или шлем.
// Работает только по модели баланса v0.1:
// - Защита A (число из таблицы набора брони).
// - Профиль материала (Quilted / Mail / Plate и т.д.) — меняет эффективность против разных типов урона.
// - Штрафы движения и частоты атак.
// Значения приходят из VomicaBalance.ArmorPresets или назначаются в рантайме из инвентаря.
/// </summary>
[System.Serializable]
public class ArmorSlot
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


    public void ApplyPreset(ArmorItemPreset preset)
    {
        _name = preset.DisplayName;
        _armorA = preset.ArmorA;
        _profile = preset.Profile;
        _movementPenalty = preset.MovementPenalty;
        _attackPenalty = preset.AttackPenalty;
        _isEquipped = true;
    }

    /// <summary>Надеть предмет с явными параметрами из инвентаря.</summary>
    public void Wear(string name, float armorA, ArmorProfile profile,
                     float movementPenalty, float attackPenalty)
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


    public void SetEquipped(bool value) => _isEquipped = value;
}
/// <summary>
//  БРОНЯ ИГРОКА

// Управляет двумя слотами защиты игрока (тело и шлем) и считает урон по модели баланса v0.1.
//
// Что делает:
// 1. Хранит два слота: тело и шлем, каждый со своей защитой A и профилем материала.
// 2. Считает потерянное HP от попадания по конкретной зоне (голова — шлем, остальное — тело).
// 3. Отдаёт штрафы движения и частоты атак — их читают PlayerController и PlayerWeapons.
// 4. Создаёт и обновляет две полоски UI (тело и шлем).
//
// Формула урона (VomicaBalance.ComputeHpLoss):
// эффективная защита = A × коэффициент профиля против типа урона.
// Далее — по формуле в духе Valheim: при низкой защите она вычитается из урона,
// при высокой — работает вторая ветвь.
/// <summary>
public class PlayerArmor : MonoBehaviour
{

    [Header("Body Armor (броня)")]
    [SerializeField] private ArmorSlot _body = new ArmorSlot();

    [Header("Helmet (шлем)")]
    [SerializeField] private ArmorSlot _helmet = new ArmorSlot();

    [Header("UI")]
    [Tooltip("Максимум шкалы для отображения A. По таблице баланса максимальная A ≈ 16")]
    [SerializeField] private float _barMaxArmor = 20f;

    [SerializeField] private ArmorBar _bodyBar;
    [SerializeField] private ArmorBar _helmetBar;


    public ArmorSlot Body => _body;
    public ArmorSlot Helmet => _helmet;


    public float MovementPenalty01 => Mathf.Clamp01(_body.MovementPenalty + _helmet.MovementPenalty);

 
    public float AttackSpeedPenalty01 => Mathf.Clamp01(_body.AttackPenalty + _helmet.AttackPenalty);
    /// <summary>
    //РАСЧЁТ УРОНА 
    // Возвращает слот, который защищает указанную зону.
    // Голова — шлем, всё остальное — тело. Броня разных зон не складывается.
    /// </summary>
    public ArmorSlot GetSlot(BodyZone zone) => zone == BodyZone.Head ? _helmet : _body;

    /// </summary>
    // Потеря HP от попадания с учётом типов урона и зоны.
    // Если у задетой зоны есть защита A — считаем по формуле документа.
    // Если A = 0 — считаем, что брони нет, урон идёт напрямую.
    /// </summary>
    public float ApplyArmor(DamagePacket damage, BodyZone zone = BodyZone.Torso)
    {
        float total = damage.Total;
        if (total <= 0f) return 0f;

        ArmorSlot slot = GetSlot(zone);

        if (slot.ArmorA > 0f)
            return VomicaBalance.ComputeHpLoss(damage, slot.ArmorA, slot.Profile);

        return total;
    }


    public void SetBodyArmor(float armorA, ArmorProfile profile)
    {
        _body.ArmorA = armorA;
        _body.Profile = profile;
    }

    public void SetHelmetArmor(float armorA, ArmorProfile profile)
    {
        _helmet.ArmorA = armorA;
        _helmet.Profile = profile;
    }


    public void EquipBody(bool equipped) => _body.SetEquipped(equipped);
    public void EquipHelmet(bool equipped) => _helmet.SetEquipped(equipped);


    private void Awake()
    {
        if (_bodyBar == null || _helmetBar == null)
            CreateBars();
    }

    private void Update()
    {
        UpdateBars();
    }

    /// <summary>Создаёт две полоски UI (тело и шлем), если они не назначены в инспекторе.</summary>
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

        _bodyBar.SetMaxValue(_barMaxArmor);
        _helmetBar.SetMaxValue(_barMaxArmor);
        _bodyBar.SetValue(_body.ArmorA);
        _helmetBar.SetValue(_helmet.ArmorA);
    }

    private void UpdateBars()
    {
        if (_bodyBar != null)
            _bodyBar.SetValue(_body.ArmorA);

        if (_helmetBar != null)
            _helmetBar.SetValue(_helmet.ArmorA);
    }
}