using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Links")]
    [SerializeField] private CharacterController _characterController;
    [SerializeField] private CinemachineCamera _cam;
    [SerializeField] private InputHandler inputHandler;
    [SerializeField] private PlayerStamina stamina;

    [Header("Visual")]
    [Tooltip("Модель игрока, которая поворачивается вслед за камерой (FPS)")]
    [SerializeField] private Transform _visualRoot;

    [Header("Cursor")]
    [SerializeField] private bool _lockCursorOnStart = true;

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

    public bool CanMove => _canMove;
    public bool IsDashing => _isDashing;
    public bool IsGrounded => _isGrounded;
    public bool IsRunning => _isRunning;

    /// <summary>Активная камера. Переключается из CameraSwitcher.</summary>
    public CinemachineCamera ActiveCamera
    {
        get => _cam;
        set => _cam = value;
    }

    // ================= LIFECYCLE =================

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void HideCursorOnLoad()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
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

        if (_animator == null)
            _animator = GetComponentInChildren<Animator>();

        if (_lockCursorOnStart)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && _lockCursorOnStart)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    public void OnMove(InputValue val)
    {
        if (inputHandler == null)
            _moveInput = val.Get<Vector2>();
    }

    public void OnJump(InputValue val)
    {
        if (val.isPressed) TryJump();
    }

    public void OnDash(InputValue val)
    {
        if (val.isPressed) TryDash();
    }

    void Update()
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

    // ================= INPUT =================

    void ReadInput()
    {
        if (inputHandler == null) return;

        _moveInput = inputHandler.MovementInput;

        if (inputHandler.JumpPressed) TryJump();
        if (inputHandler.DashPressed) TryDash();
    }

    void TryJump()
    {
        if (!_isGrounded || _isDashing || !_canMove) return;

        _velocity.y = Mathf.Sqrt(_jumpHeight * -2f * _gravity);

        if (_animator != null)
            _animator.SetTrigger(_jumpTrigger);
    }

    void TryDash()
    {
        if (_dashCooldownTimer > 0 || _isDashing || !_canMove) return;
        if (stamina != null && !stamina.TrySpendDash()) return;

        _isDashing = true;
        _dashTimer = _dashTime;
        _dashCooldownTimer = _dashCooldown;

        Vector3 move = GetCameraRelativeMove();
        if (move == Vector3.zero) move = GetCameraFlatForward();

        _dashDirection = move.normalized;

        if (_animator != null)
            _animator.SetTrigger(_dashTrigger);
    }

    // ================= GROUND =================

    void CheckGround()
    {
        float rayLength = (_characterController.height / 2f) + _groundOffset;

        _isGrounded = Physics.Raycast(
            transform.position,
            Vector3.down,
            rayLength,
            _groundMask
        );

        if (_isGrounded && _velocity.y < 0)
            _velocity.y = -2f;
    }

    // ================= MOVEMENT =================

    void HandleMovement()
    {
        if (_isDashing) return;

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

        if (_moveDirection.sqrMagnitude > 0.0001f)
        {
            _moveDirection.Normalize();
            _characterController.Move(_moveDirection * currentSpeed * Time.deltaTime);
        }
    }

    // ================= DASH =================

    void HandleDash()
    {
        if (_dashCooldownTimer > 0)
            _dashCooldownTimer -= Time.deltaTime;

        if (_isDashing)
        {
            _characterController.Move(_dashDirection * _dashSpeed * Time.deltaTime);

            _dashTimer -= Time.deltaTime;
            if (_dashTimer <= 0) _isDashing = false;
        }
    }

    // ================= GRAVITY =================

    void HandleGravity()
    {
        _velocity.y += _gravity * Time.deltaTime;
        _characterController.Move(_velocity * Time.deltaTime);
    }

    // ================= ANIMATION =================

    void UpdateAnimation()
    {
        if (_animator == null) return;

        float inputMagnitude = Mathf.Clamp01(_moveInput.magnitude);

        float targetSpeed = 0f;
        if (_canMove && inputMagnitude > 0.1f)
            targetSpeed = _isRunning ? 2f : 1f;

        float smoothedSpeed = Mathf.Lerp(
            _animator.GetFloat(_speedParam),
            targetSpeed,
            Time.deltaTime * _animationSmooth
        );

        _animator.SetFloat(_speedParam, smoothedSpeed);

        // Передаём признак бега
        _animator.SetBool(_runParam, _isRunning);
    }

    // ================= FPS BODY SYNC =================

    /// <summary>FPS: тело всегда смотрит туда же, куда камера (по горизонтали).</summary>
    private void SyncBodyToCamera()
    {
        if (_visualRoot == null || _cam == null) return;

        float camYaw = _cam.transform.eulerAngles.y;
        _visualRoot.rotation = Quaternion.Euler(0f, camYaw, 0f);
    }

    // ================= PUBLIC API =================

    public void SetCanMove(bool value)
    {
        _canMove = value;
        _moveInput = Vector2.zero;
        _isDashing = false;
        _isRunning = false;
    }

    public void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    // ================= HELPERS =================

    Vector3 GetCameraRelativeMove()
    {
        Vector3 input = new Vector3(_moveInput.x, 0f, _moveInput.y);

        if (_cam == null) return input;

        return Quaternion.AngleAxis(_cam.transform.eulerAngles.y, Vector3.up) * input;
    }

    Vector3 GetCameraFlatForward()
    {
        if (_cam == null) return transform.forward;

        Vector3 forward = _cam.transform.forward;
        forward.y = 0f;
        return forward.sqrMagnitude > 0.0001f ? forward.normalized : transform.forward;
    }

    private void OnDrawGizmos()
    {
        if (_characterController == null) return;

        float rayLength = (_characterController.height / 2f) + _groundOffset;

        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, transform.position + Vector3.down * rayLength);
    }
}