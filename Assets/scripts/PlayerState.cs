using UnityEngine;

public abstract class PlayerState
{
    protected readonly player player;

    protected PlayerState(player player)
    {
        this.player = player;
    }

    public virtual void Enter() { }
    public virtual void Exit() { }
    public virtual void Update() { }
    public virtual void FixedUpdate() { }

    protected Rigidbody2D RB => player.rb;

    protected Vector2 MoveInput
    {
        get => player.moveInput;
        set => player.moveInput = value;
    }

    // Input state (mapped onto existing variables; no new gameplay variables introduced).
    protected bool JumpHeld
    {
        get => player.jumpHeld;
        set => player.jumpHeld = value;
    }

    // "Pressed" is represented by the existing jump buffer timer being active.
    protected bool JumpPressed
    {
        get => player.jumpBufferTimer > 0f;
        set => player.jumpBufferTimer = value ? player.jumpBufferTime : 0f;
    }

    protected bool JumpReleased
    {
        get => player.jumpCutQueued;
        set => player.jumpCutQueued = value;
    }

    protected float CoyoteTimer
    {
        get => player.coyoteTimer;
        set => player.coyoteTimer = value;
    }

    protected float JumpBufferTimer
    {
        get => player.jumpBufferTimer;
        set => player.jumpBufferTimer = value;
    }

    protected bool IsGrounded => player.IsGrounded;

    protected void Flip()
    {
        player.Flip();
    }

    protected void UpdateJumpTimers()
    {
        if (IsGrounded)
        {
            CoyoteTimer = player.coyoteTime;
        }
        else
        {
            CoyoteTimer = Mathf.Max(0f, CoyoteTimer - Time.fixedDeltaTime);
        }

        JumpBufferTimer = Mathf.Max(0f, JumpBufferTimer - Time.fixedDeltaTime);
    }

    protected void ApplyHorizontalMovement()
    {
        if (player.IsKnockbackLocked)
        {
            return;
        }

        float targetSpeed = MoveInput.x * player.speed;
        RB.linearVelocity = new Vector2(targetSpeed, RB.linearVelocity.y);
    }

    protected void ApplyIdleHorizontalVelocity()
    {
        if (player.IsKnockbackLocked)
        {
            return;
        }

        RB.linearVelocity = new Vector2(0f, RB.linearVelocity.y);
    }

    protected void ApplyVariableGravity()
    {
        float gravityScale;
        if (RB.linearVelocity.y < -0.1f)
        {
            gravityScale = player.fallGravity;
        }
        else if (RB.linearVelocity.y > 0.1f)
        {
            gravityScale = player.jumpGravity;
        }
        else
        {
            gravityScale = player.normalGravity;
        }

        if (!IsGrounded && JumpHeld && player.apexHangVelocityThreshold > 0f && Mathf.Abs(RB.linearVelocity.y) <= player.apexHangVelocityThreshold)
        {
            gravityScale *= player.apexHangGravityMultiplier;
        }

        RB.gravityScale = gravityScale;
    }

    protected void ClampVerticalSpeed()
    {
        if (player.maxFallSpeed <= 0f && player.maxRiseSpeed <= 0f)
        {
            return;
        }

        Vector2 velocity = RB.linearVelocity;
        if (player.maxFallSpeed > 0f)
        {
            velocity.y = Mathf.Max(velocity.y, -player.maxFallSpeed);
        }
        if (player.maxRiseSpeed > 0f)
        {
            velocity.y = Mathf.Min(velocity.y, player.maxRiseSpeed);
        }
        RB.linearVelocity = velocity;
    }
}
