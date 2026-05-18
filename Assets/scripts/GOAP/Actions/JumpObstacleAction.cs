using CrashKonijn.Agent.Core;
using CrashKonijn.Agent.Runtime;
using CrashKonijn.Goap.Runtime;
using UnityEngine;

namespace Game.GOAP.Actions
{
    [GoapId("game.goap.action.jump_obstacle")]
    public class JumpObstacleAction : GoapActionBase<JumpObstacleAction.Data>
    {
        // Safety stop so a misconfigured ground check can't get the action stuck forever.
        private const float MaxAirSeconds = 2.0f;

        public override void Start(IMonoAgent agent, Data data)
        {
            data.StartTime = Time.time;
            data.HasJumped = false;
        }

        public override IActionRunState Perform(IMonoAgent agent, Data data, IActionContext context)
        {
            if (data.Controller == null || data.Controller.IsDead)
            {
                return ActionRunState.Stop;
            }

            if (!data.HasJumped)
            {
                data.Controller.TryJump();
                data.HasJumped = true;
            }

            if (Time.time - data.StartTime > MaxAirSeconds)
            {
                return ActionRunState.Stop;
            }

            // Keep running until we land again, then allow GOAP to replan back into chase.
            if (data.Controller.IsGrounded())
            {
                return ActionRunState.Stop;
            }

            return ActionRunState.Continue;
        }

        public override bool IsInRange(IMonoAgent agent, float distance, Data data, IComponentReference references) => true;

        public class Data : IActionData
        {
            public ITarget Target { get; set; }

            [GetComponent]
            public EnemyController Controller { get; set; }

            public float StartTime { get; set; }
            public bool HasJumped { get; set; }
        }
    }
}

