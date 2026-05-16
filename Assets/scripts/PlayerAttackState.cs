using UnityEngine;

public class PlayerAttackState : PlayerState
{
    public PlayerAttackState(player player) : base(player) { }

    private float enterTime;

    public override void Enter()
    {
        if (player.stamina != null && !player.stamina.TrySpend(player.AttackCost))
        {
            TransitionOut();
            return;
        }

        enterTime = Time.time;

        // Start attack animation (parameter name is inspector-configured on the Player).
        if (player.anim != null)
        {
            if (!string.IsNullOrWhiteSpace(player.attackBoolParam))
            {
                player.HoldAnimatorBool(player.attackBoolParam, player.attackAnimHoldSeconds);
            }
        }

        // Stop horizontal velocity but preserve vertical.
        RB.linearVelocity = new Vector2(0f, RB.linearVelocity.y);

        // Consume the attack input so it won't re-trigger.
        player.attackPressed = false;

        // Start cooldown at the beginning of the attack.
        if (player.combat != null)
        {
            player.combat.BeginAttack();
        }
    }

    public override void Update()
    {
        // If the animation event (Combat.AttackAnimationFinished -> player.OnAttackAnimationFinished) isn't wired,
        // fall back to ending the state once the attack cooldown has elapsed. This prevents "stuck" controls.
        if (Time.time - enterTime < 0.05f)
            return;

        if (player.combat == null || player.combat.CanAttack)
        {
            OnAttackAnimationFinished();
        }
    }

    public override void Exit()
    {
        // Animator bool is cleared by the timed hold in player.HoldAnimatorBool to ensure the clip can play fully.
    }

    public override void FixedUpdate()
    {
        // Keep gravity behavior consistent while attacking.
        UpdateJumpTimers();
        ApplyIdleHorizontalVelocity();
        ApplyVariableGravity();
        ClampVerticalSpeed();
    }

    public void OnAttackAnimationFinished()
    {
        // Attack > Jump > Move > Idle.
        // Jump is only applied if it's already buffered (fits existing controller logic).
        if (JumpBufferTimer > 0f && CoyoteTimer > 0f)
        {
            // Jump state will apply the impulse on Enter (as in the current controller).
            player.ChangeState(player.jumpState);
            return;
        }

        if (Mathf.Abs(MoveInput.x) > 0.1f)
        {
            player.ChangeState(player.moveState);
        }
        else
        {
            player.ChangeState(player.idleState);
        }
    }

    private void TransitionOut()
    {
        // If jump is buffered and allowed, prefer jumping.
        if (JumpBufferTimer > 0f && CoyoteTimer > 0f)
        {
            player.ChangeState(player.jumpState);
            return;
        }

        if (!IsGrounded)
        {
            player.ChangeState(player.jumpState);
            return;
        }

        if (Mathf.Abs(MoveInput.x) > 0.1f)
        {
            player.ChangeState(player.moveState);
        }
        else
        {
            player.ChangeState(player.idleState);
        }
    }
}
