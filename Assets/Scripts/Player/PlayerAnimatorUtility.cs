using UnityEngine;

/// <summary>
/// На игроке часто два Animator: пустой на корне (капсула) и рабочий на меше с Avatar.
/// Берём тот, у кого есть Avatar, и при необходимости переносим на него контроллер с корня.
/// </summary>
public static class PlayerAnimatorUtility
{
    public static Animator Resolve(Transform root, RuntimeAnimatorController preferredController = null)
    {
        if (root == null) return null;

        Animator onRoot = root.GetComponent<Animator>();
        Animator withAvatar = null;

        foreach (Animator animator in root.GetComponentsInChildren<Animator>(true))
        {
            if (animator.avatar == null) continue;

            withAvatar = animator;
            break;
        }

        if (withAvatar != null)
        {
            if (onRoot != null && onRoot != withAvatar && onRoot.avatar == null)
            {
                if (withAvatar.runtimeAnimatorController == null &&
                    onRoot.runtimeAnimatorController != null)
                {
                    withAvatar.runtimeAnimatorController = onRoot.runtimeAnimatorController;
                }

                onRoot.enabled = false;
            }

            if (preferredController != null &&
                withAvatar.runtimeAnimatorController != preferredController)
            {
                withAvatar.runtimeAnimatorController = preferredController;
            }

            return withAvatar;
        }

        if (onRoot != null)
        {
            if (preferredController != null && onRoot.runtimeAnimatorController == null)
                onRoot.runtimeAnimatorController = preferredController;

            return onRoot;
        }

        return root.GetComponentInChildren<Animator>(true);
    }

    /// <summary>Корень визуальной модели (hero_*), если есть.</summary>
    public static Transform FindVisualRoot(Transform playerRoot)
    {
        if (playerRoot == null) return null;

        foreach (Transform child in playerRoot.GetComponentsInChildren<Transform>(true))
        {
            if (child == playerRoot) continue;

            string n = child.name;
            if (n.StartsWith("hero_", System.StringComparison.OrdinalIgnoreCase) ||
                n.Contains("hero_completed", System.StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }
        }

        return null;
    }
}
