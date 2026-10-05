using UnityEngine;

namespace CharacterEquipment
{
    /// <summary>
    /// Переключает позы в Animator в зависимости от того, что в руках.
    ///
    /// Настройка Animator Controller:
    ///   Параметры:
    ///     int  RightPose      (значения из HoldPose)
    ///     int  LeftPose
    ///     int  TwoHandPose
    ///   Слои (сверху вниз):
    ///     0 Base           — idle всего тела
    ///     1 RightHandLayer — AvatarMask: правая рука; состояния-позы переходят по RightPose
    ///     2 LeftHandLayer  — AvatarMask: левая рука;  по LeftPose
    ///     3 TwoHandLayer   — AvatarMask: обе руки;    по TwoHandPose
    ///   В каждом слое состояние "None" (значение 0) — пустое, а по значению HoldPose — анимация позы.
    ///   Важно: у состояний-поз ставьте Write Defaults = on/off одинаково во всех слоях.
    /// </summary>
    [RequireComponent(typeof(CharacterEquipment))]
    public class CharacterPoseController : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private string rightLayerName = "RightHandLayer";
        [SerializeField] private string leftLayerName = "LeftHandLayer";
        [SerializeField] private string twoHandLayerName = "TwoHandLayer";
        [SerializeField, Min(0f)] private float layerBlendSpeed = 8f;

        private static readonly int RightPoseHash = Animator.StringToHash("RightPose");
        private static readonly int LeftPoseHash = Animator.StringToHash("LeftPose");
        private static readonly int TwoHandPoseHash = Animator.StringToHash("TwoHandPose");

        private CharacterEquipment equipment;
        private int rightLayer, leftLayer, twoHandLayer;
        private float rightTarget, leftTarget, twoHandTarget;

        private void Awake()
        {
            equipment = GetComponent<CharacterEquipment>();
            if (animator == null) animator = GetComponentInChildren<Animator>();

            rightLayer = animator.GetLayerIndex(rightLayerName);
            leftLayer = animator.GetLayerIndex(leftLayerName);
            twoHandLayer = animator.GetLayerIndex(twoHandLayerName);
        }

        private void OnEnable()
        {
            equipment.OnHandsChanged += Refresh;
            Refresh();
        }

        private void OnDisable() => equipment.OnHandsChanged -= Refresh;

        private void Refresh()
        {
            var right = equipment.GetEquipped(EquipmentSlot.RightHand);
            var left = equipment.GetEquipped(EquipmentSlot.LeftHand);

            // Двуручное лежит в основной руке, вторая в этот момент пуста
            var twoHanded = right != null && right.IsTwoHanded ? right
                          : left != null && left.IsTwoHanded ? left
                          : null;

            if (twoHanded != null)
            {
                animator.SetInteger(TwoHandPoseHash, (int)twoHanded.holdPose);
                animator.SetInteger(RightPoseHash, 0);
                animator.SetInteger(LeftPoseHash, 0);
                rightTarget = leftTarget = 0f;
                twoHandTarget = 1f;
            }
            else
            {
                animator.SetInteger(TwoHandPoseHash, 0);
                animator.SetInteger(RightPoseHash, right != null ? (int)right.holdPose : 0);
                animator.SetInteger(LeftPoseHash, left != null ? (int)left.holdPose : 0);
                rightTarget = right != null ? 1f : 0f;
                leftTarget = left != null ? 1f : 0f;
                twoHandTarget = 0f;
            }
        }

        private void Update()
        {
            float t = layerBlendSpeed * Time.deltaTime;
            Blend(rightLayer, rightTarget, t);
            Blend(leftLayer, leftTarget, t);
            Blend(twoHandLayer, twoHandTarget, t);
        }

        private void Blend(int layer, float target, float t)
        {
            if (layer < 0) return;
            animator.SetLayerWeight(layer, Mathf.MoveTowards(animator.GetLayerWeight(layer), target, t));
        }
    }
}
