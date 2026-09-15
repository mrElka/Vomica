using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    [Header("Настройки сцен")]
    [Tooltip("Название сцены для кнопки TestLoc (должна быть добавлена в Build Settings)")]
    [SerializeField] private string testLocSceneName;

    [Header("UI загрузки")]
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private Slider progressBar;
    [SerializeField] private GameObject mainMenuPanel; // чтобы скрыть кнопки

    public void PlayGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
    }

    public void NewGame()
    {
        Debug.Log("Начать новую игру");
    }

    public void TestLoc()
    {
        if (string.IsNullOrEmpty(testLocSceneName))
        {
            Debug.LogWarning("TestLoc: имя сцены не задано в инспекторе!");
            return;
        }

        StartCoroutine(LoadSceneAsync(testLocSceneName));
    }

    public void Continue()
    {
        Debug.Log("Продолжить игру");
    }

    public void Settings()
    {
        Debug.Log("Открыть настройки");
    }

    public void ExitGame()
    {
        Debug.Log("Выход");
        Application.Quit();
    }


    private IEnumerator LoadSceneAsync(string sceneName)
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (loadingPanel != null) loadingPanel.SetActive(true);

        if (progressBar != null)
        {
            progressBar.minValue = 0f;
            progressBar.maxValue = 1f;
            progressBar.value = 0f;
        }

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);

        // Не переключать сцену автоматически — ждём, пока UI покажет 100%
        op.allowSceneActivation = false;

        while (!op.isDone)
        {
            // progress доходит до 0.9, потом ждёт allowSceneActivation
            float progress = Mathf.Clamp01(op.progress / 0.9f);

            if (progressBar != null)
                progressBar.value = progress;

            if (progress >= 1f)
            {
                // Можно добавить небольшую паузу для красоты
                yield return new WaitForSeconds(0.2f);
                op.allowSceneActivation = true;
            }

            yield return null;
        }
    }

}