using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Переключает вид от первого и третьего лица по клавише V. Камера от первого лица
/// берётся из сцены (та, что висит на игроке), камера от третьего лица — отдельный
/// объект: либо назначенный вручную, либо созданный при старте.
/// </summary>
public class CameraSwitcher : MonoBehaviour
{
    [Header("Камеры")]
    [Tooltip("Камера от первого лица. Пусто — найдётся среди дочерних объектов игрока")]
    [SerializeField] private CinemachineCamera _firstPersonCamera;

    [Tooltip("Камера от третьего лица. Пусто — будет создана при старте")]
    [SerializeField] private CinemachineCamera _thirdPersonCamera;

    [Header("Третье лицо")]
    [Tooltip("За кем следит камера. Пусто — этот объект")]
    [SerializeField] private Transform _followTarget;

    [Tooltip("Расстояние от камеры до игрока, м")]
    [SerializeField] private float _distance = 4.5f;

    [Tooltip("Высота точки, вокруг которой вращается камера, м")]
    [SerializeField] private float _shoulderHeight = 1.4f;

    [Tooltip("Пределы наклона камеры по вертикали, град")]
    [SerializeField] private Vector2 _verticalRange = new Vector2(-30f, 60f);

    [SerializeField] private float _mouseSensitivityX = 0.25f;
    [SerializeField] private float _mouseSensitivityY = 0.2f;

    [Header("Старт")]
    [Tooltip("Начинать игру с видом от третьего лица")]
    [SerializeField] private bool _startInThirdPerson;

    [Header("Ссылки")]
    [SerializeField] private InputHandler inputHandler;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private PlayerCombat playerCombat;

    private CinemachineOrbitalFollow _orbital;
    private bool _thirdPersonActive;

    /// <summary>true — активен вид от третьего лица.</summary>
    public bool IsThirdPerson => _thirdPersonActive;

    /// <summary>Камера, которая сейчас показывает игру.</summary>
    public CinemachineCamera ActiveCamera => _thirdPersonActive ? _thirdPersonCamera : _firstPersonCamera;

    private void Awake()
    {
        if (inputHandler == null) inputHandler = GetComponent<InputHandler>();
        if (playerController == null) playerController = GetComponent<PlayerController>();
        if (playerCombat == null) playerCombat = GetComponent<PlayerCombat>();
        if (_followTarget == null) _followTarget = transform;

        if (_firstPersonCamera == null)
            _firstPersonCamera = GetComponentInChildren<CinemachineCamera>(true);

        if (_thirdPersonCamera == null)
            _thirdPersonCamera = BuildThirdPersonCamera();

        if (_thirdPersonCamera != null)
            _orbital = _thirdPersonCamera.GetComponent<CinemachineOrbitalFollow>();

        _thirdPersonActive = _startInThirdPerson;
        Apply();
    }

    private void Update()
    {
        if (TogglePressed())
            Toggle();

        HandleOrbitInput();
    }

    /// <summary>Переключить вид.</summary>
    public void Toggle() => SetThirdPerson(!_thirdPersonActive);

    public void SetThirdPerson(bool value)
    {
        _thirdPersonActive = value;
        Apply();
    }

    private bool TogglePressed()
    {
        if (inputHandler != null)
            return inputHandler.ToggleViewPressed;

        // Запас на случай, если InputHandler не повешен на игрока
        return Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame;
    }

    private void Apply()
    {
        SetCameraEnabled(_firstPersonCamera, !_thirdPersonActive);
        SetCameraEnabled(_thirdPersonCamera, _thirdPersonActive);

        CinemachineCamera active = ActiveCamera;
        if (active == null) return;

        // Движение считается относительно активной камеры. Камера от первого лица
        // висит на игроке, поэтому тело не разворачивается; от третьего — разворачивается.
        if (playerController != null) playerController.ActiveCamera = active;
        if (playerCombat != null) playerCombat.ActiveCamera = active;

        Debug.Log($"[Camera] вид: {(_thirdPersonActive ? "третье лицо" : "первое лицо")}");
    }

    private static void SetCameraEnabled(CinemachineCamera cam, bool value)
    {
        if (cam == null) return;

        cam.enabled = value;

        CinemachineInputAxisController input = cam.GetComponent<CinemachineInputAxisController>();
        if (input != null) input.enabled = value;
    }

    private void HandleOrbitInput()
    {
        if (!_thirdPersonActive || _orbital == null) return;

        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        Vector2 delta = mouse.delta.ReadValue();
        if (delta.sqrMagnitude <= 0f) return;

        _orbital.HorizontalAxis.Value += delta.x * _mouseSensitivityX;

        float tilt = _orbital.VerticalAxis.Value - delta.y * _mouseSensitivityY;
        _orbital.VerticalAxis.Value = Mathf.Clamp(tilt, _verticalRange.x, _verticalRange.y);
    }

    private CinemachineCamera BuildThirdPersonCamera()
    {
        GameObject rig = new GameObject("ThirdPersonCamera");

        CinemachineCamera cam = rig.AddComponent<CinemachineCamera>();
        cam.Target.TrackingTarget = _followTarget;

        CinemachineOrbitalFollow orbital = rig.AddComponent<CinemachineOrbitalFollow>();
        orbital.OrbitStyle = CinemachineOrbitalFollow.OrbitStyles.Sphere;
        orbital.Radius = _distance;
        orbital.TargetOffset = new Vector3(0f, _shoulderHeight, 0f);
        orbital.VerticalAxis.Range = _verticalRange;
        orbital.VerticalAxis.Value = 15f;

        CinemachineRotationComposer composer = rig.AddComponent<CinemachineRotationComposer>();
        composer.TargetOffset = new Vector3(0f, _shoulderHeight, 0f);

        return cam;
    }
}
