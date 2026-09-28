using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// можно настроить как префаб в самом скрипте прикрепив звук, пока сделал обычшый клик кнопки с клавы
/// когда добавили на кнопку звук можно поменять, замена будет локальная на кнопке на которой меняли
/// </summary>
[RequireComponent(typeof(Button))]
public class ButtonClickSound : MonoBehaviour
{
    [SerializeField] private AudioClip clickClip;
    [SerializeField, Range(0f, 1f)] private float volume = 1f;

    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
    }

    private void OnEnable()
    {
        _button.onClick.AddListener(PlayClick);
    }

    private void OnDisable()
    {
        _button.onClick.RemoveListener(PlayClick);
    }

    private void PlayClick()
    {
        UIAudio.Play(clickClip, volume);
    }
}
