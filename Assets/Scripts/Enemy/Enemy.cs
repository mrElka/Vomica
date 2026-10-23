using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

/// <summary>Всё, что может получать урон (игрок, враги, боты, разрушаемые объекты).</summary>
public interface IDamageable
{
    void TakeDamage(float amount);

    /// <summary>Урон с разбивкой по типам и зоной попадания. Реализации без зон могут игнорировать zone.</summary>
    void TakeDamage(DamagePacket damage, BodyZone zone);
}

/// <summary>
/// Враг: патруль, обнаружение игрока (дальность + угол обзора + линия видимости),
/// преследование с рывком и прыжком через препятствия, атака, защита и смерть.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class EnemyNew : MonoBehaviour, IDamageable
{
    public enum State { Patrol, Run, Attack, Dead, Defense }

    [Header("Stats")]
    public float maxHealth = 100f;
    public float currentHealth;

    [Header("Movement")]
    public float patrolSpeed = 3.5f;
    public float runSpeed = 7.0f;
    public float dashSpeed = 15f;
    public float dashTime = 0.2f;
    public float rotationSpeed = 15f;
    public float jumpHeight = 1.5f;
    public float gravity = -20f;

    [Header("Dash")]
    public float dashCooldown = 4f;
    [Tooltip("Рывок используется, только если игрок дальше этой дистанции")]
    public float dashMinDistance = 6f;
    [Tooltip("...и ближе этой дистанции")]
    public float dashMaxDistance = 12f;
    [Range(0f, 1f)] public float dashChance = 0.5f;

    [Header("Obstacles")]
    public bool canJumpObstacles = true;
    [Tooltip("Слои препятствий (стены, ящики, заборы). Без этого враг их не видит")]
    public LayerMask obstacleMask;
    [Tooltip("На каком расстоянии впереди враг замечает препятствие")]
    public float obstacleCheckDistance = 1.5f;
    [Tooltip("Прыжок начинается, когда до препятствия не дальше этого расстояния")]
    public float jumpTriggerDistance = 1f;
    [Tooltip("Прыгаем, только если препятствие ниже jumpHeight * это значение")]
    [Range(0.5f, 1f)] public float jumpHeightSafety = 0.85f;
    [Tooltip("Склоны положе этого угла не считаются препятствием")]
    public float walkableSlope = 45f;

    [Header("Navigation (обход)")]
    [Tooltip("Обходить по NavMesh. Если NavMesh не запечён — враг обходит по лучам")]
    public bool useNavMesh = true;
    public float repathInterval = 0.3f;
    public float navSampleDistance = 2f;
    public float cornerReachDistance = 0.5f;
    [Tooltip("Минимальное время, которое враг держится обхода, чтобы не дёргаться")]
    public float detourMinTime = 0.5f;

    [Header("Detection")]
    public float sightRange = 10f;
    public float loseSightrange = 20f;
    public float fieldOfView = 120f;
    public float attackRange = 2f;
    public float eyeHeight = 1.6f;
    [Tooltip("Слои, которые перекрывают обзор (стены, земля). Если пусто — обзор не блокируется")]
    public LayerMask sightBlockMask;
    public LayerMask playerLayer;

    [Header("Combat")]
    public float attackDamage = 10f;
    public float attackCooldown = 1.5f;
    public float attackDelay = 0.4f;
    [Tooltip("Тип урона — от него зависит, как его режет броня игрока")]
    public DamageType damageType = DamageType.Blunt;
    [Tooltip("Зона тела игрока, в которую целится враг")]
    public BodyZone hitZone = BodyZone.Torso;

    [Header("Defense")]
    [Range(0f, 1f)] public float defenseChance = 0.25f;
    public float defenseDuration = 1f;
    [Tooltip("Множитель урона во время защиты (0.2 = получает 20% урона)")]
    [Range(0f, 1f)] public float defenseDamageMultiplier = 0.2f;

    [Header("Patrol")]
    public Transform[] patrolPoints;
    public float patrolWaitTime = 2f;
    public float patrolPointReachDistance = 0.5f;

    [Header("Animation (optional)")]
    public Animator animator;
    public string speedParam = "Speed";
    public string attackTrigger = "Attack";
    public string dashTrigger = "Dash";
    public string jumpTrigger = "Jump";
    public string defenseTrigger = "Defense";
    public string deathTrigger = "Death";
    public float animationSmooth = 10f;

    [Header("Audio")]
    public AudioClip[] footsteps;
    public AudioClip[] attack;
    public AudioClip[] takeDamage;
    public AudioClip[] defendDamage;
    public AudioClip[] death;
    [Tooltip("Расстояние между шагами")]
    public float stepDistance = 2f;

    [Header("Death")]
    [Tooltip("Через сколько секунд удалить объект после смерти. -1 = не удалять")]
    public float destroyDelay = 5f;

    [Header("Events")]
    public UnityEvent OnDeath;
    public UnityEvent<float> OnDamageTaken;

    // --- Состояние ---
    public State CurrentState => _state;
    public bool IsDead => _state == State.Dead;

    private CharacterController controller;
    private Transform player;
    private AudioSource audioSource;

    private State _state = State.Patrol;

    private Vector3 _horizontalVelocity;
    private float _verticalVelocity;

    private int _patrolIndex;
    private float _waitTimer;

    private float _attackTimer;
    private bool _isAttacking;

    private bool _isDashing;
    private float _dashTimer;
    private float _dashCooldownTimer;
    private Vector3 _dashDirection;

    private float _defenseTimer;
    private float _stepTimer;

    private readonly Collider[] _hits = new Collider[8];

    // --- Обход препятствий ---
    private readonly NavMeshPath _navPath = new NavMeshPath();
    private readonly Vector3[] _corners = new Vector3[32];
    private int _cornerCount;
    private int _cornerIndex;
    private bool _hasPath;
    private float _repathTimer;
    private bool _detourActive;
    private float _detourTimer;
    private float _steerSide = 1f;
    private Vector3 _lastMoveDir;

    private int _speedHash, _attackHash, _dashHash, _jumpHash, _defenseHash, _deathHash;

    // ------------------------------------------------------------------

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 1f;

        if (animator == null) animator = GetComponentInChildren<Animator>();

        _speedHash = Animator.StringToHash(speedParam);
        _attackHash = Animator.StringToHash(attackTrigger);
        _dashHash = Animator.StringToHash(dashTrigger);
        _jumpHash = Animator.StringToHash(jumpTrigger);
        _defenseHash = Animator.StringToHash(defenseTrigger);
        _deathHash = Animator.StringToHash(deathTrigger);

        currentHealth = maxHealth;
        _steerSide = Random.value < 0.5f ? -1f : 1f;
    }

    private void Update()
    {
        if (_state == State.Dead) return;

        // Игрок умер — забываем его и возвращаемся к патрулю
        if (player != null && IsPlayerDead())
        {
            player = null;
            if (_state != State.Patrol) SetState(State.Patrol);
        }

        TickTimers();

        _horizontalVelocity = Vector3.zero;

        switch (_state)
        {
            case State.Patrol: UpdatePatrol(); break;
            case State.Run: UpdateRun(); break;
            case State.Attack: UpdateAttack(); break;
            case State.Defense: UpdateDefense(); break;
        }

        // Рывок перекрывает любое горизонтальное движение
        if (_isDashing)
            _horizontalVelocity = _dashDirection * dashSpeed;

        ApplyMovement();
        UpdateAnimator();
        HandleFootsteps();
    }

    private void TickTimers()
    {
        if (_attackTimer > 0f) _attackTimer -= Time.deltaTime;
        if (_dashCooldownTimer > 0f) _dashCooldownTimer -= Time.deltaTime;

        if (_isDashing)
        {
            _dashTimer -= Time.deltaTime;
            if (_dashTimer <= 0f) _isDashing = false;
        }
    }

    // ================== СОСТОЯНИЯ ==================

    /// <summary>Патруль: идём по точкам, ждём на каждой, ищем игрока.</summary>
    private void UpdatePatrol()
    {
        if (TryDetectPlayer())
        {
            SetState(State.Run);
            return;
        }

        if (patrolPoints == null || patrolPoints.Length == 0) return;

        if (_waitTimer > 0f)
        {
            _waitTimer -= Time.deltaTime;
            return;
        }

        Transform target = patrolPoints[_patrolIndex];
        if (target == null)
        {
            NextPatrolPoint();
            return;
        }

        Vector3 toTarget = FlatDirection(target.position - transform.position, out float distance);

        if (distance <= patrolPointReachDistance)
        {
            _waitTimer = patrolWaitTime;
            NextPatrolPoint();
            return;
        }

        Vector3 moveDir = GetMoveDirection(target.position);
        RotateTowards(moveDir);
        _horizontalVelocity = moveDir * patrolSpeed;
    }

    /// <summary>Погоня за игроком: бег, рывок, переход в атаку.</summary>
    private void UpdateRun()
    {
        if (player == null)
        {
            SetState(State.Patrol);
            return;
        }

        Vector3 toPlayer = FlatDirection(player.position - transform.position, out float distance);

        // Потеряли игрока
        if (distance > loseSightrange)
        {
            player = null;
            SetState(State.Patrol);
            return;
        }

        // Дошли до дистанции атаки
        if (distance <= attackRange)
        {
            SetState(State.Attack);
            return;
        }

        // Рывок, если игрок на средней дистанции
        if (!_isDashing && _dashCooldownTimer <= 0f
            && distance >= dashMinDistance && distance <= dashMaxDistance
            && controller.isGrounded
            && !IsBlocked(toPlayer, dashSpeed * dashTime + 0.5f))
        {
            _dashCooldownTimer = dashCooldown; // кулдаун ставим всегда, чтобы не бросать кубик каждый кадр
            if (Random.value <= dashChance)
                StartDash(toPlayer);
        }

        Vector3 moveDir = GetMoveDirection(player.position);
        RotateTowards(moveDir.sqrMagnitude > 0.0001f ? moveDir : toPlayer);
        _horizontalVelocity = moveDir * runSpeed;
    }

    /// <summary>Атака: стоим, смотрим на игрока, бьём по кулдауну.</summary>
    private void UpdateAttack()
    {
        if (player == null)
        {
            SetState(State.Patrol);
            return;
        }

        Vector3 toPlayer = FlatDirection(player.position - transform.position, out float distance);
        RotateTowards(toPlayer);

        // Игрок отошёл — снова бежим (небольшой гистерезис, чтобы не дёргаться на границе)
        if (!_isAttacking && distance > attackRange * 1.2f)
        {
            SetState(State.Run);
            return;
        }

        if (!_isAttacking && _attackTimer <= 0f)
            StartCoroutine(AttackRoutine());
    }

    /// <summary>Защита: стоим на месте, получаем сниженный урон.</summary>
    private void UpdateDefense()
    {
        if (player != null)
        {
            Vector3 toPlayer = FlatDirection(player.position - transform.position, out _);
            RotateTowards(toPlayer);
        }

        _defenseTimer -= Time.deltaTime;
        if (_defenseTimer <= 0f)
            SetState(player != null ? State.Run : State.Patrol);
    }

    private void SetState(State newState)
    {
        if (_state == State.Dead) return;
        _state = newState;

        // Цель сменилась — старый путь и обход больше не актуальны
        _hasPath = false;
        _detourActive = false;
        _detourTimer = 0f;
    }

    // ================== АТАКА ==================

    private IEnumerator AttackRoutine()
    {
        _isAttacking = true;

        if (animator != null) animator.SetTrigger(_attackHash);
        PlayRandom(attack);

        yield return new WaitForSeconds(attackDelay);

        // Урон проходит, только если за время замаха игрок не ушёл и враг не умер / не ушёл в защиту
        if (_state == State.Attack && player != null)
        {
            Vector3 toPlayer = FlatDirection(player.position - transform.position, out float distance);
            float angle = Vector3.Angle(transform.forward, toPlayer);

            if (distance <= attackRange * 1.2f && angle <= 60f)
            {
                PlayerHealth health = player.GetComponentInParent<PlayerHealth>();
                if (health == null) health = PlayerHealth.Instance;

                if (health != null)
                {
                    // Урон идёт через броню игрока: PlayerArmor сам срежет его по типу и зоне
                    health.TakeDamage(DamagePacket.Of(damageType, attackDamage), hitZone);
                }
                else
                {
                    // Запасной вариант для любых других целей
                    IDamageable target = player.GetComponentInParent<IDamageable>();
                    if (target != null) target.TakeDamage(attackDamage);
                }
            }
        }

        _attackTimer = attackCooldown;
        _isAttacking = false;
    }

    // ================== РЫВОК / ПРЫЖОК ==================

    private void StartDash(Vector3 direction)
    {
        _isDashing = true;
        _dashTimer = dashTime;
        _dashDirection = direction;

        if (animator != null) animator.SetTrigger(_dashHash);
    }

    // ================== ОБХОД ПРЕПЯТСТВИЙ ==================

    private enum ObstacleType { None, Jumpable, Blocked }

    /// <summary>
    /// Куда идти к цели с учётом препятствий:
    /// чисто — напрямик; низкая преграда — прыжок; высокая — обход по NavMesh
    /// (или по лучам, если NavMesh не запечён).
    /// </summary>
    private Vector3 GetMoveDirection(Vector3 targetPos)
    {
        Vector3 direct = FlatDirection(targetPos - transform.position, out _);
        if (direct == Vector3.zero) return Vector3.zero;

        // В воздухе направление не меняем — летим по инерции прыжка
        if (!controller.isGrounded && !_isDashing)
            return _lastMoveDir != Vector3.zero ? _lastMoveDir : direct;

        if (_detourTimer > 0f) _detourTimer -= Time.deltaTime;

        ObstacleType type = ScanObstacle(direct, out float obstacleDistance);

        if (type == ObstacleType.Jumpable)
        {
            if (obstacleDistance <= jumpTriggerDistance) Jump();
            _detourActive = false;
            return Remember(direct);
        }

        if (type == ObstacleType.Blocked)
        {
            if (!_detourActive)
            {
                _detourActive = true;
                _hasPath = false;
            }
            _detourTimer = detourMinTime;
        }
        else if (_detourTimer <= 0f)
        {
            // Путь напрямик свободен — идём к цели
            _detourActive = false;
            return Remember(direct);
        }

        // Обход: сначала NavMesh, если он есть, иначе лучи
        Vector3 detour = useNavMesh ? GetPathDirection(targetPos) : Vector3.zero;
        if (detour == Vector3.zero) detour = SteerAround(direct);

        return Remember(detour);
    }

    private Vector3 Remember(Vector3 direction)
    {
        _lastMoveDir = direction;
        return direction;
    }

    /// <summary>Что лежит на пути: ничего, низкое препятствие (можно перепрыгнуть) или стена.</summary>
    private ObstacleType ScanObstacle(Vector3 direction, out float distance)
    {
        distance = float.MaxValue;

        if (!CastObstacle(direction, obstacleCheckDistance, out RaycastHit hit))
            return ObstacleType.None;

        distance = hit.distance;

        if (!canJumpObstacles) return ObstacleType.Blocked;

        float feetY = controller.bounds.min.y;
        float maxHeight = jumpHeight * jumpHeightSafety;

        // 1) Выше, чем можем прыгнуть? Тогда это стена.
        Vector3 highOrigin = new Vector3(transform.position.x, feetY + maxHeight + 0.1f, transform.position.z);
        float highDistance = hit.distance + controller.radius + 0.6f;
        if (Physics.Raycast(highOrigin, direction, highDistance, obstacleMask, QueryTriggerInteraction.Ignore))
            return ObstacleType.Blocked;

        // 2) Меряем высоту сверху: луч вниз чуть «внутри» препятствия
        Vector3 probe = hit.point - hit.normal * 0.05f;
        probe.y = feetY + maxHeight + 0.2f;

        if (!Physics.Raycast(probe, Vector3.down, out RaycastHit top, maxHeight + 0.2f,
                obstacleMask, QueryTriggerInteraction.Ignore))
            return ObstacleType.Blocked;

        float height = top.point.y - feetY;
        return height > controller.stepOffset && height <= maxHeight
            ? ObstacleType.Jumpable
            : ObstacleType.Blocked;
    }

    /// <summary>
    /// Каст сферой по радиусу контроллера на высоте чуть выше stepOffset
    /// (мелкие ступеньки контроллер проходит сам). Пологие склоны не считаются препятствием.
    /// </summary>
    private bool CastObstacle(Vector3 direction, float distance, out RaycastHit hit)
    {
        hit = default;
        if (obstacleMask.value == 0 || direction == Vector3.zero) return false;

        Bounds b = controller.bounds;
        Vector3 origin = new Vector3(
            b.center.x,
            b.min.y + controller.stepOffset + controller.radius + 0.05f,
            b.center.z);

        if (!Physics.SphereCast(origin, controller.radius * 0.9f, direction, out hit, distance,
                obstacleMask, QueryTriggerInteraction.Ignore))
            return false;

        return Vector3.Angle(hit.normal, Vector3.up) > walkableSlope;
    }

    /// <summary>Есть ли на этом направлении препятствие в пределах distance.</summary>
    private bool IsBlocked(Vector3 direction, float distance)
    {
        return CastObstacle(direction, distance, out _);
    }

    private void Jump()
    {
        if (!controller.isGrounded) return;

        _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        if (animator != null) animator.SetTrigger(_jumpHash);
    }

    // ---- Обход по NavMesh (без NavMeshAgent: путь считаем, а идём сами через CharacterController) ----

    private Vector3 GetPathDirection(Vector3 targetPos)
    {
        _repathTimer -= Time.deltaTime;

        if (!_hasPath || _repathTimer <= 0f)
        {
            _repathTimer = repathInterval;
            BuildPath(targetPos);
        }

        if (!_hasPath) return Vector3.zero;

        // Пропускаем достигнутые углы пути
        while (_cornerIndex < _cornerCount)
        {
            Vector3 flat = _corners[_cornerIndex] - transform.position;
            flat.y = 0f;
            if (flat.magnitude > cornerReachDistance) break;
            _cornerIndex++;
        }

        if (_cornerIndex >= _cornerCount)
        {
            _hasPath = false;
            return Vector3.zero;
        }

        return FlatDirection(_corners[_cornerIndex] - transform.position, out _);
    }

    private void BuildPath(Vector3 targetPos)
    {
        _hasPath = false;

        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit from, navSampleDistance, NavMesh.AllAreas)) return;
        if (!NavMesh.SamplePosition(targetPos, out NavMeshHit to, navSampleDistance, NavMesh.AllAreas)) return;
        if (!NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, _navPath)) return;
        if (_navPath.status == NavMeshPathStatus.PathInvalid) return;

        _cornerCount = _navPath.GetCornersNonAlloc(_corners);
        if (_cornerCount == 0) return;

        _cornerIndex = _cornerCount > 1 ? 1 : 0; // угол 0 — это мы сами
        _hasPath = true;
    }

    // ---- Запасной обход по лучам (если NavMesh нет) ----

    private Vector3 SteerAround(Vector3 desired)
    {
        if (!IsBlocked(desired, obstacleCheckDistance)) return desired;

        for (int i = 1; i <= 6; i++)
        {
            float angle = 30f * i;

            Vector3 preferred = Quaternion.AngleAxis(angle * _steerSide, Vector3.up) * desired;
            if (!IsBlocked(preferred, obstacleCheckDistance)) return preferred;

            Vector3 opposite = Quaternion.AngleAxis(-angle * _steerSide, Vector3.up) * desired;
            if (!IsBlocked(opposite, obstacleCheckDistance))
            {
                _steerSide = -_steerSide; // запоминаем сторону, чтобы не метаться
                return opposite;
            }
        }

        return Vector3.zero; // со всех сторон закрыто
    }

    // ================== ОБНАРУЖЕНИЕ ==================

    /// <summary>Ищет игрока: дальность → угол обзора → линия видимости.</summary>
    private bool TryDetectPlayer()
    {
        if (IsPlayerDead()) return false;

        int count = Physics.OverlapSphereNonAlloc(
            transform.position, sightRange, _hits, playerLayer, QueryTriggerInteraction.Ignore);

        Vector3 eye = transform.position + Vector3.up * eyeHeight;

        for (int i = 0; i < count; i++)
        {
            Transform candidate = _hits[i].transform;
            Vector3 targetPoint = _hits[i].bounds.center;

            Vector3 flatDir = FlatDirection(candidate.position - transform.position, out _);
            if (Vector3.Angle(transform.forward, flatDir) > fieldOfView * 0.5f) continue;

            if (sightBlockMask.value != 0 &&
                Physics.Linecast(eye, targetPoint, sightBlockMask, QueryTriggerInteraction.Ignore))
                continue;

            player = candidate;
            return true;
        }

        return false;
    }

    // ================== УРОН / СМЕРТЬ ==================

    public void TakeDamage(float amount)
    {
        if (_state == State.Dead) return;

        bool defended = _state == State.Defense;

        // Шанс уйти в защиту (не во время собственного замаха)
        if (!defended && !_isAttacking && !_isDashing && Random.value < defenseChance)
        {
            EnterDefense();
            defended = true;
        }

        if (defended)
        {
            amount *= defenseDamageMultiplier;
            PlayRandom(defendDamage);
        }
        else
        {
            PlayRandom(takeDamage);
        }

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        OnDamageTaken?.Invoke(amount);

        if (currentHealth <= 0f)
        {
            Die();
            return;
        }

        // Если нас ударили из патруля — сразу узнаём, кто это
        if (_state == State.Patrol)
        {
            if (player == null) player = FindPlayerFallback();
            if (player != null) SetState(State.Run);
        }
    }

    /// <summary>Перегрузка интерфейса: у EnemyNew нет зон и типов — берём суммарный урон.</summary>
    public void TakeDamage(DamagePacket damage, BodyZone zone)
    {
        TakeDamage(damage.Total);
    }

    private void EnterDefense()
    {
        _defenseTimer = defenseDuration;
        _isDashing = false;
        SetState(State.Defense);

        if (animator != null) animator.SetTrigger(_defenseHash);
    }

    private void Die()
    {
        _state = State.Dead;
        _isDashing = false;
        _isAttacking = false;
        StopAllCoroutines();

        controller.enabled = false;

        if (animator != null)
        {
            animator.SetFloat(_speedHash, 0f);
            animator.SetTrigger(_deathHash);
        }

        PlayRandom(death);
        OnDeath?.Invoke();

        if (destroyDelay >= 0f)
            Destroy(gameObject, destroyDelay);
    }

    /// <summary>Мёртв ли игрок (по PlayerHealth).</summary>
    private static bool IsPlayerDead()
    {
        return PlayerHealth.Instance != null && PlayerHealth.Instance.IsDead;
    }

    /// <summary>Запасной способ найти игрока, если он ударил врага вне зоны видимости.</summary>
    private Transform FindPlayerFallback()
    {
        PlayerController pc = FindFirstObjectByType<PlayerController>();
        return pc != null ? pc.transform : null;
    }

    // ================== ДВИЖЕНИЕ / ФИЗИКА ==================

    private void ApplyMovement()
    {
        if (controller.isGrounded && _verticalVelocity < 0f)
            _verticalVelocity = -2f;

        _verticalVelocity += gravity * Time.deltaTime;

        Vector3 velocity = _horizontalVelocity;
        velocity.y = _verticalVelocity;

        controller.Move(velocity * Time.deltaTime);
    }

    private void RotateTowards(Vector3 flatDirection)
    {
        if (flatDirection.sqrMagnitude < 0.0001f) return;

        Quaternion targetRotation = Quaternion.LookRotation(flatDirection, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    /// <summary>Нормализованное направление без вертикали + расстояние по горизонтали.</summary>
    private static Vector3 FlatDirection(Vector3 vector, out float distance)
    {
        vector.y = 0f;
        distance = vector.magnitude;
        return distance > 0.0001f ? vector / distance : Vector3.zero;
    }

    private void NextPatrolPoint()
    {
        _patrolIndex = (_patrolIndex + 1) % patrolPoints.Length;
        _hasPath = false;
        _detourActive = false;
        _detourTimer = 0f;
    }

    // ================== АНИМАЦИЯ / ЗВУК ==================

    private void UpdateAnimator()
    {
        if (animator == null) return;

        // 0 = стоит, 1 = патруль, 2 = бег (как у игрока)
        float horizontalSpeed = new Vector3(_horizontalVelocity.x, 0f, _horizontalVelocity.z).magnitude;
        float target = 0f;

        if (horizontalSpeed > 0.1f)
            target = horizontalSpeed > patrolSpeed + 0.1f ? 2f : 1f;

        float smoothed = Mathf.Lerp(animator.GetFloat(_speedHash), target, Time.deltaTime * animationSmooth);
        animator.SetFloat(_speedHash, smoothed);
    }

    private void HandleFootsteps()
    {
        if (footsteps == null || footsteps.Length == 0) return;
        if (!controller.isGrounded || _isDashing) return;

        float speed = new Vector3(_horizontalVelocity.x, 0f, _horizontalVelocity.z).magnitude;
        if (speed < 0.1f) return;

        _stepTimer -= speed * Time.deltaTime;
        if (_stepTimer <= 0f)
        {
            PlayRandom(footsteps);
            _stepTimer = stepDistance;
        }
    }

    private void PlayRandom(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0 || audioSource == null) return;

        AudioClip clip = clips[Random.Range(0, clips.Length)];
        if (clip != null) audioSource.PlayOneShot(clip);
    }

    // ================== GIZMOS ==================

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, sightRange);

        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(transform.position, loseSightrange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // Границы конуса обзора
        Vector3 left = Quaternion.Euler(0f, -fieldOfView * 0.5f, 0f) * transform.forward;
        Vector3 right = Quaternion.Euler(0f, fieldOfView * 0.5f, 0f) * transform.forward;
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(transform.position + Vector3.up * eyeHeight, left * sightRange);
        Gizmos.DrawRay(transform.position + Vector3.up * eyeHeight, right * sightRange);

        // Текущий путь обхода
        if (Application.isPlaying && _hasPath)
        {
            Gizmos.color = Color.magenta;
            Vector3 previous = transform.position;
            for (int i = _cornerIndex; i < _cornerCount; i++)
            {
                Gizmos.DrawLine(previous, _corners[i]);
                Gizmos.DrawSphere(_corners[i], 0.15f);
                previous = _corners[i];
            }
        }

        // Точки патруля
        if (patrolPoints == null) return;
        Gizmos.color = Color.green;
        for (int i = 0; i < patrolPoints.Length; i++)
        {
            if (patrolPoints[i] == null) continue;
            Gizmos.DrawSphere(patrolPoints[i].position, 0.2f);

            Transform next = patrolPoints[(i + 1) % patrolPoints.Length];
            if (next != null) Gizmos.DrawLine(patrolPoints[i].position, next.position);
        }
    }
}