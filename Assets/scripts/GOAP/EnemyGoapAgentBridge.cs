using CrashKonijn.Goap.Runtime;
using Game.GOAP.Goals;
using System;
using UnityEngine;

namespace Game.GOAP
{
    [RequireComponent(typeof(GoapActionProvider))]
    [RequireComponent(typeof(CrashKonijn.Agent.Runtime.AgentBehaviour))]
    public class EnemyGoapAgentBridge : MonoBehaviour
    {
        [SerializeField] private GoapActionProvider actionProvider;
        [SerializeField] private EnemyDecisionModuleBase decisionModule;
        [SerializeField] private EnemyController controller;
        [SerializeField] private Health health;
        [SerializeField] private Transform player;
        [SerializeField] private float detectionRange = 6f;
        [SerializeField] private bool debugLog = false;

        public Transform Player => player;
        public float DetectionRange => detectionRange;
        public bool DebugLog => debugLog;
        public EnemyController Controller => controller;
        public bool IsRequestedGoal(Type goalType) => lastRequestedGoal == null || lastRequestedGoal == goalType;

        private Type lastRequestedGoal;
        private Type lastPlannedAction;
        private bool loggedInitialPlan;
        private float nextStatusLogTime;
        private bool eventsHooked;

        private void Awake()
        {
            var receiver = GetComponent<CrashKonijn.Agent.Runtime.AgentBehaviour>();
            if (receiver == null)
            {
                Debug.LogError("GOAP setup missing: add CrashKonijn.Agent.Runtime.AgentBehaviour to this enemy (required as ActionReceiver).", this);
                enabled = false;
                return;
            }

            if (actionProvider == null)
            {
                actionProvider = GetComponent<GoapActionProvider>();
            }

            if (actionProvider != null && actionProvider.AgentTypeBehaviour == null)
            {
                actionProvider.AgentTypeBehaviour = GetComponent<AgentTypeBehaviour>();
            }

            // Ensure Receiver wiring as early as possible (Awake order between components is not guaranteed).
            EnsureGoapReceiver();

            if (actionProvider == null || actionProvider.AgentTypeBehaviour == null)
            {
                Debug.LogError("GOAP setup missing. Enemy requires GoapActionProvider + AgentTypeBehaviour (and a scene GoapBehaviour runner).", this);
                enabled = false;
                return;
            }

            if (decisionModule == null)
            {
                decisionModule = GetComponent<EnemyDecisionModuleBase>();
            }

            if (decisionModule == null)
            {
                Debug.LogError("No EnemyDecisionModuleBase found. Add DistanceGoalSelector (or another decision module) to this enemy prefab.", this);
                enabled = false;
                return;
            }

            if (controller == null)
            {
                controller = GetComponent<EnemyController>();
            }

            if (health == null)
            {
                health = GetComponent<Health>();
            }

            var legacyBrain = GetComponent<EnemyBrain>();
            if (legacyBrain != null && legacyBrain.enabled)
            {
                legacyBrain.enabled = false;
                Debug.Log("EnemyBrain disabled because GOAP is present (Always GOAP).", this);
            }

            if (player == null)
            {
                player p = FindAnyObjectByType<player>();
                player = p != null ? p.transform : null;
            }

            if (controller != null && player != null)
            {
                controller.SetPlayer(player);
            }

            if (debugLog)
                Debug.Log($"[GOAP] Bridge ready. Provider={(actionProvider != null ? "OK" : "NULL")} Receiver={(actionProvider != null && actionProvider.Receiver != null ? "OK" : "NULL")} Player={(player != null ? player.name : "NULL")}", this);

            HookEventsIfNeeded();
        }

        private void OnEnable()
        {
            EnsureGoapReceiver();
            HookEventsIfNeeded();
        }

