using CharacterEquipment;
using TMPro;
using UnityEngine;

/// <summary>
/// Пользовательский HUD. Сам ничего не считает: каждый кадр спрашивает у игрока
/// здоровье, выносливость и ближайший предмет и отдаёт их во вью.
///
/// Игрока находит через PlayerHealth.Instance, поэтому префаб интерфейса не нужно
/// связывать с префабом игрока — достаточно положить его в сцену.
/// Повесить на корневой Canvas HUD.
/// </summary>
public class GameHUD : MonoBehaviour
{
    [Header("Полоски")]
    [SerializeField] private HudBar healthBar;

    [Tooltip("Необязательно")]
    [SerializeField] private HudBar staminaBar;

    [Header("Подсказка подбора")]
    [Tooltip("Объект подсказки целиком (включается и выключается)")]
    [SerializeField] private GameObject promptRoot;
    [SerializeField] private TMP_Text promptText;

    [Tooltip("{0} — название предмета")]
    [SerializeField] private string promptFormat = "[E] Подобрать {0}";

    [Tooltip("Вторая строка, если взятый щит вытеснит двуручное оружие. Пусто — не показывать")]
    [SerializeField] private string twoHandedWarning = "двуручное оружие упадёт на землю";

    private PlayerHealth boundPlayer;
    private PlayerStamina stamina;
    private PlayerGear gear;

    private EquipmentPickup shownPickup;
    private bool shownWarning;

    private void Start()
    {
        SetPromptVisible(false);
    }

    private void Update()
    {
        PlayerHealth player = PlayerHealth.Instance;

        if (player == null)
        {
            boundPlayer = null;
            SetPromptVisible(false);
            return;
        }

        if (player != boundPlayer)
            Bind(player);

        if (healthBar != null)
            healthBar.SetValue(player.CurrentHealth, player.MaxHealth);

        if (staminaBar != null && stamina != null)
            staminaBar.SetValue(stamina.CurrentStamina, stamina.MaxStamina);

        UpdatePrompt(player);
    }

    private void Bind(PlayerHealth player)
    {
        boundPlayer = player;
        stamina = player.GetComponent<PlayerStamina>();
        gear = player.GetComponent<PlayerGear>();
        shownPickup = null;
    }

    private void UpdatePrompt(PlayerHealth player)
    {
        EquipmentPickup pickup = gear != null ? gear.NearestPickup : null;

        if (pickup == null || player.IsDead || PauseMenu.IsPaused)
        {
            shownPickup = null;
            SetPromptVisible(false);
            return;
        }

        bool warning = !string.IsNullOrEmpty(twoHandedWarning)
                       && pickup.Item != null
                       && pickup.Item.IsOffHand
                       && gear.HandsFull;

        // Текст пересобираем, только когда сменился предмет или предупреждение
        if (pickup != shownPickup || warning != shownWarning)
        {
            shownPickup = pickup;
            shownWarning = warning;

            if (promptText != null)
            {
                string text = string.Format(promptFormat, pickup.DisplayName);
                if (warning)
                    text += $"\n<size=70%><color=#FFB84D>{twoHandedWarning}</color></size>";

                promptText.text = text;
            }
        }

        SetPromptVisible(true);
    }

    private void SetPromptVisible(bool visible)
    {
        if (promptRoot != null && promptRoot.activeSelf != visible)
            promptRoot.SetActive(visible);
    }
}
