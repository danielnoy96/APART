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
            if (player.stamina != null && !player.stamina.TrySpend(player.JumpCost))
            {
                // Not enough stamina to jump; consume the buffer so it won't trigger later unexpectedly.
                JumpBufferTimer = 0f;
                CoyoteTimer = 0f;
            }
            else
            {
            RB.linearVelocity = new Vector2(RB.linearVelocity.x, player.jumpForce);
            JumpBufferTimer = 0f;
            CoyoteTimer = 0f;
            }
        }
        JumpReleased = false;

        // Locomotion animation is synced centrally by player.SyncAnimationState().
    }

    public override void Update()
    {
        Flip();

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
