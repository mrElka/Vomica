using UnityEngine;
using UnityEngine.UI;

public class SimpleTabController : MonoBehaviour
{
    public Button graphicsButton;
    public Button soundButton;
    public Button controlsButton;

    public GameObject graphicsPanel;
    public GameObject soundPanel;
    public GameObject controlsPanel;

    void Start()
    {
        graphicsButton.onClick.AddListener(() => SwitchToTab(graphicsButton, graphicsPanel));
        soundButton.onClick.AddListener(() => SwitchToTab(soundButton, soundPanel));
        controlsButton.onClick.AddListener(() => SwitchToTab(controlsButton, controlsPanel));

        SwitchToTab(graphicsButton, graphicsPanel);
    }

    void SwitchToTab(Button activeButton, GameObject activePanel)
    {
        // Отключаем все панели
        graphicsPanel.SetActive(false);
        soundPanel.SetActive(false);
        controlsPanel.SetActive(false);

        // Включаем нужную панель
        activePanel.SetActive(true);

        // Отключаем интерактивность у всех кнопок
        graphicsButton.interactable = true;
        soundButton.interactable = true;
        controlsButton.interactable = true;

        // Делаем активную кнопку неинтерактивной
        activeButton.interactable = false;
    }
}