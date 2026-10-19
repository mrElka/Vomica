using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;


/// Что делает:
/// 1. По клавише V (или через <see cref="InputHandler.ToggleViewPressed"/>) переключает активную камеру.
/// 2. В FP скрывает меши тела игрока (руки и оружие остаются видимыми), в TP — показывает.
/// 3. Управляет поворотом камеры мышью: в FP — через CinemachinePanTilt, в TP — через CinemachineOrbitalFollow.
/// 4. Сообщает PlayerController и PlayerCombat, какая камера сейчас активна — чтобы направление атаки
///    и движения считались от нужной камеры.
///
/// Зависимости:
/// - Cinemachine (первая и третья камера, PanTilt, OrbitalFollow).
/// - InputHandler — читает нажатие V и движение мыши.
/// - PlayerController и PlayerCombat — получают ссылку на активную камеру.
/// - PlayerAnimatorUtility — ищет визуальный корень модели, если он не назначен вручную.
/// </summary>
public class CameraSwitcher : MonoBehaviour
{
    [Header("Камеры")]
    [Tooltip("Камера от первого лица. Пусто — найдётся среди дочерних объектов игрока")]
    [SerializeField] private CinemachineCamera _firstPersonCamera;

    [Tooltip("Камера от третьего лица. Пусто — будет создана автоматически при старте")]
    [SerializeField] private CinemachineCamera _thirdPersonCamera;

    [Header("Третье лицо")]
    [Tooltip("Цель, за которой следит камера TP. Пусто — этот объект")]
    [SerializeField] private Transform _followTarget;

    [Tooltip("Расстояние от камеры до игрока (радиус орбиты), м")]
    [SerializeField] private float _distance = 4.5f;

    [Tooltip("Высота точки, вокруг которой вращается TP-камера, м")]
    [SerializeField] private float _shoulderHeight = 1.4f;

    [Tooltip("Пределы наклона камеры по вертикали (min, max), град")]
    [SerializeField] private Vector2 _verticalRange = new Vector2(-30f, 60f);

    [Tooltip("Чувствительность мыши по горизонтали")]
    [SerializeField] private float _mouseSensitivityX = 0.25f;

    [Tooltip("Чувствительность мыши по вертикали")]
    [SerializeField] private float _mouseSensitivityY = 0.2f;

    [Header("Старт")]
    [Tooltip("Начинать игру с видом от третьего лица")]
    [SerializeField] private bool _startInThirdPerson;

    [Header("Body Visibility")]
    [Tooltip("Корень модели игрока (hero_completed). В FP меши тела скрываются, в TP показываются")]
    [SerializeField] private Transform _bodyRoot;

    [Tooltip("Имена частей тела, которые НЕ прячутся в FP (руки, оружие). Совпадение по подстроке")]
    [SerializeField] private string[] _visibleParts = { "Arm.L", "Arm.R", "Hand.L", "Hand.R" };

    [Header("Ссылки")]
    [Tooltip("Обработчик ввода. Пусто — будет найден на этом же объекте")]
    [SerializeField] private InputHandler inputHandler;

    [Tooltip("Контроллер игрока. Пусто — будет найден на этом же объекте")]
    [SerializeField] private PlayerController playerController;

    [Tooltip("Боевая система игрока. Пусто — будет найдена на этом же объекте")]
    [SerializeField] private PlayerCombat playerCombat;

    [Header("Отладка")]
    [Tooltip("Логировать переключение вида в консоль")]
    [SerializeField] private bool _logCameraSwitch = false;

    private CinemachineOrbitalFollow _orbital;
    private CinemachinePanTilt _firstPanTilt;
    private bool _thirdPersonActive;

    /// <summary>true — сейчас активен вид от третьего лица.</summary>
    public bool IsThirdPerson => _thirdPersonActive;

    /// <summary>Камера, которая сейчас показывает игру (FP или TP).</summary>
    public CinemachineCamera ActiveCamera =>
        _thirdPersonActive ? _thirdPersonCamera : _firstPersonCamera;

    private void Awake()
    {
        ResolveReferences();
        CacheCameras();
        CacheCameraComponents();

        _thirdPersonActive = _startInThirdPerson;
        Apply();
    }

    private void Update()
    {
        if (TogglePressed())
            Toggle();

        HandleLookInput();
    }

    /// <summary>Ищет ссылки на компоненты игрока, если они не назначены в инспекторе.</summary>
    private void ResolveReferences()
    {
        if (inputHandler == null) inputHandler = GetComponent<InputHandler>();
        if (playerController == null) playerController = GetComponent<PlayerController>();
        if (playerCombat == null) playerCombat = GetComponent<PlayerCombat>();
        if (_followTarget == null) _followTarget = transform;

        if (_bodyRoot == null)
            _bodyRoot = PlayerAnimatorUtility.FindVisualRoot(transform);
    }

    /// <summary>Определяет FP и TP камеры. TP создаётся автоматически, если не назначена.</summary>
    private void CacheCameras()
    {
        if (_firstPersonCamera == null)
            _firstPersonCamera = GetComponentInChildren<CinemachineCamera>(true);

        if (_thirdPersonCamera == null)
            _thirdPersonCamera = BuildThirdPersonCamera();
    }

