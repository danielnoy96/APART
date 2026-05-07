using UnityEngine;

public class PlayerJumpState : PlayerState
{
    public PlayerJumpState(player player) : base(player) { }

    public override void Enter()
    {
        // Apply jump impulse only when a jump is actually buffered and allowed.
        // JumpState can also be used as the generic airborne state (e.g., after dash).
        if (JumpBufferTimer > 0f && CoyoteTimer > 0f)
        {
            RB.linearVelocity = new Vector2(RB.linearVelocity.x, player.jumpForce);
            JumpBufferTimer = 0f;
            CoyoteTimer = 0f;
        }
        JumpReleased = false;

        // No airborne parameters exist in the current Animator setup; keep the original behavior:
        // when not grounded, both isIdle/isRunning evaluate false.
        Anim.SetBool("isIdle", false);
        Anim.SetBool("isRunning", false);
    }

    public override void Update()
    {
        Flip();

        Anim.SetBool("isIdle", false);
        Anim.SetBool("isRunning", false);

        // Allow dash while airborne (if available).
        if (player.dashPressed && player.CanDash)
        {
            player.ChangeState(player.dashState);
            player.dashPressed = false;
            return;
        }

        // Optional: allow life drain from Jump only if grounded (e.g., just landed).
        if (IsGrounded && player.lifeDrainPressed && player.GetDrainableCorpse() != null)
        {
            player.ChangeState(player.lifeDrainState);
            player.lifeDrainPressed = false;
            return;
        }

        if (IsGrounded && RB.linearVelocity.y <= 0f)
        {
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

    public override void FixedUpdate()
    {
        UpdateJumpTimers();

        ApplyHorizontalMovement();

        // Variable jump height (jump cut) — exact behavior preserved.
        if (JumpReleased)
        {
            if (RB.linearVelocity.y > 0f)
            {
                RB.linearVelocity = new Vector2(RB.linearVelocity.x, RB.linearVelocity.y * player.jumpCutMultiplier);
            }
            JumpReleased = false;
        }

        ApplyVariableGravity();
        ClampVerticalSpeed();
    }
}
