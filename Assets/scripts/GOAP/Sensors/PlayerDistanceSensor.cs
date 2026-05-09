using CrashKonijn.Agent.Core;
using CrashKonijn.Goap.Core;
using CrashKonijn.Goap.Runtime;
using Game.GOAP.WorldKeys;
using UnityEngine;

namespace Game.GOAP.Sensors
{
    // Sense IsPlayerInRange using Vector2.Distance (no triggers).
    [GoapId("game.goap.worldsensor.player_distance")]
    public class PlayerDistanceSensor : LocalWorldSensorBase
    {
        private player cachedPlayer;

        public override void Created()
        {
            cachedPlayer = Object.FindAnyObjectByType<player>();
        }

        public override void Update()
        {
            if (cachedPlayer == null)
            {
                cachedPlayer = Object.FindAnyObjectByType<player>();
            }
        }

        public override SenseValue Sense(IActionReceiver agent, IComponentReference references)
        {
            EnemyGoapAgentBridge bridge = references.GetCachedComponent<EnemyGoapAgentBridge>();
            if (bridge == null || cachedPlayer == null)
            {
                return false;
            }

            return Vector2.Distance(agent.Transform.position, cachedPlayer.transform.position) < bridge.DetectionRange;
        }
    }
}
