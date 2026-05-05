using UnityEngine;

public abstract class PlayerState
{
    protected readonly player player;

    protected PlayerState(player player)
    {
        this.player = player;
    }

    protected Rigidbody2D Rb => player.rb;
    protected Animator Anim => player.anim;

    protected Vector2 MoveInput
    {
        get => player.moveInput;
        set => player.moveInput = value;
    }

    protected int FacingDirection
    {
        get => player.facingDirection;
        set => player.facingDirection = value;
    }

    protected bool IsGrounded => player.IsGrounded;

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

    protected bool JumpHeld
    {
        get => player.jumpHeld;
        set => player.jumpHeld = value;
    }

    protected bool JumpCutQueued
    {
        get => player.jumpCutQueued;
        set => player.jumpCutQueued = value;
    }

    protected float Speed => player.speed;
    protected float JumpForce => player.jumpForce;
    protected float JumpCutMultiplier => player.jumpCutMultiplier;
    protected float NormalGravity => player.normalGravity;
    protected float FallGravity => player.fallGravity;
    protected float JumpGravity => player.jumpGravity;
    protected float CoyoteTime => player.coyoteTime;
    protected float JumpBufferTime => player.jumpBufferTime;
    protected float ApexHangGravityMultiplier => player.apexHangGravityMultiplier;
    protected float ApexHangVelocityThreshold => player.apexHangVelocityThreshold;
    protected float MaxFallSpeed => player.maxFallSpeed;
    protected float MaxRiseSpeed => player.maxRiseSpeed;

    public virtual void Enter() { }
    public virtual void Exit() { }
    public virtual void Update() { }
    public virtual void FixedUpdate() { }

    protected void PerFramePipeline()
    {
        player.Flip();
        HandleAnimations();
    }

    protected void FixedTickPipeline()
    {
        UpdateJumpTimers();
        HandleMovement();
        HandleJump();
        ApplyVariableGravity();
        ClampVerticalSpeed();
    }

    private void HandleMovement()
    {
        float targetSpeed = MoveInput.x * Speed;
        Rb.linearVelocity = new Vector2(1 * targetSpeed, Rb.linearVelocity.y);
    }

    private void HandleJump()
    {
        if (JumpBufferTimer > 0f && CoyoteTimer > 0f)
        {
            PerformJump();
        }

        if (JumpCutQueued)
        {
            if (Rb.linearVelocity.y > 0f) // still going up
            {
                Rb.linearVelocity = new Vector2(Rb.linearVelocity.x, Rb.linearVelocity.y * JumpCutMultiplier);
            }
            JumpCutQueued = false;
        }
    }

    private void PerformJump()
    {
        Rb.linearVelocity = new Vector2(Rb.linearVelocity.x, JumpForce);
        JumpBufferTimer = 0f;
        CoyoteTimer = 0f;
        JumpCutQueued = false;
    }

    private void ApplyVariableGravity()
    {
        float gravityScale;
        if (Rb.linearVelocity.y < -0.1f) //falling
        {
            gravityScale = FallGravity;
        }
        else if (Rb.linearVelocity.y > 0.1f) //rising
        {
            gravityScale = JumpGravity;
        }
        else //normal gravity
        {
            gravityScale = NormalGravity;
        }

        // Celeste-style subtle apex hang: while holding jump near the top, apply reduced gravity.
        if (!IsGrounded && JumpHeld && ApexHangVelocityThreshold > 0f && Mathf.Abs(Rb.linearVelocity.y) <= ApexHangVelocityThreshold)
        {
            gravityScale *= ApexHangGravityMultiplier;
        }

        Rb.gravityScale = gravityScale;
    }

    private void HandleAnimations()
    {
        Anim.SetBool("isJumping", Rb.linearVelocity.y > .1f);
        Anim.SetBool("isGrounded", IsGrounded);

        Anim.SetFloat("yVelocity", Rb.linearVelocity.y);

        Anim.SetBool("isIdle", Mathf.Abs(MoveInput.x) < 0.1f && IsGrounded);
        Anim.SetBool("isRunning", Mathf.Abs(MoveInput.x) > 0.1f && IsGrounded);
    }

    private void UpdateJumpTimers()
    {
        if (IsGrounded)
        {
            CoyoteTimer = CoyoteTime;
        }
        else
        {
            CoyoteTimer = Mathf.Max(0f, CoyoteTimer - Time.fixedDeltaTime);
        }

        JumpBufferTimer = Mathf.Max(0f, JumpBufferTimer - Time.fixedDeltaTime);
    }

    private void ClampVerticalSpeed()
    {
        if (MaxFallSpeed <= 0f && MaxRiseSpeed <= 0f)
        {
            return;
        }

        Vector2 velocity = Rb.linearVelocity;
        if (MaxFallSpeed > 0f)
        {
            velocity.y = Mathf.Max(velocity.y, -MaxFallSpeed);
        }
        if (MaxRiseSpeed > 0f)
        {
            velocity.y = Mathf.Min(velocity.y, MaxRiseSpeed);
        }
        Rb.linearVelocity = velocity;
    }
}

