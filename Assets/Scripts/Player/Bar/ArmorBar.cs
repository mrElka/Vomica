using UnityEngine;
using UnityEngine.UI;

public class ArmorBar : MonoBehaviour
{
    [Header("Sliders")]
    [SerializeField] private Slider armorSlider;
    [SerializeField] private Slider easeArmorSlider;

    [Header("Label")]
    [SerializeField] private Text _label;
    [SerializeField] private string _labelPrefix = "";

    [Header("Settings")]
    [SerializeField] private float decreaseSpeed = 80f;

    private static Sprite _whiteSprite;

    private void Update()
    {
        if (armorSlider == null || easeArmorSlider == null)
            return;

        if (easeArmorSlider.value > armorSlider.value)
        {
            easeArmorSlider.value = Mathf.MoveTowards(
                easeArmorSlider.value,
                armorSlider.value,
                decreaseSpeed * Time.deltaTime
            );
        }
        else if (easeArmorSlider.value < armorSlider.value)
        {
            easeArmorSlider.value = armorSlider.value;
        }
    }

    public void SetMaxValue(float value)
    {
        if (armorSlider == null || easeArmorSlider == null)
            return;

        armorSlider.maxValue = value;
        easeArmorSlider.maxValue = value;

        armorSlider.value = value;
        easeArmorSlider.value = value;

        UpdateLabel(value);
    }

    public void SetValue(float value)
    {
        if (armorSlider != null)
            armorSlider.value = value;

        UpdateLabel(value);
    }

    private void UpdateLabel(float value)
    {
        if (_label == null) return;

        int percent = Mathf.RoundToInt(value);
        _label.text = string.IsNullOrEmpty(_labelPrefix)
            ? $"{percent}%"
            : $"{_labelPrefix}: {percent}%";
    }

    /// <summary>
    /// Создаёт полоску брони с подписью поверх.
    /// </summary>
    public static ArmorBar CreateBar(
        Transform parent,
        string objectName,
        Vector2 anchoredPosition,
        Vector2 size,
        Color fillColor,
        Color easeColor,
        string labelPrefix = "")
    {
        GameObject root = new GameObject(objectName, typeof(RectTransform));
        root.transform.SetParent(parent, false);

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0f, 1f);
        rootRect.anchorMax = new Vector2(0f, 1f);
        rootRect.pivot = new Vector2(0f, 1f);
        rootRect.anchoredPosition = anchoredPosition;
        rootRect.sizeDelta = size;

        ArmorBar armorBar = root.AddComponent<ArmorBar>();
        armorBar._labelPrefix = labelPrefix;
        armorBar.easeArmorSlider = CreateSlider(root.transform, "EaseBar", easeColor, true);
        armorBar.armorSlider = CreateSlider(root.transform, "Bar", fillColor, false);
        armorBar.decreaseSpeed = 80f;


        armorBar._label = CreateLabel(root.transform);

        return armorBar;
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

    /// <summary>Маленькая подпись, растянутая на всю полоску, выровнена по левому краю.</summary>
    private static Text CreateLabel(Transform parent)
    {
        GameObject labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(parent, false);

        RectTransform rect = labelGo.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(6f, 0f);   
        rect.offsetMax = new Vector2(-6f, 0f);  

        Text label = labelGo.AddComponent<Text>();
        label.text = "";
        label.font = GetDefaultFont();
        label.fontSize = 11;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleLeft;
        label.color = Color.white;
        label.raycastTarget = false;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;

        Shadow shadow = labelGo.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
        shadow.effectDistance = new Vector2(1f, -1f);

        return label;
    }

    private static Font GetDefaultFont()
    {
        Font font = null;
        try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
        if (font == null)
        {
            try { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
        }
        return font;
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