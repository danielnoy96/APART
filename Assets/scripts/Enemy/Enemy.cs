using System.Collections;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Health health;
    [SerializeField] private Animator animator;
    [SerializeField] private EnemyAnimationDriver animationDriver;
    [SerializeField] private EnemyVfxController vfxController;

    [Header("Animator Parameters (Optional)")]
    [Tooltip("Trigger parameter used to play a short hurt animation.")]
    [SerializeField] private string hurtTriggerParam = "";
    [Tooltip("Trigger parameter used to play a death animation (if you have one).")]
    [SerializeField] private string deathTriggerParam = "";
    [Tooltip("Animator state to play immediately on death. Used to make death override hurt/pop/unpop transitions.")]
    [SerializeField] private string deathStateName = "small enemy dead";
    [Tooltip("Seconds to wait after the death transition starts before this enemy becomes drainable corpse.")]
    [SerializeField] private float corpseTransitionDelay = 0.25f;

    [Header("Death Handling")]
    [Tooltip("Disable this GameObject when health reaches 0 (if no death animation is used).")]
    [SerializeField] private bool disableOnDeath = true;

    [Header("Life Drain Corpse")]
    [Tooltip("Corpse stored HP = round(MaxHealth * ratio), clamped to min/max.")]
    [SerializeField] private float corpseHealRatio = 0.3f;
    [SerializeField] private int minCorpseHeal = 1;
    [SerializeField] private int maxCorpseHeal = 8;

    private bool corpseFinalized;

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
        if (animationDriver == null)
        {
            animationDriver = GetComponent<EnemyAnimationDriver>();
            if (animationDriver == null)
            {
                animationDriver = gameObject.AddComponent<EnemyAnimationDriver>();
            }
        }
        animationDriver.Initialize(animator);
        animationDriver.ConfigureDamage(hurtTriggerParam, deathTriggerParam, deathStateName);

        if (vfxController == null)
        {
            vfxController = GetComponent<EnemyVfxController>();
            if (vfxController == null)
            {
                vfxController = gameObject.AddComponent<EnemyVfxController>();
            }
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
        vfxController?.StopHitFeedback();
    }

    private void HandleDamaged(int damage)
    {
        vfxController?.PlayHitFeedback();

        if (animationDriver == null)
        {
            return;
        }

        if (health != null && health.CurrentHealth <= 0)
        {
            return;
        }

        animationDriver.PlayHurt();
    }

    private void HandleDeath()
    {
        vfxController?.PlayDeathFeedback();

        bool playedDeathAnimation = false;

        playedDeathAnimation = animationDriver != null && animationDriver.PlayDeath();

        DisableContactDamage();
        StopMovement();

        if (playedDeathAnimation && corpseTransitionDelay > 0f)
        {
            StartCoroutine(FinalizeCorpseAfterTransition());
            return;
        }

        FinalizeCorpse();
    }

    private IEnumerator FinalizeCorpseAfterTransition()
    {
        yield return new WaitForSeconds(corpseTransitionDelay);
        FinalizeCorpse();
    }

    private void FinalizeCorpse()
    {
        if (corpseFinalized)
        {
            return;
        }

        corpseFinalized = true;

        // Leave a drainable corpse in the scene after the corpse-transition animation.
        DrainableCorpse corpse = EnsureDrainableCorpse();
        if (corpse != null && health != null)
        {
            int computedHeal = Mathf.RoundToInt(health.MaxHealth * corpseHealRatio);
            computedHeal = Mathf.Clamp(computedHeal, Mathf.Max(0, minCorpseHeal), Mathf.Max(0, maxCorpseHeal));
            corpse.ConfigureHealAmount(computedHeal);
            corpse.RenderBehind(FindAnyObjectByType<player>());
        }

        vfxController?.PlayCorpseFeedback();

        // Disable this Enemy behavior so it stops reacting/acting as an enemy.
        enabled = false;

        if (disableOnDeath)
        {
            // Legacy option is intentionally a no-op: the dead enemy remains as a drainable corpse.
        }
    }

    private void StopMovement()
    {
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }

    private DrainableCorpse EnsureDrainableCorpse()
    {
        DrainableCorpse corpse = GetComponent<DrainableCorpse>();
        if (corpse == null)
        {
            corpse = gameObject.AddComponent<DrainableCorpse>();
        }

        corpse.enabled = true;
        return corpse;
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
