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
        [Header("Patrol")]
        [Tooltip("If true, request PatrolGoal while the player is outside charge range instead of hiding/stopping.")]
        [SerializeField] private bool patrolOutsideChargeRange = true;

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
                bridge.SetChargeCycleStartAllowed(false);
                ResetCharge(bridge);
                bridge.StopCurrentAction();
                return;
            }

            var player = bridge.Player;
            if (player == null)
            {
                charging = false;
                bridge.SetChargeCycleStartAllowed(false);
                ResetCharge(bridge);
                RequestPatrolOrHide(bridge, awareness);
                return;
            }

            float distance = Vector2.Distance(bridge.transform.position, player.position);
            if (useHorizontalDistanceOnly)
            {
                distance = Mathf.Abs(player.position.x - bridge.transform.position.x);
            }

            if (!useHysteresis)
            {
                bool inChargeRange = distance < enterChargeRange;
                bool shouldCharge = inChargeRange || (bridge.Controller != null && bridge.Controller.IsFixedChargeCycleActive);
                bridge.SetChargeCycleStartAllowed(inChargeRange);

                LogIfChanged(bridge, shouldCharge, distance);

                if (shouldCharge)
                {
                    RequestChargeWhenAwake(bridge, awareness);
                }
                else
                {
                    ResetCharge(bridge);
                    RequestPatrolOrHide(bridge, awareness);
                }

                return;
            }

            if (!charging && distance < enterChargeRange)
                charging = true;
            else if (charging && distance > exitChargeRange)
                charging = false;

            bool shouldKeepCommittedCharge = bridge.Controller != null && bridge.Controller.IsFixedChargeCycleActive;
            bool shouldRequestCharge = charging || shouldKeepCommittedCharge;
            bridge.SetChargeCycleStartAllowed(charging);

            if (shouldRequestCharge)
            {
                RequestChargeWhenAwake(bridge, awareness);
            }
            else
            {
                ResetCharge(bridge);
                RequestPatrolOrHide(bridge, awareness);
            }

            LogIfChanged(bridge, shouldRequestCharge, distance);
        }

        private void RequestChargeWhenAwake(EnemyGoapAgentBridge bridge, EnemyAwareness awareness)
        {
            if (awareness != null && !awareness.WakeAndReady())
            {
                ResetCharge(bridge);
                bridge.StopCurrentAction();
                return;
            }

            bridge.RequestCharge();
        }

        private void RequestPatrolOrHide(EnemyGoapAgentBridge bridge, EnemyAwareness awareness)
        {
            if (!patrolOutsideChargeRange)
            {
                bridge.SetChargeCycleStartAllowed(false);
                awareness?.Hide();
                bridge.StopCurrentAction();
                return;
            }

            RequestPatrolWhenAwake(bridge, awareness);
        }

        private void RequestPatrolWhenAwake(EnemyGoapAgentBridge bridge, EnemyAwareness awareness)
        {
            if (awareness != null && !awareness.WakeAndReady())
            {
                bridge.SetChargeCycleStartAllowed(false);
                ResetCharge(bridge);
                bridge.Controller?.StopMoving();
                bridge.StopCurrentAction();
                return;
            }

            bridge.RequestPatrol();
        }

        private void ResetCharge(EnemyGoapAgentBridge bridge)
        {
            bridge?.Controller?.CancelChargeCycle();
        }

        private void LogIfChanged(EnemyGoapAgentBridge bridge, bool shouldCharge, float distance)
        {
            if (bridge.DebugLog && (!hasLast || lastCharging != shouldCharge))
                Debug.Log($"[GOAP] ChargeGoalSelector: {(shouldCharge ? "Charge" : (patrolOutsideChargeRange ? "Patrol" : "Hide"))} (d={distance:0.00}, enter={enterChargeRange:0.00}, exit={exitChargeRange:0.00})", bridge);

            hasLast = true;
            lastCharging = shouldCharge;
        }
    }
}
