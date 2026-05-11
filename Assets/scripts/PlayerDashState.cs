using UnityEngine;

public class PlayerDashState : PlayerState
{
    private float dashEndTime;
    private float endLagEndTime;
    private int dashDirection;
    private float previousGravityScale;
    private float easeOutEndTime;
    private float dashStartXVelocity;

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
        dashStartXVelocity = dashDirection * player.DashSpeed;
        dashEndTime = Time.time + player.DashDuration;

        float easeOutDuration = Mathf.Max(0f, player.DashEndEaseOutDuration);
        easeOutEndTime = dashEndTime + easeOutDuration;

        if (player.DashEndLag > 0f)
        {
            endLagEndTime = easeOutEndTime + player.DashEndLag;
        }
        else
        {
            endLagEndTime = easeOutEndTime;
        }

        if (player.anim != null)
        {
            if (!string.IsNullOrWhiteSpace(player.dashBoolParam))
            {
                player.HoldAnimatorBool(player.dashBoolParam, player.dashAnimHoldSeconds);
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
        else if (Time.time < easeOutEndTime)
        {
            // Ease out: smoothly reduce dash horizontal movement while allowing player control.
            float duration = Mathf.Max(0.0001f, player.DashEndEaseOutDuration);
            float t = Mathf.Clamp01((Time.time - dashEndTime) / duration);
            // Cubic ease-out (fast at start, gentle at end).
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            float targetSpeed = Mathf.Abs(MoveInput.x) > 0.1f ? MoveInput.x * player.speed : 0f;
            float x = Mathf.Lerp(dashStartXVelocity, targetSpeed, eased);
            RB.linearVelocity = new Vector2(x, RB.linearVelocity.y);
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
