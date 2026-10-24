using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Вешается прямо на кнопку. Сама подписывается на Button.onClick,
/// вручную в OnClick ничего добавлять не нужно.
/// Загружает сцену по имени из инспектора (сцена должна быть в Build Settings).
/// Экран загрузки необязательный: панель поверх всего UI + слайдер прогресса.
/// </summary>
[RequireComponent(typeof(Button))]
public class SceneTransitionButton : MonoBehaviour
{
    [Header("Куда переходим")]
    [Tooltip("Точное имя сцены (должна быть в File > Build Settings > Scenes In Build)")]
    [SerializeField] private string targetSceneName = "TestingMoving";

    [Header("Экран загрузки (необязательно)")]
    [Tooltip("Полноэкранная панель, лежит на Canvas ПОСЛЕДНЕЙ (поверх всего), по умолчанию выключена")]
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private Slider progressBar;

    private Button _button;
    private bool _isLoading;

    private void Awake()
    {
        _button = GetComponent<Button>();
        _button.onClick.AddListener(OnClick);
    }

    private void OnDestroy()
    {
        if (_button != null) _button.onClick.RemoveListener(OnClick);
    }

    private void OnClick()
    {
        if (_isLoading) return;

        if (string.IsNullOrEmpty(targetSceneName))
        {
            Debug.LogWarning("SceneTransitionButton: имя сцены не задано в инспекторе!", this);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(targetSceneName))
        {
            Debug.LogError($"SceneTransitionButton: сцена '{targetSceneName}' не найдена. " +
                           "Проверь имя и Build Settings.", this);
            return;
        }

        StartCoroutine(LoadSceneAsync(targetSceneName));
    }

    private IEnumerator LoadSceneAsync(string sceneName)
    {
        _isLoading = true;
        _button.interactable = false;
        Time.timeScale = 1f;

        if (loadingPanel != null) loadingPanel.SetActive(true);

        if (progressBar != null)
        {
            progressBar.minValue = 0f;
            progressBar.maxValue = 1f;
            progressBar.value = 0f;
        }

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = false;

        while (!op.isDone)
        {
            float progress = Mathf.Clamp01(op.progress / 0.9f);
            if (progressBar != null) progressBar.value = progress;

            if (progress >= 1f)
            {
                yield return new WaitForSeconds(0.2f);
                op.allowSceneActivation = true;
            }

            yield return null;
        }
    }
}