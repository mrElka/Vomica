using UnityEngine;

/// <summary>
/// Пользовательские настройки: громкость, чувствительность мыши, полный экран.
/// Хранятся в PlayerPrefs и применяются при старте игры, поэтому работают в любой сцене.
/// Меняются из PauseMenu; остальные скрипты только читают значения.
/// </summary>
public static class GameSettings
{
    public const float MinSensitivity = 0.2f;
    public const float MaxSensitivity = 3f;

    private const string VolumeKey = "settings.volume";
    private const string SensitivityKey = "settings.sensitivity";
    private const string FullscreenKey = "settings.fullscreen";

    /// <summary>Общая громкость 0..1.</summary>
    public static float Volume { get; private set; } = 1f;

    /// <summary>Множитель чувствительности мыши (1 = как настроено в CameraSwitcher).</summary>
    public static float MouseSensitivity { get; private set; } = 1f;

    /// <summary>Полноэкранный режим.</summary>
    public static bool Fullscreen { get; private set; } = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Load()
    {
        Volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 1f));
        MouseSensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(SensitivityKey, 1f), MinSensitivity, MaxSensitivity);

        bool hasFullscreen = PlayerPrefs.HasKey(FullscreenKey);
        Fullscreen = hasFullscreen ? PlayerPrefs.GetInt(FullscreenKey) == 1 : Screen.fullScreen;

        AudioListener.volume = Volume;

        // Пока игрок ничего не выбирал, режим окна не трогаем
        if (hasFullscreen)
            Screen.fullScreen = Fullscreen;
    }

    public static void SetVolume(float value)
    {
        Volume = Mathf.Clamp01(value);
        AudioListener.volume = Volume;
        PlayerPrefs.SetFloat(VolumeKey, Volume);
    }

    public static void SetMouseSensitivity(float value)
    {
        MouseSensitivity = Mathf.Clamp(value, MinSensitivity, MaxSensitivity);
        PlayerPrefs.SetFloat(SensitivityKey, MouseSensitivity);
    }

    public static void SetFullscreen(bool value)
    {
        Fullscreen = value;
        Screen.fullScreen = value;
        PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0);
    }

    /// <summary>Сбросить изменения на диск (вызывается при закрытии паузы и выходе).</summary>
    public static void Save() => PlayerPrefs.Save();
}
