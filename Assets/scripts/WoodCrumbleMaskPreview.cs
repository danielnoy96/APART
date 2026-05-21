using UnityEngine;

public static class WoodCrumbleMaskPreview
{
    public static void Draw(SpriteRenderer renderer, bool[] mask, int width, int height, float alpha)
    {
        Sprite sprite = renderer.sprite;
        Bounds bounds = sprite.bounds;
        Rect localBounds = new Rect(bounds.min.x, bounds.min.y, bounds.size.x, bounds.size.y);
        float cellWidth = localBounds.width / width;
        float cellHeight = localBounds.height / height;
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;

        Gizmos.matrix = renderer.transform.localToWorldMatrix;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool isBlack = mask[WoodCrumbleMaskGenerator.ToIndex(x, y, width)];
                Gizmos.color = isBlack ? new Color(0f, 0f, 0f, alpha) : new Color(1f, 1f, 1f, alpha);
                DrawCell(renderer, localBounds, x, y, cellWidth, cellHeight);
            }
        }

        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }

    private static void DrawCell(SpriteRenderer renderer, Rect bounds, int x, int y, float cellWidth, float cellHeight)
    {
        Vector2 localCenter = new Vector2(
            bounds.xMin + (x + 0.5f) * cellWidth,
            bounds.yMin + (y + 0.5f) * cellHeight);
        Vector2 displayCenter = SpriteShardMeshBuilder.ApplyFlip(localCenter, renderer);

        Gizmos.DrawCube(
            new Vector3(displayCenter.x, displayCenter.y, -0.01f),
            new Vector3(cellWidth, cellHeight, 0.01f));
    }
}
