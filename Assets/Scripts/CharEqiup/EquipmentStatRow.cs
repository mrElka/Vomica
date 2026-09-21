using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CharacterEquipment
{
    /// <summary>
    /// Одна строка вида "Характеристика — [прогрессбар]" в панели информации о предмете.
    /// Image полоски должен быть типа Filled (Fill Method: Horizontal).
    /// </summary>
    public class EquipmentStatRow : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI labelText;
        [SerializeField] private Image fillImage;

        public void Setup(EquipmentStat stat)
        {
            if (labelText != null) labelText.text = stat.label;
            if (fillImage != null) fillImage.fillAmount = stat.NormalizedValue;
        }
    }
}