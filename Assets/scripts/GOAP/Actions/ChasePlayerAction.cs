using CrashKonijn.Agent.Core;
using CrashKonijn.Agent.Runtime;
using CrashKonijn.Goap.Runtime;
using UnityEngine;

namespace Game.GOAP.Actions
{
    // GOAP action will call EnemyController.ChasePlayer() later (executor owns movement).
    [GoapId("game.goap.action.chase_player")]
    public class ChasePlayerAction : GoapActionBase<ChasePlayerAction.Data>
    {
        public override IActionRunState Perform(IMonoAgent agent, Data data, IActionContext context)
        {
            if (data.Controller == null || data.Controller.IsDead)
            {
                return ActionRunState.Stop;
            }

            var bridge = data.Controller.GetComponent<Game.GOAP.EnemyGoapAgentBridge>();
            if (bridge != null && bridge.DebugLog && (Time.frameCount % 30 == 0))
                Debug.Log("[GOAP] ChasePlayerAction.Perform", data.Controller);

            data.Controller.ChasePlayer();
            return ActionRunState.Continue;
        }

        // We don't want GOAP to drive movement; EnemyController does that.
        public override bool IsInRange(IMonoAgent agent, float distance, Data data, IComponentReference references) => true;

        public class Data : IActionData
        {
            public ITarget Target { get; set; }

            [GetComponent]
            public EnemyController Controller { get; set; }
        }
    }
}
