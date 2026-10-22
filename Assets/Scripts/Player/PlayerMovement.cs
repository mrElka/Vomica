using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Отвечает за передвижение игрока: ходьбу, бег, прыжок, рывок, гравитацию,
/// проверку земли, анимацию движения и поворот тела вслед за активной камерой.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Links")]
    [SerializeField] private CharacterController _characterController;
    [SerializeField] private CinemachineCamera _cam;
    [SerializeField] private InputHandler inputHandler;
    [SerializeField] private PlayerStamina stamina;

    [Header("Visual")]
    [Tooltip("Модель игрока, которая поворачивается вслед за камерой (FPS)")]
    [SerializeField] private Transform _visualRoot;

    [Header("Movement")]
    [SerializeField] private float _speed = 5f;
    [SerializeField] private float _runMultiplier = 1.6f;
    [SerializeField] private float _airControl = 0.5f;

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
    [SerializeField] private RuntimeAnimatorController _animatorController;
    [SerializeField] private Animator _animator;
    [SerializeField] private string _speedParam = "Speed";
    [SerializeField] private string _jumpTrigger = "Jump";
    [SerializeField] private string _dashTrigger = "Dash";
    [SerializeField] private string _runParam = "Run";
    [SerializeField] private float _animationSmooth = 10f;

    private Vector2 _moveInput;
    private Vector3 _velocity;
    private Vector3 _moveDirection;
    private bool _isGrounded;
    private bool _canMove = true;
    private bool _isRunning;
    private bool _isDashing;
    private float _dashTimer;
    private float _dashCooldownTimer;
    private Vector3 _dashDirection;

    /// <summary>Может ли игрок двигаться (выключается, например, при смерти).</summary>
    public bool CanMove => _canMove;

    /// <summary>Идёт ли сейчас рывок.</summary>
    public bool IsDashing => _isDashing;

    /// <summary>Стоит ли игрок на земле.</summary>
    public bool IsGrounded => _isGrounded;

    /// <summary>Бежит ли игрок (Ctrl + движение + есть выносливость).</summary>
    public bool IsRunning => _isRunning;

    /// <summary>Активная камера. Переключается из CameraSwitcher.</summary>
    public CinemachineCamera ActiveCamera
    {
        get => _cam;
        set => _cam = value;
    }

    private void Awake()
    {
        if (_characterController == null)
            _characterController = GetComponent<CharacterController>();

        if (inputHandler == null)
            inputHandler = GetComponent<InputHandler>();

        if (inputHandler == null)
            inputHandler = gameObject.AddComponent<InputHandler>();

        if (stamina == null)
            stamina = GetComponent<PlayerStamina>();

        if (stamina == null)
            stamina = gameObject.AddComponent<PlayerStamina>();

        if (_visualRoot == null)
            _visualRoot = PlayerAnimatorUtility.FindVisualRoot(transform);

        if (_animator == null)
            _animator = PlayerAnimatorUtility.Resolve(transform, _animatorController);
    }

    public void OnMove(InputValue value)
    {
        if (inputHandler == null)
            _moveInput = value.Get<Vector2>();
    }

    public void OnJump(InputValue value)
    {
        if (value.isPressed)
            TryJump();
    }

    public void OnDash(InputValue value)
    {
        if (value.isPressed)
            TryDash();
    }

    private void Update()
    {
        ReadInput();

        if (!_canMove)
        {
            HandleGravity();
            UpdateAnimation();
            SyncBodyToCamera();
            return;
        }

        CheckGround();
        HandleDash();
        HandleMovement();
        HandleGravity();
        UpdateAnimation();
        SyncBodyToCamera();
    }

    /// <summary>Читает ввод с InputHandler и реагирует на разовые нажатия.</summary>
    private void ReadInput()
    {
        if (inputHandler == null)
            return;

        _moveInput = inputHandler.MovementInput;

        if (inputHandler.JumpPressed)
            TryJump();

        if (inputHandler.DashPressed)
            TryDash();
    }

    /// <summary>Пытается прыгнуть: только с земли, не в рывке и при доступном движении.</summary>
    private void TryJump()
    {
        if (!_isGrounded || _isDashing || !_canMove)
            return;

        _velocity.y = Mathf.Sqrt(_jumpHeight * -2f * _gravity);

        if (_animator != null)
            _animator.SetTrigger(_jumpTrigger);
    }

    /// <summary>Пытается выполнить рывок, учитывая кулдаун и выносливость.</summary>
    private void TryDash()
    {
        if (_dashCooldownTimer > 0f || _isDashing || !_canMove)
            return;

        if (stamina != null && !stamina.TrySpendDash())
            return;

        _isDashing = true;
        _dashTimer = _dashTime;
        _dashCooldownTimer = _dashCooldown;

        Vector3 move = GetCameraRelativeMove();
        if (move == Vector3.zero)
            move = GetCameraFlatForward();

        _dashDirection = move.normalized;

        if (_animator != null)
            _animator.SetTrigger(_dashTrigger);
    }

    /// <summary>
    /// Пускает луч из геометрического центра CharacterController вниз до его нижней точки
    /// с небольшим запасом. Есть пересечение с поверхностью из _groundMask — игрок на земле.
    /// </summary>
    private void CheckGround()
    {
        Bounds controllerBounds = _characterController.bounds;
        Vector3 rayOrigin = controllerBounds.center;
        float rayDistanceToFeet = rayOrigin.y - controllerBounds.min.y;
        float rayLength = rayDistanceToFeet + _groundOffset;

        _isGrounded = Physics.Raycast(
            rayOrigin,
            Vector3.down,
            rayLength,
            _groundMask
        );

        if (_isGrounded && _velocity.y < 0f)
            _velocity.y = -2f;
    }

    /// <summary>Двигает игрока в направлении ввода, учитывая бег и контроль в воздухе.</summary>
    private void HandleMovement()
    {
        if (_isDashing)
            return;

        _moveDirection = GetCameraRelativeMove();
        float inputMagnitude = Mathf.Clamp01(new Vector3(_moveInput.x, 0f, _moveInput.y).magnitude);

        _isRunning = inputHandler != null
            && inputHandler.RunHeld
            && inputMagnitude > 0.1f
            && _isGrounded
            && stamina != null
            && !stamina.IsEmpty;

        if (_isRunning && stamina != null)
            stamina.DrainRun(Time.deltaTime);

        float speed = _speed * (_isRunning ? _runMultiplier : 1f);
        float currentSpeed = inputMagnitude * (_isGrounded ? speed : speed * _airControl);

        if (_moveDirection.sqrMagnitude <= 0.0001f)
            return;

        _moveDirection.Normalize();
        _characterController.Move(_moveDirection * currentSpeed * Time.deltaTime);
    }

    /// <summary>Двигает игрока в сторону рывка, пока не вышло время.</summary>
    private void HandleDash()
    {
        if (_dashCooldownTimer > 0f)
            _dashCooldownTimer -= Time.deltaTime;

        if (!_isDashing)
            return;

        _characterController.Move(_dashDirection * _dashSpeed * Time.deltaTime);

        _dashTimer -= Time.deltaTime;
        if (_dashTimer <= 0f)
            _isDashing = false;
    }

    /// <summary>Применяет гравитацию к вертикальной скорости.</summary>
    private void HandleGravity()
    {
        _velocity.y += _gravity * Time.deltaTime;
        _characterController.Move(_velocity * Time.deltaTime);
    }

    /// <summary>Передаёт в Animator скорость и признак бега.</summary>
    private void UpdateAnimation()
    {
        if (_animator == null)
            return;

        float inputMagnitude = Mathf.Clamp01(_moveInput.magnitude);
        float targetSpeed = _canMove && inputMagnitude > 0.1f
            ? (_isRunning ? 2f : 1f)
            : 0f;

        float smoothedSpeed = Mathf.Lerp(
            _animator.GetFloat(_speedParam),
            targetSpeed,
            Time.deltaTime * _animationSmooth
        );

        _animator.SetFloat(_speedParam, smoothedSpeed);
        _animator.SetBool(_runParam, _isRunning);
    }

    /// <summary>В FPS тело всегда смотрит туда же, куда камера по горизонтали.</summary>
    private void SyncBodyToCamera()
    {
        if (_visualRoot == null || _cam == null)
            return;

        _visualRoot.rotation = Quaternion.Euler(0f, _cam.transform.eulerAngles.y, 0f);
    }

    /// <summary>Включает или выключает управление, сбрасывая ввод и рывок.</summary>
    public void SetCanMove(bool value)
    {
        _canMove = value;
        _moveInput = Vector2.zero;
        _isDashing = false;
        _isRunning = false;
    }

    /// <summary>Возвращает направление движения относительно камеры (yaw).</summary>
    private Vector3 GetCameraRelativeMove()
    {
        Vector3 input = new Vector3(_moveInput.x, 0f, _moveInput.y);

        if (_cam == null)
            return input;

        return Quaternion.AngleAxis(_cam.transform.eulerAngles.y, Vector3.up) * input;
    }

    /// <summary>Возвращает направление камеры вперёд без вертикального наклона.</summary>
    private Vector3 GetCameraFlatForward()
    {
        if (_cam == null)
            return transform.forward;

        Vector3 forward = _cam.transform.forward;
        forward.y = 0f;
        return forward.sqrMagnitude > 0.0001f ? forward.normalized : transform.forward;
    }

    private void OnDrawGizmos()
    {
        if (_characterController == null)
            return;

        Bounds controllerBounds = _characterController.bounds;
        Vector3 rayOrigin = controllerBounds.center;
        float rayDistanceToFeet = rayOrigin.y - controllerBounds.min.y;
        float rayLength = rayDistanceToFeet + _groundOffset;

        Gizmos.color = _isGrounded ? Color.green : Color.red;
        Gizmos.DrawLine(
            rayOrigin,
            rayOrigin + Vector3.down * rayLength
        );
    }
}
