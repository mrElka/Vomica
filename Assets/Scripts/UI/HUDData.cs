using UnityEngine;

[CreateAssetMenu(menuName = "HUD/HUDData", fileName = "HUDData")]
public class HUDData : ScriptableObject
{
    [Header("Visual stats")]
    [Range(0f, 1f)] public float healthPercent = 1f;
    [Range(0f, 1f)] public float staminaPercent = 1f;
    [Range(0f, 1f)] public float shieldPercent = 1f;

    [Header("Defense (player)")]
    [Tooltip("“екущее снижение урона телом как дол€ 0..1 (например, 20% = 0.2)")]
    [Range(0f, 1f)] public float armorPercent = 0f;
    [Tooltip("“екущее снижение урона шлемом как дол€ 0..1")]
    [Range(0f, 1f)] public float helmetPercent = 0f;

    [Header("Ammo")]
    public int ammoCurrent = 30;
    public int ammoMax = 30;

    [Header("Score/Timer")]
    public int score = 0;
    public float timerSeconds = 0f;

    [Header("State Flags")]
    public bool isLowHealth = false;
    public bool isDamaged = false;
    public bool hasNewItem = false;

    public string AmmoText => $"{ammoCurrent} / {ammoMax}";

    public string TimerText
    {
        get
        {
            int m = Mathf.FloorToInt(timerSeconds / 60);
            int s = Mathf.FloorToInt(timerSeconds % 60);
            return $"{m}:{s:D2}";
        }
    }
}