using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Здоровье игрока: текущий и максимальный HP, приём урона, лечение, смерть и переход в меню.
/// Урон проходит через PlayerArmor — по зоне попадания (голова — шлем, остальное — тело).
/// HP хранится с дробной частью, потому что формула брони даёт нецелый урон.
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    /// <summary>Синглтон-доступ к здоровью игрока.</summary>
    public static PlayerHealth Instance { get; private set; }

    [Header("Health")]
    [SerializeField] private int _maxHealth = 100;
    [SerializeField] private int _currentHealth = 100;
    [SerializeField] private HealthBar healthBar;

    [Header("Armor")]
    [SerializeField] private PlayerArmor armor;

    [Header("Death")]
    [SerializeField] private string _menuSceneName = "SampleScene";
    [SerializeField] private float _loadMenuDelay = 1f;

    private PlayerController _playerController;
    private bool _isDead;

    private float _hpExact;

    /// <summary>Максимальное здоровье.</summary>
    public int MaxHealth => _maxHealth;

    /// <summary>Текущее здоровье (округлённое вверх).</summary>
    public int CurrentHealth => _currentHealth;

    /// <summary>Мёртв ли игрок (по флагу или HP ≤ 0).</summary>
    public bool IsDead => _isDead || _currentHealth <= 0;

    /// <summary>Ссылка на компонент брони игрока.</summary>
    public PlayerArmor Armor => armor;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        _playerController = GetComponent<PlayerController>();

        if (armor == null)
            armor = GetComponent<PlayerArmor>();

        if (armor == null)
            armor = gameObject.AddComponent<PlayerArmor>();

        _currentHealth = Mathf.Clamp(_currentHealth, 0, _maxHealth);
        _hpExact = _currentHealth;

        if (healthBar != null)
            healthBar.SetMaxHealth(_maxHealth);

        SyncHud();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// Урон целым числом без разбивки по типу и зоне.
    /// Использует суммарную защиту брони и шлема (без учёта профиля материала).
    /// </summary>
    public void TakeDamage(int damage)
    {
        if (IsDead || damage <= 0)
            return;

        int finalDamage = armor != null
            ? Mathf.RoundToInt(armor.ApplyArmor(DamagePacket.Of(DamageType.Blunt, damage), BodyZone.Torso))
            : damage;

        ApplyHpLoss(finalDamage);

        Debug.Log($"DMG: {damage} → {finalDamage} | HP: {_currentHealth}/{_maxHealth}");

        if (_currentHealth <= 0)
            Die();
    }

    /// <summary>
    /// Урон с разбивкой по типам. Защита берётся у задетой зоны:
    /// голова — шлем, остальное — тело. Зоны не складываются.
    /// </summary>
    public void TakeDamage(DamagePacket damage, BodyZone zone = BodyZone.Torso)
    {
        if (IsDead || damage.Total <= 0f)
            return;

        float loss = armor != null ? armor.ApplyArmor(damage, zone) : damage.Total;

        ApplyHpLoss(loss);

        Debug.Log($"DMG {damage} [{zone}] -> {loss:F2} HP | HP: {_hpExact:F1}/{_maxHealth}");
    }

    /// <summary>Лечит игрока на указанное число HP.</summary>
    public void Heal(int amount)
    {
        if (IsDead || amount <= 0)
            return;

        SetExactHealth(_hpExact + amount);
    }

    /// <summary>Устанавливает HP в указанное значение (с ограничением 0..MaxHealth).</summary>
    public void SetHealth(int newHealth)
    {
        SetExactHealth(newHealth);
    }

    /// <summary>Применяет потерю HP (дробную) и проверяет смерть.</summary>
    private void ApplyHpLoss(float loss)
    {
        if (loss <= 0f) return;

        SetExactHealth(_hpExact - loss);
    }

    /// <summary>
    /// Устанавливает точное (дробное) значение HP, обновляет UI и HUD.
    /// Если HP дошло до нуля — вызывает Die().
    /// </summary>
    private void SetExactHealth(float value)
    {
        _hpExact = Mathf.Clamp(value, 0f, _maxHealth);
        _currentHealth = Mathf.CeilToInt(_hpExact);

        healthBar?.SetHealth(_currentHealth);
        SyncHud();

        if (_hpExact <= 0f)
            Die();
    }

    /// <summary>Обрабатывает смерть: отключает управление, показывает курсор, грузит меню.</summary>
    private void Die()
    {
        if (_isDead) return;

        _isDead = true;
        _playerController?.SetCanMove(false);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Debug.Log("Player died");
        StartCoroutine(LoadMenuAfterDelay());
    }

    /// <summary>Ждёт заданную задержку и загружает сцену меню.</summary>
    private IEnumerator LoadMenuAfterDelay()
    {
        if (_loadMenuDelay > 0f)
            yield return new WaitForSeconds(_loadMenuDelay);

        SceneManager.LoadScene(_menuSceneName);
    }

    /// <summary>Передаёт текущее HP в HUDData (процент, флаги повреждения и низкого HP).</summary>
    private void SyncHud()
    {
        if (HUDManager.Instance == null) return;

        HUDData data = HUDManager.Instance.GetHUDData();
        if (data == null) return;

        data.healthPercent = _maxHealth > 0 ? (float)_currentHealth / _maxHealth : 0f;
        data.isDamaged = _currentHealth < _maxHealth;
        data.isLowHealth = data.healthPercent <= 0.3f;
    }
}