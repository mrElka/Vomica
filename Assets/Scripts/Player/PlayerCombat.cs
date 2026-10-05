using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    [Header("Combat (fallback, если нет оружия)")]
    [SerializeField] private int _damage = 20;
    [SerializeField] private float _attackRange = 2f;
    [SerializeField] private float _attackDistance = 1.5f;
    [SerializeField] private float _attackCooldown = 0.45f;

    [Header("Balance v0.1")]
    [Tooltip("Арсенал игрока. Если оружие выбрано, показатели берутся из его класса")]
    [SerializeField] private PlayerWeapons _weapons;

    [Tooltip("Тип урона, когда класс оружия не выбран")]
    [SerializeField] private DamageType _fallbackDamageType = DamageType.Blunt;

    [Tooltip("Радиус сферы попадания для оружия из таблицы, м")]
    [SerializeField] private float _classHitRadius = 0.6f;

    [Tooltip("Один взмах поражает одну цель")]
    [SerializeField] private bool _singleTargetPerSwing = true;

    [Header("Links")]
    [SerializeField] private CinemachineCamera _cam;
    [SerializeField] private PlayerController _playerController;
    [SerializeField] private PlayerHealth _playerHealth;
    [SerializeField] private PlayerStamina _stamina;
    [SerializeField] private InputHandler inputHandler;

    [Header("Animation")]
    [SerializeField] private RuntimeAnimatorController _animatorController;
    [SerializeField] private Animator _animator;
    [SerializeField] private string _attackTrigger = "Attack";

    [Tooltip("Урон в момент замаха из таблицы оружия, а не в кадр нажатия")]
    [SerializeField] private bool _syncDamageToWindup = true;

    [Header("Debug")]
    [SerializeField] private bool _debugShowCanAttack = true;

    private float _nextAttackTime;
    private Coroutine _attackRoutine;

    /// <summary>Камера, вдоль которой направлен удар. Переключается CameraSwitcher.</summary>
    public CinemachineCamera ActiveCamera
    {
        get => _cam;
        set => _cam = value;
    }

    /// <summary>true — выбран класс оружия, значит показатели идут из таблицы баланса.</summary>
    private bool HasClassWeapon => _weapons != null && _weapons.HasWeapon;

    // ==== Актуальные параметры: класс оружия, затем WeaponData, затем поля выше ====

    /// <summary>Урон одного попадания с разбивкой по типам.</summary>
    public DamagePacket CurrentDamage => HasClassWeapon
        ? _weapons.Damage
        : DamagePacket.Of(_fallbackDamageType, Damage);

    public int Damage
    {
        get
        {
            if (HasClassWeapon)
                return Mathf.RoundToInt(_weapons.TotalDamage);

            return _stamina != null && _stamina.CurrentWeapon != null
                ? _stamina.CurrentWeapon.Damage
                : _damage;
        }
    }

    public float AttackCooldown
    {
        get
        {
            if (HasClassWeapon)
                return _weapons.Cycle;

            return _stamina != null && _stamina.CurrentWeapon != null
                ? _stamina.CurrentWeapon.AttackCooldown
                : _attackCooldown;
        }
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

    /// <summary>Максимальная дальность удара от центра персонажа, м.</summary>
    public float Reach => HasClassWeapon ? _weapons.Range : AttackDistance + AttackRange;

    /// <summary>Радиус сферы попадания.</summary>
    public float HitRadius => HasClassWeapon
        ? Mathf.Min(_classHitRadius, _weapons.Range * 0.5f)
        : AttackRange;

    /// <summary>Расход выносливости за один удар.</summary>
    public float StaminaCost
    {
        get
        {
            if (HasClassWeapon) return _weapons.StaminaCost;
            return _stamina != null ? _stamina.AttackCost : 0f;
        }
    }

    public bool CanAttack
    {
        get
        {
            if (_playerHealth != null && _playerHealth.IsDead) return false;
            if (_playerController != null && (!_playerController.CanMove || _playerController.IsDashing)) return false;
            if (Time.time < _nextAttackTime) return false;
            if (_stamina != null && !_stamina.CanSpend(StaminaCost)) return false;
            return true;
        }
    }

    private void Awake()
    {
        if (_playerController == null) _playerController = GetComponent<PlayerController>();
        if (_playerHealth == null) _playerHealth = GetComponent<PlayerHealth>();
        if (inputHandler == null) inputHandler = GetComponent<InputHandler>();
        if (_stamina == null) _stamina = GetComponent<PlayerStamina>();
        if (_weapons == null) _weapons = GetComponent<PlayerWeapons>();
        if (_animator == null)
            _animator = PlayerAnimatorUtility.Resolve(transform, _animatorController);
    }

    private void OnDisable()
    {
        if (_attackRoutine != null)
        {
            StopCoroutine(_attackRoutine);
            _attackRoutine = null;
        }
    }

    private void Update()
    {
        if (inputHandler != null && inputHandler.AttackPressed)
            TryAttack();
    }

    public void TryAttack()
    {
        if (!CanAttack) return;

        if (_stamina != null && !_stamina.TrySpend(StaminaCost))
            return;

        _nextAttackTime = Time.time + AttackCooldown;

        if (_animator != null)
            _animator.SetTrigger(_attackTrigger);

        if (_attackRoutine != null)
            StopCoroutine(_attackRoutine);

        if (_syncDamageToWindup && HasClassWeapon && _weapons.Windup > 0f)
            _attackRoutine = StartCoroutine(DealDamageAfter(_weapons.Windup));
        else
            DealDamage();
    }

    private IEnumerator DealDamageAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        DealDamage();
        _attackRoutine = null;
    }

    public void DealDamage()
    {
        float radius = HitRadius;
        Vector3 origin = transform.position + GetAttackDirection() * Mathf.Max(0f, Reach - radius);

        Collider[] hits = Physics.OverlapSphere(origin, radius);

        DamagePacket packet = CurrentDamage;
        Enemy nearest = null;
        float nearestDistance = float.MaxValue;

        foreach (Collider hit in hits)
        {
            if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;

            Enemy enemy = hit.GetComponentInParent<Enemy>();
            if (enemy == null || enemy.IsDead) continue;

            if (!_singleTargetPerSwing)
            {
                enemy.TakeDamage(packet);
                continue;
            }

            float distance = Vector3.SqrMagnitude(enemy.transform.position - origin);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = enemy;
            }
        }

        if (_singleTargetPerSwing && nearest != null)
            nearest.TakeDamage(packet);
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

    // ================= DEBUG =================

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
        else if (_stamina != null && !_stamina.CanSpend(StaminaCost))
            reason = $"stamina {_stamina.CurrentStamina:F0}/{StaminaCost:F0}";

        GUI.Label(new Rect(x, y, blockWidth, lineHeight),
            $"ATTACK: {state}   <color=orange>[{reason}]</color>", style);
        y += lineHeight;

        if (_stamina != null)
        {
            string weaponName = HasClassWeapon
                ? _weapons.SelectedName
                : (_stamina.CurrentWeapon != null ? _stamina.CurrentWeapon.DisplayName : "none");

            GUI.Label(new Rect(x, y, blockWidth, lineHeight),
                $"STAMINA: {_stamina.CurrentStamina:F0}/{_stamina.MaxStamina:F0}  " +
                $"(atk {StaminaCost:F0}, dash {_stamina.DashCost:F0}, run {_stamina.RunCostPerSecond:F0}/s)  " +
                $"WEAPON: <color=yellow>{weaponName}</color>",
                style);
            y += lineHeight;
        }

        if (HasClassWeapon)
        {
            GUI.Label(new Rect(x, y, blockWidth, lineHeight),
                $"<color=yellow>{_weapons.GetStatsSummary()}</color>", style);
            y += lineHeight;
        }

        if (_playerHealth != null && _playerHealth.Armor != null)
        {
            var a = _playerHealth.Armor;

            if (a.UsesBalanceModel)
            {
                GUI.Label(new Rect(x, y, blockWidth, lineHeight),
                    $"ARMOR: body A={a.Body.ArmorA:F0} {a.Body.Profile} | " +
                    $"helmet A={a.Helmet.ArmorA:F0} {a.Helmet.Profile} | " +
                    $"move <color=cyan>-{a.MovementPenalty01 * 100f:F0}%</color> " +
                    $"atk <color=cyan>-{a.AttackSpeedPenalty01 * 100f:F0}%</color>",
                    style);
            }
            else
            {
                GUI.Label(new Rect(x, y, blockWidth, lineHeight),
                    $"ARMOR: body {a.Body.DamageReductionPercent:F0}% " +
                    $"{(a.Body.IsEquipped ? "" : "<color=gray>(off)</color>")} | " +
                    $"helmet {a.Helmet.DamageReductionPercent:F0}% " +
                    $"{(a.Helmet.IsEquipped ? "" : "<color=gray>(off)</color>")} | " +
                    $"total <color=cyan>-{a.TotalReduction * 100f:F0}%</color>",
                    style);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue;

        float radius = HitRadius;
        Gizmos.DrawWireSphere(
            transform.position + GetAttackDirection() * Mathf.Max(0f, Reach - radius),
            radius
        );
    }
}