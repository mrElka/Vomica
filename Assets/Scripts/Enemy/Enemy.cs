using System;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class Enemy : MonoBehaviour
{
    private Transform target;
    private NavMeshAgent agent;

    [Header("Health")]
    public int health = 100;
    [SerializeField] private HealthBar healthBar;

    [Header("Combat")]
    public int damage = 10;
    public float attackDistance = 2.5f;
    [SerializeField] private float attackCooldown = 1f;

    [Header("Attack Hit Detection")]
    [Tooltip("Радиус сферы, в которой ищутся хитбоксы игрока при ударе")]
    [SerializeField] private float _attackHitRadius = 1.2f;

    [Tooltip("Смещение центра сферы вперёд от врага")]
    [SerializeField] private float _attackHitForward = 0.8f;

    [Tooltip("Смещение центра сферы вверх (примерно на уровень груди)")]
    [SerializeField] private float _attackHitHeight = 1f;

    [Header("AI")]
    public float lookRadius = 10f;

    private bool canAttack = true;
    private bool isDead;

    public event Action OnDeath;
    public bool IsDead => isDead;
    public int CurrentHealth => health;

    private void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.stoppingDistance = attackDistance;
        agent.speed = Mathf.Max(agent.speed, 3.5f);

        if (PlayerHealth.Instance != null)
            target = PlayerHealth.Instance.transform;

        if (healthBar == null)
            healthBar = HealthBar.CreateWorldBar(transform, new Vector3(0f, 1.35f, 0f));

        if (healthBar != null)
            healthBar.SetMaxHealth(health);
    }

    private void Update()
    {
        if (PlayerHealth.Instance != null && PlayerHealth.Instance.IsDead)
        {
            StopAgent();
            return;
        }

        if (target == null || isDead)
            return;

        float distance = Vector3.Distance(target.position, transform.position);

        if (distance > lookRadius)
        {
            StopAgent();
            return;
        }

        if (distance > attackDistance)
        {
            SetAgentStopped(false);
            if (agent.isOnNavMesh)
                agent.SetDestination(target.position);
        }
        else
        {
            StopAgent();
            LookTarget();

            if (canAttack)
            {
                canAttack = false;
                DealDamage();
                Invoke(nameof(EndAttack), attackCooldown);
            }
        }
    }

    public void DealDamage()
    {
        if (PlayerHealth.Instance == null || PlayerHealth.Instance.IsDead)
            return;

        if (target == null)
            return;

        float distance = Vector3.Distance(target.position, transform.position);
        if (distance > attackDistance + 1f)
            return;

        // Центр сферы — впереди врага, на высоте груди игрока
        Vector3 origin = transform.position
                       + transform.forward * _attackHitForward
                       + Vector3.up * _attackHitHeight;

        Collider[] hits = Physics.OverlapSphere(
            origin, _attackHitRadius, ~0, QueryTriggerInteraction.Collide
        );

        int bestDamage = 0;
        HitboxPart bestPart = HitboxPart.Generic;
        bool foundHitbox = false;

        foreach (Collider hit in hits)
        {
            // Ищем Hitbox на костях игрока
            Hitbox hitbox = hit.GetComponentInParent<Hitbox>();
            if (hitbox == null) continue;

            Transform owner = hitbox.Owner;
            if (owner == null) continue;

            // Проверяем, что владелец хитбокса — игрок
            if (owner.GetComponent<PlayerHealth>() == null) continue;

            foundHitbox = true;

            int finalDamage = Mathf.RoundToInt(damage * hitbox.DamageMultiplier);

            // Если задело несколько хитбоксов сразу — берём самый сильный урон
            if (finalDamage > bestDamage)
            {
                bestDamage = finalDamage;
                bestPart = hitbox.Part;
            }
        }

        if (foundHitbox)
        {
            PlayerHealth.Instance.TakeDamage(bestDamage);
            Debug.Log($"Enemy hit {bestPart} for {bestDamage} | Player HP: {PlayerHealth.Instance.CurrentHealth}");
        }
        else
        {
            // Fallback — если у игрока нет хитбоксов
            PlayerHealth.Instance.TakeDamage(damage);
            Debug.Log($"Player HP (no hitbox): {PlayerHealth.Instance.CurrentHealth}");
        }
    }

    public void EndAttack()
    {
        canAttack = true;
    }

    /// <summary>
    /// Перегрузка для системы баланса VomicaBalance.
    /// Принимает DamagePacket (Slashing/Piercing/Blunt), суммирует урон
    /// через .Total и передаёт в основную логику TakeDamage(int).
    /// </summary>
    public void TakeDamage(DamagePacket packet)
    {
        int amount = Mathf.RoundToInt(packet.Total);
        TakeDamage(amount);
    }

    /// <summary>Базовый метод: принимает готовое число урона.</summary>
    public void TakeDamage(int damageAmount)
    {
        if (isDead) return;

        health -= damageAmount;

        if (healthBar != null)
            healthBar.SetHealth(health);

        if (health <= 0)
            Die();
    }

    private void Die()
    {
        isDead = true;
        StopAgent();
        OnDeath?.Invoke();
        Destroy(gameObject, 3f);
    }

    private void LookTarget()
    {
        if (target == null) return;

        Vector3 direction = target.position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(direction);
    }

    private void StopAgent()
    {
        SetAgentStopped(true);
    }

    private void SetAgentStopped(bool stopped)
    {
        if (agent == null || !agent.isOnNavMesh) return;

        agent.isStopped = stopped;
        if (stopped) agent.ResetPath();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, lookRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackDistance);

        // Зона удара
        Vector3 origin = transform.position
                       + transform.forward * _attackHitForward
                       + Vector3.up * _attackHitHeight;
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.6f);
        Gizmos.DrawWireSphere(origin, _attackHitRadius);
    }
}