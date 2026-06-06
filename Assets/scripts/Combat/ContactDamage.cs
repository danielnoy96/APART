using UnityEngine;

public class ContactDamage : MonoBehaviour
{
    [Header("Damage")]
    [SerializeField] private int damageAmount = 1;
    [SerializeField] private float damageCooldown = 0.5f;
    [SerializeField] private LayerMask targetLayer;

    private float nextDamageTime;
    private Collider2D selfCollider;
    private Health ownerHealth;

    private void Awake()
    {
        selfCollider = GetComponent<Collider2D>();
        ownerHealth = GetComponentInParent<Health>();
    }

    private void Reset()
    {
        // Default to Player layer if it exists; otherwise leave as Nothing.
        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer >= 0)
        {
            targetLayer = 1 << playerLayer;
        }
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryDamage(other);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        TryDamage(collision.collider);
    }

    private void TryDamage(Collider2D other)
    {
        if (other == null)
        {
            return;
        }

        // If the damage source (enemy/hazard) is dead, it should not deal contact damage.
        if (ownerHealth != null && ownerHealth.IsDead)
        {
            return;
        }

        if (Time.time < nextDamageTime)
        {
            return;
        }

        if (((1 << other.gameObject.layer) & targetLayer.value) == 0)
        {
            return;
        }

        player player = other.GetComponentInParent<player>();
        if (player != null)
        {
            Vector2 knockbackSource = selfCollider != null
                ? selfCollider.ClosestPoint(player.transform.position)
                : (Vector2)transform.position;
            Vector2 vfxSource = transform.position;

            if (player.TryTakeDamage(damageAmount, vfxSource))
            {
                // KnockbackReceiver may be on the root or a child.
                KnockbackReceiver knockback = player.GetComponentInParent<KnockbackReceiver>();
                if (knockback == null)
                {
                    knockback = player.GetComponentInChildren<KnockbackReceiver>(true);
                }
                if (knockback != null)
                {
                    knockback.ApplyKnockback(knockbackSource);
                    player.StartKnockbackLock(knockback.KnockbackDuration);
                }

                nextDamageTime = Time.time + damageCooldown;
            }
            return;
        }

        Health health = other.GetComponentInParent<Health>();
        if (health == null || health.IsDead)
        {
            return;
        }

        health.TakeDamage(damageAmount);
        nextDamageTime = Time.time + damageCooldown;
    }
}
