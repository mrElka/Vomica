using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerHealth : MonoBehaviour
{
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

    // HP хранится с дробной частью: формула брони из документа даёт нецелый урон
    private float _hpExact;

    public int MaxHealth => _maxHealth;
    public int CurrentHealth => _currentHealth;
    public bool IsDead => _isDead || _currentHealth <= 0;
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
            armor = gameObject.AddComponent<PlayerArmor>(); // на случай, если забыл повесить вручную

        _currentHealth = Mathf.Clamp(_currentHealth, 0, _maxHealth);
        _hpExact = _currentHealth;

        if (healthBar == null)
            healthBar = HealthBar.CreateScreenBar();

        if (healthBar != null)
            healthBar.SetMaxHealth(_maxHealth);

        SyncHud();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void TakeDamage(int damage)
    {
        if (IsDead || damage <= 0)
            return;

        // Броня (тело) + шлем режут урон раздельно
        int finalDamage = armor != null ? armor.ApplyArmor(damage) : damage;

        ApplyHpLoss(finalDamage);

        if (armor != null)
        {
            Debug.Log(
                $"DMG: {damage} → {finalDamage} " +
                $"(body {armor.Body.DamageReductionPercent:F0}%, " +
                $"helmet {armor.Helmet.DamageReductionPercent:F0}%, " +
                $"total -{armor.TotalReduction * 100f:F0}%) | " +
                $"HP: {_currentHealth}/{_maxHealth}"
            );
        }
        else
        {
            Debug.Log($"DMG: {damage} | HP: {_currentHealth}/{_maxHealth}");
        }

        if (_currentHealth <= 0)
            Die();
    }

    public void Heal(int amount)
    {
        if (IsDead || amount <= 0)
            return;

        SetExactHealth(_hpExact + amount);
    }

    public void SetHealth(int newHealth)
    {
        SetExactHealth(newHealth);
    }

    /// <summary>
    /// Урон с разбивкой по типам. Защита берётся у задетой зоны: шлем отвечает
    /// за голову, броня корпуса — за остальное, и они не складываются.
    /// </summary>
    public void TakeDamage(DamagePacket damage, BodyZone zone = BodyZone.Torso)
    {
        if (IsDead || damage.Total <= 0f)
            return;

        float loss = armor != null ? armor.ApplyArmor(damage, zone) : damage.Total;

        ApplyHpLoss(loss);

        Debug.Log($"DMG {damage} [{zone}] -> {loss:F2} HP | HP: {_hpExact:F1}/{_maxHealth}");
    }

    private void ApplyHpLoss(float loss)
    {
        if (loss <= 0f) return;

        SetExactHealth(_hpExact - loss);
    }

    private void SetExactHealth(float value)
    {
        _hpExact = Mathf.Clamp(value, 0f, _maxHealth);
        _currentHealth = Mathf.CeilToInt(_hpExact);

        healthBar?.SetHealth(_currentHealth);
        SyncHud();

        if (_hpExact <= 0f)
            Die();
    }

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

    private IEnumerator LoadMenuAfterDelay()
    {
        if (_loadMenuDelay > 0f)
            yield return new WaitForSeconds(_loadMenuDelay);

        SceneManager.LoadScene(_menuSceneName);
    }

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