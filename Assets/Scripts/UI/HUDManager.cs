using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Синглтон менеджер HUD
/// Управляет видимостью и обновлением всех UI элементов
/// Подключение к HUDData для получения игровых данных
/// </summary>
public class HUDManager : MonoBehaviour
{
    public static HUDManager Instance {  get; private set; }
    
    [Header("Canvas")]
    [SerializeField] private Canvas hudCanvas;
    [SerializeField] private CanvasGroup hudCanvasGroup;

    [Header("Data")]
    [SerializeField] private HUDData hudData;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        ValidateReference();
    }

    private void Start()
    {
        ShowHUD(true);
    }

    /// <summary>
    /// Показать или скрыть ВЕСЬ худ
    /// </summary>
    public void ShowHUD(bool visible)
    {
        if (hudCanvasGroup == null) return;
        hudCanvasGroup.alpha = visible ? 1f : 0f;
        hudCanvasGroup.interactable = visible;
        hudCanvasGroup.blocksRaycasts = visible;
    }

    /// <summary>
    /// Задать прозрачность 0...1 
    /// </summary>
    public void SetHUDAlpha(float alpha)
    {
        if (hudCanvasGroup != null)
            hudCanvasGroup.alpha = Mathf.Clamp01(alpha);
    }

    /// <summary>
    /// Получить текущие данные HUD (для других скриптов)
    /// </summary>
    public HUDData GetHUDData() => hudData;

    private void ValidateReference()
    {
        if (hudCanvas == null)
            Debug.LogWarning("[HUDManager] hudCanvas не назначен");
        if (hudData == null)
            Debug.LogWarning("[HUDManager] hudData не назначен - нужно создать HUDData asset и назначить его в поле");
    }
}


