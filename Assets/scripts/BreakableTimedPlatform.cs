using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BreakableTimedPlatform : MonoBehaviour
{
    [System.Serializable]
    public class WoodCutLine
    {
        public List<Vector2> points = new List<Vector2>();
    }

    [SerializeField] private float breakDelay = 0.9f;
    [SerializeField] private float respawnDelay = 0.9f;
    [SerializeField] private LayerMask triggerLayer;

    [Header("Crumble Visual")]
    [SerializeField] private SpriteRenderer visualTarget;
    [SerializeField] private Material shardMaterial;
    [SerializeField, Min(1)] private int minPieces = 24;
    [SerializeField, Min(1)] private int maxPieces = 36;
    [SerializeField] private float explosionForce = 2.5f;
    [SerializeField] private float upwardForce = 1.5f;
    [SerializeField] private float torqueForce = 180f;
    [SerializeField, Min(0.05f)] private float pieceLifetime = 2f;
    [SerializeField] private bool useDeterministicSeed;
    [SerializeField] private int seed;

    [Header("Wood Chips")]
    [SerializeField] private Vector2 chipWidthRange = new Vector2(0.18f, 0.62f);
    [SerializeField] private Vector2 chipHeightRange = new Vector2(0.045f, 0.14f);
    [SerializeField, Range(6, 18)] private int chipEdgePoints = 12;
    [SerializeField, Range(0f, 0.35f)] private float chipEdgeJitter = 0.16f;
    [SerializeField, Range(0f, 0.6f)] private float chipPointiness = 0.28f;
    [SerializeField] private List<WoodCutLine> guidedCutLines = new List<WoodCutLine>();

    [Header("Crumble Animation")]
    [SerializeField, Min(0f)] private float bottomToTopWaveDuration = 0.45f;
    [SerializeField, Min(0f)] private float releaseDelayJitter = 0.06f;

    private Collider2D[] colliders;
    private Renderer[] renderers;
    private bool busy;
    private bool warnedMissingVisual;

    public SpriteRenderer VisualTarget => visualTarget;
    public int GuidedCutLineCount => guidedCutLines != null ? guidedCutLines.Count : 0;

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

        if (!spawnedShards)
        {
            SetVisualTargetEnabled(false);
            WarnMissingVisual();
        }
        else
        {
            yield return new WaitForSeconds(bottomToTopWaveDuration + releaseDelayJitter);
            SetVisualTargetEnabled(false);
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
        System.Random random = CreateRandom();
        List<List<Vector2>> chips = GuidedCutLineCount > 0
            ? GenerateGuidedCutLineCells(localBounds, random)
            : GenerateWoodChipCells(localBounds, pieceCount, random);

        if (chips.Count == 0)
        {
            return false;
        }

        GameObject container = new GameObject($"{visualTarget.name}_CrumbleShards");
        Material runtimeMaterial = CreateShardMaterial(sprite);
        var meshes = new List<Mesh>(chips.Count);

        foreach (List<Vector2> chip in chips)
        {
            Mesh mesh = CreateShardMesh(sprite, chip, out Vector2 localCentroid);
            if (mesh == null)
            {
                continue;
            }

            Vector2 displayCentroid = ApplySpriteFlip(localCentroid);
            Vector3 worldPosition = visualTarget.transform.TransformPoint(displayCentroid);
            GameObject shard = new GameObject($"{visualTarget.name}_Chip");
            shard.transform.SetParent(container.transform, true);
            shard.transform.position = worldPosition;
            shard.transform.rotation = visualTarget.transform.rotation;
            shard.transform.localScale = visualTarget.transform.lossyScale;
            shard.layer = visualTarget.gameObject.layer;

            MeshFilter meshFilter = shard.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;
            meshes.Add(mesh);

            MeshRenderer meshRenderer = shard.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = runtimeMaterial;
            meshRenderer.sortingLayerID = visualTarget.sortingLayerID;
            meshRenderer.sortingOrder = visualTarget.sortingOrder;

            Rigidbody2D body = shard.AddComponent<Rigidbody2D>();
            body.simulated = false;
            body.gravityScale = 1f;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            float releaseDelay = GetBottomToTopReleaseDelay(localCentroid, localBounds, random);
            StartCoroutine(ReleaseShard(body, worldPosition, releaseDelay, random));
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

    private List<List<Vector2>> GenerateWoodChipCells(Rect bounds, int count, System.Random random)
    {
        Vector2 widthRange = SortRange(chipWidthRange);
        Vector2 heightRange = SortRange(chipHeightRange);
        int edgePoints = Mathf.Max(6, chipEdgePoints);
        var chips = new List<List<Vector2>>(count);

        for (int i = 0; i < count; i++)
        {
            float width = bounds.width * RandomRange(random, widthRange.x, widthRange.y);
            float height = bounds.height * RandomRange(random, heightRange.x, heightRange.y);
            width = Mathf.Clamp(width, bounds.width * 0.04f, bounds.width);
            height = Mathf.Clamp(height, bounds.height * 0.02f, bounds.height);

            float centerX = RandomRange(random, bounds.xMin, bounds.xMax);
            float centerY = RandomRange(random, bounds.yMin, bounds.yMax);
            List<Vector2> chip = CreateWoodChipPolygon(bounds, centerX, centerY, width, height, edgePoints, random);

            chip = CleanPolygon(chip);
            if (chip.Count >= 3 && Mathf.Abs(GetSignedArea(chip)) > 0.0001f)
            {
                chips.Add(chip);
            }
        }

        return chips;
    }

    private List<List<Vector2>> GenerateGuidedCutLineCells(Rect bounds, System.Random random)
    {
        Vector2 widthRange = SortRange(chipWidthRange);
        var cutLines = new List<List<Vector2>>(GuidedCutLineCount + 2);
        cutLines.Add(new List<Vector2>
        {
            new Vector2(bounds.xMin, bounds.yMin),
            new Vector2(bounds.xMax, bounds.yMin)
        });

        foreach (WoodCutLine cutLine in guidedCutLines)
        {
            List<Vector2> localLine = ConvertCutLineToLocal(bounds, cutLine);
            if (localLine.Count >= 2)
            {
                cutLines.Add(localLine);
            }
        }

        cutLines.Add(new List<Vector2>
        {
            new Vector2(bounds.xMin, bounds.yMax),
            new Vector2(bounds.xMax, bounds.yMax)
        });

        cutLines.Sort((a, b) => GetAverageY(a).CompareTo(GetAverageY(b)));

        var chips = new List<List<Vector2>>();
        for (int lineIndex = 0; lineIndex < cutLines.Count - 1; lineIndex++)
        {
            List<float> rowCuts = CreateRowCuts(bounds, widthRange, random);
            List<Vector2> lowerLine = cutLines[lineIndex];
            List<Vector2> upperLine = cutLines[lineIndex + 1];

            for (int cutIndex = 0; cutIndex < rowCuts.Count - 1; cutIndex++)
            {
                List<Vector2> chip = CreateCutLineChip(lowerLine, upperLine, rowCuts[cutIndex], rowCuts[cutIndex + 1]);
                chip = CleanPolygon(chip);
                if (chip.Count >= 3 && Mathf.Abs(GetSignedArea(chip)) > 0.0001f)
                {
                    chips.Add(chip);
                }
            }
        }

        return chips;
    }

    private List<Vector2> ConvertCutLineToLocal(Rect bounds, WoodCutLine cutLine)
    {
        var localPoints = new List<Vector2>();
        if (cutLine == null || cutLine.points == null)
        {
            return localPoints;
        }

        foreach (Vector2 point in cutLine.points)
        {
            localPoints.Add(new Vector2(
                Mathf.Lerp(bounds.xMin, bounds.xMax, Mathf.Clamp01(point.x)),
                Mathf.Lerp(bounds.yMin, bounds.yMax, Mathf.Clamp01(point.y))));
        }

        localPoints.Sort((a, b) => a.x.CompareTo(b.x));
        if (localPoints.Count >= 2)
        {
            float leftY = EvaluateCutLineY(localPoints, bounds.xMin);
            float rightY = EvaluateCutLineY(localPoints, bounds.xMax);

            if (localPoints[0].x > bounds.xMin)
            {
                localPoints.Insert(0, new Vector2(bounds.xMin, leftY));
            }
            else
            {
                localPoints[0] = new Vector2(bounds.xMin, localPoints[0].y);
            }

            int lastIndex = localPoints.Count - 1;
            if (localPoints[lastIndex].x < bounds.xMax)
            {
                localPoints.Add(new Vector2(bounds.xMax, rightY));
            }
            else
            {
                localPoints[lastIndex] = new Vector2(bounds.xMax, localPoints[lastIndex].y);
            }
        }

        return localPoints;
    }

    private List<float> CreateRowCuts(Rect bounds, Vector2 widthRange, System.Random random)
    {
        var cuts = new List<float> { bounds.xMin };
        float currentX = bounds.xMin;
        float minimumWidth = Mathf.Max(bounds.width * Mathf.Clamp01(widthRange.x), bounds.width * 0.04f);
        float maximumWidth = Mathf.Max(minimumWidth, bounds.width * Mathf.Clamp01(widthRange.y));

        while (currentX < bounds.xMax - minimumWidth)
        {
            currentX += RandomRange(random, minimumWidth, maximumWidth);
            if (currentX < bounds.xMax - minimumWidth)
            {
                cuts.Add(currentX);
            }
        }

        cuts.Add(bounds.xMax);
        return cuts;
    }

    private static List<Vector2> CreateCutLineChip(List<Vector2> lowerLine, List<Vector2> upperLine, float leftX, float rightX)
    {
        var chip = new List<Vector2>();
        AddCutLineSegment(chip, lowerLine, leftX, rightX, false);
        AddCutLineSegment(chip, upperLine, rightX, leftX, true);
        return chip;
    }

    private static void AddCutLineSegment(List<Vector2> polygon, List<Vector2> cutLine, float startX, float endX, bool reverse)
    {
        polygon.Add(new Vector2(startX, EvaluateCutLineY(cutLine, startX)));

        if (!reverse)
        {
            foreach (Vector2 point in cutLine)
            {
                if (point.x > startX && point.x < endX)
                {
                    polygon.Add(point);
                }
            }
        }
        else
        {
            for (int i = cutLine.Count - 1; i >= 0; i--)
            {
                Vector2 point = cutLine[i];
                if (point.x < startX && point.x > endX)
                {
                    polygon.Add(point);
                }
            }
        }

        polygon.Add(new Vector2(endX, EvaluateCutLineY(cutLine, endX)));
    }

    private static float EvaluateCutLineY(List<Vector2> cutLine, float pointX)
    {
        if (pointX <= cutLine[0].x)
        {
            return cutLine[0].y;
        }

        for (int i = 0; i < cutLine.Count - 1; i++)
        {
            Vector2 left = cutLine[i];
            Vector2 right = cutLine[i + 1];
            if (pointX <= right.x)
            {
                float segmentPercent = Mathf.InverseLerp(left.x, right.x, pointX);
                return Mathf.Lerp(left.y, right.y, segmentPercent);
            }
        }

        return cutLine[cutLine.Count - 1].y;
    }

    private static float GetAverageY(List<Vector2> points)
    {
        float total = 0f;
        foreach (Vector2 point in points)
        {
            total += point.y;
        }

        return total / points.Count;
    }

    private List<Vector2> CreateWoodChipPolygon(Rect bounds, float centerX, float centerY, float width, float height, int edgePoints, System.Random random)
    {
        return CreateWoodChipPolygon(bounds, centerX, centerY, width, height, edgePoints, 0f, random);
    }

    private List<Vector2> CreateWoodChipPolygon(Rect bounds, float centerX, float centerY, float width, float height, int edgePoints, float rotationDegrees, System.Random random)
    {
        float halfWidth = width * 0.5f;
        float halfHeight = height * 0.5f;
        float rotation = rotationDegrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(rotation);
        float cos = Mathf.Cos(rotation);
        var chip = new List<Vector2>(edgePoints);

        for (int pointIndex = 0; pointIndex < edgePoints; pointIndex++)
        {
            float angle = pointIndex / (float)edgePoints * Mathf.PI * 2f;
            float alternatingPoint = pointIndex % 2 == 0 ? chipPointiness : -chipPointiness * 0.45f;
            float radiusJitter = RandomRange(random, 1f - chipEdgeJitter, 1f + chipEdgeJitter);
            float pointScale = Mathf.Max(0.25f, 1f + alternatingPoint);
            float localX = Mathf.Cos(angle) * halfWidth * radiusJitter * pointScale;
            float localY = Mathf.Sin(angle) * halfHeight * radiusJitter * pointScale;
            float x = centerX + localX * cos - localY * sin;
            float y = centerY + localX * sin + localY * cos;
            chip.Add(new Vector2(
                Mathf.Clamp(x, bounds.xMin, bounds.xMax),
                Mathf.Clamp(y, bounds.yMin, bounds.yMax)));
        }

        return chip;
    }

    public WoodCutLine GetGuidedCutLine(int index)
    {
        return guidedCutLines[index];
    }

    public void AddGuidedCutLine(List<Vector2> normalizedPoints)
    {
        if (guidedCutLines == null)
        {
            guidedCutLines = new List<WoodCutLine>();
        }

        var line = new WoodCutLine();
        foreach (Vector2 point in normalizedPoints)
        {
            line.points.Add(new Vector2(Mathf.Clamp01(point.x), Mathf.Clamp01(point.y)));
        }

        if (line.points.Count >= 2)
        {
            guidedCutLines.Add(line);
        }
    }

    public bool RemoveGuidedCutLine(Vector2 normalizedPoint, float normalizedRadius)
    {
        if (guidedCutLines == null || guidedCutLines.Count == 0)
        {
            return false;
        }

        float bestDistance = normalizedRadius * normalizedRadius;
        int bestIndex = -1;

        for (int i = 0; i < guidedCutLines.Count; i++)
        {
            float distance = GetCutLineDistanceSqr(guidedCutLines[i], normalizedPoint);
            if (distance <= bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        if (bestIndex < 0)
        {
            return false;
        }

        guidedCutLines.RemoveAt(bestIndex);
        return true;
    }

    public void ClearGuidedCutLines()
    {
        if (guidedCutLines != null)
        {
            guidedCutLines.Clear();
        }
    }

    private static float GetCutLineDistanceSqr(WoodCutLine line, Vector2 point)
    {
        float bestDistance = float.PositiveInfinity;

        foreach (Vector2 linePoint in line.points)
        {
            bestDistance = Mathf.Min(bestDistance, (linePoint - point).sqrMagnitude);
        }

        return bestDistance;
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

    private Mesh CreateShardMesh(Sprite sprite, List<Vector2> chip, out Vector2 centroid)
    {
        centroid = GetPolygonCentroid(chip);
        if (chip.Count < 3 || Mathf.Abs(GetSignedArea(chip)) < 0.0001f)
        {
            return null;
        }

        Vector3[] vertices = new Vector3[chip.Count + 1];
        Vector2[] uvs = new Vector2[chip.Count + 1];
        Color[] colors = new Color[chip.Count + 1];
        int[] triangles = new int[chip.Count * 3];
        Color shardColor = visualTarget != null ? visualTarget.color : Color.white;

        vertices[0] = Vector3.zero;
        uvs[0] = GetSpriteUv(sprite, centroid);
        colors[0] = shardColor;

        for (int i = 0; i < chip.Count; i++)
        {
            Vector2 displayVertex = ApplySpriteFlip(chip[i]) - ApplySpriteFlip(centroid);
            vertices[i + 1] = new Vector3(displayVertex.x, displayVertex.y, 0f);
            uvs[i + 1] = GetSpriteUv(sprite, chip[i]);
            colors[i + 1] = shardColor;
        }

        bool counterClockwise = GetSignedArea(chip) > 0f;
        int triangleIndex = 0;
        for (int i = 0; i < chip.Count; i++)
        {
            int current = i + 1;
            int next = (i + 1) % chip.Count + 1;

            triangles[triangleIndex++] = 0;
            triangles[triangleIndex++] = counterClockwise ? current : next;
            triangles[triangleIndex++] = counterClockwise ? next : current;
        }

        Mesh mesh = new Mesh
        {
            name = $"{sprite.name}_WoodChipMesh",
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
        yield return new WaitForSeconds(lifetime + bottomToTopWaveDuration + releaseDelayJitter);

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

    private float GetBottomToTopReleaseDelay(Vector2 localCentroid, Rect localBounds, System.Random random)
    {
        if (bottomToTopWaveDuration <= 0f)
        {
            return 0f;
        }

        float normalizedHeight = Mathf.InverseLerp(localBounds.yMin, localBounds.yMax, localCentroid.y);
        float jitter = RandomRange(random, 0f, releaseDelayJitter);
        return normalizedHeight * bottomToTopWaveDuration + jitter;
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

    private static Vector2 SortRange(Vector2 range)
    {
        return new Vector2(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y));
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
