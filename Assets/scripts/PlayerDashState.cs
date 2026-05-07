using UnityEngine;

public class PlayerDashState : PlayerState
{
    private float dashEndTime;
    private float endLagEndTime;
    private int dashDirection;
    private float previousGravityScale;

    public PlayerDashState(player player) : base(player) { }

    public override void Enter()
    {
        // Cooldown starts on dash attempt (successful).
        if (player.stamina == null || !player.stamina.TrySpend(player.DashCost))
        {
            TransitionOut();
            return;
        }

        player.StartDashCooldown();
        player.dashPressed = false;

        dashDirection = GetDashDirection();
        dashEndTime = Time.time + player.DashDuration;

        if (player.DashEndLag > 0f)
        {
            endLagEndTime = dashEndTime + player.DashEndLag;
        }
        else
        {
            endLagEndTime = dashEndTime;
        }

        if (player.anim != null)
        {
            if (!string.IsNullOrWhiteSpace(player.dashTriggerParam))
            {
                player.anim.SetTrigger(player.dashTriggerParam);
            }
            if (!string.IsNullOrWhiteSpace(player.dashBoolParam))
            {
                player.anim.SetBool(player.dashBoolParam, true);
            }
        }

        previousGravityScale = RB.gravityScale;
        if (player.DashIgnoreGravity)
        {
            RB.gravityScale = 0f;
        }

        ApplyDashVelocity();
    }

    public override void Exit()
    {
        if (player.anim != null && !string.IsNullOrWhiteSpace(player.dashBoolParam))
        {
            player.anim.SetBool(player.dashBoolParam, false);
        }

        if (player.anim != null && !string.IsNullOrWhiteSpace(player.dashTriggerParam))
        {
            player.anim.ResetTrigger(player.dashTriggerParam);
        }

        if (player.DashIgnoreGravity)
        {
            RB.gravityScale = previousGravityScale;
        }
    }

    public override void FixedUpdate()
    {
        UpdateJumpTimers();

        if (Time.time < dashEndTime)
        {
            ApplyDashVelocity();
        }
        else if (Time.time < endLagEndTime)
        {
            // End lag: stop horizontal movement, preserve vertical feel.
            RB.linearVelocity = new Vector2(0f, RB.linearVelocity.y);
        }
        else
        {
            TransitionOut();
            return;
        }

        if (!player.DashIgnoreGravity)
        {
            ApplyVariableGravity();
        }

        ClampVerticalSpeed();
    }

    private void ApplyDashVelocity()
    {
        float targetX = dashDirection * player.DashSpeed;

        float targetY = player.DashPreserveVerticalVelocity ? RB.linearVelocity.y : 0f;
        RB.linearVelocity = new Vector2(targetX, targetY);
    }

    private int GetDashDirection()
    {
        if (Mathf.Abs(MoveInput.x) > 0.1f)
        {
            return MoveInput.x > 0f ? 1 : -1;
        }

        return player.facingDirection != 0 ? player.facingDirection : 1;
    }

    private void TransitionOut()
    {
        // Prefer JumpState if airborne.
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
