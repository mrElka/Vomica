using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Меню паузы: подложка со страницами «главная / настройки / выход».
/// Кнопка на главной странице открывает свою страницу, и сбоку появляется кнопка «Назад».
///
/// Esc: открывает паузу; на подстранице возвращает на главную; на главной закрывает паузу.
///
/// ВАЖНО: этот скрипт вешается на объект, который всегда активен (Canvas паузы).
/// Root — дочерний объект, он выключен, пока паузы нет.
/// Остановка ввода игрока делается через PauseMenu.IsPaused в InputHandler и CameraSwitcher.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    /// <summary>Идёт ли пауза. Читается из InputHandler и CameraSwitcher.</summary>
    public static bool IsPaused { get; private set; }

    [Header("Корень меню (выключен, пока паузы нет)")]
    [SerializeField] private GameObject root;

    [Header("Страницы")]
    [SerializeField] private GameObject mainPage;
    [SerializeField] private GameObject settingsPage;
    [SerializeField] private GameObject exitPage;

    [Header("Главная страница")]
    [SerializeField] private Button continueButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button exitButton;

    [Header("Боковая кнопка (видна на подстраницах)")]
    [SerializeField] private Button backButton;

    [Header("Страница выхода")]
    [SerializeField] private Button toMenuButton;
    [SerializeField] private Button quitButton;

    [Tooltip("Сцена главного меню. Должна быть добавлена в Build Settings. " +
             "Лучше то же имя, что в PlayerHealth → Menu Scene Name")]
    [SerializeField] private string menuSceneName = "SampleScene";

    [Header("Страница настроек")]
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private TMP_Text volumeValue;
    [SerializeField] private Slider sensitivitySlider;
    [SerializeField] private TMP_Text sensitivityValue;
    [SerializeField] private Toggle fullscreenToggle;

    private GameObject currentPage;
    private bool resuming;
    private readonly List<CinemachineInputAxisController> frozenCameraInput =
        new List<CinemachineInputAxisController>();

    // ================= ЖИЗНЕННЫЙ ЦИКЛ =================

    private void Awake()
    {
        IsPaused = false;

        if (root == null)
        {
            Debug.LogError("[PauseMenu] Не назначен Root", this);
            enabled = false;
            return;
        }

        root.SetActive(false);

        Bind(continueButton, Resume);
        Bind(settingsButton, () => ShowPage(settingsPage));
        Bind(exitButton, () => ShowPage(exitPage));
        Bind(backButton, () => ShowPage(mainPage));
        Bind(toMenuButton, GoToMenu);
        Bind(quitButton, QuitGame);

        SetupSettingsControls();
    }

    private void OnDestroy()
    {
        if (IsPaused)
            ForceUnpause();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            OnEscape();
    }

    private void LateUpdate()
    {
        // PlayerController снова запирает курсор при возврате фокуса окна — на паузе не даём
        if (!IsPaused) return;

        if (Cursor.lockState != CursorLockMode.None)
            Cursor.lockState = CursorLockMode.None;

        if (!Cursor.visible)
            Cursor.visible = true;
    }

    // ================= ПАУЗА =================

    private void OnEscape()
    {
        if (resuming) return;

        if (!IsPaused)
        {
            PlayerHealth player = PlayerHealth.Instance;
            if (player == null || !player.IsDead)
                Pause();
        }
        else if (currentPage != mainPage)
        {
            ShowPage(mainPage);
        }
        else
        {
            Resume();
        }
    }

    public void Pause()
    {
        if (IsPaused || resuming) return;

        IsPaused = true;
        Time.timeScale = 0f;
        AudioListener.pause = true;

        FreezeCameraInput();

        root.SetActive(true);
        ShowPage(mainPage);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Resume()
    {
        if (!IsPaused || resuming) return;

        GameSettings.Save();
        root.SetActive(false);
        StartCoroutine(ResumeNextFrame());
    }

    /// <summary>
    /// Снимаем паузу на следующий кадр: клик по «Продолжить» не должен
    /// в тот же кадр превратиться в удар или подбор предмета.
    /// </summary>
    private IEnumerator ResumeNextFrame()
    {
        resuming = true;
        yield return null;

        ForceUnpause();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        resuming = false;
    }

    private void ForceUnpause()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        RestoreCameraInput();
    }

    // ================= СТРАНИЦЫ =================

    private void ShowPage(GameObject page)
    {
        if (page == null) return;

        currentPage = page;

        SetPageActive(mainPage, page);
        SetPageActive(settingsPage, page);
        SetPageActive(exitPage, page);

        if (backButton != null)
            backButton.gameObject.SetActive(page != mainPage);
    }

    private static void SetPageActive(GameObject candidate, GameObject shown)
    {
        if (candidate != null)
            candidate.SetActive(candidate == shown);
    }

    // ================= ВЫХОД =================

    private void GoToMenu()
    {
        GameSettings.Save();
        ForceUnpause();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SceneManager.LoadScene(menuSceneName);
    }

    private void QuitGame()
    {
        GameSettings.Save();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ================= НАСТРОЙКИ =================

    private void SetupSettingsControls()
    {
        if (volumeSlider != null)
        {
            volumeSlider.wholeNumbers = false;
            volumeSlider.minValue = 0f;
            volumeSlider.maxValue = 1f;
            volumeSlider.SetValueWithoutNotify(GameSettings.Volume);
            volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        }

        if (sensitivitySlider != null)
        {
            sensitivitySlider.wholeNumbers = false;
            sensitivitySlider.minValue = GameSettings.MinSensitivity;
            sensitivitySlider.maxValue = GameSettings.MaxSensitivity;
            sensitivitySlider.SetValueWithoutNotify(GameSettings.MouseSensitivity);
            sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
        }

        if (fullscreenToggle != null)
        {
            fullscreenToggle.SetIsOnWithoutNotify(GameSettings.Fullscreen);
            fullscreenToggle.onValueChanged.AddListener(GameSettings.SetFullscreen);
        }

        RefreshValueLabels();
    }

    private void OnVolumeChanged(float value)
    {
        GameSettings.SetVolume(value);
        RefreshValueLabels();
    }

    private void OnSensitivityChanged(float value)
    {
        GameSettings.SetMouseSensitivity(value);
        RefreshValueLabels();
    }

    private void RefreshValueLabels()
    {
        if (volumeValue != null)
            volumeValue.text = $"{Mathf.RoundToInt(GameSettings.Volume * 100f)}%";

        if (sensitivityValue != null)
            sensitivityValue.text = $"{GameSettings.MouseSensitivity:0.0}x";
    }

    // ================= КАМЕРА =================

    /// <summary>Если на камерах висит CinemachineInputAxisController, он читает мышь сам — на паузе выключаем.</summary>
    private void FreezeCameraInput()
    {
        frozenCameraInput.Clear();

        foreach (var controller in FindObjectsByType<CinemachineInputAxisController>(FindObjectsSortMode.None))
        {
            if (!controller.enabled) continue;

            controller.enabled = false;
            frozenCameraInput.Add(controller);
        }
    }

    private void RestoreCameraInput()
    {
        foreach (var controller in frozenCameraInput)
        {
            if (controller != null)
                controller.enabled = true;
        }

        frozenCameraInput.Clear();
    }

    private static void Bind(Button button, UnityAction action)
    {
        if (button != null)
            button.onClick.AddListener(action);
    }
}
