using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Боевая система бота: OverlapSphere → IDamageable, синхронизация урона с замахом,
/// кулдаун от оружия, защита (F1c — шанс при получении урона).
/// </summary>
public class BotCombat : MonoBehaviour
{
    [Header("Links")]
    [SerializeField] private BotWeapons _weapons;
    [SerializeField] private BotStamina _stamina;
    [SerializeField] private BotController _controller;
    [SerializeField] private BotHealth _health;
    [SerializeField] private Animator _animator;
    [SerializeField] private string _attackTrigger = "Attack";
    [SerializeField] private string _defenseTrigger = "Defense";

    [Header("Fallback (если нет оружия)")]
    [SerializeField] private float _fallbackDamage = 20f;
    [SerializeField] private DamageType _fallbackDamageType = DamageType.Blunt;
    [SerializeField] private float _fallbackRange = 2f;
    [SerializeField] private float _fallbackCooldown = 0.45f;
    [SerializeField] private float _fallbackWindup = 0.2f;
    [SerializeField] private float _fallbackStaminaCost = 15f;

    [Header("Hit")]
    [Tooltip("Радиус сферы попадания для оружия из таблицы, м")]
    [SerializeField] private float _classHitRadius = 0.6f;
    [Tooltip("Один взмах поражает одну цель")]
    [SerializeField] private bool _singleTargetPerSwing = true;

    [Header("Defense (F1c)")]
    [Tooltip("Шанс уйти в защиту при получении урона")]
    [SerializeField, Range(0f, 1f)] private float _defenseChance = 0.25f;
    [Tooltip("Длительность защиты, с")]
    [SerializeField] private float _defenseDuration = 1f;

    [Header("Audio")]
    [SerializeField] private AudioClip[] _attackSounds;
    [SerializeField] private AudioSource _audioSource;

    [Header("Events")]
    public Action<bool> OnDefenseStateChanged;

    private float _nextAttackTime;
    private bool _isAttacking;
    private bool _defenseActive;

    /// <summary>Идёт ли сейчас замах (для BotBrain: не двигаться, пока бьём).</summary>
    public bool IsAttacking => _isAttacking;

    /// <summary>Идёт ли замах прямо сейчас (для уклонения).</summary>
    public bool IsWindingUp => _isAttacking;

    /// <summary>Активна ли сейчас защита.</summary>
    public bool IsDefending => _defenseActive;

    /// <summary>Может ли бот ударить прямо сейчас.</summary>
    public bool CanAttack
    {
        get
        {
            if (_health != null && _health.IsDead) return false;
            if (_controller != null && (!_controller.CanMove || _controller.IsDashing)) return false;
            if (Time.time < _nextAttackTime) return false;
            if (_stamina != null && !_stamina.CanSpend(StaminaCost)) return false;
            return true;
        }
    }

    private bool HasWeapon => _weapons != null && _weapons.HasWeapon;
    public DamagePacket CurrentDamage => HasWeapon ? _weapons.Damage : DamagePacket.Of(_fallbackDamageType, _fallbackDamage);
    public float AttackCooldown => HasWeapon ? _weapons.Cycle : _fallbackCooldown;
    public float Reach => HasWeapon ? _weapons.Range : _fallbackRange;
    public float HitRadius => HasWeapon ? Mathf.Min(_classHitRadius, _weapons.Range * 0.5f) : _fallbackRange;
    public float StaminaCost => HasWeapon ? _weapons.Stats.StaminaCost : _fallbackStaminaCost;
    private float Windup => HasWeapon ? _weapons.Windup : _fallbackWindup;

    private void Awake()
    {
        if (_weapons == null) _weapons = GetComponent<BotWeapons>();
        if (_stamina == null) _stamina = GetComponent<BotStamina>();
        if (_controller == null) _controller = GetComponent<BotController>();
        if (_health == null) _health = GetComponent<BotHealth>();
        if (_animator == null) _animator = GetComponentInChildren<Animator>();
        if (_audioSource == null) _audioSource = GetComponent<AudioSource>();
    }

    /// <summary>Пытается выполнить удар: проверки, стамина, анимация, урон после замаха.</summary>
    public bool TryAttack()
    {
        if (!CanAttack) return false;
        if (_stamina != null && !_stamina.TrySpendAttack()) return false;

        _nextAttackTime = Time.time + AttackCooldown;
        _isAttacking = true;

        if (_animator != null) _animator.SetTrigger(_attackTrigger);
        PlayRandom(_attackSounds);

        StartCoroutine(DealDamageAfter(Windup));
        return true;
    }

    private IEnumerator DealDamageAfter(float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        DealDamage();
        _isAttacking = false;
    }

    /// <summary>Наносит урон всем IDamageable в сфере перед ботом.</summary>
    public void DealDamage()
    {
        float radius = HitRadius;
        Vector3 origin = transform.position + transform.forward * Mathf.Max(0f, Reach - radius);

        Collider[] hits = Physics.OverlapSphere(origin, radius);

        DamagePacket packet = CurrentDamage;
        IDamageable nearest = null;
        float nearestDistance = float.MaxValue;

        foreach (Collider hit in hits)
        {
            if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;

            IDamageable target = hit.GetComponentInParent<IDamageable>();
            if (target == null) continue;

            if (target is PlayerHealth ph && ph.IsDead) continue;
            if (target is BotHealth bh && bh.IsDead) continue;
            if (target is EnemyNew enemy && enemy.IsDead) continue;

            if (!_singleTargetPerSwing)
            {
                target.TakeDamage(packet, BodyZone.Torso);
                continue;
            }

            float distance = Vector3.SqrMagnitude(hit.transform.position - origin);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = target;
            }
        }

        if (_singleTargetPerSwing && nearest != null)
            nearest.TakeDamage(packet, BodyZone.Torso);
    }

    /// <summary>
    /// F1c: шанс уйти в защиту при получении урона.
    /// Вызывается из BotHealth.TakeDamage. true — защита включилась.
    /// </summary>
    public bool TryEnterDefense()
    {
        if (_defenseActive) return true;
        if (_isAttacking) return false;
        if (_controller != null && _controller.IsDashing) return false;
        if (UnityEngine.Random.value > _defenseChance) return false;

        StartCoroutine(DefenseRoutine());
        return true;
    }

    private IEnumerator DefenseRoutine()
    {
        _defenseActive = true;

        if (_health != null) _health.IsDefending = true;
        if (_animator != null) _animator.SetTrigger(_defenseTrigger);

        OnDefenseStateChanged?.Invoke(true);

        yield return new WaitForSeconds(_defenseDuration);

        _defenseActive = false;
        if (_health != null) _health.IsDefending = false;

        OnDefenseStateChanged?.Invoke(false);
    }

    private void PlayRandom(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0 || _audioSource == null) return;
        AudioClip clip = clips[UnityEngine.Random.Range(0, clips.Length)];
        if (clip != null) _audioSource.PlayOneShot(clip);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        float radius = HitRadius;
        Gizmos.DrawWireSphere(
            transform.position + transform.forward * Mathf.Max(0f, Reach - radius),
            radius);
    }
}