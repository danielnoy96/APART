using UnityEngine;

public class PlayerGroundShadow : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private LayerMask groundLayer;

    [Header("Shape")]
    [SerializeField] private int pixelCount = 24;
    [SerializeField] private float shadowWidth = 1.35f;
    [SerializeField] private float pixelHeight = 0.06f;
    [SerializeField] private bool chamferEdges = true;
    [SerializeField] private Vector2 originOffset;

    [Header("Grounding")]
    [SerializeField] private float maxGroundDistance = 5f;
    [SerializeField] private float groundOffset = 0.03f;
    [SerializeField] private float minGroundNormalY = 0.75f;
    [SerializeField] private float ledgeDropThreshold = 0.35f;

    [Header("Jump Fade")]
    [SerializeField] private float groundedRayDistance = 0.05f;
    [SerializeField] private int minimumVisiblePixels = 6;
    [SerializeField] private float fadeDistance = 4f;
    [SerializeField] private Color closeColor = new Color(0f, 0f, 0f, 0.45f);
    [SerializeField] private Color farColor = new Color(0f, 0f, 0f, 0.1f);

    [Header("Rendering")]
    [SerializeField] private SpriteRenderer sortingReference;
    [SerializeField] private int sortingOrderOffset = -1;

    private const string PixelNamePrefix = "Shadow Pixel ";

    private SpriteRenderer[] pixels;
    private RaycastHit2D[] hits;
    private Sprite pixelSprite;

    private void Awake()
    {
        BuildPixels();
    }

    private void LateUpdate()
    {
        if (player == null || groundLayer.value == 0)
        {
            SetAllVisible(false);
            return;
        }

        EnsurePixels();
        UpdateShadow();
    }

    private void BuildPixels()
    {
        pixelCount = Mathf.Max(1, pixelCount);
        minimumVisiblePixels = Mathf.Clamp(minimumVisiblePixels, 1, pixelCount);

        pixels = new SpriteRenderer[pixelCount];
        hits = new RaycastHit2D[pixelCount];

        SpriteRenderer rootRenderer = GetComponent<SpriteRenderer>();
        if (rootRenderer != null)
        {
            if (sortingReference == null)
            {
                sortingReference = rootRenderer;
            }

            rootRenderer.enabled = false;
        }

        if (pixelSprite == null)
        {
            pixelSprite = CreatePixelSprite();
        }

        for (int i = 0; i < pixelCount; i++)
        {
            Transform pixel = FindOrCreatePixel(i);
            SpriteRenderer renderer = pixel.GetComponent<SpriteRenderer>();

            renderer.sprite = pixelSprite;
            ApplySorting(renderer);
            pixels[i] = renderer;
        }

        DisableExtraPixels();
    }

    private void UpdateShadow()
    {
        Vector2 origin = (Vector2)player.position + originOffset;
        float spacing = GetPixelSpacing();
        float left = origin.x - shadowWidth * 0.5f;
        float closestDistance = maxGroundDistance;
        bool anyHit = false;

        for (int i = 0; i < pixelCount; i++)
        {
            Vector2 rayOrigin = new Vector2(left + spacing * (i + 0.5f), origin.y);
            hits[i] = GetGroundHit(rayOrigin);

            if (hits[i].collider != null)
            {
                closestDistance = Mathf.Min(closestDistance, hits[i].distance);
                anyHit = true;
            }
        }

        if (!anyHit)
        {
            SetAllVisible(false);
            return;
        }

        float heightAboveGround = Mathf.Max(0f, closestDistance - groundedRayDistance);
        float fade = Mathf.Clamp01(heightAboveGround / fadeDistance);
        int visibleCount = Mathf.RoundToInt(Mathf.Lerp(pixelCount, minimumVisiblePixels, fade));
        int startIndex = Mathf.Clamp((pixelCount - visibleCount) / 2, 0, pixelCount - 1);
        int endIndex = Mathf.Clamp(startIndex + visibleCount - 1, startIndex, pixelCount - 1);

        CullAtLedge(ref startIndex, ref endIndex);

        Color color = Color.Lerp(closeColor, farColor, fade);

        for (int i = 0; i < pixelCount; i++)
        {
            bool visible = i >= startIndex && i <= endIndex && hits[i].collider != null;
            pixels[i].enabled = visible;

            if (!visible)
            {
                continue;
            }

            pixels[i].transform.position = new Vector3(
                hits[i].point.x,
                hits[i].point.y + groundOffset,
                transform.position.z);

            pixels[i].transform.localScale = GetPixelScale(i, startIndex, endIndex);
            pixels[i].color = color;
        }
    }

    private RaycastHit2D GetGroundHit(Vector2 rayOrigin)
    {
        RaycastHit2D[] rayHits = Physics2D.RaycastAll(rayOrigin, Vector2.down, maxGroundDistance, groundLayer);
        RaycastHit2D bestHit = default;
        float bestDistance = float.PositiveInfinity;

        for (int i = 0; i < rayHits.Length; i++)
        {
            if (rayHits[i].normal.y < minGroundNormalY || rayHits[i].distance >= bestDistance || IsIgnoredSurface(rayHits[i].collider))
            {
                continue;
            }

            bestHit = rayHits[i];
            bestDistance = rayHits[i].distance;
        }

        return bestHit;
    }

    private bool IsIgnoredSurface(Collider2D surface)
    {
        if (surface == null)
        {
            return true;
        }

        Checkpoint checkpoint = surface.GetComponentInParent<Checkpoint>();
        return checkpoint != null && checkpoint.IsMini;
    }

    private void CullAtLedge(ref int startIndex, ref int endIndex)
    {
        if (startIndex >= endIndex)
        {
            return;
        }

        bool leftIsHigher = GetHitY(startIndex) >= GetHitY(endIndex);

        if (leftIsHigher)
        {
            for (int i = startIndex + 1; i <= endIndex; i++)
            {
                if (IsDropBetween(i - 1, i))
                {
                    endIndex = i - 1;
                    return;
                }
            }
        }
        else
        {
            for (int i = endIndex - 1; i >= startIndex; i--)
            {
                if (IsDropBetween(i + 1, i))
                {
                    startIndex = i + 1;
                    return;
                }
            }
        }
    }

    private bool IsDropBetween(int higherIndex, int testIndex)
    {
        if (hits[higherIndex].collider == null || hits[testIndex].collider == null)
        {
            return true;
        }

        return GetHitY(higherIndex) - GetHitY(testIndex) > ledgeDropThreshold;
    }

    private float GetHitY(int index)
    {
        return hits[index].collider != null ? hits[index].point.y : float.NegativeInfinity;
    }

    private Vector3 GetPixelScale(int index, int startIndex, int endIndex)
    {
        bool isOuterEdge = index == startIndex || index == endIndex;
        float height = chamferEdges && isOuterEdge ? pixelHeight : pixelHeight * 2f;

        return new Vector3(GetPixelSpacing(), height, 1f);
    }

    private float GetPixelSpacing()
    {
        return pixelCount > 0 ? shadowWidth / pixelCount : shadowWidth;
    }

    private void EnsurePixels()
    {
        if (pixels == null || pixels.Length != Mathf.Max(1, pixelCount))
        {
            BuildPixels();
        }
    }

    private Transform FindOrCreatePixel(int index)
    {
        string pixelName = PixelNamePrefix + index;
        Transform pixel = transform.Find(pixelName);

        if (pixel == null)
        {
            GameObject pixelObject = new GameObject(pixelName);
            pixelObject.transform.SetParent(transform, false);
            pixel = pixelObject.transform;
        }

        if (!pixel.TryGetComponent(out SpriteRenderer _))
        {
            pixel.gameObject.AddComponent<SpriteRenderer>();
        }

        return pixel;
    }

    private void DisableExtraPixels()
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (!child.name.StartsWith(PixelNamePrefix))
            {
                continue;
            }

            string indexText = child.name.Substring(PixelNamePrefix.Length);
            if (!int.TryParse(indexText, out int index) || index < pixelCount)
            {
                continue;
            }

            if (child.TryGetComponent(out SpriteRenderer renderer))
            {
                renderer.enabled = false;
            }
        }
    }

    private Sprite CreatePixelSprite()
    {
        Texture2D texture = new Texture2D(1, 1)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        texture.SetPixel(0, 0, Color.white);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
    }

    private void ApplySorting(SpriteRenderer renderer)
    {
        if (sortingReference == null)
        {
            return;
        }

        renderer.sortingLayerID = sortingReference.sortingLayerID;
        renderer.sortingOrder = sortingReference.sortingOrder + sortingOrderOffset;
    }

    private void SetAllVisible(bool visible)
    {
        if (pixels == null)
        {
            return;
        }

        for (int i = 0; i < pixels.Length; i++)
        {
            if (pixels[i] != null)
            {
                pixels[i].enabled = visible;
            }
        }
    }
}
