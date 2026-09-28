using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// проигрывает звуки для UI (кнопки ползунки и тД) в соответствующую группу аудиомикшера
/// ничего вешать на сцену не нужно, источник создаётся сам при первом вызове
/// в MainMixer должна быть група UI, а то ниче работать не будет
/// </summary>
public static class UIAudio
{
    private static AudioSource _source;

    private static void EnsureSource()
    {
        if (_source != null) return;

        var go = new GameObject("UIAudioSource");
        Object.DontDestroyOnLoad(go);

        _source = go.AddComponent<AudioSource>();
        _source.playOnAwake = false;
        _source.spatialBlend = 0f;          // 2д звук, без привязки к 3д
        _source.ignoreListenerPause = true; // играет и на паузе

        var mixer = Resources.Load<AudioMixer>("MainMixer");
        if (mixer != null)
        {
            var groups = mixer.FindMatchingGroups("Master/UI");
            if (groups.Length > 0)
                _source.outputAudioMixerGroup = groups[0];
            else
                Debug.LogWarning("Группа Master/UI не найдена в MainMixer.");
        }
    }

    public static void Play(AudioClip clip, float volume = 1f)
    {
        if (clip == null) return;
        EnsureSource();
        _source.PlayOneShot(clip, volume);
    }
}
