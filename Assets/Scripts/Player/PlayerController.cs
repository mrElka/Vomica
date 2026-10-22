using UnityEngine;

/// <summary>
/// Общие настройки игрока, не связанные с механикой движения:
/// курсор, подготовка контроллера поз и скрытие капсулы-заглушки.
/// Передвижение реализует <see cref="PlayerMovement"/>.
/// </summary>
[RequireComponent(typeof(PlayerMovement))]
public class PlayerController : MonoBehaviour
{
    [Header("Visual")]
    [Tooltip("Корень модели игрока. Если не указан, будет найден автоматически.")]
    [SerializeField] private Transform _visualRoot;

    [Header("Cursor")]
    [SerializeField] private bool _lockCursorOnStart = true;

    private void Awake()
    {
        if (_visualRoot == null)
            _visualRoot = PlayerAnimatorUtility.FindVisualRoot(transform);

        TryEnsurePoseController();
        HidePlaceholderCapsuleMesh();

        if (_lockCursorOnStart)
            SetCursorLocked(true);
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && _lockCursorOnStart)
            SetCursorLocked(true);
    }

    /// <summary>Заблокировать или разблокировать курсор.</summary>
    public void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    /// <summary>Добавляет контроллер поз, если экипировка игрока уже присутствует.</summary>
    private void TryEnsurePoseController()
    {
        var equipment = GetComponent<CharacterEquipment.CharacterEquipment>();
        if (equipment == null)
            return;

        var poseController = GetComponent<CharacterEquipment.CharacterPoseController>();
        if (poseController == null)
            gameObject.AddComponent<CharacterEquipment.CharacterPoseController>();
    }

    /// <summary>Прячет меш капсулы-заглушки, когда у игрока есть визуальная модель.</summary>
    private void HidePlaceholderCapsuleMesh()
    {
        if (_visualRoot == null)
            return;

        var meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer != null)
            meshRenderer.enabled = false;

        var meshFilter = GetComponent<MeshFilter>();
        if (meshFilter != null)
            meshFilter.hideFlags = HideFlags.HideInInspector;
    }
}
