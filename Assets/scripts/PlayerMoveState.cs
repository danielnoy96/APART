using UnityEngine;

public sealed class PlayerMoveState : PlayerState
{
    public PlayerMoveState(player player) : base(player) { }

    public override void Update()
    {
        PerFramePipeline();

        if (!IsGrounded)
        {
            player.ChangeState(player.jumpState);
        }
        else if (Mathf.Abs(MoveInput.x) <= 0.1f)
        {
            player.ChangeState(player.idleState);
        }
    }

    public override void FixedUpdate()
    {
        FixedTickPipeline();

        if (!IsGrounded)
        {
            player.ChangeState(player.jumpState);
        }
        else if (Mathf.Abs(MoveInput.x) <= 0.1f)
        {
            player.ChangeState(player.idleState);
        }
    }
}

