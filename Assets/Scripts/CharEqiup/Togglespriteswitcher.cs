using UnityEngine;
using UnityEngine.UI;

namespace CharacterEquipment
{
    [RequireComponent(typeof(Toggle))]
    public class ToggleSpriteSwitcher : MonoBehaviour
    {
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Sprite activeSprite;
        [SerializeField] private Sprite inactiveSprite;

        private Toggle toggle;

        private void Awake()
        {
            toggle = GetComponent<Toggle>();
            if (backgroundImage == null)
                backgroundImage = toggle.targetGraphic as Image;
        }

        private void OnEnable()
        {
            if (toggle == null) toggle = GetComponent<Toggle>();
            toggle.onValueChanged.RemoveListener(SetVisual);
            toggle.onValueChanged.AddListener(SetVisual);
            SetVisual(toggle.isOn);
        }

        private void OnDisable()
        {
            if (toggle != null)
                toggle.onValueChanged.RemoveListener(SetVisual);
        }

        public void Refresh()
        {
            if (toggle == null) toggle = GetComponent<Toggle>();
            SetVisual(toggle.isOn);
        }

        private void SetVisual(bool isOn)
        {
            if (backgroundImage == null) return;
            backgroundImage.sprite = isOn ? activeSprite : inactiveSprite;
        }
    }
}