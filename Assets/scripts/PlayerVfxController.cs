using UnityEngine;

public class PlayerVfxController : MonoBehaviour
{
    [Header("Hit Feedback")]
    [SerializeField] private ParticleSystem hitParticles;
    [SerializeField, Min(0)] private int hitParticleCount = 35;

    [Header("Life Drain Feedback")]
    [SerializeField] private ParticleSystem lifeDrainParticles;

    [Header("Hit Direction")]
    [SerializeField] private player playerController;
    [SerializeField] private bool orientHitVelocityByFacing = true;
    [SerializeField, Min(0f)] private float hitBackVelocityX = 6f;

    private void Awake()
    {
        ResolveReferences();
        ConfigureHitParticles();
        ConfigureLifeDrainParticles();
        StopLifeDrainFeedback();
    }

    public void PlayHitFeedback()
    {
        PlayHitFeedback(GetCurrentFacingDirection());
    }

    public void PlayHitFeedback(int facingDirection)
    {
        ResolveReferences();

        if (hitParticles == null || hitParticleCount <= 0)
        {
            return;
        }

        ApplyHitDirection(facingDirection);

        if (!hitParticles.isPlaying)
        {
            hitParticles.Play(false);
        }

        hitParticles.Emit(hitParticleCount);
    }

    public void PlayHitFeedbackFromSource(Vector2 sourcePosition)
    {
        int facingForVelocityAwayFromSource = sourcePosition.x < transform.position.x ? -1 : 1;
        PlayHitFeedback(facingForVelocityAwayFromSource);
    }

    public void ClearHitFeedback()
    {
        if (hitParticles == null)
        {
            return;
        }

        hitParticles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    public void StartLifeDrainFeedback()
    {
        ResolveReferences();

        if (lifeDrainParticles == null)
        {
            return;
        }

        ConfigureLifeDrainParticles();
        lifeDrainParticles.Play(true);
    }

    public void StopLifeDrainFeedback()
    {
        if (lifeDrainParticles == null)
        {
            return;
        }

        lifeDrainParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void ResolveReferences()
    {
        if (playerController == null)
        {
            playerController = GetComponent<player>();
        }

        if (hitParticles == null)
        {
            Transform hitVfx = transform.Find("sprite/Hit vfx");
            if (hitVfx == null)
            {
                hitVfx = transform.Find("Hit vfx");
            }

            if (hitVfx != null)
            {
                hitParticles = hitVfx.GetComponent<ParticleSystem>();
            }
        }

        if (lifeDrainParticles == null)
        {
            Transform lifeDrainVfx = transform.Find("sprite/Life Drain vfx");
            if (lifeDrainVfx == null)
            {
                lifeDrainVfx = transform.Find("sprite/life drain vfx");
            }
            if (lifeDrainVfx == null)
            {
                lifeDrainVfx = transform.Find("Life Drain vfx");
            }
            if (lifeDrainVfx == null)
            {
                lifeDrainVfx = transform.Find("life drain vfx");
            }

            if (lifeDrainVfx != null)
            {
                lifeDrainParticles = lifeDrainVfx.GetComponent<ParticleSystem>();
            }

            if (lifeDrainParticles == null)
            {
                Transform sprite = transform.Find("sprite");
                if (sprite != null)
                {
                    ParticleSystem spriteParticles = sprite.GetComponent<ParticleSystem>();
                    if (spriteParticles != hitParticles)
                    {
                        lifeDrainParticles = spriteParticles;
                    }
                }
            }
        }
    }

    private int GetCurrentFacingDirection()
    {
        if (transform.localScale.x < 0f)
        {
            return -1;
        }

        if (transform.localScale.x > 0f)
        {
            return 1;
        }

        if (playerController != null && playerController.facingDirection != 0)
        {
            return playerController.facingDirection;
        }

        return 1;
    }

    private void ApplyHitDirection(int facingDirection)
    {
        if (!orientHitVelocityByFacing || hitParticles == null)
        {
            return;
        }

        int facing = facingDirection != 0 ? facingDirection : GetCurrentFacingDirection();

        ParticleSystem.VelocityOverLifetimeModule velocity = hitParticles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-facing * Mathf.Abs(hitBackVelocityX));
    }

    private void ConfigureHitParticles()
    {
        if (hitParticles == null)
        {
            return;
        }

        ParticleSystem.MainModule main = hitParticles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.None;

        ParticleSystem.EmissionModule emission = hitParticles.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.rateOverDistance = 0f;
        emission.SetBursts(System.Array.Empty<ParticleSystem.Burst>());
    }

    private void ConfigureLifeDrainParticles()
    {
        if (lifeDrainParticles == null)
        {
            return;
        }

        ParticleSystem.MainModule main = lifeDrainParticles.main;
        main.loop = true;
        main.playOnAwake = false;

        ParticleSystem.EmissionModule emission = lifeDrainParticles.emission;
        emission.enabled = true;
    }
}
