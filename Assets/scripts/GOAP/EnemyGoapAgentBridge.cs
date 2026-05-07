using CrashKonijn.Goap.Runtime;
using Game.GOAP.Goals;
using UnityEngine;

namespace Game.GOAP
{
    // Temporary "brain" that selects which goal to request.
    // Later: replace this goal selection with CrashKonijn GOAP goal selection logic (or a custom selector).
    [RequireComponent(typeof(GoapActionProvider))]
    public class EnemyGoapAgentBridge : MonoBehaviour
    {
        [SerializeField] private GoapActionProvider actionProvider;
        [SerializeField] private EnemyController controller;
        [SerializeField] private Health health;
        [SerializeField] private Transform player;
        [SerializeField] private float detectionRange = 6f;

        public float DetectionRange => detectionRange;

        private void Awake()
        {
            if (actionProvider == null)
            {
                actionProvider = GetComponent<GoapActionProvider>();
            }

            if (controller == null)
            {
                controller = GetComponent<EnemyController>();
            }

            if (health == null)
            {
                health = GetComponent<Health>();
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
                return;
            }

            bool inRange = Vector2.Distance(transform.position, player.position) < detectionRange;
            if (inRange)
            {
                actionProvider.RequestGoal(new[] { typeof(ChasePlayerGoal) });
            }
            else
            {
                actionProvider.RequestGoal(new[] { typeof(PatrolGoal) });
            }
        }
    }
}
