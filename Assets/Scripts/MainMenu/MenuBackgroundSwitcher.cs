using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class MenuBackgroundSwitcher : MonoBehaviour
{
    [Header("Спрайты фона")]
    [Tooltip("Спрайт, который показывается в начале игры")]
    [SerializeField] private Sprite defaultBackground;

    [Tooltip("Спрайт, который показывается после прохождения игры")]
    [SerializeField] private Sprite completedBackground;

    [Header("Состояние игры")]
    [Tooltip("Включите галочку, если игра пройдена")]
    [SerializeField] private bool gameComplete = false;

    private Image backgroundImage;

    // Публичное свойство, чтобы можно было менять из других скриптов
    public bool GameComplete
    {
        get => gameComplete;
        set
        {
            gameComplete = value;
            UpdateBackground();
        }
    }

    private void Awake()
    {
        backgroundImage = GetComponent<Image>();
    }

    private void Start()
    {
        UpdateBackground();
    }

    // Вызывается автоматически в редакторе при изменении значений в инспекторе
    private void OnValidate()
    {
        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();

        UpdateBackground();
    }

    private void UpdateBackground()
    {
        if (backgroundImage == null) return;

        backgroundImage.sprite = gameComplete ? completedBackground : defaultBackground;
    }
}