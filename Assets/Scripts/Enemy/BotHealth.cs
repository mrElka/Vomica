using System;
using UnityEngine;

/// <summary>
/// Здоровье бота: HP + броня (тело/шлем), приём урона, смерть при 0 HP.
/// Реализует IDamageable — его видят и PlayerCombat, и BotCombat.
/// При получении урона с шансом уходит в защиту через BotCombat.TryEnterDefense.
/// </summary>
public class BotHealth : MonoBehaviour, IDamageable
{
    [Header("Health")]
    [SerializeField] private float _maxHealth = 100f;
    [SerializeField] private float _currentHealth = 100f;

    [Header("Links")]
    [SerializeField] private BotArmor _armor;
    [SerializeField] private BotCombat _combat;
    [SerializeField] private BotBrain _brain;

    [Header("Defense")]
    [SerializeField] private bool _isDefending;
    [SerializeField, Range(0f, 1f)] private float _defenseDamageMultiplier = 0.2f;

    [Header("Death")]
    [Tooltip("Через сколько секунд удалить объект после смерти. -1 = не удалять")]
    [SerializeField] private float _destroyDelay = 5f;

    [Header("Audio")]
    [SerializeField] private AudioClip[] _takeDamageSounds;
    [SerializeField] private AudioClip[] _defendSounds;
    [SerializeField] private AudioClip[] _deathSounds;
    [SerializeField] private AudioSource _audioSource;

    [Header("Events")]
    public Action<float, float> OnHealthChanged;
    public Action OnDeath;

    public float MaxHealth => _maxHealth;
    public float CurrentHealth => _currentHealth;
    public bool IsDead { get; private set; }

    public bool IsDefending
    {
        get => _isDefending;
        set => _isDefending = value;
    }

    private void Awake()
    {
        if (_armor == null) _armor = GetComponent<BotArmor>();
        if (_combat == null) _combat = GetComponent<BotCombat>();
        if (_brain == null) _brain = GetComponent<BotBrain>();
        if (_audioSource == null) _audioSource = GetComponent<AudioSource>();

        _currentHealth = Mathf.Clamp(_currentHealth, 0f, _maxHealth);
        NotifyHealthChanged();
    }

    public void TakeDamage(float amount)
    {
        TakeDamage(DamagePacket.Of(DamageType.Blunt, amount), BodyZone.Torso);
    }

    public void TakeDamage(DamagePacket damage, BodyZone zone)
    {
        if (IsDead || damage.Total <= 0f) return;

        // Сообщаем мозгу, что нас бьют (для ответного удара)
        if (_brain != null) _brain.OnDamaged();

        // Шанс уйти в защиту (F1c). Активирует BotCombat.DefenseRoutine.
        if (!_isDefending && _combat != null)
            _combat.TryEnterDefense();

        float loss = _armor != null ? _armor.ApplyArmor(damage, zone) : damage.Total;

        if (_isDefending)
        {
            loss *= _defenseDamageMultiplier;
            PlayRandom(_defendSounds);
        }
        else
        {
            PlayRandom(_takeDamageSounds);
        }

        _currentHealth = Mathf.Max(0f, _currentHealth - loss);
        NotifyHealthChanged();

        if (_currentHealth <= 0f)
            Die();
    }

    private void NotifyHealthChanged()
    {
        OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
    }

    private void Die()
    {
        if (IsDead) return;
        IsDead = true;

        PlayRandom(_deathSounds);
        OnDeath?.Invoke();

        if (_destroyDelay >= 0f)
            Destroy(gameObject, _destroyDelay);
    }

    private void PlayRandom(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0 || _audioSource == null) return;
        AudioClip clip = clips[UnityEngine.Random.Range(0, clips.Length)];
        if (clip != null) _audioSource.PlayOneShot(clip);
    }
}