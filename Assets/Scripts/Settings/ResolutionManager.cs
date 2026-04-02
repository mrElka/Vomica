using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;  // вместо Task

public class ResolutionManager : MonoBehaviour
{
    public TMP_Dropdown resolutionDropdown;
    public Toggle fullscreenToggle;
    List<Resolution> validResolutions = new List<Resolution>();

    void Start()
    {
        Resolution[] allResolutions = Screen.resolutions;
        resolutionDropdown.ClearOptions();

        List<string> options = new List<string>();
        int currentResolutionIndex = 0;

        for (int i = 0; i < allResolutions.Length; i++)
        {
            if (allResolutions[i].width < 800 || allResolutions[i].height < 600)
                continue;

            validResolutions.Add(allResolutions[i]);
            string option = allResolutions[i].width + " x " + allResolutions[i].height;
            options.Add(option);

            if (allResolutions[i].width == Screen.currentResolution.width &&
                allResolutions[i].height == Screen.currentResolution.height)
            {
                currentResolutionIndex = validResolutions.Count - 1;
            }
        }

        resolutionDropdown.AddOptions(options);
        resolutionDropdown.value = currentResolutionIndex;
        resolutionDropdown.RefreshShownValue();

        resolutionDropdown.onValueChanged.AddListener(SetResolution);

        if (fullscreenToggle != null)
        {
            fullscreenToggle.isOn = Screen.fullScreen;
            fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
        }
    }

    // Используем Coroutine вместо async (работает надёжнее)
    public void SetResolution(int resolutionIndex)
    {
        if (resolutionIndex < 0 || resolutionIndex >= validResolutions.Count) return;
        StartCoroutine(ApplyResolution(resolutionIndex));
    }

    private IEnumerator ApplyResolution(int resolutionIndex)
    {
        Resolution resolution = validResolutions[resolutionIndex];
        bool wasFullscreen = Screen.fullScreen;

        Debug.Log($"Пытаюсь установить: {resolution.width} x {resolution.height}");

        // 1. Выходим из полноэкранного режима
        if (wasFullscreen)
            Screen.fullScreen = false;

        // Ждём 2 кадра
        yield return null;
        yield return null;

        // 2. Устанавливаем разрешение
        Screen.SetResolution(resolution.width, resolution.height, false);

        // Ждём ещё кадр
        yield return null;

        // 3. Возвращаем полноэкранный режим
        if (wasFullscreen)
        {
            Screen.fullScreen = true;
        }

        yield return null;
        Debug.Log($"После применения: {Screen.width} x {Screen.height}");
    }

    public void SetFullscreen(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
    }
}