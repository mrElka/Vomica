using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace CharacterEquipment
{
    /// <summary>
    /// Нижняя часть бежевой подложки: слева название + описание,
    /// справа список статов выбранного предмета в виде полосок.
    /// </summary>
    public class EquipmentDetailsPanel : MonoBehaviour
    {
        [Header("Левая часть")]
        [SerializeField] private TextMeshProUGUI nameLabel;
        [SerializeField] private TextMeshProUGUI descriptionLabel;

        [Header("Правая часть (статы)")]
        [SerializeField] private Transform statsContainer;
        [SerializeField] private EquipmentStatRow statRowPrefab;

        private readonly List<EquipmentStatRow> spawnedRows = new List<EquipmentStatRow>();

        private void Awake()
        {
            Clear();
        }

        public void Show(EquipmentItem item)
        {
            if (item == null)
            {
                Clear();
                return;
            }

            gameObject.SetActive(true);

            if (nameLabel != null) nameLabel.text = item.displayName;
            if (descriptionLabel != null) descriptionLabel.text = item.description;

            foreach (var row in spawnedRows)
            {
                if (row != null) Destroy(row.gameObject);
            }
            spawnedRows.Clear();

            foreach (var stat in item.stats)
            {
                var row = Instantiate(statRowPrefab, statsContainer);
                row.Setup(stat);
                spawnedRows.Add(row);
            }
        }

        /// <summary>Спрятать панель, когда ни один предмет не выбран.</summary>
        public void Clear()
        {
            if (nameLabel != null) nameLabel.text = string.Empty;
            if (descriptionLabel != null) descriptionLabel.text = string.Empty;

            foreach (var row in spawnedRows)
            {
                if (row != null) Destroy(row.gameObject);
            }
            spawnedRows.Clear();
        }
    }
}