using Unity.Cinemachine;
using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    [Header("Combat (fallback, если нет оружия)")]
    [SerializeField] private int _damage = 20;
    [SerializeField] private float _attackRange = 2f;
    [SerializeField] private float _attackDistance = 1.5f;
    [SerializeField] private float _attackCooldown = 0.45f;

    [Header("Links")]
    [SerializeField] private CinemachineCamera _cam;
    [SerializeField] private PlayerController _playerController;
    [SerializeField] private PlayerHealth _playerHealth;
    [SerializeField] private PlayerStamina _stamina;
    [SerializeField] private InputHandler inputHandler;

    [Header("Debug")]
    [SerializeField] private bool _debugShowCanAttack = true;

    private float _nextAttackTime;

    // ==== Актуальные параметры (из оружия, если есть) ====

    public int Damage
    {
        get => _stamina != null && _stamina.CurrentWeapon != null
            ? _stamina.CurrentWeapon.Damage
            : _damage;
    }

    public float AttackCooldown
    {
        get => _stamina != null && _stamina.CurrentWeapon != null
            ? _stamina.CurrentWeapon.AttackCooldown
            : _attackCooldown;
    }

    public float AttackRange
    {
        get => _stamina != null && _stamina.CurrentWeapon != null
            ? _stamina.CurrentWeapon.AttackRange
            : _attackRange;
    }

    public float AttackDistance
    {
        get => _stamina != null && _stamina.CurrentWeapon != null
            ? _stamina.CurrentWeapon.AttackDistance
            : _attackDistance;
    }

    public bool CanAttack
    {
        get
        {
            if (_playerHealth != null && _playerHealth.IsDead) return false;
            if (_playerController != null && (!_playerController.CanMove || _playerController.IsDashing)) return false;
            if (Time.time < _nextAttackTime) return false;
            if (_stamina != null && !_stamina.CanSpend(_stamina.AttackCost)) return false;
            return true;
        }
    }

    private void Awake()
    {
        if (_playerController == null) _playerController = GetComponent<PlayerController>();
        if (_playerHealth == null) _playerHealth = GetComponent<PlayerHealth>();
        if (inputHandler == null) inputHandler = GetComponent<InputHandler>();
        if (_stamina == null) _stamina = GetComponent<PlayerStamina>();
    }

    private void Update()
    {
        if (inputHandler != null && inputHandler.AttackPressed)
            TryAttack();
    }

    public void TryAttack()
    {
        if (!CanAttack) return;

        if (_stamina != null && !_stamina.TrySpendAttack())
            return;

        _nextAttackTime = Time.time + AttackCooldown;
        DealDamage();
    }

    public void DealDamage()
    {
        Vector3 origin = transform.position + GetAttackDirection() * AttackDistance;
        Collider[] hits = Physics.OverlapSphere(origin, AttackRange);

        foreach (Collider hit in hits)
        {
            if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;

            Enemy enemy = hit.GetComponentInParent<Enemy>();
            if (enemy == null) continue;

            enemy.TakeDamage(Damage);
            Debug.Log($"Hit enemy for {Damage}");
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

    private void OnGUI()
    {
        if (!_debugShowCanAttack) return;

        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            richText = true,
            fontSize = 16,
            alignment = TextAnchor.MiddleRight
        };

  
        const float blockWidth = 700f;
        const float rightPadding = 15f;
        const float lineHeight = 25f;
        float x = Screen.width - blockWidth - rightPadding;
        float y = 15f;

        string state = CanAttack ? "<color=lime>READY</color>" : "<color=red>BLOCKED</color>";
        string reason = "";
        if (_playerHealth != null && _playerHealth.IsDead) reason = "dead";
        else if (_playerController != null && _playerController.IsDashing) reason = "dashing";
        else if (Time.time < _nextAttackTime) reason = $"cooldown {(_nextAttackTime - Time.time):F2}s";
        else if (_stamina != null && !_stamina.CanSpend(_stamina.AttackCost))
            reason = $"stamina {_stamina.CurrentStamina:F0}/{_stamina.AttackCost:F0}";

        GUI.Label(new Rect(x, y, blockWidth, lineHeight),
            $"ATTACK: {state}   <color=orange>[{reason}]</color>", style);
        y += lineHeight;

   
        if (_stamina != null)
        {
            string weaponName = _stamina.CurrentWeapon != null ? _stamina.CurrentWeapon.DisplayName : "none";
            GUI.Label(new Rect(x, y, blockWidth, lineHeight),
                $"STAMINA: {_stamina.CurrentStamina:F0}/{_stamina.MaxStamina:F0}  " +
                $"(atk {_stamina.AttackCost:F0}, dash {_stamina.DashCost:F0}, run {_stamina.RunCostPerSecond:F0}/s)  " +
                $"WEAPON: <color=yellow>{weaponName}</color>",
                style);
            y += lineHeight;
        }

        if (_playerHealth != null && _playerHealth.Armor != null)
        {
            var a = _playerHealth.Armor;
            GUI.Label(new Rect(x, y, blockWidth, lineHeight),
                $"ARMOR: body {a.Body.DamageReductionPercent:F0}% " +
                $"{(a.Body.IsEquipped ? "" : "<color=gray>(off)</color>")} | " +
                $"helmet {a.Helmet.DamageReductionPercent:F0}% " +
                $"{(a.Helmet.IsEquipped ? "" : "<color=gray>(off)</color>")} | " +
                $"total <color=cyan>-{a.TotalReduction * 100f:F0}%</color>",
                style);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(
            transform.position + GetAttackDirection() * AttackDistance,
            AttackRange
        );
    }
}