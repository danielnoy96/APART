using System.Collections;
using UnityEngine;

public class EnemyVfxController : MonoBehaviour
{
    [Header("Hit Feedback")]
    [SerializeField] private ParticleSystem hitEffect;
    [SerializeField, Min(0)] private int hitEffectBurstCount = 10;
    [SerializeField, Min(0.01f)] private float hitEffectTrailDuration = 0.22f;
    [SerializeField] private int hitSortingOrderOffset = 6;

    [Header("Death Feedback")]
    [SerializeField] private ParticleSystem deathEffect;
    [SerializeField] private int deathSortingOrderOffset = 20;

    [Header("Corpse Feedback")]
    [SerializeField] private ParticleSystem corpseEffect;

    private Coroutine hitEffectTrailRoutine;

    private void Awake()
    {
        ResolveReferences();
        ConfigureCorpseEffect();
    }

    private void OnDisable()
    {
        StopHitFeedback();
    }

    public void PlayHitFeedback()
    {
        ResolveReferences();

        if (hitEffect == null || hitEffectBurstCount <= 0)
        {
            return;
        }

        ConfigureHitEffect(hitEffect);
        StopHitFeedback();
        hitEffectTrailRoutine = StartCoroutine(HitEffectTrailRoutine());
    }

    public void StopHitFeedback()
    {
        if (hitEffectTrailRoutine == null)
        {
            return;
        }

        StopCoroutine(hitEffectTrailRoutine);
        hitEffectTrailRoutine = null;
    }

    public void PlayDeathFeedback()
    {
        ResolveReferences();

        if (deathEffect == null)
        {
            return;
        }

        StopHitFeedback();

        deathEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        deathEffect.Clear(true);
        ConfigureParticleRenderer(deathEffect, deathSortingOrderOffset);
        deathEffect.Play(true);
    }

    public void PlayCorpseFeedback()
    {
        ResolveReferences();

        if (corpseEffect == null)
        {
            return;
        }

        ConfigureCorpseEffect();
        corpseEffect.Play(true);
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

    private void ResolveReferences()
    {
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

        if (corpseEffect == null)
        {
            Transform child = transform.Find("enemy dead life drain effect");
            if (child == null)
            {
                child = transform.Find("enemy corpse effect");
            }
            if (child == null)
            {
                child = transform.Find("enemy corps effect");
            }

            if (child != null)
            {
                corpseEffect = child.GetComponent<ParticleSystem>();
            }
        }
    }

    private void ConfigureCorpseEffect()
    {
        if (corpseEffect == null)
        {
            return;
        }

        ParticleSystem[] particleSystems = corpseEffect.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            ParticleSystem.MainModule main = particleSystem.main;
            main.playOnAwake = false;

            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particleSystem.Clear(true);
        }
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
        ConfigureParticleRenderer(particleSystem, hitSortingOrderOffset);
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

    private static void ConfigureParticleFade(
        ParticleSystem particleSystem,
        float startAlpha,
        float peakAlpha,
        float holdAlpha,
        float endAlpha)
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
}
