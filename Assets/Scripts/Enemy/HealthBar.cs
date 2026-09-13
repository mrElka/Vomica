using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    [Header("Sliders")]
    [SerializeField] private Slider healthSlider;
    [SerializeField] private Slider easeHealthSlider;

    [Header("Settings")]
    [SerializeField] private float decreaseSpeed = 100f;

    private static Sprite _whiteSprite;

    private void Update()
    {
        if (healthSlider == null || easeHealthSlider == null)
            return;

        if (easeHealthSlider.value > healthSlider.value)
        {
            easeHealthSlider.value = Mathf.MoveTowards(
                easeHealthSlider.value,
                healthSlider.value,
                decreaseSpeed * Time.deltaTime
            );
        }
        else if (easeHealthSlider.value < healthSlider.value)
        {
            easeHealthSlider.value = healthSlider.value;
        }
    }

    public void SetMaxHealth(int health)
    {
        if (healthSlider == null || easeHealthSlider == null)
            return;

        healthSlider.maxValue = health;
        easeHealthSlider.maxValue = health;

        healthSlider.value = health;
        easeHealthSlider.value = health;
    }

    public void SetHealth(int health)
    {
        if (healthSlider != null)
            healthSlider.value = health;
    }

    public static HealthBar CreateWorldBar(Transform owner, Vector3 localOffset)
    {
        GameObject root = new GameObject("MobHealth");
        root.transform.SetParent(owner, false);
        root.transform.localPosition = localOffset;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one * 0.01f;

        RectTransform rootRect = root.AddComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(140f, 16f);

        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        root.AddComponent<CanvasScaler>();
        root.AddComponent<GraphicRaycaster>();
        root.AddComponent<BillBoard>();

        HealthBar healthBar = root.AddComponent<HealthBar>();
        healthBar.easeHealthSlider = CreateSlider(root.transform, "EaseHealthBar", new Color(1f, 0.85f, 0.2f, 1f), true);
        healthBar.healthSlider = CreateSlider(root.transform, "HealthBar", new Color(0.25f, 0.85f, 0.3f, 1f), false);
        healthBar.decreaseSpeed = 100f;
        return healthBar;
    }

    public static HealthBar CreateScreenBar()
    {
        GameObject canvasGo = new GameObject("PlayerHealthCanvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGo.AddComponent<CanvasScaler>();
        canvasGo.AddComponent<GraphicRaycaster>();

        GameObject root = new GameObject("PlayerHealthBar", typeof(RectTransform));
        root.transform.SetParent(canvasGo.transform, false);

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0f, 1f);
        rootRect.anchorMax = new Vector2(0f, 1f);
        rootRect.pivot = new Vector2(0f, 1f);
        rootRect.anchoredPosition = new Vector2(24f, -24f);
        rootRect.sizeDelta = new Vector2(260f, 22f);

        HealthBar healthBar = root.AddComponent<HealthBar>();
        healthBar.easeHealthSlider = CreateSlider(root.transform, "EaseHealthBar", new Color(1f, 0.85f, 0.2f, 1f), true);
        healthBar.healthSlider = CreateSlider(root.transform, "HealthBar", new Color(0.85f, 0.2f, 0.2f, 1f), false);
        healthBar.decreaseSpeed = 80f;
        return healthBar;
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
