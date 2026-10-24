using UnityEngine;

/// <summary>
/// «Глаза» бота: поиск целей (IDamageable) сферой, проверка угла обзора и линии видимости.
/// Также проверяет стены перед прыжком/рывком.
/// </summary>
public class BotSenses : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private float _sightRange = 12f;
    [SerializeField] private float _loseSightRange = 20f;
    [SerializeField] private float _fieldOfView = 120f;
    [SerializeField] private float _eyeHeight = 1.6f;

    [Header("Layers")]
    [Tooltip("Слой игрока + слой ботов")]
    [SerializeField] private LayerMask _targetMask;
    [Tooltip("Стены/земля, перекрывающие обзор")]
    [SerializeField] private LayerMask _sightBlockMask;
    [Tooltip("Стены/ящики для проверки прыжка и рывка")]
    [SerializeField] private LayerMask _obstacleMask;

    [Header("Obstacles")]
    [SerializeField] private float _obstacleCheckDistance = 1.5f;
    [Tooltip("Склоны положе этого угла не считаются препятствием")]
    [SerializeField] private float _walkableSlope = 45f;

    private readonly Collider[] _hits = new Collider[16];

    public Transform CurrentTarget { get; private set; }
    public IDamageable CurrentDamageable { get; private set; }

    public float SightRange => _sightRange;
    public float LoseSightRange => _loseSightRange;
    public float EyeHeight => _eyeHeight;
    public LayerMask ObstacleMask => _obstacleMask;
    public LayerMask SightBlockMask => _sightBlockMask;

    /// <summary>Ищет ближайшую видимую цель. true — нашёл (записана в CurrentTarget).</summary>
    public bool TryDetectTarget()
    {
        if (_targetMask.value == 0) return false;

        int count = Physics.OverlapSphereNonAlloc(
            transform.position, _sightRange, _hits, _targetMask, QueryTriggerInteraction.Ignore);

        Vector3 eye = transform.position + Vector3.up * _eyeHeight;

        Transform best = null;
        IDamageable bestDmg = null;
        float bestDist = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            Collider col = _hits[i];
            if (col.transform == transform || col.transform.IsChildOf(transform)) continue;

            IDamageable dmg = col.GetComponentInParent<IDamageable>();
            if (dmg == null) continue;

            if (dmg is PlayerHealth ph && ph.IsDead) continue;
            if (dmg is BotHealth bh && bh.IsDead) continue;
            if (dmg is EnemyNew e && e.IsDead) continue;

            Vector3 targetPoint = col.bounds.center;
            Vector3 flat = targetPoint - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.0001f) continue;

            if (Vector3.Angle(transform.forward, flat) > _fieldOfView * 0.5f) continue;

            if (_sightBlockMask.value != 0 &&
                Physics.Linecast(eye, targetPoint, _sightBlockMask, QueryTriggerInteraction.Ignore))
                continue;

            float dist = flat.sqrMagnitude;
            if (dist < bestDist)
            {
                bestDist = dist;
                best = col.transform;
                bestDmg = dmg;
            }
        }

        if (best != null)
        {
            CurrentTarget = best;
            CurrentDamageable = bestDmg;
            return true;
        }

        return false;
    }

    /// <summary>Ищет обидчика в радиусе 5 метров на 360 градусов (без учета угла обзора).</summary>
    public Transform FindAggressor()
    {
        if (_targetMask.value == 0) return null;

        int count = Physics.OverlapSphereNonAlloc(
            transform.position, 5f, _hits, _targetMask, QueryTriggerInteraction.Ignore);

        Transform closest = null;
        float minDist = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            Collider col = _hits[i];
            if (col.transform == transform || col.transform.IsChildOf(transform)) continue;

            IDamageable dmg = col.GetComponentInParent<IDamageable>();
            if (dmg == null) continue;

            if (dmg is PlayerHealth ph && ph.IsDead) continue;
            if (dmg is BotHealth bh && bh.IsDead) continue;
            if (dmg is EnemyNew e && e.IsDead) continue;

            float dist = Vector3.SqrMagnitude(col.transform.position - transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                closest = col.transform;
            }
        }

        if (closest != null)
        {
            CurrentTarget = closest;
            CurrentDamageable = closest.GetComponentInParent<IDamageable>();
            return closest;
        }

        return null;
    }

    /// <summary>Видна ли текущая цель (для удержания в Chase).</summary>
    public bool CanSeeCurrentTarget()
    {
        if (CurrentTarget == null) return false;
        if (CurrentDamageable is PlayerHealth ph && ph.IsDead) return false;
        if (CurrentDamageable is BotHealth bh && bh.IsDead) return false;
        if (CurrentDamageable is EnemyNew e && e.IsDead) return false;

        float dist = Vector3.Distance(transform.position, CurrentTarget.position);
        if (dist > _loseSightRange) return false;

        if (_sightBlockMask.value != 0)
        {
            Vector3 eye = transform.position + Vector3.up * _eyeHeight;

            Vector3 targetPoint = CurrentTarget.position;
            if (CurrentTarget.TryGetComponent<Collider>(out var col))
                targetPoint = col.bounds.center;
            else
                targetPoint += Vector3.up * 1f;

            if (Physics.Linecast(eye, targetPoint, _sightBlockMask, QueryTriggerInteraction.Ignore))
                return false;
        }

        return true;
    }

    public void ClearTarget()
    {
        CurrentTarget = null;
        CurrentDamageable = null;
    }

    /// <summary>Есть ли препятствие в направлении (не пологий склон) в пределах distance.</summary>
    public bool IsBlocked(Vector3 direction, float distance, out RaycastHit hit)
    {
        hit = default;
        if (_obstacleMask.value == 0 || direction.sqrMagnitude < 0.0001f) return false;

        Vector3 origin = transform.position + Vector3.up * 0.5f;
        if (!Physics.Raycast(origin, direction, out hit, distance, _obstacleMask, QueryTriggerInteraction.Ignore))
            return false;

        return Vector3.Angle(hit.normal, Vector3.up) > _walkableSlope;
    }

    /// <summary>Есть ли препятствие прямо перед ботом (для прыжка).</summary>
    public bool HasObstacleAhead(out RaycastHit hit)
    {
        return IsBlocked(transform.forward, _obstacleCheckDistance, out hit);
    }
}