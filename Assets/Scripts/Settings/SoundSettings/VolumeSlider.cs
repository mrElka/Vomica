using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// вешается на слайдер 0..1, в инспекторе нужн выбрать группу аудиомикшера
/// сейчас для главного меню, но в дальнейшем можно использовать в меню паузы
/// </summary>


[RequireComponent(typeof(Slider))]
public class VolumeSlider : MonoBehaviour
{
    [SerializeField] private AudioChannel channel;
    [SerializeField] private Text percentText; // это для текста с отображением значения
    // если будет тмп, то это ^^^ надо поменять на TMP формат

    private Slider _slider;

    private void Awake()
    {
        _slider = GetComponent<Slider>();
        _slider.minValue = 0f;
        _slider.maxValue = 1f;
    }

    private void OnEnable()
    {
        // подтягивает актуальное значение каждый раз, когда панель открывается
        _slider.SetValueWithoutNotify(AudioSettingsManager.GetVolume(channel));
        UpdateText(_slider.value);

        _slider.onValueChanged.AddListener(OnSliderChanged);
        AudioSettingsManager.OnVolumeChanged += OnExternalChange;
    }

    private void OnDisable()
    {
        _slider.onValueChanged.RemoveListener(OnSliderChanged);
        AudioSettingsManager.OnVolumeChanged -= OnExternalChange;
        AudioSettingsManager.Save(); // сохранение чтобы в некст раз настройки не сбросились
    }

    private void OnSliderChanged(float value)
    {
        AudioSettingsManager.SetVolume(channel, value);
        UpdateText(value);
    }

    private void OnExternalChange(AudioChannel c, float value)
    {
        if (c != channel) return;
        _slider.SetValueWithoutNotify(value);
        UpdateText(value);
    }

    private void UpdateText(float value)
    {
        if (percentText != null)
            percentText.text = Mathf.RoundToInt(value * 100f) + "%";
    }
}
