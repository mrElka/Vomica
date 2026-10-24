using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Мозг бота: FSM (Patrol / Chase / Attack / Defend / Dead).
/// Патруль по точкам, обнаружение цели через BotSenses, преследование по NavMesh-пути
/// (fallback — обход по лучам), атака по дистанции оружия, прыжки через препятствия,
/// рывок для догона, защита (F1c — через BotCombat.TryEnterDefense).
/// </summary>
public class BotBrain : MonoBehaviour
{
    public enum State { Patrol, Chase, Attack, Defend, Dead }

    [Header("Links")]
    [SerializeField] private BotController _controller;
    [SerializeField] private BotCombat _combat;
    [SerializeField] private BotSenses _senses;
    [SerializeField] private BotHealth _health;

    [Header("Patrol")]
    [Tooltip("Точки патруля. Порядок — как в массиве, по кругу")]
    [SerializeField] private Transform[] _patrolPoints;
    [SerializeField] private float _patrolWaitTime = 2f;
    [SerializeField] private float _patrolReachDistance = 0.5f;

    [Header("Chase")]
    [Tooltip("Доля от Reach, при которой бот переходит в атаку (0.9 = чуть заранее)")]
    [SerializeField, Range(0.5f, 1f)] private float _attackRangeFactor = 0.9f;
    [SerializeField] private float _repathInterval = 0.3f;
    [SerializeField] private float _navSampleDistance = 2f;
    [SerializeField] private float _cornerReachDistance = 0.5f;

    [Header("Dash (догон)")]
    [SerializeField] private float _dashMinDistance = 6f;
    [SerializeField] private float _dashMaxDistance = 12f;
    [SerializeField, Range(0f, 1f)] private float _dashChance = 0.5f;
    [SerializeField] private float _dashCooldown = 4f;

    [Header("Jump")]
    [SerializeField] private bool _canJumpObstacles = true;
    [SerializeField] private float _jumpTriggerDistance = 1f;
    [Tooltip("Прыгаем, только если препятствие ниже JumpHeight * это значение")]
    [SerializeField, Range(0.5f, 1f)] private float _jumpHeightSafety = 0.85f;

    [Header("Debug")]
    [SerializeField] private bool _logStateChanges = false;

    public State CurrentState { get; private set; } = State.Patrol;

    private int _patrolIndex;
    private float _waitTimer;
    private float _repathTimer;
    private float _dashCooldownTimer;

    private readonly NavMeshPath _navPath = new NavMeshPath();
    private readonly Vector3[] _corners = new Vector3[32];
    private int _cornerCount;
    private int _cornerIndex;
    private bool _hasPath;

    private Vector3 _lastMoveDir;
    private float _strafeDirection = 1f;
    private float _strafeTimer = 0f;

    private void Awake()
    {
        if (_controller == null) _controller = GetComponent<BotController>();
        if (_combat == null) _combat = GetComponent<BotCombat>();
        if (_senses == null) _senses = GetComponent<BotSenses>();
        if (_health == null) _health = GetComponent<BotHealth>();

        if (_health != null) _health.OnDeath += OnDead;
        if (_combat != null) _combat.OnDefenseStateChanged += OnDefenseStateChanged;
    }

    private void OnDestroy()
    {
        if (_health != null) _health.OnDeath -= OnDead;
        if (_combat != null) _combat.OnDefenseStateChanged -= OnDefenseStateChanged;
    }

    private void Update()
    {
        if (CurrentState == State.Dead) return;

        if (_dashCooldownTimer > 0f) _dashCooldownTimer -= Time.deltaTime;
        if (_strafeTimer > 0f) _strafeTimer -= Time.deltaTime;

        switch (CurrentState)
        {
            case State.Patrol: UpdatePatrol(); break;
            case State.Chase: UpdateChase(); break;
            case State.Attack: UpdateAttack(); break;
            case State.Defend: UpdateDefend(); break;
        }
    }

    // ================== PATROL ==================