        private void Update()
        {
            if (controller == null || actionProvider == null)
            {
                return;
            }

            if (health != null && health.IsDead)
            {
                // Stop GOAP execution on death; keep corpse for DrainableCorpse.
                controller.SetDead();
                actionProvider.enabled = false;
                return;
            }

            if (player == null)
            {
                player p = FindAnyObjectByType<player>();
                player = p != null ? p.transform : null;
                if (controller != null && player != null)
                    controller.SetPlayer(player);
            }

            EnsureGoapReceiver();
            decisionModule.Tick(this);

            if (debugLog && actionProvider != null)
            {
                var action = actionProvider.CurrentPlan != null ? actionProvider.CurrentPlan.Action : null;
                var actionType = action != null ? action.GetType() : null;

                if (!loggedInitialPlan)
                {
                    loggedInitialPlan = true;
                    lastPlannedAction = actionType;
                    Debug.Log($"[GOAP] CurrentPlan.Action = {(actionType != null ? actionType.Name : "NULL")}", this);
                }
                else if (!ReferenceEquals(actionType, lastPlannedAction))
                {
                    lastPlannedAction = actionType;
                    Debug.Log($"[GOAP] CurrentPlan.Action = {(actionType != null ? actionType.Name : "NULL")}", this);
                }

                if (Time.unscaledTime >= nextStatusLogTime)
                {
                    nextStatusLogTime = Time.unscaledTime + 1f;
                    var receiver = actionProvider.Receiver as CrashKonijn.Agent.Runtime.AgentBehaviour;
                    string receiverState = receiver != null
                        ? $"ReceiverState={receiver.State} MoveState={receiver.MoveState} ActionState={(receiver.ActionState.Action != null ? receiver.ActionState.Action.GetType().Name : "NULL")}"
                        : "ReceiverState=N/A";

                    string requested = lastRequestedGoal != null ? lastRequestedGoal.Name : "NULL";
                    string goal = actionProvider.CurrentPlan != null && actionProvider.CurrentPlan.Goal != null ? actionProvider.CurrentPlan.Goal.GetType().Name : "NULL";

                    Debug.Log($"[GOAP] Status: Requested={requested} PlanGoal={goal} PlanAction={(actionType != null ? actionType.Name : "NULL")} {receiverState}", this);
                }
            }
        }

        private void EnsureGoapReceiver()
        {
            // GOAP v3 requires an ActionReceiver (CrashKonijn.Agent.Runtime.AgentBehaviour) to be assigned as Receiver.
            // If scene/prefab wiring missed it or Awake order is unfavorable, set it up at runtime.
            if (actionProvider == null || actionProvider.Receiver != null)
                return;

            var agent = GetComponent<CrashKonijn.Agent.Runtime.AgentBehaviour>();
            if (agent == null)
                return;

            if (agent.ActionProviderBase == null)
                agent.ActionProviderBase = actionProvider;

            if (!ReferenceEquals(agent.ActionProvider, actionProvider))
                agent.ActionProvider = actionProvider;
        }

        public void RequestPatrol()
        {
            if (actionProvider == null)
                return;

            EnsureGoapReceiver();
            if (actionProvider.Receiver == null)
            {
                Debug.LogError("GOAP action provider has no Receiver. Add CrashKonijn.Agent.Runtime.AgentBehaviour to this enemy and assign its ActionProviderBase to the GoapActionProvider.", this);
                return;
            }

            // Don't permanently suppress requests: the resolver may initially fail due to missing
            // sensors/targets during scene startup. Allow re-requesting if there is no plan yet.
            if (lastRequestedGoal == typeof(PatrolGoal) && actionProvider.CurrentPlan != null)
                return;

            bool isGoalChange = lastRequestedGoal != typeof(PatrolGoal);
            lastRequestedGoal = typeof(PatrolGoal);
            if (debugLog)
                Debug.Log("[GOAP] RequestGoal: PatrolGoal", this);
            actionProvider.RequestGoal(new[] { typeof(PatrolGoal) });
            StopRunningActionOnGoalChange(isGoalChange);
        }

