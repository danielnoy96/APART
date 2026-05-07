using UnityEngine;

public class Enemy : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Health health;
    [SerializeField] private Animator animator;

    [Header("Animator Parameters (Optional)")]
    [Tooltip("Trigger parameter used to play a short hurt animation.")]
    [SerializeField] private string hurtTriggerParam = "";
    [Tooltip("Trigger parameter used to play a death animation (if you have one).")]
    [SerializeField] private string deathTriggerParam = "";

    [Header("Death Handling")]
    [Tooltip("Disable this GameObject when health reaches 0 (if no death animation is used).")]
    [SerializeField] private bool disableOnDeath = true;

    private void Awake()
    {
        if (health == null)
        {
            health = GetComponent<Health>();
        }

        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }
    }

    private void OnEnable()
    {
        if (health == null)
        {
            return;
        }

        health.OnDamaged += HandleDamaged;
        health.OnDeath += HandleDeath;
    }

    private void OnDisable()
    {
        if (health == null)
        {
            return;
        }

        health.OnDamaged -= HandleDamaged;
        health.OnDeath -= HandleDeath;
    }

    private void HandleDamaged(int damage)
    {
        if (animator == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(hurtTriggerParam))
        {
            animator.SetTrigger(hurtTriggerParam);
        }
    }

    private void HandleDeath()
    {
        bool playedDeathAnimation = false;

        if (animator != null && !string.IsNullOrWhiteSpace(deathTriggerParam))
        {
            animator.SetTrigger(deathTriggerParam);
            playedDeathAnimation = true;
        }

        // Leave a drainable corpse in the scene (do not destroy immediately).
        EnsureDrainableCorpse();
        DisableContactDamage();

        // Disable this Enemy behavior so it stops reacting/acting as an enemy.
        enabled = false;

        // Optionally disable physics-driven movement (keeps the corpse in place).
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        // Optional legacy behavior (keep off by default for corpse-based drain design).
        if (!playedDeathAnimation)
        {
            if (disableOnDeath)
            {
                // Don't disable the whole GameObject; we need it present for draining.
                // Kept for backward compatibility; no-op intentionally.
            }
            else
            {
                // Don't destroy the GameObject; corpse must persist.
            }
        }
    }

    private void EnsureDrainableCorpse()
    {
        DrainableCorpse corpse = GetComponent<DrainableCorpse>();
        if (corpse == null)
        {
            corpse = gameObject.AddComponent<DrainableCorpse>();
        }

        corpse.enabled = true;
    }

    private void DisableContactDamage()
    {
        ContactDamage[] damageSources = GetComponentsInChildren<ContactDamage>(true);
        for (int i = 0; i < damageSources.Length; i++)
        {
            damageSources[i].enabled = false;
        }
    }
}
