using CrashKonijn.Agent.Core;
using CrashKonijn.Goap.Core;
using CrashKonijn.Goap.Runtime;
using UnityEngine;

namespace Game.GOAP.Sensors
{
    // Provides HasTarget world state as 1/0.
    [GoapId("game.goap.worldsensor.has_player_target")]
    public class HasPlayerTargetSensor : LocalWorldSensorBase
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
            return cachedPlayer != null;
        }
    }
}
