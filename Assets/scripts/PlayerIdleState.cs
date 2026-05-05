using UnityEngine;

public sealed class PlayerIdleState : PlayerState
{
    public PlayerIdleState(player player) : base(player) { }

    public override void Update()
    {
        PerFramePipeline();

        if (!IsGrounded)
        {
            player.ChangeState(player.jumpState);
        }
        else if (Mathf.Abs(MoveInput.x) > 0.1f)
        {
            player.ChangeState(player.moveState);
        }
    }

    public override void FixedUpdate()
    {
        FixedTickPipeline();

        if (!IsGrounded)
        {
            player.ChangeState(player.jumpState);
        }
        else if (Mathf.Abs(MoveInput.x) > 0.1f)
        {
            player.ChangeState(player.moveState);
        }
    }
}