    private void UpdatePatrol()
    {
        if (_senses.TryDetectTarget())
        {
            SetState(State.Chase);
            return;
        }

        if (_patrolPoints == null || _patrolPoints.Length == 0)
        {
            Stop();
            return;
        }

        if (_waitTimer > 0f)
        {
            _waitTimer -= Time.deltaTime;
            Stop();
            return;
        }

        Transform target = _patrolPoints[_patrolIndex];
        if (target == null) { NextPatrolPoint(); return; }

        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;
        if (toTarget.magnitude <= _patrolReachDistance)
        {
            _waitTimer = _patrolWaitTime;
            NextPatrolPoint();
            return;
        }

        Vector3 moveDir = GetMoveDirection(target.position);
        ApplyMove(moveDir, run: false);
    }

    private void NextPatrolPoint()
    {
        _patrolIndex = (_patrolIndex + 1) % _patrolPoints.Length;
        _hasPath = false;
        _cornerIndex = 0;
    }

    // ================== CHASE ==================

    private void UpdateChase()
    {
        if (!_senses.CanSeeCurrentTarget())
        {
            _senses.ClearTarget();
            SetState(State.Patrol);
            return;
        }

        Transform target = _senses.CurrentTarget;
        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;
        Vector3 dirToTarget = distance > 0.0001f ? toTarget / distance : transform.forward;

        // Дошли до дистанции атаки
        float reach = _combat.Reach * _attackRangeFactor;
        if (distance <= reach)
        {
            SetState(State.Attack);
            return;
        }

        TryChaseDash(dirToTarget, distance);

        Vector3 moveDir = GetMoveDirection(target.position);
        ApplyMove(moveDir, run: true);
    }

    private void TryChaseDash(Vector3 dirToTarget, float distance)
    {
        if (_dashCooldownTimer > 0f) return;
        if (_controller.IsDashing) return;
        if (distance < _dashMinDistance || distance > _dashMaxDistance) return;
        if (!_controller.IsGrounded) return;

        _dashCooldownTimer = _dashCooldown;

        if (Random.value > _dashChance) return;
        if (_senses.IsBlocked(dirToTarget, _dashMaxDistance * 0.5f, out _)) return;

        _controller.TryDash(dirToTarget);
    }

    // ================== ATTACK ==================

    private void UpdateAttack()
    {
        if (!_senses.CanSeeCurrentTarget())
        {
            _senses.ClearTarget();
            SetState(State.Patrol);
            return;
        }

        Transform target = _senses.CurrentTarget;
        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;
        Vector3 dirToTarget = distance > 0.0001f ? toTarget / distance : transform.forward;

        _controller.RotateTowards(dirToTarget);

        // Цель отошла — снова бежим
        if (distance > _combat.Reach * 1.2f && !_combat.IsAttacking)
        {
            SetState(State.Chase);
            return;
        }

        // === УКЛОНЕНИЕ (DODGE) ===
        // Если враг замахивается, а мы не в защите и не бьём — пытаемся увернуться рывком вбок
        BotCombat enemyCombat = target.GetComponentInParent<BotCombat>();
        PlayerCombat playerCombat = target.GetComponentInParent<PlayerCombat>();

        bool enemyIsWindingUp = (enemyCombat != null && enemyCombat.IsWindingUp) ||
                                (playerCombat != null && playerCombat.IsWindingUp);

        if (enemyIsWindingUp && !_combat.IsAttacking && !_controller.IsDashing && _dashCooldownTimer <= 0f)
        {
            // Рывок в сторону (перпендикулярно направлению на врага)
            Vector3 right = Vector3.Cross(Vector3.up, dirToTarget);
            // Случайно выбираем влево или вправо
            float dodgeSide = Random.value > 0.5f ? 1f : -1f;

            if (_controller.TryDash(right * dodgeSide))
            {
                _dashCooldownTimer = _dashCooldown;
                return; // Пропускаем кадр, так как мы в рывке
            }
        }

        // === СТРЕЙФ (КРУЖЕНИЕ) ===
        // Если мы не можем атаковать (кулдаун) и не в рывке — кружим вокруг цели
        if (!_combat.CanAttack && !_combat.IsAttacking && !_controller.IsDashing)
        {
            if (_strafeTimer <= 0f)
            {
                // Меняем направление кружения раз в 1-2 секунды, чтобы быть непредсказуемым
                _strafeDirection = Random.value > 0.5f ? 1f : -1f;
                _strafeTimer = Random.Range(1f, 2f);
            }

            Vector3 right = Vector3.Cross(Vector3.up, dirToTarget);
            _controller.MoveInput = new Vector2(right.x * _strafeDirection, right.z * _strafeDirection) * 0.5f;
            _controller.RunHeld = false;
        }
        else if (!_combat.IsAttacking)
        {
            Stop();
        }

        // Пытаемся ударить
        if (_combat.CanAttack)
            _combat.TryAttack();
    }

