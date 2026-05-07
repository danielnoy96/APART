using System.Collections.Generic;
using UnityEngine;

public class Combat : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform attackPoint;

    [Header("Attack Settings")]
    [SerializeField] private float attackRadius = 0.35f;
    [SerializeField] private LayerMask damageableLayer;
    [SerializeField] private int damage = 1;
    [SerializeField] private float attackCooldown = 0.25f;
    [Tooltip("Logs hit results to the Console (useful while you don't have animations/UI yet).")]
    [SerializeField] private bool logHits = false;

    private float nextAttackTime;
    private player player;
    private Coroutine debugAttackRoutine;

    public bool CanAttack => Time.time >= nextAttackTime;

    [Header("Debug (No Animation)")]
    [Tooltip("If enabled, BeginAttack will perform the hit check and finish the attack automatically (useful before you have attack animations/events).")]
    [SerializeField] private bool debugInstantAttack = false;
    [Tooltip("Delay (seconds) before hit check in debug mode.")]
    [SerializeField] private float debugHitDelaySeconds = 0.05f;
    [Tooltip("Delay (seconds) before finishing attack in debug mode.")]
    [SerializeField] private float debugFinishDelaySeconds = 0.1f;

    private void Awake()
    {
        player = GetComponentInParent<player>();
    }

    public void BeginAttack()
    {
        nextAttackTime = Time.time + attackCooldown;

        if (debugInstantAttack)
        {
            if (debugAttackRoutine != null)
            {
                StopCoroutine(debugAttackRoutine);
            }
            debugAttackRoutine = StartCoroutine(DebugInstantAttackRoutine());
        }
    }

    // Animation event (hit frame)
    public void PerformHitCheck()
    {
        Transform origin = attackPoint != null ? attackPoint : transform;
        if (attackPoint == null && logHits)
        {
            Debug.LogWarning("Combat.PerformHitCheck: attackPoint is not assigned, using this transform position.", this);
        }

        if (damageableLayer.value == 0 && logHits)
        {
            Debug.LogWarning("Combat.PerformHitCheck: Damageable Layer is set to Nothing; no colliders will be detected.", this);
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(origin.position, attackRadius, damageableLayer);
        if (hits == null || hits.Length == 0)
        {
            if (logHits)
            {
                Debug.Log("Combat.PerformHitCheck: no colliders hit.", this);
            }
            return;
        }

        HashSet<Health> damaged = new HashSet<Health>();
        int collidersWithNoHealth = 0;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
            {
                continue;
            }

            Health health = hit.GetComponentInParent<Health>();
            if (health == null || health.IsDead)
            {
                collidersWithNoHealth++;
                continue;
            }

            if (!damaged.Add(health))
            {
                continue;
            }

            health.TakeDamage(damage);
            if (logHits)
            {
                Debug.Log($"Hit {health.gameObject.name} for {damage}", health.gameObject);
            }

            KnockbackReceiver knockback = health.GetComponentInParent<KnockbackReceiver>();
            if (knockback != null)
            {
                Vector2 source = attackPoint != null ? (Vector2)attackPoint.position : (Vector2)transform.position;
                knockback.ApplyKnockback(source);
            }
        }

        if (logHits && damaged.Count == 0)
        {
            Debug.Log($"Combat.PerformHitCheck: {hits.Length} collider(s) overlapped, but none had a Health component (noHealth={collidersWithNoHealth}).", this);
        }
    }

    // Animation event (final frame)
    public void AttackAnimationFinished()
    {
        if (player == null)
        {
            player = GetComponentInParent<player>();
        }

        player?.OnAttackAnimationFinished();
    }

    private System.Collections.IEnumerator DebugInstantAttackRoutine()
    {
        if (debugHitDelaySeconds > 0f)
        {
            yield return new WaitForSeconds(debugHitDelaySeconds);
        }
        else
        {
            yield return null;
        }

        PerformHitCheck();

        if (debugFinishDelaySeconds > 0f)
        {
            yield return new WaitForSeconds(debugFinishDelaySeconds);
        }
        else
        {
            yield return null;
        }

        AttackAnimationFinished();
        debugAttackRoutine = null;
    }

    private void OnDrawGizmosSelected()
    {
        if (attackPoint == null)
        {
            return;
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(attackPoint.position, attackRadius);
    }
}