    /// <summary>Находит компоненты управления осями у камер (PanTilt для FP, Orbital для TP).</summary>
    private void CacheCameraComponents()
    {
        if (_thirdPersonCamera != null)
            _orbital = _thirdPersonCamera.GetComponent<CinemachineOrbitalFollow>();

        if (_firstPersonCamera != null)
            _firstPanTilt = _firstPersonCamera.GetComponent<CinemachinePanTilt>();
    }

    /// <summary>Переключить вид: FP ↔ TP.</summary>
    public void Toggle() => SetThirdPerson(!_thirdPersonActive);

    /// <summary>Принудительно установить вид.</summary>
    /// <param name="value">true — TP, false — FP.</param>
    public void SetThirdPerson(bool value)
    {
        _thirdPersonActive = value;
        Apply();
    }

    /// <summary>Проверяет, была ли нажата клавиша переключения вида.</summary>
    private bool TogglePressed()
    {
        if (inputHandler != null)
            return inputHandler.ToggleViewPressed;

        return Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame;
    }

    /// <summary>
    /// Применяет текущий режим: включает нужную камеру, выключает другую,
    /// сообщает ссылку PlayerController/PlayerCombat, обновляет видимость тела.
    /// </summary>
    private void Apply()
    {
        if (_thirdPersonActive && _thirdPersonCamera == null)
        {
            Debug.LogWarning("[CameraSwitcher] TP-камера недоступна. Остаюсь в FP.");
            _thirdPersonActive = false;
        }

        SetCameraEnabled(_firstPersonCamera, !_thirdPersonActive);
        SetCameraEnabled(_thirdPersonCamera, _thirdPersonActive);

        CinemachineCamera active = ActiveCamera;
        if (active == null) return;

        if (playerController != null) playerController.ActiveCamera = active;
        if (playerCombat != null) playerCombat.ActiveCamera = active;

        UpdateBodyVisibility();

        if (_logCameraSwitch)
            Debug.Log($"[CameraSwitcher] Вид: {(_thirdPersonActive ? "третье лицо" : "первое лицо")}");
    }

    /// <summary>Включает/выключает камеру и синхронный с ней ввод Cinemachine.</summary>
    private static void SetCameraEnabled(CinemachineCamera cam, bool value)
    {
        if (cam == null) return;

        cam.enabled = value;

        CinemachineInputAxisController input = cam.GetComponent<CinemachineInputAxisController>();
        if (input != null) input.enabled = value;
    }

    /// <summary>
    /// В FP скрывает меши тела (Skinned Mesh), оставляя руки и оружие видимыми.
    /// Использует ShadowCastingMode.ShadowsOnly — тело не рисуется, но тень остаётся.
    /// </summary>
    private void UpdateBodyVisibility()
    {
        if (_bodyRoot == null) return;

        bool hideBody = !_thirdPersonActive;

        foreach (Transform child in _bodyRoot.GetComponentsInChildren<Transform>(true))
        {
            if (ShouldStayVisible(child.name))
                continue;

            SkinnedMeshRenderer skinned = child.GetComponent<SkinnedMeshRenderer>();
            if (skinned == null) continue;

            skinned.shadowCastingMode = hideBody
                ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly
                : UnityEngine.Rendering.ShadowCastingMode.On;
        }
    }

    /// <summary>Проверяет, должна ли часть тела оставаться видимой в FP (совпадение по подстроке).</summary>
    private bool ShouldStayVisible(string objectName)
    {
        if (_visibleParts == null || _visibleParts.Length == 0) return false;

        foreach (string part in _visibleParts)
        {
            if (string.IsNullOrEmpty(part)) continue;
            if (objectName == part || objectName.Contains(part)) return true;
        }

        return false;
    }

    /// <summary>Читает дельту мыши и применяет её к активной камере.</summary>
    private void HandleLookInput()
    {
        if (PauseMenu.IsPaused) return;

        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        Vector2 delta = mouse.delta.ReadValue();
        if (delta.sqrMagnitude <= 0f) return;

        if (!_thirdPersonActive)
            ApplyLookDeltaFP(delta);
        else
            ApplyLookDeltaTP(delta);
    }

    /// <summary>Поворот FP-камеры: горизонталь — Pan, вертикаль — Tilt с ограничением.</summary>
    private void ApplyLookDeltaFP(Vector2 delta)
    {
        if (_firstPanTilt == null) return;

        _firstPanTilt.PanAxis.Value += delta.x * _mouseSensitivityX;

        float tilt = _firstPanTilt.TiltAxis.Value - delta.y * _mouseSensitivityY;
        _firstPanTilt.TiltAxis.Value = Mathf.Clamp(tilt, _verticalRange.x, _verticalRange.y);
    }

    /// <summary>Поворот TP-камеры: горизонталь — орбита, вертикаль — наклон с ограничением.</summary>
    private void ApplyLookDeltaTP(Vector2 delta)
    {
        if (_orbital == null) return;

        _orbital.HorizontalAxis.Value += delta.x * _mouseSensitivityX;

        float tilt = _orbital.VerticalAxis.Value - delta.y * _mouseSensitivityY;
        _orbital.VerticalAxis.Value = Mathf.Clamp(tilt, _verticalRange.x, _verticalRange.y);
    }

    /// <summary>
    /// Создаёт TP-камеру с орбитальным вращением, если она не назначена в инспекторе.
    /// Следит за _followTarget на высоте плеча, орбита радиуса _distance.
    /// </summary>
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

        return rig.GetComponent<CinemachineCamera>();
    }
}