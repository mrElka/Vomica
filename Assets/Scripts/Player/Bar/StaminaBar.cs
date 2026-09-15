using UnityEngine;
using UnityEngine.UI;

public class StaminaBar : MonoBehaviour
{
    [Header("Sliders")]
    [SerializeField] private Slider staminaSlider;
    [SerializeField] private Slider easeStaminaSlider;

    [Header("Settings")]
    [SerializeField] private float decreaseSpeed = 100f;

    private static Sprite _whiteSprite;

    private void Update()
    {
        if (staminaSlider == null || easeStaminaSlider == null)
            return;

        if (easeStaminaSlider.value > staminaSlider.value)
        {
            easeStaminaSlider.value = Mathf.MoveTowards(
                easeStaminaSlider.value,
                staminaSlider.value,
                decreaseSpeed * Time.deltaTime
            );
        }
        else if (easeStaminaSlider.value < staminaSlider.value)
        {
            easeStaminaSlider.value = staminaSlider.value;
        }
    }

    public void SetMaxStamina(float stamina)
    {
        if (staminaSlider == null || easeStaminaSlider == null)
            return;

        staminaSlider.maxValue = stamina;
        easeStaminaSlider.maxValue = stamina;

        staminaSlider.value = stamina;
        easeStaminaSlider.value = stamina;
    }

    public void SetStamina(float stamina)
    {
        if (staminaSlider != null)
            staminaSlider.value = stamina;
    }

    public static StaminaBar CreateScreenBar()
    {
        GameObject canvasGo = new GameObject("PlayerStaminaCanvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGo.AddComponent<CanvasScaler>();
        canvasGo.AddComponent<GraphicRaycaster>();

        GameObject root = new GameObject("PlayerStaminaBar", typeof(RectTransform));
        root.transform.SetParent(canvasGo.transform, false);

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0f, 1f);
        rootRect.anchorMax = new Vector2(0f, 1f);
        rootRect.pivot = new Vector2(0f, 1f);
        rootRect.anchoredPosition = new Vector2(24f, -54f);
        rootRect.sizeDelta = new Vector2(260f, 14f);

        StaminaBar staminaBar = root.AddComponent<StaminaBar>();
        staminaBar.easeStaminaSlider = CreateSlider(root.transform, "EaseStaminaBar",
            new Color(0.6f, 0.5f, 0.1f, 1f), true);
        staminaBar.staminaSlider = CreateSlider(root.transform, "StaminaBar",
            new Color(1f, 0.85f, 0.2f, 1f), false);
        staminaBar.decreaseSpeed = 120f;
        return staminaBar;
    }

    private static Slider CreateSlider(Transform parent, string objectName, Color fillColor, bool showBackground)
    {
        GameObject sliderGo = new GameObject(objectName, typeof(RectTransform));
        sliderGo.transform.SetParent(parent, false);

        RectTransform sliderRect = sliderGo.GetComponent<RectTransform>();
        sliderRect.anchorMin = Vector2.zero;
        sliderRect.anchorMax = Vector2.one;
        sliderRect.offsetMin = Vector2.zero;
        sliderRect.offsetMax = Vector2.zero;

        Image background = sliderGo.AddComponent<Image>();
        background.sprite = GetWhiteSprite();
        background.color = showBackground
            ? new Color(0.12f, 0.12f, 0.12f, 0.9f)
            : new Color(0f, 0f, 0f, 0f);

        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(sliderGo.transform, false);
        RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = new Vector2(2f, 2f);
        fillAreaRect.offsetMax = new Vector2(-2f, -2f);

        GameObject fill = new GameObject("Fill", typeof(RectTransform));
        fill.transform.SetParent(fillArea.transform, false);
        Image fillImage = fill.AddComponent<Image>();
        fillImage.sprite = GetWhiteSprite();
        fillImage.color = fillColor;
        fillImage.type = Image.Type.Simple;

        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        Slider slider = sliderGo.AddComponent<Slider>();
        slider.fillRect = fillRect;
        slider.minValue = 0f;
        slider.maxValue = 100f;
        slider.wholeNumbers = false;
        slider.value = 100f;
        slider.interactable = false;
        slider.transition = Selectable.Transition.None;
        return slider;
    }

    private static Sprite GetWhiteSprite()
    {
        if (_whiteSprite != null)
            return _whiteSprite;

        _whiteSprite = Sprite.Create(
            Texture2D.whiteTexture,
            new Rect(0f, 0f, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
            new Vector2(0.5f, 0.5f),
            100f
        );
        return _whiteSprite;
    }
}