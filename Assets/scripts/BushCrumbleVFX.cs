using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BushCrumbleVFX : MonoBehaviour
{
    [Header("Particles")]
    [SerializeField] private ParticleSystem crumbleParticlePrefab;
    [SerializeField] private ParticleSystem[] existingParticles;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private bool forceWorldSimulation = true;
    [SerializeField, Min(0)] private int forcedEmissionCount = 18;
    [SerializeField] private bool boostParticleVisibility = true;
    [SerializeField] private int particleSortingOrderOffset = 8;

    [Header("Vertical Crumble")]
    [SerializeField] private bool useVerticalCrumble = true;
    [SerializeField, Min(0.05f)] private float durationMultiplier = 1.55f;
    [SerializeField, Min(1)] private int crumbleSteps = 6;
    [SerializeField] private Shader crumbleShader;
    [SerializeField, Min(0)] private int particleBursts = 7;
    [SerializeField, Min(0)] private int particlesPerBurst = 5;
    [SerializeField, Range(0.001f, 0.25f)] private float crumbleSoftness = 0.015f;
    [SerializeField, Range(0f, 0.35f)] private float crumbleNoise = 0.22f;

    [Header("Visibility")]
    [SerializeField] private bool hideWhenFinished = true;
    [SerializeField] private Renderer[] renderersToHide;
    [SerializeField] private GameObject[] objectsToHide;

    private readonly List<ParticleSystem> spawnedParticles = new List<ParticleSystem>();
    private RendererState[] rendererStates;
    private ObjectState[] objectStates;
    private Coroutine crumbleRoutine;
    private bool cachedState;

    private struct RendererState
    {
        public Renderer renderer;
        public bool enabled;
        public Material sharedMaterial;
        public Material runtimeMaterial;
    }

    private struct ObjectState
    {
        public GameObject gameObject;
        public bool activeSelf;
    }

    private void Awake()
    {
        if (spawnPoint == null)
        {
            spawnPoint = transform;
        }

        if (crumbleShader == null)
        {
            crumbleShader = Shader.Find("Custom/SpriteVerticalCrumble");
        }

        if (renderersToHide == null || renderersToHide.Length == 0)
        {
            Renderer ownRenderer = GetComponent<Renderer>();
            if (ownRenderer != null)
            {
                renderersToHide = new[] { ownRenderer };
            }
        }
    }

    private void OnDestroy()
    {
        StopCrumbleRoutine();
        DestroyRuntimeMaterials();
    }

    public void Play(float sourceDuration)
    {
        CacheOriginalState();
        StopCrumbleRoutine();
        SetVisible(true);

        float duration = Mathf.Max(0.05f, sourceDuration * durationMultiplier);
        PrepareCrumbleMaterials();
        PrepareParticles(duration);

        crumbleRoutine = StartCoroutine(CrumbleRoutine(duration));
    }

    public void Restore()
    {
        CacheOriginalState();
        StopCrumbleRoutine();
        RestoreRenderers();
        RestoreObjects();
        StopAndClearParticles();
    }

    private IEnumerator CrumbleRoutine(float duration)
    {
        SetCrumbleCutoff(-0.1f);

        int stepCount = Mathf.Max(1, crumbleSteps);
        int burstCount = Mathf.Max(0, particleBursts);
        float elapsed = 0f;
        float nextStepTime = 0f;
        float nextBurstTime = 0f;
        int emittedBursts = 0;

        while (elapsed < duration)
        {
            float normalized = Mathf.Clamp01(elapsed / duration);
            if (elapsed >= nextStepTime)
            {
                float stepped = Mathf.Ceil(normalized * stepCount) / stepCount;
                SetCrumbleCutoff(Mathf.Lerp(-0.1f, 1.1f, stepped));
                nextStepTime += duration / stepCount;
            }

            if (burstCount > 0 && particlesPerBurst > 0 && elapsed >= nextBurstTime)
            {
                EmitParticles(particlesPerBurst);
                emittedBursts++;
                nextBurstTime = duration * Mathf.Clamp01((emittedBursts + 1f) / burstCount);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        SetCrumbleCutoff(1.1f);

        if (hideWhenFinished)
        {
            SetVisible(false);
        }

        crumbleRoutine = null;
    }

    private void CacheOriginalState()
    {
        if (cachedState)
        {
            return;
        }

        int rendererCount = renderersToHide != null ? renderersToHide.Length : 0;
        rendererStates = new RendererState[rendererCount];
        for (int i = 0; i < rendererCount; i++)
        {
            Renderer renderer = renderersToHide[i];
            rendererStates[i] = new RendererState
            {
                renderer = renderer,
                enabled = renderer != null && renderer.enabled,
                sharedMaterial = renderer != null ? renderer.sharedMaterial : null
            };
        }

        int objectCount = objectsToHide != null ? objectsToHide.Length : 0;
        objectStates = new ObjectState[objectCount];
        for (int i = 0; i < objectCount; i++)
        {
            GameObject targetObject = objectsToHide[i];
            objectStates[i] = new ObjectState
            {
                gameObject = targetObject,
                activeSelf = targetObject != null && targetObject.activeSelf
            };
        }

        cachedState = true;
    }

    private void PrepareCrumbleMaterials()
    {
        if (!useVerticalCrumble || crumbleShader == null || rendererStates == null)
        {
            return;
        }

        for (int i = 0; i < rendererStates.Length; i++)
        {
            Renderer renderer = rendererStates[i].renderer;
            if (renderer == null)
            {
                continue;
            }

            Material material = rendererStates[i].runtimeMaterial;
            if (material == null)
            {
                material = new Material(crumbleShader);
                rendererStates[i].runtimeMaterial = material;
            }

            CopyMaterialState(rendererStates[i].sharedMaterial, material, renderer);
            renderer.sharedMaterial = material;
            ConfigureCrumbleBounds(renderer, material);
        }
    }

    private void CopyMaterialState(Material source, Material target, Renderer renderer)
    {
        if (source != null)
        {
            if (source.HasProperty("_MainTex") && target.HasProperty("_MainTex"))
            {
                target.SetTexture("_MainTex", source.GetTexture("_MainTex"));
            }

            if (source.HasProperty("_Color") && target.HasProperty("_Color"))
            {
                target.SetColor("_Color", source.GetColor("_Color"));
            }
        }

        SpriteRenderer spriteRenderer = renderer as SpriteRenderer;
        if (spriteRenderer != null)
        {
            if (spriteRenderer.sprite != null && target.HasProperty("_MainTex"))
            {
                target.SetTexture("_MainTex", spriteRenderer.sprite.texture);
            }

            if (target.HasProperty("_Color"))
            {
                target.SetColor("_Color", spriteRenderer.color);
            }
        }
    }

    private void ConfigureCrumbleBounds(Renderer renderer, Material material)
    {
        Bounds bounds = renderer.localBounds;
        if (material.HasProperty("_CrumbleMinY"))
        {
            material.SetFloat("_CrumbleMinY", bounds.min.y);
        }

        if (material.HasProperty("_CrumbleHeight"))
        {
            material.SetFloat("_CrumbleHeight", Mathf.Max(0.0001f, bounds.size.y));
        }

        if (material.HasProperty("_CrumbleSoftness"))
        {
            material.SetFloat("_CrumbleSoftness", crumbleSoftness);
        }

        if (material.HasProperty("_CrumbleNoise"))
        {
            material.SetFloat("_CrumbleNoise", crumbleNoise);
        }
    }

    private void PrepareParticles(float duration)
    {
        List<ParticleSystem> particles = GetParticles();

        if (crumbleParticlePrefab != null)
        {
            ParticleSystem instance = Instantiate(crumbleParticlePrefab, spawnPoint.position, spawnPoint.rotation, transform);
            spawnedParticles.Add(instance);
            particles.Add(instance);
        }

        foreach (ParticleSystem particleSystem in particles)
        {
            ConfigureParticleSystem(particleSystem, duration);
            particleSystem.Play(true);
        }

        EmitParticles(forcedEmissionCount);
    }

    private List<ParticleSystem> GetParticles()
    {
        List<ParticleSystem> particles = new List<ParticleSystem>();
        if (existingParticles != null)
        {
            for (int i = 0; i < existingParticles.Length; i++)
            {
                if (existingParticles[i] != null)
                {
                    particles.Add(existingParticles[i]);
                }
            }
        }

        return particles;
    }

    private void ConfigureParticleSystem(ParticleSystem particleSystem, float duration)
    {
        if (particleSystem == null)
        {
            return;
        }

        particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        particleSystem.Clear(true);

        ParticleSystem.MainModule main = particleSystem.main;
        main.duration = Mathf.Max(0.05f, duration);
        main.loop = false;
        main.playOnAwake = false;
        if (forceWorldSimulation)
        {
            main.simulationSpace = ParticleSystemSimulationSpace.World;
        }

        ParticleSystem.EmissionModule emission = particleSystem.emission;
        emission.enabled = false;

        ParticleSystemRenderer particleRenderer = particleSystem.GetComponent<ParticleSystemRenderer>();
        if (boostParticleVisibility && particleRenderer != null)
        {
            Renderer referenceRenderer = GetReferenceRenderer();
            if (referenceRenderer != null)
            {
                particleRenderer.sortingLayerID = referenceRenderer.sortingLayerID;
                particleRenderer.sortingOrder = referenceRenderer.sortingOrder + particleSortingOrderOffset;
            }
        }
    }

    private void EmitParticles(int count)
    {
        if (count <= 0)
        {
            return;
        }

        foreach (ParticleSystem particleSystem in GetParticles())
        {
            particleSystem.Emit(count);
        }

        for (int i = 0; i < spawnedParticles.Count; i++)
        {
            if (spawnedParticles[i] != null)
            {
                spawnedParticles[i].Emit(count);
            }
        }
    }

    private Renderer GetReferenceRenderer()
    {
        if (rendererStates != null)
        {
            for (int i = 0; i < rendererStates.Length; i++)
            {
                if (rendererStates[i].renderer != null)
                {
                    return rendererStates[i].renderer;
                }
            }
        }

        return GetComponent<Renderer>();
    }

    private void SetCrumbleCutoff(float cutoff)
    {
        if (rendererStates == null)
        {
            return;
        }

        for (int i = 0; i < rendererStates.Length; i++)
        {
            Material material = rendererStates[i].runtimeMaterial;
            if (material != null && material.HasProperty("_CrumbleCutoff"))
            {
                material.SetFloat("_CrumbleCutoff", cutoff);
            }
        }
    }

    private void SetVisible(bool visible)
    {
        if (rendererStates != null)
        {
            for (int i = 0; i < rendererStates.Length; i++)
            {
                if (rendererStates[i].renderer != null)
                {
                    rendererStates[i].renderer.enabled = visible;
                }
            }
        }

        if (objectStates != null)
        {
            for (int i = 0; i < objectStates.Length; i++)
            {
                if (objectStates[i].gameObject != null)
                {
                    objectStates[i].gameObject.SetActive(visible);
                }
            }
        }
    }

    private void RestoreRenderers()
    {
        if (rendererStates == null)
        {
            return;
        }

        for (int i = 0; i < rendererStates.Length; i++)
        {
            RendererState state = rendererStates[i];
            if (state.renderer == null)
            {
                continue;
            }

            state.renderer.sharedMaterial = state.sharedMaterial;
            state.renderer.enabled = state.enabled;
        }
    }

    private void RestoreObjects()
    {
        if (objectStates == null)
        {
            return;
        }

        for (int i = 0; i < objectStates.Length; i++)
        {
            ObjectState state = objectStates[i];
            if (state.gameObject != null)
            {
                state.gameObject.SetActive(state.activeSelf);
            }
        }
    }

    private void StopAndClearParticles()
    {
        foreach (ParticleSystem particleSystem in GetParticles())
        {
            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particleSystem.Clear(true);
        }

        for (int i = spawnedParticles.Count - 1; i >= 0; i--)
        {
            if (spawnedParticles[i] != null)
            {
                Destroy(spawnedParticles[i].gameObject);
            }
        }

        spawnedParticles.Clear();
    }

    private void StopCrumbleRoutine()
    {
        if (crumbleRoutine != null)
        {
            StopCoroutine(crumbleRoutine);
            crumbleRoutine = null;
        }
    }

    private void DestroyRuntimeMaterials()
    {
        if (rendererStates == null)
        {
            return;
        }

        for (int i = 0; i < rendererStates.Length; i++)
        {
            if (rendererStates[i].runtimeMaterial != null)
            {
                Destroy(rendererStates[i].runtimeMaterial);
                rendererStates[i].runtimeMaterial = null;
            }
        }
    }
}
