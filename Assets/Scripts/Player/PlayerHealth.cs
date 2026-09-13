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

    [Header("Death")]
    [SerializeField] private string _menuSceneName = "SampleScene";
    [SerializeField] private float _loadMenuDelay = 1f;

    private PlayerController _playerController;
    private bool _isDead;

    public int MaxHealth => _maxHealth;
    public int CurrentHealth => _currentHealth;
    public bool IsDead => _isDead || _currentHealth <= 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        _playerController = GetComponent<PlayerController>();
        _currentHealth = Mathf.Clamp(_currentHealth, 0, _maxHealth);

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

        _currentHealth = Mathf.Max(0, _currentHealth - damage);
        healthBar?.SetHealth(_currentHealth);
        SyncHud();

        Debug.Log($"Player HP: {_currentHealth}/{_maxHealth}");

        if (_currentHealth <= 0)
            Die();
    }

    public void Heal(int amount)
    {
        if (IsDead || amount <= 0)
            return;

        _currentHealth = Mathf.Min(_maxHealth, _currentHealth + amount);
        healthBar?.SetHealth(_currentHealth);
        SyncHud();
    }

    public void SetHealth(int newHealth)
    {
        _currentHealth = Mathf.Clamp(newHealth, 0, _maxHealth);
        healthBar?.SetHealth(_currentHealth);
        SyncHud();

        if (_currentHealth <= 0)
            Die();
    }

    private void Die()
    {
        if (_isDead)
            return;

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
        if (HUDManager.Instance == null)
            return;

        HUDData data = HUDManager.Instance.GetHUDData();
        if (data == null)
            return;

        data.healthPercent = _maxHealth > 0 ? (float)_currentHealth / _maxHealth : 0f;
        data.isDamaged = _currentHealth < _maxHealth;
        data.isLowHealth = data.healthPercent <= 0.3f;
    }
}
