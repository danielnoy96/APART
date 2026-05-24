using CrashKonijn.Agent.Core;
using CrashKonijn.Agent.Runtime;
using CrashKonijn.Goap.Runtime;
using Game.GOAP.Goals;
using UnityEngine;

namespace Game.GOAP.Actions
{
    // GOAP action will call EnemyController.Patrol() later (executor owns movement).
    [GoapId("game.goap.action.patrol")]
    public class PatrolAction : GoapActionBase<PatrolAction.Data>
    {
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
            if (bridge != null && !bridge.IsRequestedGoal(typeof(PatrolGoal)))
            {
                return ActionRunState.Stop;
            }

            if (bridge != null && bridge.DebugLog && (Time.frameCount % 60 == 0))
                Debug.Log("[GOAP] PatrolAction.Perform", data.Controller);

            data.Controller.Patrol();
            return ActionRunState.Continue;
        }

        // We don't want GOAP to drive movement; EnemyController does that.
        public override bool IsInRange(IMonoAgent agent, float distance, Data data, IComponentReference references) => true;

        public class Data : IActionData
        {
            public ITarget Target { get; set; }

            [GetComponent]
            public EnemyController Controller { get; set; }

            [GetComponent]
            public EnemyAwareness Awareness { get; set; }
        }
    }
}
