using Game.GOAP.Goals;
using UnityEngine;

namespace Game.GOAP
{
    public class DistanceGoalSelector : EnemyDecisionModuleBase
    {
        [Header("Detection")]
        [Tooltip("If enabled, uses separate in/out thresholds to prevent rapid toggling near the boundary.")]
        [SerializeField] private bool useHysteresis = false;
        [Tooltip("If true, uses only horizontal distance (|dx|). This allows chase/jump when the player is on a platform above without the vertical distance forcing Patrol.")]
        [SerializeField] private bool useHorizontalDistanceOnly = true;
        [Tooltip("Enter chase when distance is below this value.")]
        [SerializeField] private float enterChaseRange = 6f;
        [Tooltip("Exit chase when distance is above this value (must be >= Enter Chase Range).")]
        [SerializeField] private float exitChaseRange = 7f;

        private bool chasing;
        private bool hasLast;
        private bool lastChasing;

        private void OnValidate()
        {
            if (exitChaseRange < enterChaseRange)
                exitChaseRange = enterChaseRange;
        }

        public override void Tick(EnemyGoapAgentBridge bridge)
        {
            if (bridge == null)
                return;

            var player = bridge.Player;
            if (player == null)
            {
                chasing = false;
                bridge.RequestPatrol();
                return;
            }

            float distance = Vector2.Distance(bridge.transform.position, player.position);
            if (useHorizontalDistanceOnly)
            {
                distance = Mathf.Abs(player.position.x - bridge.transform.position.x);
            }

            if (!useHysteresis)
            {
                bool shouldChase = distance < enterChaseRange;
                if (bridge.DebugLog && (!hasLast || lastChasing != shouldChase))
                    Debug.Log($"[GOAP] DistanceGoalSelector: {(shouldChase ? "Chase" : "Patrol")} (d={distance:0.00}, range={enterChaseRange:0.00})", bridge);

                hasLast = true;
                lastChasing = shouldChase;

                if (shouldChase)
                    bridge.RequestChase();
                else
                    bridge.RequestPatrol();
                return;
            }

            if (!chasing && distance < enterChaseRange)
                chasing = true;
            else if (chasing && distance > exitChaseRange)
                chasing = false;

            if (chasing)
                bridge.RequestChase();
            else
                bridge.RequestPatrol();

            if (bridge.DebugLog && (!hasLast || lastChasing != chasing))
                Debug.Log($"[GOAP] DistanceGoalSelector: {(chasing ? "Chase" : "Patrol")} (d={distance:0.00}, enter={enterChaseRange:0.00}, exit={exitChaseRange:0.00})", bridge);

            hasLast = true;
            lastChasing = chasing;
        }
    }
}
