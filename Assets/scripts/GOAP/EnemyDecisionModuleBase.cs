using UnityEngine;

namespace Game.GOAP
{
    public abstract class EnemyDecisionModuleBase : MonoBehaviour
    {
        public abstract void Tick(EnemyGoapAgentBridge bridge);
    }
}

