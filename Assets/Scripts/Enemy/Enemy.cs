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

    [Header("AI")]
    public float lookRadius = 10f;

    [Header("Balance v0.1")]
    [Tooltip("Класс оружия. Unarmed - урон, дальность и цикл берутся из полей выше")]
    public WeaponClass weaponClass = WeaponClass.Unarmed;

    [Tooltip("Тип урона, когда класс оружия не выбран")]
    public DamageType damageType = DamageType.Blunt;

    [Tooltip("Куда приходит удар по игроку: шлем закрывает голову, броня - корпус")]
    public BodyZone hitZone = BodyZone.Torso;

    [Tooltip("Защита A этого противника. 0 - урон проходит целиком")]
    public float armorA = 0f;

    [Tooltip("Профиль материала брони: задаёт стойкость к рубящему, колющему и дробящему")]
    public ArmorProfile armorProfile = ArmorProfile.None;

    private bool canAttack = true;
    private bool isDead;

    // HP с дробной частью: формула брони даёт нецелый урон
    private float hpExact;

    public event Action OnDeath;
    public bool IsDead => isDead;
    public int CurrentHealth => health;

    /// <summary>Урон одного удара с разбивкой по типам.</summary>
    public DamagePacket AttackDamage => weaponClass != WeaponClass.Unarmed
        ? VomicaBalance.GetWeapon(weaponClass).Damage
        : DamagePacket.Of(damageType, damage);

    /// <summary>Дальность удара от центра противника, м.</summary>
    public float AttackReach => weaponClass != WeaponClass.Unarmed
        ? VomicaBalance.GetWeapon(weaponClass).Range
        : attackDistance;

    /// <summary>Полный цикл атаки, с.</summary>
    public float AttackCycle => weaponClass != WeaponClass.Unarmed
        ? VomicaBalance.GetWeapon(weaponClass).Cycle
        : attackCooldown;

    private void Awake()
    {
        hpExact = health;
    }

    private void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.stoppingDistance = AttackReach;
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

        if (distance > AttackReach)
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
                Invoke(nameof(EndAttack), AttackCycle);
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

        if (distance <= AttackReach + 1f)
        {
            PlayerHealth.Instance.TakeDamage(AttackDamage, hitZone);
            Debug.Log("Player HP: " + PlayerHealth.Instance.CurrentHealth);
        }
    }

    public void EndAttack()
    {
        canAttack = true;
    }

    /// <summary>
    /// Урон с разбивкой по типам. Защита считается по формуле из документа:
    /// эффективная броня = A x коэффициент материала под этот удар.
    /// </summary>
    public void TakeDamage(DamagePacket incoming)
    {
        if (isDead)
            return;

        float loss = VomicaBalance.ComputeHpLoss(incoming, armorA, armorProfile);

        Debug.Log($"{name}: {incoming} vs A={armorA:0.#} {armorProfile} -> {loss:F2} HP");

        ApplyHpLoss(loss);
    }

    /// <summary>Урон без типа: проходит мимо расчёта брони.</summary>
    public void TakeDamage(int damageAmount)
    {
        ApplyHpLoss(damageAmount);
    }

    private void ApplyHpLoss(float loss)
    {
        if (isDead || loss <= 0f)
            return;

        hpExact = Mathf.Max(0f, hpExact - loss);
        health = Mathf.CeilToInt(hpExact);

        if (healthBar != null)
            healthBar.SetHealth(health);

        if (hpExact <= 0f)
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
        if (target == null)
            return;

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
        if (agent == null || !agent.isOnNavMesh)
            return;

        agent.isStopped = stopped;

        if (stopped)
            agent.ResetPath();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, lookRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, AttackReach);
    }
}
