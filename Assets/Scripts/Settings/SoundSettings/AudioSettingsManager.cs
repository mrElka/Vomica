using System;
using UnityEngine;
using UnityEngine.Audio;

public enum AudioChannel
{
    Master,
    Music,
    Ambience,
    Voice,
    UI
}

/// <summary>
/// статический менеджер громкости, не требует объектов на сцене:
/// сам загружает микшер из Resources и применяет сохранённые настройки при старте игры
/// 
/// !!ВАЖНО!! микшер должен лежать по пути Assets/Resources/MainMixer.mixer <-- иначе работать не будет
/// хз пока как сделать более гибко, но МИКШЕР ПЕРЕНОСИТЬ НЕЛЬЗЯ!
/// </summary>
public static class AudioSettingsManager
{
    private const string MixerResourceName = "MainMixer";
    private const float MinLinear = 0.0001f; // -80 дб

    private static AudioMixer _mixer;

    public static event Action<AudioChannel, float> OnVolumeChanged;

    // имена exposed-параметров в микшере (должны совпадать точно!!!!!)
    private static string ParamName(AudioChannel c) => c + "Volume";
    private static string PrefsKey(AudioChannel c) => "Audio_" + c;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        _mixer = Resources.Load<AudioMixer>(MixerResourceName);
        if (_mixer == null)
        {
            Debug.LogError($"AudioMixer '{MixerResourceName}' не найден в папке Resources!");
            return;
        }

        foreach (AudioChannel c in Enum.GetValues(typeof(AudioChannel)))
            ApplyToMixer(c, GetVolume(c));
    }

    /// <summary>
    /// громкость канала в диапазоне 0..1 (из сохранённых настроек).
    /// </summary>
    public static float GetVolume(AudioChannel channel)
    {
        return PlayerPrefs.GetFloat(PrefsKey(channel), 1f);
    }

    /// <summary>
    /// установить громкость 0..1, применить к микшеру и сохранить
    /// </summary>
    public static void SetVolume(AudioChannel channel, float value)
    {
        value = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(PrefsKey(channel), value);
        ApplyToMixer(channel, value);
        OnVolumeChanged?.Invoke(channel, value);
    }

    public static void Save()
    {
        PlayerPrefs.Save();
    }

    private static void ApplyToMixer(AudioChannel channel, float linear)
    {
        if (_mixer == null) return;
        float dB = Mathf.Log10(Mathf.Max(linear, MinLinear)) * 20f;
        _mixer.SetFloat(ParamName(channel), dB);
    }
}
