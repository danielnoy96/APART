using CrashKonijn.Agent.Core;
using CrashKonijn.Agent.Runtime;
using CrashKonijn.Goap.Runtime;
using Game.GOAP.Goals;
using UnityEngine;

namespace Game.GOAP.Actions
{
    [GoapId("game.goap.action.jump_obstacle")]
    public class JumpObstacleAction : GoapActionBase<JumpObstacleAction.Data>
    {
        // Safety stop so a misconfigured ground check can't get the action stuck forever.
        private const float MaxAirSeconds = 2.0f;
        private const float MinJumpCommitSeconds = 0.12f;

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

            if (data.Awareness != null && !data.Awareness.CanRunRegularBehavior)
            {
                data.Controller.StopMoving();
                return ActionRunState.Stop;
            }

            var bridge = data.Controller.GetComponent<Game.GOAP.EnemyGoapAgentBridge>();
            if (bridge != null && !bridge.IsRequestedGoal(typeof(ChasePlayerGoal)))
            {
                return ActionRunState.Stop;
            }

            if (!data.Controller.IsPlayerAbove())
            {
                data.Controller.ChasePlayer();
                return ActionRunState.Stop;
            }

            if (!data.HasJumped)
            {
                if (!data.Controller.IsPlayerAboveReadyToJump())
                {
                    data.Controller.ChasePlayer();
                    return Time.time - data.StartTime > MaxAirSeconds
                        ? ActionRunState.Stop
                        : ActionRunState.Continue;
                }

                if (!data.Controller.TryJump())
                {
                    data.Controller.ChasePlayer();
                    return Time.time - data.StartTime > MaxAirSeconds
                        ? ActionRunState.Stop
                        : ActionRunState.Continue;
                }

                data.HasJumped = true;
                data.JumpTime = Time.time;
            }

            data.Controller.ChasePlayer();

            if (Time.time - data.StartTime > MaxAirSeconds)
            {
                return ActionRunState.Stop;
            }

            // Keep running until we land again, then allow GOAP to replan back into chase.
            if (Time.time - data.JumpTime >= MinJumpCommitSeconds && data.Controller.IsGrounded())
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

            [GetComponent]
            public EnemyAwareness Awareness { get; set; }

            public float StartTime { get; set; }
            public float JumpTime { get; set; }
            public bool HasJumped { get; set; }
        }
    }
}
