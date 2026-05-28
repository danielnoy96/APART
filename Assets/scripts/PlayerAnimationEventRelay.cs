using UnityEngine;

public class PlayerAnimationEventRelay : MonoBehaviour
{
    [SerializeField] private player player;
    [SerializeField] private Combat combat;

    private void Awake()
    {
        ResolveReferences();
    }

    public void AttackHit()
    {
        ResolveReferences();

        if (combat == null)
        {
            Debug.LogWarning("PlayerAnimationEventRelay.AttackHit: Combat is not assigned.", this);
            return;
        }

        combat.PerformHitCheck();
    }

    public void AttackFinished()
    {
        ResolveReferences();

        if (player == null)
        {
            Debug.LogWarning("PlayerAnimationEventRelay.AttackFinished: player is not assigned.", this);
            return;
        }

        player.OnAttackAnimationFinished();
    }

    private void ResolveReferences()
    {
        if (player == null)
        {
            player = GetComponentInParent<player>();
        }

        if (combat == null)
        {
            combat = GetComponentInParent<Combat>();
            if (combat == null && player != null)
            {
                combat = player.combat;
            }
        }
    }
}
