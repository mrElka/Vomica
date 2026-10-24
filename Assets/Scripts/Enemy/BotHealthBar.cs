using UnityEngine;

/// <summary>
/// Связывает BotHealth и визуальную полоску HealthBar.
/// Вешается на тот же объект, где создаётся бар.
/// </summary>
[RequireComponent(typeof(HealthBar))]
public class BotHealthBar : MonoBehaviour
{
    private BotHealth _botHealth;
    private HealthBar _healthBar;

    private void Awake()
    {
        _healthBar = GetComponent<HealthBar>();
    }

    private void Start()
    {
        // Ищем здоровье бота в родителях (так как бар обычно дочерний объект)
        _botHealth = GetComponentInParent<BotHealth>();

        if (_botHealth == null)
        {
            Debug.LogError($"[BotHealthBar] Не найден BotHealth в родителях объекта {name}");
            return;
        }

        // Подписываемся на событие изменения здоровья
        _botHealth.OnHealthChanged += OnHealthChanged;

        // Инициализируем полоску текущими значениями
        // HealthBar использует int в методах, поэтому приводим float к int
        _healthBar.SetMaxHealth(Mathf.RoundToInt(_botHealth.MaxHealth));
        _healthBar.SetHealth(Mathf.RoundToInt(_botHealth.CurrentHealth));
    }

    private void OnDestroy()
    {
        if (_botHealth != null)
            _botHealth.OnHealthChanged -= OnHealthChanged;
    }

    private void OnHealthChanged(float currentHealth, float maxHealth)
    {
        // Обновляем только текущее значение, чтобы не сбрасывать maxValue каждый раз
        _healthBar.SetHealth(Mathf.RoundToInt(currentHealth));
    }
}
