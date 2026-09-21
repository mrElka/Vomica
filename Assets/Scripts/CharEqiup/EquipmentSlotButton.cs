using System;
using UnityEngine;
using UnityEngine.UI;

namespace CharacterEquipment
{
    public class EquipmentSlotButton : MonoBehaviour
    {
        [SerializeField] private Toggle toggle;
        [SerializeField] private Image icon;
        [SerializeField] private Text label;

        private Action onSelected;
        private ToggleSpriteSwitcher switcher;

        private void Awake()
        {
            if (toggle == null) toggle = GetComponent<Toggle>();
            switcher = GetComponent<ToggleSpriteSwitcher>();
        }

        public void Setup(EquipmentItem item, ToggleGroup group, Action onSelected)
        {
            this.onSelected = onSelected;

            toggle.group = group;

            // ВАЖНО: не RemoveAllListeners, а только свой слушатель
            toggle.onValueChanged.RemoveListener(OnToggleChanged);
            toggle.onValueChanged.AddListener(OnToggleChanged);

            toggle.isOn = false;

            if (icon != null && item.icon != null) icon.sprite = item.icon;
            if (label != null) label.text = item.displayName;

            // Принудительно обновить визуал
            if (switcher != null) switcher.Refresh();
        }

        private void OnToggleChanged(bool isOn)
        {
            if (isOn) onSelected?.Invoke();
        }
    }
}