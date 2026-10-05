using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// скрипт-плеер, который не стирается на новой сцене
/// плавный фейд между треками как и внутри плейлиста, так и между ними
/// использует два AudioSource и кроссфейдит громкость между ними
/// </summary>
public class AmbiencePlayer : MonoBehaviour
{
    public static AmbiencePlayer Instance { get; private set; }

    [Header("Mixer")]
    [SerializeField] private string mixerGroupPath = "Master/Music";
    [SerializeField] private AudioMixer mixer;

    [Header("Fade")]
    [SerializeField] private float defaultFadeDuration = 2f;

    private AudioSource _sourceA;
    private AudioSource _sourceB;
    private AudioSource _activeSource;
    private AudioSource _inactiveSource => _activeSource == _sourceA ? _sourceB : _sourceA;

    private List<AudioClip> _playlist = new List<AudioClip>();
    private bool _shuffle;
    private int _currentIndex = -1;

    private Coroutine _fadeRoutine;
    private Coroutine _playlistRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        _sourceA = CreateSource("AmbienceSourceA");
        _sourceB = CreateSource("AmbienceSourceB");
        _activeSource = _sourceA;
    }

    private AudioSource CreateSource(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform);

        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = false;
        src.spatialBlend = 0f;
        src.volume = 0f;

        if (mixer == null)
            mixer = Resources.Load<AudioMixer>("MainMixer");

        if (mixer != null)
        {
            var groups = mixer.FindMatchingGroups(mixerGroupPath);
            if (groups.Length > 0)
                src.outputAudioMixerGroup = groups[0];
            else
                Debug.LogWarning($"Группа '{mixerGroupPath}' не найдена в микшере.");
        }

        return src;
    }

    /// <summary>
    /// задать новый плейлист для текущей сцены и плавно перейти к нему
    /// если плейлист совпадает с уже играющим — ничего не делает
    /// </summary>
    public void SetPlaylist(List<AudioClip> clips, bool shuffle = true, float fadeDuration = -1f)
    {
        if (clips == null || clips.Count == 0) return;
        if (IsSamePlaylist(clips)) return;

        if (fadeDuration < 0f) fadeDuration = defaultFadeDuration;

        _playlist = new List<AudioClip>(clips);
        _shuffle = shuffle;
        _currentIndex = -1;

        if (_playlistRoutine != null) StopCoroutine(_playlistRoutine);
        _playlistRoutine = StartCoroutine(PlaylistLoop(fadeDuration));
    }

    /// <summary>
    /// плавно остановить всё 
    /// например, при переходе на сцену без музыки
    /// </summary>
    public void Stop(float fadeDuration = -1f)
    {
        if (fadeDuration < 0f) fadeDuration = defaultFadeDuration;

        _playlist.Clear();
        if (_playlistRoutine != null) StopCoroutine(_playlistRoutine);
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);

        _fadeRoutine = StartCoroutine(FadeOutAndStop(_activeSource, fadeDuration));
    }

    private bool IsSamePlaylist(List<AudioClip> clips)
    {
        if (clips.Count != _playlist.Count) return false;
        for (int i = 0; i < clips.Count; i++)
            if (clips[i] != _playlist[i]) return false;
        return true;
    }

    private IEnumerator PlaylistLoop(float firstFadeDuration)
    {
        bool first = true;

        while (_playlist.Count > 0)
        {
            AudioClip clip = GetNextClip();
            if (clip == null) yield break;

            float fadeDuration = first ? firstFadeDuration : defaultFadeDuration;
            first = false;

            yield return CrossfadeTo(clip, fadeDuration);

            float waitTime = Mathf.Max(0f, clip.length - defaultFadeDuration);
            yield return new WaitForSeconds(waitTime);
        }
    }

    private AudioClip GetNextClip()
    {
        if (_playlist.Count == 0) return null;

        if (_shuffle)
        {
            if (_playlist.Count == 1)
            {
                _currentIndex = 0;
            }
            else
            {
                int next;
                do { next = Random.Range(0, _playlist.Count); }
                while (next == _currentIndex);
                _currentIndex = next;
            }
        }
        else
        {
            _currentIndex = (_currentIndex + 1) % _playlist.Count;
        }

        return _playlist[_currentIndex];
    }

    private IEnumerator CrossfadeTo(AudioClip clip, float duration)
    {
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);

        AudioSource incoming = _inactiveSource;
        AudioSource outgoing = _activeSource;

        incoming.clip = clip;
        incoming.volume = 0f;
        incoming.Play();

        float targetVolume = 1f;
        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = duration > 0f ? t / duration : 1f;

            incoming.volume = Mathf.Lerp(0f, targetVolume, k);
            outgoing.volume = Mathf.Lerp(targetVolume, 0f, k);

            yield return null;
        }

        incoming.volume = targetVolume;
        outgoing.volume = 0f;
        outgoing.Stop();

        _activeSource = incoming;
    }

    private IEnumerator FadeOutAndStop(AudioSource source, float duration)
    {
        float startVolume = source.volume;
        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            source.volume = Mathf.Lerp(startVolume, 0f, duration > 0f ? t / duration : 1f);
            yield return null;
        }

        source.volume = 0f;
        source.Stop();
    }
}
