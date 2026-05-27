using System.Collections;
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
    [Tooltip("Animator state to play immediately on death. Used to make death override hurt/pop/unpop transitions.")]
    [SerializeField] private string deathStateName = "small enemy dead";
    [Tooltip("Seconds to wait after the death transition starts before this enemy becomes drainable corpse.")]
    [SerializeField] private float corpseTransitionDelay = 0.25f;

    [Header("Death Handling")]
    [Tooltip("Disable this GameObject when health reaches 0 (if no death animation is used).")]
    [SerializeField] private bool disableOnDeath = true;

    [Header("Hit VFX")]
    [SerializeField] private ParticleSystem hitEffect;
    [SerializeField, Min(0)] private int hitEffectBurstCount = 10;
    [SerializeField, Min(0.01f)] private float hitEffectTrailDuration = 0.22f;

    [Header("Death VFX")]
    [SerializeField] private ParticleSystem deathEffect;
    [SerializeField, Min(0)] private int deathEffectBurstCount = 160;

    [Header("Life Drain Corpse")]
    [Tooltip("Corpse stored HP = round(MaxHealth * ratio), clamped to min/max.")]
    [SerializeField] private float corpseHealRatio = 0.3f;
    [SerializeField] private int minCorpseHeal = 1;
    [SerializeField] private int maxCorpseHeal = 8;

    private bool corpseFinalized;
    private Coroutine hitEffectTrailRoutine;
    private Coroutine deathEffectTrailRoutine;

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

        if (hitEffect == null)
        {
            Transform child = transform.Find("enemy hit effect");
            if (child != null)
            {
                hitEffect = child.GetComponent<ParticleSystem>();
            }
        }

        if (deathEffect == null)
        {
            Transform child = transform.Find("enemy dead effect");
            if (child != null)
            {
                deathEffect = child.GetComponent<ParticleSystem>();
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
        StopHitEffectTrail();
    }

    private void HandleDamaged(int damage)
    {
        PlayHitEffectTrail();

        if (animator == null)
        {
            return;
        }

        if (health != null && health.CurrentHealth <= 0)
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
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        Vector2 deathVelocity = rb != null ? rb.linearVelocity : Vector2.zero;
        PlayDeathEffect(deathVelocity);

        bool playedDeathAnimation = false;

        if (animator != null && !string.IsNullOrWhiteSpace(deathTriggerParam))
        {
            if (!string.IsNullOrWhiteSpace(hurtTriggerParam))
            {
                animator.ResetTrigger(hurtTriggerParam);
            }

            animator.SetTrigger(deathTriggerParam);
            if (!string.IsNullOrWhiteSpace(deathStateName))
            {
                animator.Play(deathStateName, 0, 0f);
            }

            playedDeathAnimation = true;
        }

        DisableContactDamage();
        StopMovement();

        if (playedDeathAnimation && corpseTransitionDelay > 0f)
        {
            StartCoroutine(FinalizeCorpseAfterTransition());
            return;
        }

        FinalizeCorpse();
    }

    private void PlayHitEffectTrail()
    {
        if (hitEffect == null || hitEffectBurstCount <= 0)
        {
            return;
        }

        ConfigureHitEffect(hitEffect);
        StopHitEffectTrail();
        hitEffectTrailRoutine = StartCoroutine(HitEffectTrailRoutine());
    }

    private IEnumerator HitEffectTrailRoutine()
    {
        int burstCount = 3;
        int remaining = hitEffectBurstCount;
        float interval = hitEffectTrailDuration / Mathf.Max(1, burstCount - 1);

        for (int i = 0; i < burstCount && remaining > 0; i++)
        {
            int emitCount = i == burstCount - 1 ? remaining : Mathf.Max(1, hitEffectBurstCount / burstCount);
            hitEffect.Emit(emitCount);
            remaining -= emitCount;

            if (i < burstCount - 1)
            {
                yield return new WaitForSeconds(interval);
            }
        }

        hitEffectTrailRoutine = null;
    }

    private void StopHitEffectTrail()
    {
        if (hitEffectTrailRoutine != null)
        {
            StopCoroutine(hitEffectTrailRoutine);
            hitEffectTrailRoutine = null;
        }
    }

    private void PlayDeathEffect(Vector2 deathVelocity)
    {
        if (deathEffect == null || deathEffectBurstCount <= 0)
        {
            return;
        }

        StopHitEffectTrail();
        ConfigureDeathEffect(deathEffect);

        int centerBurstCount = Mathf.RoundToInt(deathEffectBurstCount * 0.7f);
        int explosiveBurstCount = Mathf.Max(0, deathEffectBurstCount - centerBurstCount);

        deathEffect.Emit(centerBurstCount);
        if (explosiveBurstCount > 0)
        {
            ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
            {
                velocity = GetDeathParticleVelocity(deathVelocity)
            };
            deathEffect.Emit(emitParams, explosiveBurstCount);
        }

        if (deathEffectTrailRoutine != null)
        {
            StopCoroutine(deathEffectTrailRoutine);
        }

        deathEffectTrailRoutine = StartCoroutine(DeathEffectTrailRoutine(deathVelocity));
    }

    private IEnumerator DeathEffectTrailRoutine(Vector2 deathVelocity)
    {
        int trailBursts = 3;
        int trailBurstSize = Mathf.Max(1, Mathf.RoundToInt(deathEffectBurstCount * 0.08f));
        float interval = 0.08f;

        for (int i = 0; i < trailBursts; i++)
        {
            yield return new WaitForSeconds(interval);

            if (deathEffect == null)
            {
                break;
            }

            ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
            {
                velocity = GetDeathParticleVelocity(deathVelocity) * Mathf.Lerp(0.35f, 0.1f, i / (float)Mathf.Max(1, trailBursts - 1))
            };
            deathEffect.Emit(emitParams, trailBurstSize);
        }

        deathEffectTrailRoutine = null;
    }

    private static Vector3 GetDeathParticleVelocity(Vector2 deathVelocity)
    {
        Vector2 baseVelocity = deathVelocity.sqrMagnitude > 0.01f ? deathVelocity * 0.35f : Vector2.up * 1.2f;
        baseVelocity += Vector2.up * 0.65f;
        return baseVelocity;
    }

    private void ConfigureHitEffect(ParticleSystem particleSystem)
    {
        particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        particleSystem.Clear(true);

        ParticleSystem.MainModule main = particleSystem.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.85f, 1.45f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.9f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
        main.gravityModifier = new ParticleSystem.MinMaxCurve(0.12f, 0.3f);

        ParticleSystem.EmissionModule emission = particleSystem.emission;
        emission.enabled = false;

        ParticleSystem.VelocityOverLifetimeModule velocity = particleSystem.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);
        velocity.y = new ParticleSystem.MinMaxCurve(-0.55f, -0.2f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        ParticleSystem.NoiseModule noise = particleSystem.noise;
        noise.enabled = true;
        noise.strength = 0.18f;
        noise.frequency = 0.4f;
        noise.scrollSpeed = 0.15f;

        ParticleSystem.RotationOverLifetimeModule rotation = particleSystem.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-180f, 180f);

        ConfigureParticleFade(particleSystem, 0f, 1f, 0.15f, 0f);
        ConfigureParticleRenderer(particleSystem, 6);
    }

    private void ConfigureDeathEffect(ParticleSystem particleSystem)
    {
        particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        particleSystem.Clear(true);

        ParticleSystem.MainModule main = particleSystem.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 3.3f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.58f);
        main.gravityModifier = new ParticleSystem.MinMaxCurve(0.18f, 0.45f);
        main.maxParticles = Mathf.Max(480, deathEffectBurstCount * 3);

        ParticleSystem.EmissionModule emission = particleSystem.emission;
        emission.enabled = false;

        ParticleSystem.VelocityOverLifetimeModule velocity = particleSystem.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.65f, 0.65f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.15f, 1.2f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        ParticleSystem.ForceOverLifetimeModule force = particleSystem.forceOverLifetime;
        force.enabled = true;
        force.space = ParticleSystemSimulationSpace.World;
        force.y = new ParticleSystem.MinMaxCurve(-0.8f, -0.35f);

        ParticleSystem.NoiseModule noise = particleSystem.noise;
        noise.enabled = true;
        noise.strength = 0.35f;
        noise.frequency = 0.7f;
        noise.scrollSpeed = 0.25f;

        ParticleSystem.RotationOverLifetimeModule rotation = particleSystem.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-360f, 360f);

        ParticleSystem.TrailModule trails = particleSystem.trails;
        trails.enabled = false;

        ConfigureParticleFade(particleSystem, 0f, 1f, 0.9f, 0f);
        ConfigureParticleRenderer(particleSystem, 20);
    }

    private void ConfigureParticleRenderer(ParticleSystem particleSystem, int sortingOrderOffset)
    {
        ParticleSystemRenderer particleRenderer = particleSystem.GetComponent<ParticleSystemRenderer>();
        if (particleRenderer == null)
        {
            return;
        }

        SpriteRenderer spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            particleRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
            particleRenderer.sortingOrder = spriteRenderer.sortingOrder + sortingOrderOffset;
        }

        particleRenderer.maxParticleSize = Mathf.Max(particleRenderer.maxParticleSize, 1.2f);
    }

    private static void ConfigureParticleFade(ParticleSystem particleSystem, float startAlpha, float peakAlpha, float holdAlpha, float endAlpha)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(startAlpha, 0f),
                new GradientAlphaKey(peakAlpha, 0.12f),
                new GradientAlphaKey(holdAlpha, 0.72f),
                new GradientAlphaKey(endAlpha, 1f)
            });

        ParticleSystem.ColorOverLifetimeModule color = particleSystem.colorOverLifetime;
        color.enabled = true;
        color.color = new ParticleSystem.MinMaxGradient(gradient);
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
