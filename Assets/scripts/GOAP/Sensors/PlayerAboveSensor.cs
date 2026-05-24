using CrashKonijn.Agent.Core;
using CrashKonijn.Goap.Core;
using CrashKonijn.Goap.Runtime;
using Game.GOAP.WorldKeys;

namespace Game.GOAP.Sensors
{
    [GoapId("game.goap.worldsensor.player_above")]
    public class PlayerAboveSensor : LocalWorldSensorBase
    {
        public override void Created()
        {
        }

        public override void Update()
        {
        }

        public override SenseValue Sense(IActionReceiver agent, IComponentReference references)
        {
            EnemyController controller = references.GetCachedComponent<EnemyController>();
            if (controller == null || controller.IsDead)
            {
                return false;
            }

            if (!controller.IsGrounded())
            {
                return false;
            }

            return controller.IsPlayerAboveReadyToJump();
        }
    }
}
