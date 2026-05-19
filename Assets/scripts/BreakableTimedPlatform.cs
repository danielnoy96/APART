using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BreakableTimedPlatform : MonoBehaviour
{
    [SerializeField] private float breakDelay = 0.9f;
    [SerializeField] private float respawnDelay = 0.9f;
    [SerializeField] private LayerMask triggerLayer;

    [Header("Crumble Visual")]
    [SerializeField] private SpriteRenderer visualTarget;
    [SerializeField] private Material shardMaterial;
    [SerializeField, Min(1)] private int minPieces = 8;
    [SerializeField, Min(1)] private int maxPieces = 16;
    [SerializeField] private float explosionForce = 2.5f;
    [SerializeField] private float upwardForce = 1.5f;
    [SerializeField] private float torqueForce = 180f;
    [SerializeField, Min(0.05f)] private float pieceLifetime = 2f;
    [SerializeField] private bool useDeterministicSeed;
    [SerializeField] private int seed;

    private Collider2D[] colliders;
    private Renderer[] renderers;
    private bool busy;
    private bool warnedMissingVisual;

    private void Awake()
    {
        colliders = GetComponentsInChildren<Collider2D>();
        renderers = GetComponentsInChildren<Renderer>();
    }

    private void Reset()
    {
        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer >= 0)
        {
            triggerLayer = 1 << playerLayer;
        }
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

        yield return new WaitForSeconds(breakDelay);

        bool spawnedShards = TrySpawnShards();
        SetEnabled(false);
        SetVisualTargetEnabled(false);

        if (!spawnedShards)
        {
            WarnMissingVisual();
        }

        yield return new WaitForSeconds(respawnDelay);
        SetEnabled(true);
        SetVisualTargetEnabled(true);

        busy = false;
    }

    private void SetEnabled(bool enabled)
    {
        if (colliders != null)
        {
            foreach (var c in colliders)
            {
                if (c != null)
                {
                    c.enabled = enabled;
                }
            }
        }

        if (renderers != null)
        {
            foreach (var r in renderers)
            {
                if (r != null)
                {
                    r.enabled = enabled;
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

    private bool TrySpawnShards()
    {
        if (visualTarget == null || visualTarget.sprite == null)
        {
            return false;
        }

        Sprite sprite = visualTarget.sprite;
        Bounds bounds = sprite.bounds;
        Rect localBounds = new Rect(bounds.min.x, bounds.min.y, bounds.size.x, bounds.size.y);
        int pieceCount = Mathf.Max(1, GetPieceCount());
        var random = CreateRandom();
        List<List<Vector2>> cells = GenerateVoronoiCells(localBounds, pieceCount, random);

        if (cells.Count == 0)
        {
            return false;
        }

        GameObject container = new GameObject($"{visualTarget.name}_CrumbleShards");
        Material runtimeMaterial = CreateShardMaterial(sprite);
        var meshes = new List<Mesh>(cells.Count);

        foreach (List<Vector2> cell in cells)
        {
            Mesh mesh = CreateShardMesh(sprite, cell, out Vector2 localCentroid);
            if (mesh == null)
            {
                continue;
            }

            Vector2 displayCentroid = ApplySpriteFlip(localCentroid);
            Vector3 worldPosition = visualTarget.transform.TransformPoint(displayCentroid);
            GameObject shard = new GameObject($"{visualTarget.name}_Shard");
            shard.transform.SetParent(container.transform, true);
            shard.transform.position = worldPosition;
            shard.transform.rotation = visualTarget.transform.rotation;
            shard.transform.localScale = visualTarget.transform.lossyScale;
            shard.layer = visualTarget.gameObject.layer;

            var meshFilter = shard.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;
            meshes.Add(mesh);

            var meshRenderer = shard.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = runtimeMaterial;
            meshRenderer.sortingLayerID = visualTarget.sortingLayerID;
            meshRenderer.sortingOrder = visualTarget.sortingOrder;

            Rigidbody2D body = shard.AddComponent<Rigidbody2D>();
            body.gravityScale = 1f;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            ApplyShardImpulse(body, worldPosition, random);
        }

        if (meshes.Count == 0)
        {
            if (runtimeMaterial != null)
            {
                Destroy(runtimeMaterial);
            }

            Destroy(container);
            return false;
        }

        StartCoroutine(DestroyShardGroup(container, meshes, runtimeMaterial, pieceLifetime));
        return true;
    }

    private int GetPieceCount()
    {
        int lower = Mathf.Min(minPieces, maxPieces);
        int upper = Mathf.Max(minPieces, maxPieces);
        return useDeterministicSeed ? lower + Mathf.Abs(seed % (upper - lower + 1)) : UnityEngine.Random.Range(lower, upper + 1);
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

    private Mesh CreateShardMesh(Sprite sprite, List<Vector2> cell, out Vector2 centroid)
    {
        centroid = GetPolygonCentroid(cell);
        float area = Mathf.Abs(GetSignedArea(cell));
        if (cell.Count < 3 || area < 0.0001f)
        {
            return null;
        }

        Vector3[] vertices = new Vector3[cell.Count];
        Vector2[] uvs = new Vector2[cell.Count];
        Color[] colors = new Color[cell.Count];
        int[] triangles = new int[(cell.Count - 2) * 3];
        Color shardColor = visualTarget != null ? visualTarget.color : Color.white;

        for (int i = 0; i < cell.Count; i++)
        {
            Vector2 displayVertex = ApplySpriteFlip(cell[i]) - ApplySpriteFlip(centroid);
            vertices[i] = new Vector3(displayVertex.x, displayVertex.y, 0f);
            uvs[i] = GetSpriteUv(sprite, cell[i]);
            colors[i] = shardColor;
        }

        int triangleIndex = 0;
        for (int i = 1; i < cell.Count - 1; i++)
        {
            triangles[triangleIndex++] = 0;
            triangles[triangleIndex++] = i;
            triangles[triangleIndex++] = i + 1;
        }

        var mesh = new Mesh
        {
            name = $"{sprite.name}_ShardMesh",
            vertices = vertices,
            uv = uvs,
            colors = colors,
            triangles = triangles
        };
        mesh.RecalculateBounds();
        return mesh;
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

    private IEnumerator DestroyShardGroup(GameObject container, List<Mesh> meshes, Material material, float lifetime)
    {
        yield return new WaitForSeconds(lifetime);

        foreach (Mesh mesh in meshes)
        {
            if (mesh != null)
            {
                Destroy(mesh);
            }
        }

        if (material != null)
        {
            Destroy(material);
        }

        if (container != null)
        {
            Destroy(container);
        }
    }

    private void WarnMissingVisual()
    {
        if (warnedMissingVisual)
        {
            return;
        }

        warnedMissingVisual = true;
        Debug.LogWarning($"{nameof(BreakableTimedPlatform)} on {name} has no visual target sprite assigned, so it used the old hide/respawn behavior.", this);
    }

    private static List<List<Vector2>> GenerateVoronoiCells(Rect bounds, int count, System.Random random)
    {
        var sites = new List<Vector2>(count);
        for (int i = 0; i < count; i++)
        {
            sites.Add(new Vector2(
                RandomRange(random, bounds.xMin, bounds.xMax),
                RandomRange(random, bounds.yMin, bounds.yMax)));
        }

        var cells = new List<List<Vector2>>(count);
        foreach (Vector2 site in sites)
        {
            var cell = new List<Vector2>
            {
                new Vector2(bounds.xMin, bounds.yMin),
                new Vector2(bounds.xMax, bounds.yMin),
                new Vector2(bounds.xMax, bounds.yMax),
                new Vector2(bounds.xMin, bounds.yMax)
            };

            foreach (Vector2 other in sites)
            {
                if ((other - site).sqrMagnitude < 0.000001f)
                {
                    continue;
                }

                cell = ClipToCloserSide(cell, site, other);
                if (cell.Count == 0)
                {
                    break;
                }
            }

            cell = CleanPolygon(cell);
            if (cell.Count >= 3 && Mathf.Abs(GetSignedArea(cell)) > 0.0001f)
            {
                cells.Add(cell);
            }
        }

        return cells;
    }

    private static List<Vector2> ClipToCloserSide(List<Vector2> polygon, Vector2 site, Vector2 other)
    {
        var result = new List<Vector2>(polygon.Count + 1);

        for (int i = 0; i < polygon.Count; i++)
        {
            Vector2 current = polygon[i];
            Vector2 previous = polygon[(i + polygon.Count - 1) % polygon.Count];
            bool currentInside = IsCloserToSite(current, site, other);
            bool previousInside = IsCloserToSite(previous, site, other);

            if (currentInside != previousInside)
            {
                result.Add(GetBisectorIntersection(previous, current, site, other));
            }

            if (currentInside)
            {
                result.Add(current);
            }
        }

        return result;
    }

    private static bool IsCloserToSite(Vector2 point, Vector2 site, Vector2 other)
    {
        return (point - site).sqrMagnitude <= (point - other).sqrMagnitude;
    }

    private static Vector2 GetBisectorIntersection(Vector2 a, Vector2 b, Vector2 site, Vector2 other)
    {
        Vector2 direction = b - a;
        Vector2 middle = (site + other) * 0.5f;
        Vector2 normal = other - site;
        float denominator = Vector2.Dot(direction, normal);

        if (Mathf.Abs(denominator) < 0.000001f)
        {
            return a;
        }

        float t = Vector2.Dot(middle - a, normal) / denominator;
        return a + direction * Mathf.Clamp01(t);
    }

    private static List<Vector2> CleanPolygon(List<Vector2> polygon)
    {
        if (polygon.Count < 3)
        {
            return polygon;
        }

        var cleaned = new List<Vector2>(polygon.Count);
        for (int i = 0; i < polygon.Count; i++)
        {
            Vector2 previous = polygon[(i + polygon.Count - 1) % polygon.Count];
            Vector2 current = polygon[i];
            Vector2 next = polygon[(i + 1) % polygon.Count];

            if ((current - previous).sqrMagnitude < 0.000001f)
            {
                continue;
            }

            Vector2 toCurrent = (current - previous).normalized;
            Vector2 toNext = (next - current).normalized;
            float cross = toCurrent.x * toNext.y - toCurrent.y * toNext.x;
            if (Mathf.Abs(cross) < 0.0001f)
            {
                continue;
            }

            cleaned.Add(current);
        }

        return cleaned;
    }

    private static Vector2 GetPolygonCentroid(List<Vector2> polygon)
    {
        float signedArea = GetSignedArea(polygon);
        if (Mathf.Abs(signedArea) < 0.0001f)
        {
            Vector2 average = Vector2.zero;
            foreach (Vector2 point in polygon)
            {
                average += point;
            }

            return average / polygon.Count;
        }

        float centroidX = 0f;
        float centroidY = 0f;
        for (int i = 0; i < polygon.Count; i++)
        {
            Vector2 current = polygon[i];
            Vector2 next = polygon[(i + 1) % polygon.Count];
            float cross = current.x * next.y - next.x * current.y;
            centroidX += (current.x + next.x) * cross;
            centroidY += (current.y + next.y) * cross;
        }

        float factor = 1f / (6f * signedArea);
        return new Vector2(centroidX * factor, centroidY * factor);
    }

    private static float GetSignedArea(List<Vector2> polygon)
    {
        float area = 0f;
        for (int i = 0; i < polygon.Count; i++)
        {
            Vector2 current = polygon[i];
            Vector2 next = polygon[(i + 1) % polygon.Count];
            area += current.x * next.y - next.x * current.y;
        }

        return area * 0.5f;
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