    // ================== DEFEND ==================

    private void UpdateDefend()
    {
        Stop();

        if (_senses.CurrentTarget != null)
        {
            Vector3 toTarget = _senses.CurrentTarget.position - transform.position;
            toTarget.y = 0f;
            _controller.RotateTowards(toTarget.normalized);
        }

        // Выход из защиты — когда BotCombat её выключил
        if (_combat == null || !_combat.IsDefending)
            SetState(_senses.CurrentTarget != null ? State.Chase : State.Patrol);
    }

    // ================== STATE ==================

    /// <summary>Вызывается из BotHealth, когда бот получает урон.</summary>
    public void OnDamaged()
    {
        if (CurrentState == State.Dead) return;

        // Если мы патрулируем, но нас бьют — ищем агрессора и переходим в атаку
        if (CurrentState == State.Patrol)
        {
            if (_senses.FindAggressor() != null)
            {
                SetState(State.Chase);
            }
        }
    }

    private void SetState(State newState)
    {
        if (CurrentState == State.Dead) return;
        if (CurrentState == newState) return;

        if (_logStateChanges)
            Debug.Log($"[BotBrain] {name}: {CurrentState} -> {newState}");

        CurrentState = newState;
        _hasPath = false;
        _cornerIndex = 0;

        if (newState == State.Defend || newState == State.Attack || newState == State.Patrol)
            Stop();
    }

    private void OnDefenseStateChanged(bool active)
    {
        if (CurrentState == State.Dead) return;

        if (active)
            SetState(State.Defend);
        else if (CurrentState == State.Defend)
            SetState(_senses.CurrentTarget != null ? State.Chase : State.Patrol);
    }

    private void OnDead()
    {
        CurrentState = State.Dead;
        _controller.SetCanMove(false);
        Stop();
    }

    // ================== NAVIGATION ==================

    /// <summary>Куда идти к цели: напрямик, прыжок, NavMesh-путь или fallback по лучам.</summary>
    private Vector3 GetMoveDirection(Vector3 targetPos)
    {
        Vector3 direct = targetPos - transform.position;
        direct.y = 0f;
        if (direct.sqrMagnitude < 0.0001f) return Vector3.zero;
        direct.Normalize();

        // В воздухе — держим прошлое направление
        if (!_controller.IsGrounded)
            return _lastMoveDir != Vector3.zero ? _lastMoveDir : direct;

        // Прыжок через низкое препятствие
        if (_canJumpObstacles && _senses.IsBlocked(direct, _jumpTriggerDistance, out RaycastHit hit))
        {
            if (TryJumpOver(hit))
                return Remember(direct);
        }

        // NavMesh-путь (если запечён)
        Vector3 navDir = GetPathDirection(targetPos);
        if (navDir != Vector3.zero)
            return Remember(navDir);

        // Fallback — обход по лучам
        Vector3 steer = SteerAround(direct);
        return Remember(steer != Vector3.zero ? steer : direct);
    }

