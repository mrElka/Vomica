using UnityEngine;

/// <summary>
/// Контроллер бота: движение, бег, прыжок, рывок, гравитация через CharacterController.
/// Ввод приходит из BotBrain (мировые направления), камеры нет.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class BotController : MonoBehaviour
{
    [Header("Links")]
    [SerializeField] private CharacterController _characterController;
    [SerializeField] private BotStamina _stamina;

    [Header("Movement")]
    [SerializeField] private float _speed = 5f;
    [SerializeField] private float _runMultiplier = 1.6f;
    [SerializeField] private float _airControl = 0.5f;
    [SerializeField] private float _rotationSpeed = 12f;

    [Header("Jump")]
    [SerializeField] private float _jumpHeight = 1.5f;
    [SerializeField] private float _gravity = -20f;

    [Header("Dash")]
    [SerializeField] private float _dashSpeed = 15f;
    [SerializeField] private float _dashTime = 0.2f;
    [SerializeField] private float _dashCooldown = 1f;

    [Header("Ground")]
    [SerializeField] private float _groundOffset = 0.2f;
    [SerializeField] private LayerMask _groundMask;

    [Header("Animation")]
    [SerializeField] private Animator _animator;
    [SerializeField] private string _speedParam = "Speed";
    [SerializeField] private string _jumpTrigger = "Jump";
    [SerializeField] private string _dashTrigger = "Dash";
    [SerializeField] private string _runParam = "Run";
    [SerializeField] private float _animationSmooth = 10f;

    [Header("Audio")]
    [SerializeField] private AudioClip[] _footsteps;
    [SerializeField] private float _stepDistance = 2f;
    [SerializeField] private AudioSource _audioSource;

    // --- Ввод от BotBrain (мировые оси XZ) ---
    public Vector2 MoveInput { get; set; }
    public bool RunHeld { get; set; }

    // --- Состояние ---
    public bool IsDashing => _isDashing;
    public bool IsGrounded => _isGrounded;
    public bool IsRunning => _isRunning;
    public bool CanMove { get; private set; } = true;
    public float JumpHeight => _jumpHeight;

    private Vector3 _velocity;
    private Vector3 _moveDirection;
    private bool _isGrounded;
    private bool _isRunning;

    private bool _isDashing;
    private float _dashTimer;
    private float _dashCooldownTimer;
    private Vector3 _dashDirection;

    private float _stepTimer;

    private void Awake()
    {
        if (_characterController == null)
            _characterController = GetComponent<CharacterController>();
        if (_stamina == null) _stamina = GetComponent<BotStamina>();
        if (_audioSource == null) _audioSource = GetComponent<AudioSource>();
    }

    private void Update()
    {
        if (!CanMove)
        {
            HandleGravity();
            UpdateAnimation();
            return;
        }

        CheckGround();
        HandleDash();
        HandleMovement();
        HandleGravity();
        UpdateAnimation();
        HandleFootsteps();
    }

    /// <summary>Прыжок. true — удалось (стоим на земле, не в рывке).</summary>
    public bool TryJump()
    {
        if (!_isGrounded || _isDashing || !CanMove) return false;

        _velocity.y = Mathf.Sqrt(_jumpHeight * -2f * _gravity);
        if (_animator != null) _animator.SetTrigger(_jumpTrigger);
        return true;
    }

    /// <summary>Рывок в мировом направлении. true — удалось (кулдаун прошёл, стамина списалась).</summary>
    public bool TryDash(Vector3 worldDirection)
    {
        if (_dashCooldownTimer > 0f || _isDashing || !CanMove) return false;
        if (_stamina != null && !_stamina.TrySpendDash()) return false;

        _isDashing = true;
        _dashTimer = _dashTime;
        _dashCooldownTimer = _dashCooldown;

        Vector3 dir = worldDirection;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
        _dashDirection = dir.normalized;

        if (_animator != null) _animator.SetTrigger(_dashTrigger);
        return true;
    }

    /// <summary>Повернуть бота в сторону плоского направления.</summary>
    public void RotateTowards(Vector3 flatDirection)
    {
        if (flatDirection.sqrMagnitude < 0.0001f) return;
        Quaternion target = Quaternion.LookRotation(flatDirection, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, _rotationSpeed * Time.deltaTime);
    }

    /// <summary>Включить / выключить движение. При выключении сбрасывает ввод и рывок.</summary>
    public void SetCanMove(bool value)
    {
        CanMove = value;
        MoveInput = Vector2.zero;
        _isDashing = false;
        _isRunning = false;
    }

    private void CheckGround()
    {
        Bounds b = _characterController.bounds;
        float rayLength = b.extents.y + _groundOffset;
        _isGrounded = Physics.Raycast(b.center, Vector3.down, rayLength, _groundMask);
        if (_isGrounded && _velocity.y < 0f) _velocity.y = -2f;
    }

    private void HandleMovement()
    {
        if (_isDashing) return;

        Vector3 input = new Vector3(MoveInput.x, 0f, MoveInput.y);
        _moveDirection = input.sqrMagnitude > 0.0001f ? input.normalized : Vector3.zero;

        float inputMagnitude = Mathf.Clamp01(input.magnitude);

        _isRunning = RunHeld && inputMagnitude > 0.1f && _isGrounded
                     && _stamina != null && !_stamina.IsEmpty;

        if (_isRunning && _stamina != null)
            _stamina.DrainRun(Time.deltaTime);

        float speed = _speed * (_isRunning ? _runMultiplier : 1f);
        float currentSpeed = inputMagnitude * (_isGrounded ? speed : speed * _airControl);

        if (_moveDirection.sqrMagnitude > 0.0001f)
            _characterController.Move(_moveDirection * currentSpeed * Time.deltaTime);
    }

    private void HandleDash()
    {
        if (_dashCooldownTimer > 0f) _dashCooldownTimer -= Time.deltaTime;

        if (_isDashing)
        {
            _characterController.Move(_dashDirection * _dashSpeed * Time.deltaTime);
            _dashTimer -= Time.deltaTime;
            if (_dashTimer <= 0f) _isDashing = false;
        }
    }

    private void HandleGravity()
    {
        _velocity.y += _gravity * Time.deltaTime;
        _characterController.Move(_velocity * Time.deltaTime);
    }

    private void UpdateAnimation()
    {
        if (_animator == null) return;

        float inputMagnitude = Mathf.Clamp01(MoveInput.magnitude);

        float targetSpeed = 0f;
        if (CanMove && inputMagnitude > 0.1f)
            targetSpeed = _isRunning ? 2f : 1f;

        float smoothed = Mathf.Lerp(_animator.GetFloat(_speedParam), targetSpeed, Time.deltaTime * _animationSmooth);
        _animator.SetFloat(_speedParam, smoothed);
        _animator.SetBool(_runParam, _isRunning);
    }

    private void HandleFootsteps()
    {
        if (_footsteps == null || _footsteps.Length == 0 || _audioSource == null) return;
        if (!_isGrounded || _isDashing) return;

        float speed = new Vector3(_moveDirection.x, 0f, _moveDirection.z).magnitude;
        if (speed < 0.1f) return;

        _stepTimer -= speed * Time.deltaTime;
        if (_stepTimer <= 0f)
        {
            AudioClip clip = _footsteps[Random.Range(0, _footsteps.Length)];
            if (clip != null) _audioSource.PlayOneShot(clip);
            _stepTimer = _stepDistance;
        }
    }
}