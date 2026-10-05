using UnityEngine;

namespace CharacterEquipment
{
    /// <summary>
    /// Вешается на пустой объект который является дочерним для кости персонажа
    /// (кисть, голова, предплечье и тд)
    /// </summary>
    public class AttachmentPoint : MonoBehaviour
    {
        public AttachmentPointId id;

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = id switch
            {
                AttachmentPointId.Head => Color.cyan,
                AttachmentPointId.Body => Color.green,
                AttachmentPointId.LeftHand or AttachmentPointId.RightHand => Color.red,
                AttachmentPointId.LeftArm or AttachmentPointId.RightArm => Color.yellow,
                AttachmentPointId.LeftLeg or AttachmentPointId.RightLeg => Color.magenta,
                _ => Color.white
            };
            Gizmos.DrawSphere(transform.position, 0.03f);
        }
#endif
    }
}