using UnityEngine;
using CrashKonijn.Goap.Runtime;

public class EnemyBrain : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private EnemyController controller;
    [SerializeField] private EnemyAwareness awareness;
    [SerializeField] private Health health;
    [SerializeField] private Transform player;

    [Header("Senses")]
    [SerializeField] private float detectionRange = 6f;

    private void Awake()
    {
        // Always GOAP policy: if GOAP is present, legacy brain must never run.
        if (GetComponent<GoapActionProvider>() != null || GetComponent<AgentTypeBehaviour>() != null || GetComponent<Game.GOAP.EnemyGoapAgentBridge>() != null)
        {
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

        if (awareness == null)
        {
            awareness = GetComponent<EnemyAwareness>();
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
        if (controller == null)
        {
            return;
        }

        if (health != null && health.IsDead)
        {
            // GOAP action will call SetDead() later.
            controller.SetDead();
            return;
        }

        if (awareness != null && awareness.IsAsleep)
        {
            controller.StopMoving();
            return;
        }

        bool playerInRange = player != null &&
                             Vector2.Distance(transform.position, player.position) < detectionRange;

        if (playerInRange)
        {
            if (awareness != null && !awareness.WakeAndReady())
            {
                return;
            }

            // GOAP action will call ChasePlayer() later.
            controller.SetStateChase();
        }
        else
        {
            awareness?.Hide();
        }
    }
}
