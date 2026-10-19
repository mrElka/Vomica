using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Вью полоски (здоровье, выносливость). Ничего не знает об игроке:
/// получает значения через SetValue, а рисует заливку, «хвост» потерянного
/// здоровья и текст «75 / 100».
///
/// Устройство в префабе (порядок важен — что ниже в иерархии, то рисуется выше):
///   Trail  — Image, «хвост» (необязательно)
///   Fill   — Image, основная заливка
///   Frame  — Image, окантовка поверх
///   Label  — TextMeshPro (необязательно)
/// Image заливки и хвоста сами переключаются в Filled / Horizontal / Left.
/// </summary>
public class HudBar : MonoBehaviour
{
    [Header("Заливка")]
    [Tooltip("Основная заливка. У Image должен быть назначен спрайт")]
    [SerializeField] private Image fill;

    [Tooltip("Хвост потерянного значения (лежит под заливкой). Можно не назначать")]
    [SerializeField] private Image trailFill;

    [Header("Текст")]
    [SerializeField] private TMP_Text label;

    [Tooltip("{0} — текущее значение, {1} — максимум")]
    [SerializeField] private string labelFormat = "{0} / {1}";

    [Header("Анимация")]
    [Tooltip("Скорость основной заливки, доля шкалы в секунду")]
    [SerializeField, Min(0.1f)] private float fillSpeed = 3f;

    [Tooltip("Сколько хвост ждёт после потери значения, с")]
    [SerializeField, Min(0f)] private float trailDelay = 0.4f;

    [Tooltip("Скорость сжатия хвоста, доля шкалы в секунду")]
    [SerializeField, Min(0.05f)] private float trailSpeed = 0.6f;

    private float target = 1f;
    private float shown = 1f;
    private float trail = 1f;
    private float trailTimer;
    private bool initialized;

    private int lastCurrent = int.MinValue;
    private int lastMax = int.MinValue;

    private void Awake()
    {
        MakeFilled(fill);
        MakeFilled(trailFill);
        Apply();
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;

        shown = Mathf.MoveTowards(shown, target, fillSpeed * dt);

        if (trail < shown)
        {
            // Лечение: хвост не должен быть короче заливки
            trail = shown;
        }
        else if (trailTimer > 0f)
        {
            trailTimer -= dt;
        }
        else
        {
            trail = Mathf.MoveTowards(trail, shown, trailSpeed * dt);
        }

        Apply();
    }

    /// <summary>Задать значение. Первый вызов выставляет полоску сразу, без анимации.</summary>
    public void SetValue(float current, float max)
    {
        float t = max > 0f ? Mathf.Clamp01(current / max) : 0f;

        if (!initialized)
        {
            initialized = true;
            shown = trail = target = t;
            Apply();
        }
        else if (t < target)
        {
            trailTimer = trailDelay;
        }

        target = t;

        if (label == null) return;

        // Текст меняем только при изменении числа, чтобы TMP не перестраивал меш каждый кадр
        int c = Mathf.CeilToInt(current);
        int m = Mathf.CeilToInt(max);
        if (c == lastCurrent && m == lastMax) return;

        lastCurrent = c;
        lastMax = m;
        label.text = string.Format(labelFormat, c, m);
    }

    private void Apply()
    {
        if (fill != null) fill.fillAmount = shown;
        if (trailFill != null) trailFill.fillAmount = trail;
    }

    private static void MakeFilled(Image image)
    {
        if (image == null) return;

        if (image.sprite == null)
            Debug.LogWarning($"[HudBar] У '{image.name}' нет спрайта — заливка не будет сжиматься. " +
                             "Назначьте любой спрайт (например, встроенный UISprite).", image);

        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
        image.fillOrigin = (int)Image.OriginHorizontal.Left;
    }
}
