using CrashKonijn.Agent.Core;
using CrashKonijn.Goap.Runtime;
using UnityEngine;

namespace Game.GOAP.Targets
{
    // Provides the player Transform as a GOAP target.
    public class PlayerTargetResolver : LocalTargetSensorBase
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

        public override ITarget Sense(IActionReceiver agent, IComponentReference references, ITarget existingTarget)
        {
            if (cachedPlayer == null)
            {
                return null;
            }

            if (existingTarget is TransformTarget t)
            {
                return t.SetTransform(cachedPlayer.transform);
            }

            return new TransformTarget(cachedPlayer.transform);
        }
    }
}
