using Unity.Cinemachine;
using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    [Header("Combat")]
    [SerializeField] private int _damage = 20;
    [SerializeField] private float _attackRange = 2f;
    [SerializeField] private float _attackDistance = 1.5f;
    [SerializeField] private float _attackCooldown = 0.45f;

    [Header("Links")]
    [SerializeField] private CinemachineCamera _cam;
    [SerializeField] private PlayerController _playerController;
    [SerializeField] private PlayerHealth _playerHealth;
    [SerializeField] private InputHandler inputHandler;

    private float _nextAttackTime;

    public int Damage
    {
        get => _damage;
        set => _damage = Mathf.Max(0, value);
    }

    private void Awake()
    {
        if (_playerController == null)
            _playerController = GetComponent<PlayerController>();

        if (_playerHealth == null)
            _playerHealth = GetComponent<PlayerHealth>();

        if (inputHandler == null)
            inputHandler = GetComponent<InputHandler>();
    }

    private void Update()
    {
        if (inputHandler != null && inputHandler.AttackPressed)
            TryAttack();
    }

    public void TryAttack()
    {
        if (_playerHealth != null && _playerHealth.IsDead)
            return;

        if (_playerController != null && (!_playerController.CanMove || _playerController.IsDashing))
            return;

        if (Time.time < _nextAttackTime)
            return;

        _nextAttackTime = Time.time + _attackCooldown;
        DealDamage();
    }

    public void DealDamage()
    {
        Vector3 origin = transform.position + GetAttackDirection() * _attackDistance;
        Collider[] hits = Physics.OverlapSphere(origin, _attackRange);

        foreach (Collider hit in hits)
        {
            if (hit.transform == transform || hit.transform.IsChildOf(transform))
                continue;

            Enemy enemy = hit.GetComponentInParent<Enemy>();
            if (enemy == null)
                continue;

            enemy.TakeDamage(_damage);
            Debug.Log($"Hit enemy for {_damage}");
        }
    }

    private Vector3 GetAttackDirection()
    {
        if (_cam != null)
        {
            Vector3 forward = _cam.transform.forward;
            forward.y = 0f;

            if (forward.sqrMagnitude > 0.0001f)
                return forward.normalized;
        }

        return transform.forward;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(
            transform.position + GetAttackDirection() * _attackDistance,
            _attackRange
        );
    }
}
