using System.Collections.Generic;
using UnityEngine;

public static class SpriteShardMeshBuilder
{
    public static Mesh Create(
        Sprite sprite,
        Rect localBounds,
        List<int> cells,
        int maskWidth,
        int maskHeight,
        SpriteRenderer renderer,
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
        Vector2 displayCentroid = ApplyFlip(centroid, renderer);
        Color color = renderer != null ? renderer.color : Color.white;

        float cellWidth = localBounds.width / maskWidth;
        float cellHeight = localBounds.height / maskHeight;
        int vertexIndex = 0;
        int triangleIndex = 0;

        foreach (int cell in cells)
        {
            AddCellQuad(sprite, localBounds, cell, maskWidth, cellWidth, cellHeight, renderer, displayCentroid, color, vertices, uvs, colors, triangles, vertexIndex, triangleIndex);
            vertexIndex += 4;
            triangleIndex += 6;
        }

        Mesh mesh = new Mesh { name = $"{sprite.name}_WoodMaskShardMesh" };
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

    public static Vector2 ApplyFlip(Vector2 point, SpriteRenderer renderer)
    {
        if (renderer == null)
        {
            return point;
        }

        return new Vector2(renderer.flipX ? -point.x : point.x, renderer.flipY ? -point.y : point.y);
    }

    private static void AddCellQuad(
        Sprite sprite,
        Rect bounds,
        int cell,
        int maskWidth,
        float cellWidth,
        float cellHeight,
        SpriteRenderer renderer,
        Vector2 displayCentroid,
        Color color,
        Vector3[] vertices,
        Vector2[] uvs,
        Color[] colors,
        int[] triangles,
        int vertexIndex,
        int triangleIndex)
    {
        int x = cell % maskWidth;
        int y = cell / maskWidth;
        float left = bounds.xMin + x * cellWidth;
        float bottom = bounds.yMin + y * cellHeight;

        Vector2 bottomLeft = new Vector2(left, bottom);
        Vector2 bottomRight = new Vector2(left + cellWidth, bottom);
        Vector2 topRight = new Vector2(left + cellWidth, bottom + cellHeight);
        Vector2 topLeft = new Vector2(left, bottom + cellHeight);

        vertices[vertexIndex] = ToVertex(bottomLeft, renderer, displayCentroid);
        vertices[vertexIndex + 1] = ToVertex(bottomRight, renderer, displayCentroid);
        vertices[vertexIndex + 2] = ToVertex(topRight, renderer, displayCentroid);
        vertices[vertexIndex + 3] = ToVertex(topLeft, renderer, displayCentroid);

        uvs[vertexIndex] = GetSpriteUv(sprite, bottomLeft);
        uvs[vertexIndex + 1] = GetSpriteUv(sprite, bottomRight);
        uvs[vertexIndex + 2] = GetSpriteUv(sprite, topRight);
        uvs[vertexIndex + 3] = GetSpriteUv(sprite, topLeft);

        colors[vertexIndex] = color;
        colors[vertexIndex + 1] = color;
        colors[vertexIndex + 2] = color;
        colors[vertexIndex + 3] = color;

        WriteQuadTriangles(vertices, vertexIndex, triangles, triangleIndex);
    }

    private static Vector3 ToVertex(Vector2 localPoint, SpriteRenderer renderer, Vector2 displayCentroid)
    {
        Vector2 displayPoint = ApplyFlip(localPoint, renderer) - displayCentroid;
        return new Vector3(displayPoint.x, displayPoint.y, 0f);
    }

    private static Vector2 GetCellCentroid(Rect bounds, List<int> cells, int maskWidth, int maskHeight)
    {
        float cellWidth = bounds.width / maskWidth;
        float cellHeight = bounds.height / maskHeight;
        Vector2 total = Vector2.zero;

        foreach (int cell in cells)
        {
            total += new Vector2(
                bounds.xMin + (cell % maskWidth + 0.5f) * cellWidth,
                bounds.yMin + (cell / maskWidth + 0.5f) * cellHeight);
        }

        return total / cells.Count;
    }

    private static Vector2 GetSpriteUv(Sprite sprite, Vector2 localPoint)
    {
        Rect rect = sprite.rect;
        Vector2 pixel = sprite.pivot + localPoint * sprite.pixelsPerUnit;
        return new Vector2((rect.x + pixel.x) / sprite.texture.width, (rect.y + pixel.y) / sprite.texture.height);
    }

    private static void WriteQuadTriangles(Vector3[] vertices, int vertexIndex, int[] triangles, int triangleIndex)
    {
        Vector3 first = vertices[vertexIndex];
        Vector3 second = vertices[vertexIndex + 1];
        Vector3 third = vertices[vertexIndex + 2];
        bool counterClockwise = (second.x - first.x) * (third.y - first.y) - (second.y - first.y) * (third.x - first.x) >= 0f;

        triangles[triangleIndex] = vertexIndex;
        triangles[triangleIndex + 1] = counterClockwise ? vertexIndex + 1 : vertexIndex + 2;
        triangles[triangleIndex + 2] = counterClockwise ? vertexIndex + 2 : vertexIndex + 1;
        triangles[triangleIndex + 3] = vertexIndex;
        triangles[triangleIndex + 4] = counterClockwise ? vertexIndex + 2 : vertexIndex + 3;
        triangles[triangleIndex + 5] = counterClockwise ? vertexIndex + 3 : vertexIndex + 2;
    }
}