        public void RequestChase()
        {
            if (actionProvider == null)
                return;

            EnsureGoapReceiver();
            if (actionProvider.Receiver == null)
            {
                Debug.LogError("GOAP action provider has no Receiver. Add CrashKonijn.Agent.Runtime.AgentBehaviour to this enemy and assign its ActionProviderBase to the GoapActionProvider.", this);
                return;
            }

            // Don't permanently suppress requests: the resolver may initially fail due to missing
            // sensors/targets during scene startup. Allow re-requesting if there is no plan yet.
            if (lastRequestedGoal == typeof(ChasePlayerGoal) && actionProvider.CurrentPlan != null)
                return;

            bool isGoalChange = lastRequestedGoal != typeof(ChasePlayerGoal);
            lastRequestedGoal = typeof(ChasePlayerGoal);
            if (debugLog)
                Debug.Log("[GOAP] RequestGoal: ChasePlayerGoal", this);
            actionProvider.RequestGoal(new[] { typeof(ChasePlayerGoal) });
            StopRunningActionOnGoalChange(isGoalChange);
        }

        private void StopRunningActionOnGoalChange(bool isGoalChange)
        {
            if (!isGoalChange)
                return;

            var receiver = actionProvider != null ? actionProvider.Receiver as CrashKonijn.Agent.Runtime.AgentBehaviour : null;
            if (receiver == null || receiver.ActionState.Action == null)
                return;

            receiver.StopAction();
        }

        private void HookEventsIfNeeded()
        {
            if (eventsHooked)
                return;

            if (actionProvider == null)
                return;

            eventsHooked = true;
            actionProvider.Events.OnResolve += OnGoapResolveRequested;
            actionProvider.Events.OnNoActionFound += OnNoActionFound;
            actionProvider.Events.OnGoalStart += OnGoalStart;
            actionProvider.Events.OnGoalCompleted += OnGoalCompleted;
        }

        private void OnGoapResolveRequested()
        {
            if (!debugLog)
                return;

            Debug.Log("[GOAP] Events.Resolve()", this);
        }

        private void OnNoActionFound(CrashKonijn.Goap.Core.IGoalRequest request)
        {
            Type failedGoal = lastRequestedGoal;
            if (failedGoal == null && request != null && request.Goals != null && request.Goals.Count > 0 && request.Goals[0] != null)
                failedGoal = request.Goals[0].GetType();

            int goalCount = request != null && request.Goals != null ? request.Goals.Count : 0;
            string goals = request != null && request.Goals != null
                ? string.Join(", ", request.Goals.ConvertAll(g => g != null ? g.GetType().Name : "NULL"))
                : "NULL";

            Debug.LogWarning($"[GOAP] Events.NoActionFound (goals={goalCount}: {goals})", this);

            // Allow the decision module to re-request goals next frame (common during startup when
            // targets/sensors aren't ready on the first resolve).
            lastRequestedGoal = null;

            // Practical fallback: if the resolver can't find an action, keep gameplay responsive by
            // driving the controller directly based on the requested goal. This is not a new system;
            // it is a safety net so the enemy doesn't freeze while GOAP config is being iterated.
            if (controller == null)
                return;

            if (failedGoal == typeof(ChasePlayerGoal))
            {
                if (debugLog)
                    Debug.Log("[GOAP] NoActionFound fallback: ChasePlayer", this);
                controller.ChasePlayer();
            }
            else if (failedGoal == typeof(PatrolGoal))
            {
                if (debugLog)
                    Debug.Log("[GOAP] NoActionFound fallback: Patrol", this);
                controller.Patrol();
            }
        }

        private void OnGoalStart(CrashKonijn.Goap.Core.IGoal goal)
        {
            if (!debugLog)
                return;

            Debug.Log($"[GOAP] Events.GoalStart: {(goal != null ? goal.GetType().Name : "NULL")}", this);
        }

        private void OnGoalCompleted(CrashKonijn.Goap.Core.IGoal goal)
        {
            if (!debugLog)
                return;

            Debug.Log($"[GOAP] Events.GoalCompleted: {(goal != null ? goal.GetType().Name : "NULL")}", this);
        }
    }
}
