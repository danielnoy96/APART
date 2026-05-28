using UnityEngine;

public class PlayerMoveState : PlayerState
{
    public PlayerMoveState(player player) : base(player) { }

    public override void Enter()
    {
    }

    public override void Update()
    {
        Flip();

        // Dash > LifeDrain > Attack > Jump > Move > Idle (priority).
        if (player.dashPressed && player.CanDash)
        {
            player.ChangeState(player.dashState);
            player.dashPressed = false;
            return;
        }

        if (player.lifeDrainPressed && IsGrounded && player.GetDrainableCorpse() != null)
        {
            player.ChangeState(player.lifeDrainState);
            player.lifeDrainPressed = false;
            return;
        }

        if (player.attackPressed && player.combat != null && player.combat.CanAttack)
        {
            player.ChangeState(player.attackState);
            player.attackPressed = false;
            return;
        }

        if (JumpPressed)
        {
            return;
        }

        if (Mathf.Abs(MoveInput.x) <= 0.1f)
        {
            player.ChangeState(player.idleState);
        }
    }

    public override void FixedUpdate()
    {
        UpdateJumpTimers();

        // Preserve original FixedUpdate order:
        // 1) movement
        // 2) jump (may change vertical velocity)
        // 3) gravity + clamps
        ApplyHorizontalMovement();

        if (JumpBufferTimer > 0f && CoyoteTimer > 0f)
        {
            // Enter jump state to apply the exact jump impulse now, then continue this tick.
            player.ChangeState(player.jumpState);
        }

        // ApplyVariableGravity / ClampVerticalSpeed must still run in the same fixed tick as jump (as before).
        ApplyVariableGravity();
        ClampVerticalSpeed();
    }
}
