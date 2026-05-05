using UnityEngine;

public sealed class PlayerJumpState : PlayerState
{
    public PlayerJumpState(player player) : base(player) { }

    public override void Update()
    {
        PerFramePipeline();

        if (IsGrounded)
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
        FixedTickPipeline();

        if (IsGrounded)
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
}

