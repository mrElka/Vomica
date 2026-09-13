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

    [Header("Movement")]
    [SerializeField] private float _speed = 5f;
    [SerializeField] private float _airControl = 0.5f;
    [SerializeField] private float _rotationSpeed = 720f;

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

    private Vector2 _moveInput;
    private Vector3 _velocity;
    private Vector3 _moveDirection;
    private bool _isGrounded;
    private bool _canMove = true;

    private bool _isDashing;
    private float _dashTimer;
    private float _dashCooldownTimer;
    private Vector3 _dashDirection;

    public bool CanMove => _canMove;
    public bool IsDashing => _isDashing;
    public bool IsGrounded => _isGrounded;

    private void Awake()
    {
        if (_characterController == null)
            _characterController = GetComponent<CharacterController>();

        if (inputHandler == null)
            inputHandler = GetComponent<InputHandler>();

        if (inputHandler == null)
            inputHandler = gameObject.AddComponent<InputHandler>();
    }

    public void OnMove(InputValue val)
    {
        if (inputHandler == null)
            _moveInput = val.Get<Vector2>();
    }

    public void OnJump(InputValue val)
    {
        if (val.isPressed)
            TryJump();
    }

    public void OnDash(InputValue val)
    {
        if (val.isPressed)
            TryDash();
    }

    void Update()
    {
        ReadInput();

        if (!_canMove)
        {
            HandleGravity();
            return;
        }

        CheckGround();
        HandleDash();
        HandleMovement();
        HandleGravity();
    }

    // ================= INPUT =================

    void ReadInput()
    {
        if (inputHandler == null)
            return;

        _moveInput = inputHandler.MovementInput;

        if (inputHandler.JumpPressed)
            TryJump();

        if (inputHandler.DashPressed)
            TryDash();
    }

    void TryJump()
    {
        if (_isGrounded && !_isDashing && _canMove)
        {
            _velocity.y = Mathf.Sqrt(_jumpHeight * -2f * _gravity);
        }
    }

    void TryDash()
    {
        if (_dashCooldownTimer <= 0 && !_isDashing && _canMove)
        {
            _isDashing = true;
            _dashTimer = _dashTime;
            _dashCooldownTimer = _dashCooldown;

            Vector3 move = GetCameraRelativeMove();

            if (move == Vector3.zero)
                move = GetCameraFlatForward();

            _dashDirection = move.normalized;
        }
    }

    // ================= GROUND (Raycast от центра) =================

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
        {
            _velocity.y = -2f;
        }
    }

    // ================= MOVEMENT =================

    void HandleMovement()
    {
        if (_isDashing) return;

        _moveDirection = GetCameraRelativeMove();

        float inputMagnitude = Mathf.Clamp01(new Vector3(_moveInput.x, 0f, _moveInput.y).magnitude);
        float currentSpeed = inputMagnitude * (_isGrounded ? _speed : _speed * _airControl);

        if (_moveDirection.sqrMagnitude > 0.0001f)
        {
            _moveDirection.Normalize();
            _characterController.Move(_moveDirection * currentSpeed * Time.deltaTime);
            RotateTowards(_moveDirection);
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
            RotateTowards(_dashDirection);

            _dashTimer -= Time.deltaTime;

            if (_dashTimer <= 0)
                _isDashing = false;
        }
    }

    // ================= GRAVITY =================

    void HandleGravity()
    {
        _velocity.y += _gravity * Time.deltaTime;
        _characterController.Move(_velocity * Time.deltaTime);
    }

    public void SetCanMove(bool value)
    {
        _canMove = value;
        _moveInput = Vector2.zero;
        _isDashing = false;
    }

    Vector3 GetCameraRelativeMove()
    {
        Vector3 input = new Vector3(_moveInput.x, 0f, _moveInput.y);

        if (_cam == null)
            return input;

        return Quaternion.AngleAxis(_cam.transform.eulerAngles.y, Vector3.up) * input;
    }

    Vector3 GetCameraFlatForward()
    {
        if (_cam == null)
            return transform.forward;

        Vector3 forward = _cam.transform.forward;
        forward.y = 0f;
        return forward.sqrMagnitude > 0.0001f ? forward.normalized : transform.forward;
    }

    void RotateTowards(Vector3 direction)
    {
        if (_cam != null && _cam.transform.IsChildOf(transform))
            return;

        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
            return;

        Quaternion toRotation = Quaternion.LookRotation(direction, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            toRotation,
            _rotationSpeed * Time.deltaTime
        );
    }

    // ================= DEBUG =================

    private void OnDrawGizmos()
    {
        if (_characterController == null) return;

        float rayLength = (_characterController.height / 2f) + _groundOffset;

        Gizmos.color = Color.red;
        Gizmos.DrawLine(
            transform.position,
            transform.position + Vector3.down * rayLength
        );
    }
}