    private bool TryJumpOver(RaycastHit hit)
    {
        if (!_controller.IsGrounded) return false;

        float feetY = transform.position.y;
        float maxHeight = _controller.JumpHeight;
        if (maxHeight <= 0f) return false;

        Vector3 probe = hit.point - hit.normal * 0.05f;
        probe.y = feetY + maxHeight + 0.2f;

        if (!Physics.Raycast(probe, Vector3.down, out RaycastHit top, maxHeight + 0.2f,
                _senses.ObstacleMask, QueryTriggerInteraction.Ignore))
            return false;

        float height = top.point.y - feetY;
        if (height <= 0f || height > maxHeight * _jumpHeightSafety)
            return false;

        return _controller.TryJump();
    }

    private Vector3 Remember(Vector3 dir)
    {
        _lastMoveDir = dir;
        return dir;
    }

    private void ApplyMove(Vector3 worldDir, bool run)
    {
        if (worldDir.sqrMagnitude < 0.0001f)
        {
            Stop();
            return;
        }

        _controller.MoveInput = new Vector2(worldDir.x, worldDir.z);
        _controller.RunHeld = run;
        _controller.RotateTowards(worldDir);
    }

    private void Stop()
    {
        _controller.MoveInput = Vector2.zero;
        _controller.RunHeld = false;
    }

    private Vector3 GetPathDirection(Vector3 targetPos)
    {
        _repathTimer -= Time.deltaTime;

        if (!_hasPath || _repathTimer <= 0f)
        {
            _repathTimer = _repathInterval;
            BuildPath(targetPos);
        }

        if (!_hasPath) return Vector3.zero;

        while (_cornerIndex < _cornerCount)
        {
            Vector3 flat = _corners[_cornerIndex] - transform.position;
            flat.y = 0f;
            if (flat.magnitude > _cornerReachDistance) break;
            _cornerIndex++;
        }

        if (_cornerIndex >= _cornerCount)
        {
            _hasPath = false;
            return Vector3.zero;
        }

        Vector3 dir = _corners[_cornerIndex] - transform.position;
        dir.y = 0f;
        return dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.zero;
    }

    private void BuildPath(Vector3 targetPos)
    {
        _hasPath = false;

        if (_navPath == null) return;

        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit from, _navSampleDistance, NavMesh.AllAreas))
            return;

        if (!NavMesh.SamplePosition(targetPos, out NavMeshHit to, _navSampleDistance, NavMesh.AllAreas))
            return;

        if (!NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, _navPath)) return;
        if (_navPath.status == NavMeshPathStatus.PathInvalid) return;

        _cornerCount = _navPath.GetCornersNonAlloc(_corners);
        if (_cornerCount == 0) return;

        _cornerIndex = _cornerCount > 1 ? 1 : 0;
        _hasPath = true;
    }

    private Vector3 SteerAround(Vector3 desired)
    {
        if (!_senses.IsBlocked(desired, 1.5f, out _)) return desired;

        for (int i = 1; i <= 6; i++)
        {
            float angle = 30f * i;

            Vector3 left = Quaternion.AngleAxis(angle, Vector3.up) * desired;
            if (!_senses.IsBlocked(left, 1.5f, out _)) return left;

            Vector3 right = Quaternion.AngleAxis(-angle, Vector3.up) * desired;
            if (!_senses.IsBlocked(right, 1.5f, out _)) return right;
        }

        return Vector3.zero;
    }

    // ================== GIZMOS ==================

    private void OnDrawGizmosSelected()
    {
        if (_senses != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _senses.SightRange);

            Gizmos.color = new Color(1f, 0.5f, 0f);
            Gizmos.DrawWireSphere(transform.position, _senses.LoseSightRange);
        }

        if (_patrolPoints == null) return;
        Gizmos.color = Color.green;
        for (int i = 0; i < _patrolPoints.Length; i++)
        {
            if (_patrolPoints[i] == null) continue;
            Gizmos.DrawSphere(_patrolPoints[i].position, 0.2f);

            Transform next = _patrolPoints[(_patrolPoints.Length == 0) ? 0 : (i + 1) % _patrolPoints.Length];
            if (next != null) Gizmos.DrawLine(_patrolPoints[i].position, next.position);
        }
    }
}