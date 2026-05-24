using Game.GOAP.Goals;
using UnityEngine;

namespace Game.GOAP
{
    public class DistanceGoalSelector : EnemyDecisionModuleBase
    {
        [Header("Detection")]
        [Tooltip("If enabled, uses separate in/out thresholds to prevent rapid toggling near the boundary.")]
        [SerializeField] private bool useHysteresis = true;
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

            EnemyAwareness awareness = bridge.Awareness;
            if (awareness != null && awareness.IsAsleep)
            {
                chasing = false;
                bridge.StopCurrentAction();
                return;
            }

            var player = bridge.Player;
            if (player == null)
            {
                chasing = false;
                awareness?.Hide();
                bridge.StopCurrentAction();
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
                    Debug.Log($"[GOAP] DistanceGoalSelector: {(shouldChase ? "Chase" : "Hide")} (d={distance:0.00}, range={enterChaseRange:0.00})", bridge);

                hasLast = true;
                lastChasing = shouldChase;

                if (shouldChase)
                {
                    RequestChaseWhenAwake(bridge, awareness);
                }
                else
                {
                    awareness?.Hide();
                    bridge.StopCurrentAction();
                }

                return;
            }

            if (!chasing && distance < enterChaseRange)
                chasing = true;
            else if (chasing && distance > exitChaseRange)
                chasing = false;

            if (chasing)
            {
                RequestChaseWhenAwake(bridge, awareness);
            }
            else
            {
                awareness?.Hide();
                bridge.StopCurrentAction();
            }

            if (bridge.DebugLog && (!hasLast || lastChasing != chasing))
                Debug.Log($"[GOAP] DistanceGoalSelector: {(chasing ? "Chase" : "Hide")} (d={distance:0.00}, enter={enterChaseRange:0.00}, exit={exitChaseRange:0.00})", bridge);

            hasLast = true;
            lastChasing = chasing;
        }

        private void RequestChaseWhenAwake(EnemyGoapAgentBridge bridge, EnemyAwareness awareness)
        {
            if (awareness != null && !awareness.WakeAndReady())
            {
                bridge.StopCurrentAction();
                return;
            }

            bridge.RequestChase();
        }
    }
}
