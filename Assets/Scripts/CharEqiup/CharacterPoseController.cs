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

            if (animator == null)
            {
                Debug.LogWarning("[Pose] Animator не найден на персонаже, позы не будут переключаться", this);
                return;
            }

            rightLayer = animator.GetLayerIndex(rightLayerName);
            leftLayer = animator.GetLayerIndex(leftLayerName);
            twoHandLayer = animator.GetLayerIndex(twoHandLayerName);

            Debug.Log($"[Pose] Awake animator='{animator.name}' controller='{animator.runtimeAnimatorController?.name ?? "NULL"}' layers: Right={rightLayer} Left={leftLayer} TwoHand={twoHandLayer} | hashes R={RightPoseHash} L={LeftPoseHash} T={TwoHandPoseHash}");
            if (rightLayer < 0) Debug.LogWarning($"[Pose] Слой '{rightLayerName}' не найден в контроллере", this);
            if (leftLayer < 0) Debug.LogWarning($"[Pose] Слой '{leftLayerName}' не найден", this);
            if (twoHandLayer < 0) Debug.LogWarning($"[Pose] Слой '{twoHandLayerName}' не найден", this);
        }

        private void OnEnable()
        {
            if (equipment != null) equipment.OnHandsChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (equipment != null) equipment.OnHandsChanged -= Refresh;
        }

        private void Refresh()
        {
            if (animator == null || equipment == null) return;

            var right = equipment.GetEquipped(EquipmentSlot.RightHand);
            var left = equipment.GetEquipped(EquipmentSlot.LeftHand);

            // Двуручное лежит в основной руке, вторая в этот момент пуста
            var twoHanded = right != null && right.IsTwoHanded ? right
                          : left != null && left.IsTwoHanded ? left
                          : null;

            if (twoHanded != null)
            {
                SafeSetInt(TwoHandPoseHash, (int)twoHanded.holdPose);
                SafeSetInt(RightPoseHash, 0);
                SafeSetInt(LeftPoseHash, 0);
                rightTarget = leftTarget = 0f;
                twoHandTarget = 1f;
                Debug.Log($"[Pose] TwoHanded '{twoHanded.displayName}' pose={twoHanded.holdPose} ({(int)twoHanded.holdPose}) -> TwoHandLayer=1, Right/Left=0");
            }
            else
            {
                SafeSetInt(TwoHandPoseHash, 0);
                int rp = right != null ? (int)right.holdPose : 0;
                int lp = left != null ? (int)left.holdPose : 0;
                SafeSetInt(RightPoseHash, rp);
                SafeSetInt(LeftPoseHash, lp);
                rightTarget = right != null ? 1f : 0f;
                leftTarget = left != null ? 1f : 0f;
                twoHandTarget = 0f;
                Debug.Log($"[Pose] Right='{right?.displayName ?? "пусто"}' pose={right?.holdPose ?? HoldPose.None} ({rp}) | Left='{left?.displayName ?? "пусто"}' pose={left?.holdPose ?? HoldPose.None} ({lp}) -> weights R={rightTarget} L={leftTarget} T={twoHandTarget}");
            }
        }

        private void SafeSetInt(int hash, int value)
        {
            if (animator == null) return;
            // Animator.StringToHash не бросает если параметра нет, но SetInteger молча игнорирует — логируем что параметр существует
            // Проверяем через parameters массив для диагностики
            bool found = false;
            foreach (var p in animator.parameters)
                if (p.nameHash == hash) { found = true; break; }
            if (!found)
            {
                Debug.LogWarning($"[Pose] Параметр hash={hash} не найден в контроллере '{animator.runtimeAnimatorController?.name}'", this);
                return;
            }
            animator.SetInteger(hash, value);
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
