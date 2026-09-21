using UnityEngine;

namespace CharacterEquipment
{
    public class AttachmentPoint : MonoBehaviour
    {
        public AttachmentSlotType slotType;

        [Tooltip("Заполнять только для точек под оружие (slotTyuupe = Weapon)" +
            "None - точка принимает любую категория оружия (например одна точка в руке под всё")]
        public WeaponSubCategory weaponSubCategory = WeaponSubCategory.None;

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = slotType switch
            {
                AttachmentSlotType.Helmet => Color.cyan,
                AttachmentSlotType.Armor => Color.green,
                AttachmentSlotType.Weapon => Color.red,
                AttachmentSlotType.Shield => Color.yellow,
                _ => Color.white
            };
            Gizmos.DrawSphere(transform.position, 0.03f);
        }
#endif
    }
}
