using UnityEngine;

namespace Game.GOAP
{
    public class ChargeGoalSelector : EnemyDecisionModuleBase
    {
        [Header("Detection")]
        [Tooltip("If enabled, uses separate in/out thresholds to prevent rapid toggling near the boundary.")]
        [SerializeField] private bool useHysteresis = true;
        [Tooltip("If true, uses only horizontal distance so platform height does not stop charge selection.")]
        [SerializeField] private bool useHorizontalDistanceOnly = true;
        [Tooltip("Enter charge when distance is below this value.")]
        [SerializeField] private float enterChargeRange = 6f;
        [Tooltip("Exit charge when distance is above this value (must be >= Enter Charge Range).")]
        [SerializeField] private float exitChargeRange = 7f;

        private bool charging;
        private bool hasLast;
        private bool lastCharging;

        private void OnValidate()
        {
            if (exitChargeRange < enterChargeRange)
                exitChargeRange = enterChargeRange;
        }

        public override void Tick(EnemyGoapAgentBridge bridge)
        {
            if (bridge == null)
                return;

            EnemyAwareness awareness = bridge.Awareness;
            if (awareness != null && awareness.IsAsleep)
            {
                charging = false;
                bridge.StopCurrentAction();
                return;
            }

            var player = bridge.Player;
            if (player == null)
            {
                charging = false;
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
                bool shouldCharge = distance < enterChargeRange;
                LogIfChanged(bridge, shouldCharge, distance);

                if (shouldCharge)
                {
                    RequestChargeWhenAwake(bridge, awareness);
                }
                else
                {
                    awareness?.Hide();
                    bridge.StopCurrentAction();
                }

                return;
            }

            if (!charging && distance < enterChargeRange)
                charging = true;
            else if (charging && distance > exitChargeRange)
                charging = false;

            if (charging)
            {
                RequestChargeWhenAwake(bridge, awareness);
            }
            else
            {
                awareness?.Hide();
                bridge.StopCurrentAction();
            }

            LogIfChanged(bridge, charging, distance);
        }

        private void RequestChargeWhenAwake(EnemyGoapAgentBridge bridge, EnemyAwareness awareness)
        {
            if (awareness != null && !awareness.WakeAndReady())
            {
                bridge.StopCurrentAction();
                return;
            }

            bridge.RequestCharge();
        }

        private void LogIfChanged(EnemyGoapAgentBridge bridge, bool shouldCharge, float distance)
        {
            if (bridge.DebugLog && (!hasLast || lastCharging != shouldCharge))
                Debug.Log($"[GOAP] ChargeGoalSelector: {(shouldCharge ? "Charge" : "Hide")} (d={distance:0.00}, enter={enterChargeRange:0.00}, exit={exitChargeRange:0.00})", bridge);

            hasLast = true;
            lastCharging = shouldCharge;
        }
    }
}
