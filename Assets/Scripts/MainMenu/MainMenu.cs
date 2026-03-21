using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    public void PlayGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
    }

    public void NewGame()
    {
        Debug.Log("Начать новую игру");
    }

    public void OpenSaves()
    {
        Debug.Log("Открыть сохранения");
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
}
