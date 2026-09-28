using UnityEngine;

public enum HitboxPart
{
    Head,
    Torso,
    Arm,
    Leg,
    Generic
}

public class Hitbox : MonoBehaviour
{
    [Header("Hitbox")]
    [Tooltip("Часть тела, к которой привязан этот хитбокс")]
    [SerializeField] private HitboxPart _part = HitboxPart.Generic;

    [Tooltip("Множитель урона по этой части. Голова = 2, тело = 1, рука = 0.7, нога = 0.6")]
    [SerializeField] private float _damageMultiplier = 1f;

    [Tooltip("Корень персонажа. Если пусто — берётся transform.root.")]
    [SerializeField] private Transform _owner;

    public HitboxPart Part => _part;
    public float DamageMultiplier => _damageMultiplier;
    public Transform Owner => _owner != null ? _owner : transform.root;

    /// <summary>
    /// Автоподстановка владельца при добавлении компонента в редакторе.
    /// </summary>
    private void Reset()
    {
        _owner = transform.root;
    }

    /// <summary>
    /// Цветной кружок в Scene при выделении кости — для отладки расположения.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = _part switch
        {
            HitboxPart.Head => Color.red,
            HitboxPart.Torso => Color.yellow,
            HitboxPart.Arm => Color.cyan,
            HitboxPart.Leg => Color.green,
            _ => Color.magenta
        };

        SphereCollider sphere = GetComponent<SphereCollider>();
        if (sphere != null)
        {
            Gizmos.DrawWireSphere(
                transform.TransformPoint(sphere.center),
                sphere.radius * transform.lossyScale.x
            );
        }
    }
}