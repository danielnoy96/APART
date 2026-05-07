using UnityEngine;
using CrashKonijn.Goap.Runtime;

public class EnemyBrain : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private EnemyController controller;
    [SerializeField] private Health health;
    [SerializeField] private Transform player;

    [Header("Senses")]
    [SerializeField] private float detectionRange = 6f;

    private void Awake()
    {
        // If GOAP is present, this temporary brain should be disabled to avoid double-driving the enemy.
        if (GetComponent<GoapActionProvider>() != null)
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

        bool playerInRange = player != null &&
                             Vector2.Distance(transform.position, player.position) < detectionRange;

        if (playerInRange)
        {
            // GOAP action will call ChasePlayer() later.
            controller.SetStateChase();
        }
        else
        {
            // GOAP action will call Patrol() later.
            controller.SetStatePatrol();
        }
    }
}
