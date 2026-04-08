using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Links")]
    [SerializeField] private CharacterController _characterController;
    [SerializeField] private CinemachineCamera _cam;

    [Header("Movement")]
    [SerializeField] private float _speed = 5f;
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

    private Vector2 _moveInput;
    private Vector3 _velocity;
    private bool _isGrounded;

    private bool _isDashing;
    private float _dashTimer;
    private float _dashCooldownTimer;
    private Vector3 _dashDirection;

    void Update()
    {
        CheckGround();
        HandleDash();
        HandleMovement();
        HandleGravity();
    }

    // ================= INPUT =================

    public void OnMove(InputValue val)
    {
        _moveInput = val.Get<Vector2>();
    }

    public void OnJump(InputValue val)
    {
        if (val.isPressed && _isGrounded && !_isDashing)
        {
            _velocity.y = Mathf.Sqrt(_jumpHeight * -2f * _gravity);
        }
    }

    public void OnDash(InputValue val)
    {
        if (val.isPressed && _dashCooldownTimer <= 0 && !_isDashing)
        {
            _isDashing = true;
            _dashTimer = _dashTime;
            _dashCooldownTimer = _dashCooldown;

            Vector3 camForward = _cam.transform.forward;
            Vector3 camRight = _cam.transform.right;

            camForward.y = 0;
            camRight.y = 0;

            camForward.Normalize();
            camRight.Normalize();

            Vector3 move = camRight * _moveInput.x + camForward * _moveInput.y;

            if (move == Vector3.zero)
                move = camForward;

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

        Vector3 camForward = _cam.transform.forward;
        Vector3 camRight = _cam.transform.right;

        camForward.y = 0;
        camRight.y = 0;

        camForward.Normalize();
        camRight.Normalize();

        Vector3 move = camRight * _moveInput.x + camForward * _moveInput.y;

        float currentSpeed = _isGrounded ? _speed : _speed * _airControl;

        _characterController.Move(move * currentSpeed * Time.deltaTime);
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