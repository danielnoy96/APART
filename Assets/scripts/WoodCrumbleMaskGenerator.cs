using UnityEngine;

public static class WoodCrumbleMaskGenerator
{
    public static bool[] Generate(
        Vector2Int resolution,
        float scaleX,
        float scaleY,
        float threshold,
        float warp,
        int octaves,
        float falloff,
        float edgeSmoothness,
        float edgeDetailStrength,
        System.Random random,
        out int width,
        out int height)
    {
        width = Mathf.Clamp(resolution.x, 16, 256);
        height = Mathf.Clamp(resolution.y, 8, 256);
        bool[] mask = new bool[width * height];
        Vector2 offset = new Vector2(RandomRange(random, -10000f, 10000f), RandomRange(random, -10000f, 10000f));

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float nx = x / (float)width;
                float ny = y / (float)height;
                float warpNoise = Sample(nx * 5f, ny * 5f, offset, Mathf.Max(1, octaves - 1), falloff) * (warp / 100f);
                float value = Sample(nx * scaleX, (ny + warpNoise) * scaleY, offset * 0.37f, octaves, falloff);
                float cutThreshold = GetDetailedThreshold(nx, ny, warpNoise, scaleX, scaleY, threshold, edgeSmoothness, edgeDetailStrength, offset, octaves, falloff, value);

                mask[ToIndex(x, y, width)] = value > cutThreshold;
            }
        }

        return mask;
    }

    public static int ToIndex(int x, int y, int width)
    {
        return x + y * width;
    }

    private static float GetDetailedThreshold(
        float nx,
        float ny,
        float warpNoise,
        float scaleX,
        float scaleY,
        float threshold,
        float edgeSmoothness,
        float edgeDetailStrength,
        Vector2 offset,
        int octaves,
        float falloff,
        float value)
    {
        float edgeBand = Mathf.Max(0.0001f, edgeSmoothness);
        if (value <= threshold - edgeBand || value >= threshold + edgeBand)
        {
            return threshold;
        }

        float edgeNoise = Sample(nx * scaleX * 3.25f, (ny + warpNoise) * scaleY * 1.65f, offset * 1.91f, Mathf.Max(1, octaves - 1), falloff);
        return threshold + (edgeNoise - 0.5f) * edgeDetailStrength;
    }

    private static float Sample(float x, float y, Vector2 offset, int octaves, float falloff)
    {
        float amplitude = 1f;
        float frequency = 1f;
        float total = 0f;
        float weight = 0f;
        int octaveCount = Mathf.Max(1, octaves);

        for (int octave = 0; octave < octaveCount; octave++)
        {
            total += Mathf.PerlinNoise(x * frequency + offset.x, y * frequency + offset.y) * amplitude;
            weight += amplitude;
            amplitude *= Mathf.Clamp01(falloff);
            frequency *= 2f;
        }

        return weight > 0f ? total / weight : 0f;
    }

    private static float RandomRange(System.Random random, float min, float max)
    {
        return min + (float)random.NextDouble() * (max - min);
    }
}
