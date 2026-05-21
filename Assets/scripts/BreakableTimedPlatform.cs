using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BreakableTimedPlatform : MonoBehaviour
{
    private sealed class MaskRegion
    {
        public readonly List<int> cells = new List<int>();
        public float centerX;
        public float centerY;

        public void FinalizeCenter()
        {
            if (cells.Count == 0)
            {
                return;
            }

            centerX /= cells.Count;
            centerY /= cells.Count;
        }
    }

    private sealed class ShardInstance
    {
        public GameObject gameObject;
        public Rigidbody2D body;
        public Mesh mesh;
        public Vector2 localCentroid;
    }

    [SerializeField] private float breakDelay = 0.9f;
    [SerializeField] private float respawnDelay = 0.9f;
    [SerializeField] private LayerMask triggerLayer;

    [Header("Crumble Visual")]
    [SerializeField] private SpriteRenderer visualTarget;
    [SerializeField] private Material shardMaterial;
    [SerializeField] private float explosionForce = 2.5f;
    [SerializeField] private float upwardForce = 1.5f;
    [SerializeField] private float torqueForce = 180f;
    [SerializeField, Min(0.05f)] private float pieceLifetime = 2f;
    [SerializeField] private bool useDeterministicSeed;
    [SerializeField] private int seed;

    [Header("Wood Texture Mask")]
    [SerializeField] private Vector2Int maskResolution = new Vector2Int(128, 72);
    [SerializeField, Range(1f, 50f)] private float scaleX = 15f;
    [SerializeField, Range(10f, 200f)] private float scaleY = 85f;
    [SerializeField, Range(0.3f, 0.7f)] private float threshold = 0.52f;
    [SerializeField, Range(0f, 50f)] private float warp = 15f;
    [SerializeField, Range(1, 8)] private int noiseOctaves = 4;
    [SerializeField, Range(0f, 1f)] private float noiseFalloff = 0.5f;
    [SerializeField, Range(0.001f, 0.12f)] private float edgeSmoothness = 0.03f;
    [SerializeField, Range(0f, 0.5f)] private float edgeDetailStrength = 0.12f;
    [SerializeField, Min(1)] private int minIslandArea = 8;
    [SerializeField, Min(1)] private int maxPieces = 48;
    [SerializeField] private bool invertMask;
    [SerializeField] private bool showMaskPreview;
    [SerializeField, Range(0.05f, 0.9f)] private float maskPreviewAlpha = 0.45f;

    [Header("Crumble Animation")]
    [SerializeField, Min(0f)] private float releaseDelayJitter = 0.06f;

    [Header("Performance")]
    [SerializeField] private bool prewarmShardsOnStart = true;
    [SerializeField] private bool regenerateShardsEveryBreak;

    private Collider2D[] colliders;
    private Renderer[] renderers;
    private readonly List<ShardInstance> shardCache = new List<ShardInstance>();
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

        DrawMaskPreview();
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
        if (busy || other == null)
        {
            return;
        }

        if (((1 << other.gameObject.layer) & triggerLayer.value) == 0)
        {
            return;
        }

        StartCoroutine(BreakRoutine());
    }

    private IEnumerator BreakRoutine()
    {
        busy = true;
        float crumbleDuration = Mathf.Max(0f, breakDelay);
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

        busy = false;
    }

    private void SetEnabled(bool enabled)
    {
        if (colliders != null)
        {
            foreach (Collider2D collider in colliders)
            {
                if (collider != null)
                {
                    collider.enabled = enabled;
                }
            }
        }

        if (renderers != null)
        {
            foreach (Renderer renderer in renderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = enabled;
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

    private bool TrySpawnShards(float crumbleDuration)
    {
        if (visualTarget == null || visualTarget.sprite == null)
        {
            return false;
        }

        System.Random random = CreateRandom();

        if (regenerateShardsEveryBreak || cachedSprite != visualTarget.sprite || shardCache.Count == 0)
        {
            if (!BuildShardCache(random))
            {
                return false;
            }
        }

        if (shardContainer != null)
        {
            shardContainer.SetActive(true);
        }

        Bounds bounds = visualTarget.sprite.bounds;
        Rect localBounds = new Rect(bounds.min.x, bounds.min.y, bounds.size.x, bounds.size.y);
        shardActivationVersion++;

        foreach (ShardInstance shard in shardCache)
        {
            Vector2 displayCentroid = ApplySpriteFlip(shard.localCentroid);
            Vector3 worldPosition = visualTarget.transform.TransformPoint(displayCentroid);
            shard.gameObject.transform.position = worldPosition;
            shard.gameObject.transform.rotation = visualTarget.transform.rotation;
            shard.gameObject.transform.localScale = visualTarget.transform.lossyScale;
            shard.gameObject.layer = visualTarget.gameObject.layer;
            shard.gameObject.SetActive(true);

            if (shard.body != null)
            {
                shard.body.simulated = false;
                shard.body.linearVelocity = Vector2.zero;
                shard.body.angularVelocity = 0f;
            }

            float releaseDelay = GetBottomToTopReleaseDelay(shard.localCentroid, localBounds, crumbleDuration, random);
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
        Bounds bounds = sprite.bounds;
        Rect localBounds = new Rect(bounds.min.x, bounds.min.y, bounds.size.x, bounds.size.y);
        List<List<int>> regions = GenerateContrastMaskRegions(random, out int maskWidth, out int maskHeight);

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
            Mesh mesh = CreateShardMesh(sprite, localBounds, region, maskWidth, maskHeight, out Vector2 localCentroid);
            if (mesh == null)
            {
                continue;
            }

            GameObject shardObject = new GameObject($"{visualTarget.name}_Chip");
            shardObject.transform.SetParent(shardContainer.transform, true);
            shardObject.SetActive(false);

            MeshFilter meshFilter = shardObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;

            MeshRenderer meshRenderer = shardObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = cachedShardMaterial;
            meshRenderer.sortingLayerID = visualTarget.sortingLayerID;
            meshRenderer.sortingOrder = visualTarget.sortingOrder;

            Rigidbody2D body = shardObject.AddComponent<Rigidbody2D>();
            body.simulated = false;
            body.gravityScale = 1f;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            shardCache.Add(new ShardInstance
            {
                gameObject = shardObject,
                body = body,
                mesh = mesh,
                localCentroid = localCentroid
            });
        }

        if (shardCache.Count > 0)
        {
            return true;
        }

        ClearShardCache();
        return false;
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

    private List<List<int>> GenerateContrastMaskRegions(System.Random random, out int width, out int height)
    {
        bool[] mask = GenerateContrastMask(random, out width, out height);
        int totalCells = mask.Length;

        List<MaskRegion> regions = FloodFillRegions(mask, width, height);
        if (regions.Count == 0)
        {
            return new List<List<int>>();
        }

        regions.Sort((left, right) => right.cells.Count.CompareTo(left.cells.Count));
        return MergeAndBuildShardCells(regions, width, height, totalCells);
    }

    private bool[] GenerateContrastMask(System.Random random, out int width, out int height)
    {
        width = Mathf.Clamp(maskResolution.x, 16, 256);
        height = Mathf.Clamp(maskResolution.y, 8, 256);
        bool[] mask = new bool[width * height];
        Vector2 noiseOffset = new Vector2(RandomRange(random, -10000f, 10000f), RandomRange(random, -10000f, 10000f));

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float normalizedX = x / (float)width;
                float normalizedY = y / (float)height;
                float warpNoise = SampleFractalNoise(
                    normalizedX * 5f,
                    normalizedY * 5f,
                    noiseOffset,
                    Mathf.Max(1, noiseOctaves - 1),
                    noiseFalloff) * (warp / 100f);
                float noiseValue = SampleFractalNoise(
                    normalizedX * scaleX,
                    (normalizedY + warpNoise) * scaleY,
                    noiseOffset * 0.37f,
                    noiseOctaves,
                    noiseFalloff);
                float edgeBand = Mathf.Max(0.0001f, edgeSmoothness);
                float thresholdValue = threshold;

                if (noiseValue > threshold - edgeBand && noiseValue < threshold + edgeBand)
                {
                    float edgeNoise = SampleFractalNoise(
                        normalizedX * scaleX * 3.25f,
                        (normalizedY + warpNoise) * scaleY * 1.65f,
                        noiseOffset * 1.91f,
                        Mathf.Max(1, noiseOctaves - 1),
                        noiseFalloff);
                    thresholdValue += (edgeNoise - 0.5f) * edgeDetailStrength;
                }

                bool isBlack = noiseValue > thresholdValue;

                if (invertMask)
                {
                    isBlack = !isBlack;
                }

                mask[ToCellIndex(x, y, width)] = isBlack;
            }
        }

        return mask;
    }

    private static float SampleFractalNoise(float x, float y, Vector2 offset, int octaves, float falloff)
    {
        float amplitude = 1f;
        float frequency = 1f;
        float value = 0f;
        float amplitudeTotal = 0f;
        int octaveCount = Mathf.Max(1, octaves);
        float persistence = Mathf.Clamp01(falloff);

        for (int octave = 0; octave < octaveCount; octave++)
        {
            value += Mathf.PerlinNoise(
                x * frequency + offset.x,
                y * frequency + offset.y) * amplitude;
            amplitudeTotal += amplitude;
            amplitude *= persistence;
            frequency *= 2f;
        }

        if (amplitudeTotal <= 0f)
        {
            return 0f;
        }

        return value / amplitudeTotal;
    }

    private List<MaskRegion> FloodFillRegions(bool[] mask, int width, int height)
    {
        bool[] visited = new bool[mask.Length];
        var regions = new List<MaskRegion>();
        var pending = new Queue<int>();

        for (int startIndex = 0; startIndex < mask.Length; startIndex++)
        {
            if (visited[startIndex])
            {
                continue;
            }

            bool regionValue = mask[startIndex];
            var region = new MaskRegion();
            visited[startIndex] = true;
            pending.Enqueue(startIndex);

            while (pending.Count > 0)
            {
                int cell = pending.Dequeue();
                int x = cell % width;
                int y = cell / width;
                region.cells.Add(cell);
                region.centerX += x + 0.5f;
                region.centerY += y + 0.5f;

                TryQueueNeighbor(x - 1, y, width, height, regionValue, mask, visited, pending);
                TryQueueNeighbor(x + 1, y, width, height, regionValue, mask, visited, pending);
                TryQueueNeighbor(x, y - 1, width, height, regionValue, mask, visited, pending);
                TryQueueNeighbor(x, y + 1, width, height, regionValue, mask, visited, pending);
            }

            region.FinalizeCenter();
            regions.Add(region);
        }

        return regions;
    }

    private static void TryQueueNeighbor(
        int x,
        int y,
        int width,
        int height,
        bool regionValue,
        bool[] mask,
        bool[] visited,
        Queue<int> pending)
    {
        if (x < 0 || x >= width || y < 0 || y >= height)
        {
            return;
        }

        int index = ToCellIndex(x, y, width);
        if (visited[index] || mask[index] != regionValue)
        {
            return;
        }

        visited[index] = true;
        pending.Enqueue(index);
    }

    private List<List<int>> MergeAndBuildShardCells(List<MaskRegion> regions, int width, int height, int totalCells)
    {
        int pieceLimit = Mathf.Clamp(maxPieces, 1, regions.Count);
        int minimumArea = Mathf.Max(1, minIslandArea);
        var keptRegions = new List<MaskRegion>(pieceLimit);

        foreach (MaskRegion region in regions)
        {
            if (keptRegions.Count >= pieceLimit)
            {
                break;
            }

            if (region.cells.Count >= minimumArea)
            {
                keptRegions.Add(region);
            }
        }

        if (keptRegions.Count == 0)
        {
            keptRegions.Add(regions[0]);
        }

        int[] assignedRegionByCell = new int[totalCells];
        for (int i = 0; i < assignedRegionByCell.Length; i++)
        {
            assignedRegionByCell[i] = -1;
        }

        for (int keptIndex = 0; keptIndex < keptRegions.Count; keptIndex++)
        {
            foreach (int cell in keptRegions[keptIndex].cells)
            {
                assignedRegionByCell[cell] = keptIndex;
            }
        }

        FillUnassignedCells(assignedRegionByCell, width, height, keptRegions);

        var shardCells = new List<List<int>>(keptRegions.Count);
        for (int i = 0; i < keptRegions.Count; i++)
        {
            shardCells.Add(new List<int>());
        }

        for (int cell = 0; cell < assignedRegionByCell.Length; cell++)
        {
            int keptIndex = assignedRegionByCell[cell];
            if (keptIndex >= 0)
            {
                shardCells[keptIndex].Add(cell);
            }
        }

        return shardCells;
    }

    private static void FillUnassignedCells(int[] assignedRegionByCell, int width, int height, List<MaskRegion> keptRegions)
    {
        int remaining = 0;
        for (int i = 0; i < assignedRegionByCell.Length; i++)
        {
            if (assignedRegionByCell[i] < 0)
            {
                remaining++;
            }
        }

        int safety = assignedRegionByCell.Length;
        while (remaining > 0 && safety > 0)
        {
            bool madeProgress = false;

            for (int cell = 0; cell < assignedRegionByCell.Length; cell++)
            {
                if (assignedRegionByCell[cell] >= 0)
                {
                    continue;
                }

                int x = cell % width;
                int y = cell / width;
                int neighborRegion = GetAssignedNeighbor(assignedRegionByCell, x, y, width, height);
                if (neighborRegion < 0)
                {
                    continue;
                }

                assignedRegionByCell[cell] = neighborRegion;
                remaining--;
                madeProgress = true;
            }

            if (!madeProgress)
            {
                break;
            }

            safety--;
        }

        if (remaining <= 0)
        {
            return;
        }

        for (int cell = 0; cell < assignedRegionByCell.Length; cell++)
        {
            if (assignedRegionByCell[cell] >= 0)
            {
                continue;
            }

            assignedRegionByCell[cell] = GetNearestKeptRegion(cell, width, keptRegions);
        }
    }

    private static int GetAssignedNeighbor(int[] assignedRegionByCell, int x, int y, int width, int height)
    {
        int left = GetAssignedCell(assignedRegionByCell, x - 1, y, width, height);
        if (left >= 0)
        {
            return left;
        }

        int right = GetAssignedCell(assignedRegionByCell, x + 1, y, width, height);
        if (right >= 0)
        {
            return right;
        }

        int down = GetAssignedCell(assignedRegionByCell, x, y - 1, width, height);
        if (down >= 0)
        {
            return down;
        }

        return GetAssignedCell(assignedRegionByCell, x, y + 1, width, height);
    }

    private static int GetAssignedCell(int[] assignedRegionByCell, int x, int y, int width, int height)
    {
        if (x < 0 || x >= width || y < 0 || y >= height)
        {
            return -1;
        }

        return assignedRegionByCell[ToCellIndex(x, y, width)];
    }

    private static int GetNearestKeptRegion(int cell, int width, List<MaskRegion> keptRegions)
    {
        int x = cell % width;
        int y = cell / width;
        int nearestIndex = 0;
        float nearestDistance = float.PositiveInfinity;

        for (int i = 0; i < keptRegions.Count; i++)
        {
            float dx = keptRegions[i].centerX - x;
            float dy = keptRegions[i].centerY - y;
            float distance = dx * dx + dy * dy;
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestIndex = i;
            }
        }

        return nearestIndex;
    }

    private Material CreateShardMaterial(Sprite sprite)
    {
        Material material;
        if (shardMaterial != null)
        {
            material = new Material(shardMaterial);
        }
        else
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            }

            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (shader == null)
            {
                Material source = visualTarget != null ? visualTarget.sharedMaterial : null;
                material = source != null ? new Material(source) : null;
            }
            else
            {
                material = new Material(shader);
            }
        }

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

    private Mesh CreateShardMesh(
        Sprite sprite,
        Rect localBounds,
        List<int> cells,
        int maskWidth,
        int maskHeight,
        out Vector2 centroid)
    {
        centroid = GetCellCentroid(localBounds, cells, maskWidth, maskHeight);
        if (cells.Count == 0)
        {
            return null;
        }

        int vertexCount = cells.Count * 4;
        var vertices = new Vector3[vertexCount];
        var uvs = new Vector2[vertexCount];
        var colors = new Color[vertexCount];
        var triangles = new int[cells.Count * 6];
        Color shardColor = visualTarget != null ? visualTarget.color : Color.white;
        Vector2 displayCentroid = ApplySpriteFlip(centroid);
        float cellWidth = localBounds.width / maskWidth;
        float cellHeight = localBounds.height / maskHeight;
        int vertexIndex = 0;
        int triangleIndex = 0;

        foreach (int cell in cells)
        {
            int x = cell % maskWidth;
            int y = cell / maskWidth;
            float left = localBounds.xMin + x * cellWidth;
            float right = left + cellWidth;
            float bottom = localBounds.yMin + y * cellHeight;
            float top = bottom + cellHeight;

            Vector2 bottomLeft = new Vector2(left, bottom);
            Vector2 bottomRight = new Vector2(right, bottom);
            Vector2 topRight = new Vector2(right, top);
            Vector2 topLeft = new Vector2(left, top);

            vertices[vertexIndex] = ToShardVertex(bottomLeft, displayCentroid);
            vertices[vertexIndex + 1] = ToShardVertex(bottomRight, displayCentroid);
            vertices[vertexIndex + 2] = ToShardVertex(topRight, displayCentroid);
            vertices[vertexIndex + 3] = ToShardVertex(topLeft, displayCentroid);

            uvs[vertexIndex] = GetSpriteUv(sprite, bottomLeft);
            uvs[vertexIndex + 1] = GetSpriteUv(sprite, bottomRight);
            uvs[vertexIndex + 2] = GetSpriteUv(sprite, topRight);
            uvs[vertexIndex + 3] = GetSpriteUv(sprite, topLeft);

            colors[vertexIndex] = shardColor;
            colors[vertexIndex + 1] = shardColor;
            colors[vertexIndex + 2] = shardColor;
            colors[vertexIndex + 3] = shardColor;

            WriteQuadTriangles(vertices, vertexIndex, triangles, triangleIndex);
            vertexIndex += 4;
            triangleIndex += 6;
        }

        Mesh mesh = new Mesh
        {
            name = $"{sprite.name}_WoodMaskShardMesh"
        };

        if (vertexCount > 65000)
        {
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.colors = colors;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        return mesh;
    }

    private Vector3 ToShardVertex(Vector2 localPoint, Vector2 displayCentroid)
    {
        Vector2 displayPoint = ApplySpriteFlip(localPoint) - displayCentroid;
        return new Vector3(displayPoint.x, displayPoint.y, 0f);
    }

    private static void WriteQuadTriangles(Vector3[] vertices, int vertexIndex, int[] triangles, int triangleIndex)
    {
        Vector3 first = vertices[vertexIndex];
        Vector3 second = vertices[vertexIndex + 1];
        Vector3 third = vertices[vertexIndex + 2];
        float signedArea = (second.x - first.x) * (third.y - first.y) - (second.y - first.y) * (third.x - first.x);

        if (signedArea >= 0f)
        {
            triangles[triangleIndex] = vertexIndex;
            triangles[triangleIndex + 1] = vertexIndex + 1;
            triangles[triangleIndex + 2] = vertexIndex + 2;
            triangles[triangleIndex + 3] = vertexIndex;
            triangles[triangleIndex + 4] = vertexIndex + 2;
            triangles[triangleIndex + 5] = vertexIndex + 3;
            return;
        }

        triangles[triangleIndex] = vertexIndex;
        triangles[triangleIndex + 1] = vertexIndex + 2;
        triangles[triangleIndex + 2] = vertexIndex + 1;
        triangles[triangleIndex + 3] = vertexIndex;
        triangles[triangleIndex + 4] = vertexIndex + 3;
        triangles[triangleIndex + 5] = vertexIndex + 2;
    }

    private static Vector2 GetCellCentroid(Rect localBounds, List<int> cells, int maskWidth, int maskHeight)
    {
        float cellWidth = localBounds.width / maskWidth;
        float cellHeight = localBounds.height / maskHeight;
        Vector2 total = Vector2.zero;

        foreach (int cell in cells)
        {
            int x = cell % maskWidth;
            int y = cell / maskWidth;
            total += new Vector2(
                localBounds.xMin + (x + 0.5f) * cellWidth,
                localBounds.yMin + (y + 0.5f) * cellHeight);
        }

        return total / cells.Count;
    }

    private Vector2 ApplySpriteFlip(Vector2 point)
    {
        if (visualTarget == null)
        {
            return point;
        }

        return new Vector2(
            visualTarget.flipX ? -point.x : point.x,
            visualTarget.flipY ? -point.y : point.y);
    }

    private Vector2 GetSpriteUv(Sprite sprite, Vector2 localPoint)
    {
        Rect rect = sprite.rect;
        Vector2 pixel = sprite.pivot + localPoint * sprite.pixelsPerUnit;
        return new Vector2(
            (rect.x + pixel.x) / sprite.texture.width,
            (rect.y + pixel.y) / sprite.texture.height);
    }

    private void ApplyShardImpulse(Rigidbody2D body, Vector3 worldPosition, System.Random random)
    {
        Vector3 worldCenter = visualTarget.transform.TransformPoint(ApplySpriteFlip(visualTarget.sprite.bounds.center));
        Vector2 direction = (Vector2)(worldPosition - worldCenter);
        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = RandomInsideUnitCircle(random).normalized;
        }
        else
        {
            direction.Normalize();
        }

        Vector2 impulse = direction * explosionForce + Vector2.up * upwardForce;
        body.AddForce(impulse, ForceMode2D.Impulse);
        body.AddTorque(RandomRange(random, -torqueForce, torqueForce), ForceMode2D.Impulse);
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

    private IEnumerator ReleaseShard(Rigidbody2D body, Vector3 worldPosition, float delay, System.Random random)
    {
        yield return new WaitForSeconds(delay);

        if (body == null)
        {
            yield break;
        }

        body.simulated = true;
        ApplyShardImpulse(body, worldPosition, random);
    }

    private float GetBottomToTopReleaseDelay(Vector2 localCentroid, Rect localBounds, float crumbleDuration, System.Random random)
    {
        if (crumbleDuration <= 0f)
        {
            return 0f;
        }

        float normalizedHeight = Mathf.InverseLerp(localBounds.yMin, localBounds.yMax, localCentroid.y);
        float jitterMax = Mathf.Min(releaseDelayJitter, crumbleDuration);
        float releaseWindow = Mathf.Max(0f, crumbleDuration - jitterMax);
        float jitter = RandomRange(random, 0f, jitterMax);
        return normalizedHeight * releaseWindow + jitter;
    }

    private void WarnMissingVisual()
    {
        if (warnedMissingVisual)
        {
            return;
        }

        warnedMissingVisual = true;
        Debug.LogWarning($"{nameof(BreakableTimedPlatform)} on {name} could not create crumble shards, so it used the simple hide/respawn behavior. Assign a Visual Target sprite and check mask settings.", this);
    }

    private void DrawMaskPreview()
    {
        Sprite sprite = visualTarget.sprite;
        Bounds bounds = sprite.bounds;
        Rect localBounds = new Rect(bounds.min.x, bounds.min.y, bounds.size.x, bounds.size.y);
        bool[] mask = GenerateContrastMask(CreatePreviewRandom(), out int width, out int height);
        float cellWidth = localBounds.width / width;
        float cellHeight = localBounds.height / height;
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;

        Gizmos.matrix = visualTarget.transform.localToWorldMatrix;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool isBlack = mask[ToCellIndex(x, y, width)];
                Gizmos.color = isBlack
                    ? new Color(0f, 0f, 0f, maskPreviewAlpha)
                    : new Color(1f, 1f, 1f, maskPreviewAlpha);

                Vector2 localCenter = new Vector2(
                    localBounds.xMin + (x + 0.5f) * cellWidth,
                    localBounds.yMin + (y + 0.5f) * cellHeight);
                Vector2 displayCenter = ApplySpriteFlip(localCenter);
                Gizmos.DrawCube(
                    new Vector3(displayCenter.x, displayCenter.y, -0.01f),
                    new Vector3(cellWidth, cellHeight, 0.01f));
            }
        }

        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }

    private System.Random CreatePreviewRandom()
    {
        return new System.Random(useDeterministicSeed ? seed : 0);
    }

    private static int ToCellIndex(int x, int y, int width)
    {
        return x + y * width;
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
