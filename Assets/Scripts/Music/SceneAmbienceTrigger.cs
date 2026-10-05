using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// этот скрипт уже нужно кидать на что-то на сцене, тот же сценменеджер хз
/// при запуске сцены передаёт свой плейлист в постоянный AmbiencePlayer
/// который плавно переключится на него с предыдущей сцены
/// </summary>
public class SceneAmbienceTrigger : MonoBehaviour
{
    [SerializeField] private List<AudioClip> playlist;
    [SerializeField] private bool shuffle = true;
    [SerializeField] private float fadeDuration = 2f;

    [Tooltip("Если true — этот плейлист запустится сразу при загрузке сцены")]
    [SerializeField] private bool playOnStart = true;

    private void Start()
    {
        if (playOnStart)
            Play();
    }

    /// <summary>
    /// можно вызвать вручную
    /// например при входе игрока в новую зону или локацию
    /// </summary>
    public void Play()
    {
        if (AmbiencePlayer.Instance == null)
        {
            Debug.LogWarning("AmbiencePlayer не найден на сцене. Добавьте его один раз на стартовую сцену.");
            return;
        }

        AmbiencePlayer.Instance.SetPlaylist(playlist, shuffle, fadeDuration);
    }
}
