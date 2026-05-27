using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BreakableTimedPlatform : MonoBehaviour
{
    private sealed class ShardInstance
    {
        public GameObject gameObject;
        public Rigidbody2D body;
        public Mesh mesh;
        public Vector2 localCentroid;
    }

    [SerializeField] private float breakDelay = 0.9f;
    [SerializeField] private float respawnDelay = 1.5f;
    [SerializeField] private LayerMask triggerLayer = 8192;

    [Header("Crumble Visual")]
    [SerializeField] private SpriteRenderer visualTarget;
    [SerializeField] private Material shardMaterial;
    [SerializeField] private float explosionForce = 0.5f;
    [SerializeField] private float upwardForce;
    [SerializeField] private float torqueForce;
    [SerializeField, Min(0.05f)] private float pieceLifetime = 2f;
    [SerializeField] private bool useDeterministicSeed;
    [SerializeField] private int seed;

    [Header("Wood Texture Mask")]
    [SerializeField] private Vector2Int maskResolution = new Vector2Int(800, 500);
    [SerializeField, Range(1f, 50f)] private float scaleX = 8.2f;
    [SerializeField, Range(10f, 200f)] private float scaleY = 30f;
    [SerializeField, Range(0.3f, 0.7f)] private float threshold = 0.458f;
    [SerializeField, Range(0f, 50f)] private float warp;
    [SerializeField, Range(1, 8)] private int noiseOctaves = 6;
    [SerializeField, Range(0f, 1f)] private float noiseFalloff = 0.59f;
    [SerializeField, Range(0.001f, 0.12f)] private float edgeSmoothness = 0.12f;
    [SerializeField, Range(0f, 0.5f)] private float edgeDetailStrength = 0.283f;
    [SerializeField, Min(1)] private int minIslandArea = 8;
    [SerializeField, Min(1)] private int maxPieces = 50;
    [SerializeField] private bool showMaskPreview = true;
    [SerializeField, Range(0.05f, 0.9f)] private float maskPreviewAlpha = 0.45f;

    [Header("Crumble Animation")]
    [SerializeField, Min(0f)] private float releaseDelayJitter = 0.06f;
    [SerializeField, Range(1f, 5f)] private float crumbleAcceleration = 2.25f;

    [Header("Performance")]
    [SerializeField] private bool prewarmShardsOnStart = true;

    [Header("Linked Crumble VFX")]
    [SerializeField] private BushCrumbleVFX[] linkedCrumbleVFX;
    [SerializeField] private bool autoFindChildCrumbleVFX = true;

    private readonly List<ShardInstance> shardCache = new List<ShardInstance>();
    private Collider2D[] colliders;
    private Renderer[] renderers;
    private GameObject shardContainer;
    private Material cachedShardMaterial;
    private Sprite cachedSprite;
    private bool busy;
    private bool warnedMissingVisual;
    private int shardActivationVersion;

    private void Awake()
    {
        colliders = GetComponentsInChildren<Collider2D>();
        renderers = GetComponentsInChildren<Renderer>();
    }

    private void Start()
    {
        if (prewarmShardsOnStart)
        {
            BuildShardCache(CreateRandom());
        }
    }

    private void OnDestroy()
    {
        ClearShardCache();
    }

    private void Reset()
    {
        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer >= 0)
        {
            triggerLayer = 1 << playerLayer;
        }
    }

    private void OnDrawGizmos()
    {
        if (!showMaskPreview || visualTarget == null || visualTarget.sprite == null)
        {
            return;
        }
        bool[] mask = GenerateMask(CreatePreviewRandom(), out int width, out int height);
        WoodCrumbleMaskPreview.Draw(visualTarget, mask, width, height, maskPreviewAlpha);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryTrigger(other);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryTrigger(collision.collider);
    }

    private void TryTrigger(Collider2D other)
    {
        if (busy || other == null || ((1 << other.gameObject.layer) & triggerLayer.value) == 0)
        {
            return;
        }
        StartCoroutine(BreakRoutine());
    }

    private IEnumerator BreakRoutine()
    {
        busy = true;
        float crumbleDuration = Mathf.Max(0f, breakDelay);
        PlayLinkedCrumbleVFX(crumbleDuration);
        bool spawnedShards = TrySpawnShards(crumbleDuration);

        if (spawnedShards)
        {
            SetVisualTargetEnabled(false);
        }
        else
        {
            WarnMissingVisual();
        }
        if (crumbleDuration > 0f)
        {
            yield return new WaitForSeconds(crumbleDuration);
        }

        SetEnabled(false);
        SetVisualTargetEnabled(false);

        yield return new WaitForSeconds(respawnDelay);
        SetEnabled(true);
        SetVisualTargetEnabled(true);
        RestoreLinkedCrumbleVFX();
        busy = false;
    }

    private bool TrySpawnShards(float crumbleDuration)
    {
        if (visualTarget == null || visualTarget.sprite == null)
        {
            return false;
        }

        System.Random random = CreateRandom();
        if ((cachedSprite != visualTarget.sprite || shardCache.Count == 0) && !BuildShardCache(random))
        {
            return false;
        }

        Rect localBounds = GetSpriteLocalBounds(visualTarget.sprite);
        shardActivationVersion++;
        shardContainer.SetActive(true);

        foreach (ShardInstance shard in shardCache)
        {
            Vector3 worldPosition = visualTarget.transform.TransformPoint(SpriteShardMeshBuilder.ApplyFlip(shard.localCentroid, visualTarget));
            ResetShard(shard, worldPosition);

            float releaseDelay = GetReleaseDelay(shard.localCentroid, localBounds, crumbleDuration, random);
            StartCoroutine(ReleaseShard(shard.body, worldPosition, releaseDelay, random));
        }

        StartCoroutine(DeactivateShardGroup(pieceLifetime + crumbleDuration, shardActivationVersion));
        return true;
    }

    private bool BuildShardCache(System.Random random)
    {
        ClearShardCache();

        if (visualTarget == null || visualTarget.sprite == null)
        {
            return false;
        }

        Sprite sprite = visualTarget.sprite;
        Rect localBounds = GetSpriteLocalBounds(sprite);
        bool[] mask = GenerateMask(random, out int maskWidth, out int maskHeight);
        List<List<int>> regions = WoodCrumbleRegionBuilder.Build(mask, maskWidth, maskHeight, minIslandArea, maxPieces);

        if (regions.Count == 0)
        {
            return false;
        }

        shardContainer = new GameObject($"{visualTarget.name}_CrumbleShards");
        shardContainer.SetActive(false);
        cachedShardMaterial = CreateShardMaterial(sprite);
        cachedSprite = sprite;

        foreach (List<int> region in regions)
        {
            Mesh mesh = SpriteShardMeshBuilder.Create(sprite, localBounds, region, maskWidth, maskHeight, visualTarget, out Vector2 centroid);
            if (mesh != null)
            {
                shardCache.Add(CreateShardInstance(mesh, centroid));
            }
        }

        if (shardCache.Count > 0)
        {
            return true;
        }

        ClearShardCache();
        return false;
    }

    private ShardInstance CreateShardInstance(Mesh mesh, Vector2 centroid)
    {
        GameObject shardObject = new GameObject($"{visualTarget.name}_Chip");
        shardObject.transform.SetParent(shardContainer.transform, true);
        shardObject.SetActive(false);

        shardObject.AddComponent<MeshFilter>().sharedMesh = mesh;

        MeshRenderer meshRenderer = shardObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = cachedShardMaterial;
        meshRenderer.sortingLayerID = visualTarget.sortingLayerID;
        meshRenderer.sortingOrder = visualTarget.sortingOrder;

        Rigidbody2D body = shardObject.AddComponent<Rigidbody2D>();
        body.simulated = false;
        body.gravityScale = 1f;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;

        return new ShardInstance
        {
            gameObject = shardObject,
            body = body,
            mesh = mesh,
            localCentroid = centroid
        };
    }

    private void ResetShard(ShardInstance shard, Vector3 worldPosition)
    {
        Transform shardTransform = shard.gameObject.transform;
        shardTransform.position = worldPosition;
        shardTransform.rotation = visualTarget.transform.rotation;
        shardTransform.localScale = visualTarget.transform.lossyScale;
        shard.gameObject.layer = visualTarget.gameObject.layer;
        shard.gameObject.SetActive(true);

        shard.body.simulated = false;
        shard.body.linearVelocity = Vector2.zero;
        shard.body.angularVelocity = 0f;
    }

    private void ClearShardCache()
    {
        foreach (ShardInstance shard in shardCache)
        {
            if (shard.mesh != null)
            {
                Destroy(shard.mesh);
            }
        }

        shardCache.Clear();

        if (cachedShardMaterial != null)
        {
            Destroy(cachedShardMaterial);
            cachedShardMaterial = null;
        }

        if (shardContainer != null)
        {
            Destroy(shardContainer);
            shardContainer = null;
        }

        cachedSprite = null;
    }

    private bool[] GenerateMask(System.Random random, out int width, out int height)
    {
        return WoodCrumbleMaskGenerator.Generate(
            maskResolution,
            scaleX,
            scaleY,
            threshold,
            warp,
            noiseOctaves,
            noiseFalloff,
            edgeSmoothness,
            edgeDetailStrength,
            random,
            out width,
            out height);
    }

    private Material CreateShardMaterial(Sprite sprite)
    {
        Material material = shardMaterial != null ? new Material(shardMaterial) : CreateDefaultShardMaterial();
        if (material == null)
        {
            return null;
        }

        if (material.HasProperty("_MainTex"))
        {
            material.SetTexture("_MainTex", sprite.texture);
        }

        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", sprite.texture);
        }

        if (material.HasProperty("_Cull"))
        {
            material.SetFloat("_Cull", 0f);
        }

        if (visualTarget != null)
        {
            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", visualTarget.color);
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", visualTarget.color);
            }
        }

        return material;
    }

    private Material CreateDefaultShardMaterial()
    {
        Shader shader = Shader.Find("Sprites/Default")
            ?? Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
            ?? Shader.Find("Universal Render Pipeline/Unlit");

        if (shader != null)
        {
            return new Material(shader);
        }

        Material source = visualTarget != null ? visualTarget.sharedMaterial : null;
        return source != null ? new Material(source) : null;
    }

    private void SetEnabled(bool enabled)
    {
        if (colliders != null)
        {
            foreach (Collider2D platformCollider in colliders)
            {
                if (platformCollider != null)
                {
                    platformCollider.enabled = enabled;
                }
            }
        }

        if (renderers != null)
        {
            foreach (Renderer platformRenderer in renderers)
            {
                if (platformRenderer != null)
                {
                    platformRenderer.enabled = enabled;
                }
            }
        }
    }

    private void SetVisualTargetEnabled(bool enabled)
    {
        if (visualTarget != null)
        {
            visualTarget.enabled = enabled;
        }
    }

    private void PlayLinkedCrumbleVFX(float duration)
    {
        List<BushCrumbleVFX> crumbleVFX = GetLinkedCrumbleVFX();
        for (int i = 0; i < crumbleVFX.Count; i++)
        {
            if (crumbleVFX[i] != null)
            {
                crumbleVFX[i].Play(duration);
            }
        }
    }

    private void RestoreLinkedCrumbleVFX()
    {
        List<BushCrumbleVFX> crumbleVFX = GetLinkedCrumbleVFX();
        for (int i = 0; i < crumbleVFX.Count; i++)
        {
            if (crumbleVFX[i] != null)
            {
                crumbleVFX[i].Restore();
            }
        }
    }

    private List<BushCrumbleVFX> GetLinkedCrumbleVFX()
    {
        List<BushCrumbleVFX> results = new List<BushCrumbleVFX>();
        HashSet<BushCrumbleVFX> seen = new HashSet<BushCrumbleVFX>();

        AddLinkedCrumbleVFX(linkedCrumbleVFX, results, seen);

        if (autoFindChildCrumbleVFX)
        {
            AddLinkedCrumbleVFX(GetComponentsInChildren<BushCrumbleVFX>(true), results, seen);

            if (visualTarget != null && visualTarget.transform.parent != null)
            {
                AddLinkedCrumbleVFX(visualTarget.transform.parent.GetComponentsInChildren<BushCrumbleVFX>(true), results, seen);
            }
        }

        return results;
    }

    private static void AddLinkedCrumbleVFX(BushCrumbleVFX[] source, List<BushCrumbleVFX> results, HashSet<BushCrumbleVFX> seen)
    {
        if (source == null)
        {
            return;
        }

        for (int i = 0; i < source.Length; i++)
        {
            BushCrumbleVFX crumbleVFX = source[i];
            if (crumbleVFX != null && seen.Add(crumbleVFX))
            {
                results.Add(crumbleVFX);
            }
        }
    }

    private IEnumerator ReleaseShard(Rigidbody2D body, Vector3 worldPosition, float delay, System.Random random)
    {
        yield return new WaitForSeconds(delay);

        if (body != null)
        {
            body.simulated = true;
            ApplyShardImpulse(body, worldPosition, random);
        }
    }

    private IEnumerator DeactivateShardGroup(float delay, int activationVersion)
    {
        yield return new WaitForSeconds(delay);

        if (activationVersion != shardActivationVersion)
        {
            yield break;
        }

        foreach (ShardInstance shard in shardCache)
        {
            if (shard.body != null)
            {
                shard.body.simulated = false;
            }

            if (shard.gameObject != null)
            {
                shard.gameObject.SetActive(false);
            }
        }

        if (shardContainer != null)
        {
            shardContainer.SetActive(false);
        }
    }

    private void ApplyShardImpulse(Rigidbody2D body, Vector3 worldPosition, System.Random random)
    {
        Vector3 worldCenter = visualTarget.transform.TransformPoint(SpriteShardMeshBuilder.ApplyFlip(visualTarget.sprite.bounds.center, visualTarget));
        Vector2 direction = (Vector2)(worldPosition - worldCenter);
        direction = direction.sqrMagnitude < 0.0001f ? RandomInsideUnitCircle(random).normalized : direction.normalized;

        body.AddForce(direction * explosionForce + Vector2.up * upwardForce, ForceMode2D.Impulse);
        body.AddTorque(RandomRange(random, -torqueForce, torqueForce), ForceMode2D.Impulse);
    }

    private float GetReleaseDelay(Vector2 localCentroid, Rect localBounds, float crumbleDuration, System.Random random)
    {
        if (crumbleDuration <= 0f)
        {
            return 0f;
        }

        float normalizedHeight = Mathf.InverseLerp(localBounds.yMin, localBounds.yMax, localCentroid.y);
        float easedHeight = 1f - Mathf.Pow(1f - normalizedHeight, crumbleAcceleration);
        float jitterMax = Mathf.Min(releaseDelayJitter, crumbleDuration);
        return easedHeight * Mathf.Max(0f, crumbleDuration - jitterMax) + RandomRange(random, 0f, jitterMax);
    }

    private void WarnMissingVisual()
    {
        if (!warnedMissingVisual)
        {
            warnedMissingVisual = true;
            Debug.LogWarning($"{nameof(BreakableTimedPlatform)} on {name} could not create crumble shards. Assign a Visual Target sprite and check mask settings.", this);
        }
    }

    private System.Random CreateRandom()
    {
        if (useDeterministicSeed)
        {
            return new System.Random(seed);
        }

        unchecked
        {
            int runtimeSeed = System.Environment.TickCount;
            runtimeSeed = (runtimeSeed * 397) ^ gameObject.GetHashCode();
            runtimeSeed = (runtimeSeed * 397) ^ Time.frameCount;
            return new System.Random(runtimeSeed);
        }
    }

    private System.Random CreatePreviewRandom()
    {
        return new System.Random(useDeterministicSeed ? seed : 0);
    }

    private static Rect GetSpriteLocalBounds(Sprite sprite)
    {
        Bounds bounds = sprite.bounds;
        return new Rect(bounds.min.x, bounds.min.y, bounds.size.x, bounds.size.y);
    }

    private static Vector2 RandomInsideUnitCircle(System.Random random)
    {
        float angle = RandomRange(random, 0f, Mathf.PI * 2f);
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
    }

    private static float RandomRange(System.Random random, float min, float max)
    {
        return min + (float)random.NextDouble() * (max - min);
    }
}
