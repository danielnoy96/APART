using System.Collections.Generic;
using UnityEngine;

public static class WoodCrumbleRegionBuilder
{
    private sealed class Region
    {
        public readonly List<int> cells = new List<int>();
        public float centerX;
        public float centerY;

        public void Add(int cell, int width)
        {
            cells.Add(cell);
            centerX += cell % width + 0.5f;
            centerY += cell / width + 0.5f;
        }

        public void FinalizeCenter()
        {
            centerX /= cells.Count;
            centerY /= cells.Count;
        }
    }

    public static List<List<int>> Build(bool[] mask, int width, int height, int minIslandArea, int maxPieces)
    {
        List<Region> regions = FloodFill(mask, width, height);
        if (regions.Count == 0)
        {
            return new List<List<int>>();
        }

        regions.Sort((left, right) => right.cells.Count.CompareTo(left.cells.Count));
        return AssignAllCellsToKeptRegions(regions, width, height, mask.Length, minIslandArea, maxPieces);
    }

    private static List<Region> FloodFill(bool[] mask, int width, int height)
    {
        bool[] visited = new bool[mask.Length];
        var regions = new List<Region>();
        var pending = new Queue<int>();

        for (int start = 0; start < mask.Length; start++)
        {
            if (visited[start])
            {
                continue;
            }

            bool value = mask[start];
            var region = new Region();
            visited[start] = true;
            pending.Enqueue(start);

            while (pending.Count > 0)
            {
                int cell = pending.Dequeue();
                int x = cell % width;
                int y = cell / width;
                region.Add(cell, width);

                QueueNeighbor(x - 1, y, width, height, value, mask, visited, pending);
                QueueNeighbor(x + 1, y, width, height, value, mask, visited, pending);
                QueueNeighbor(x, y - 1, width, height, value, mask, visited, pending);
                QueueNeighbor(x, y + 1, width, height, value, mask, visited, pending);
            }

            region.FinalizeCenter();
            regions.Add(region);
        }

        return regions;
    }

    private static List<List<int>> AssignAllCellsToKeptRegions(List<Region> regions, int width, int height, int totalCells, int minIslandArea, int maxPieces)
    {
        List<Region> kept = GetKeptRegions(regions, minIslandArea, maxPieces);
        int[] assignment = CreateInitialAssignment(kept, totalCells);

        GrowAssignments(assignment, width, height);
        FillRemainingByNearestRegion(assignment, width, kept);

        var shards = new List<List<int>>(kept.Count);
        for (int i = 0; i < kept.Count; i++)
        {
            shards.Add(new List<int>());
        }

        for (int cell = 0; cell < assignment.Length; cell++)
        {
            if (assignment[cell] >= 0)
            {
                shards[assignment[cell]].Add(cell);
            }
        }

        return shards;
    }

    private static List<Region> GetKeptRegions(List<Region> regions, int minIslandArea, int maxPieces)
    {
        int limit = Mathf.Clamp(maxPieces, 1, regions.Count);
        int minimumArea = Mathf.Max(1, minIslandArea);
        var kept = new List<Region>(limit);

        foreach (Region region in regions)
        {
            if (kept.Count >= limit)
            {
                break;
            }

            if (region.cells.Count >= minimumArea)
            {
                kept.Add(region);
            }
        }

        if (kept.Count == 0)
        {
            kept.Add(regions[0]);
        }

        return kept;
    }

    private static int[] CreateInitialAssignment(List<Region> kept, int totalCells)
    {
        int[] assignment = new int[totalCells];
        for (int i = 0; i < assignment.Length; i++)
        {
            assignment[i] = -1;
        }

        for (int regionIndex = 0; regionIndex < kept.Count; regionIndex++)
        {
            foreach (int cell in kept[regionIndex].cells)
            {
                assignment[cell] = regionIndex;
            }
        }

        return assignment;
    }

    private static void GrowAssignments(int[] assignment, int width, int height)
    {
        int remaining = CountUnassigned(assignment);
        int safety = assignment.Length;

        while (remaining > 0 && safety-- > 0)
        {
            bool changed = false;

            for (int cell = 0; cell < assignment.Length; cell++)
            {
                if (assignment[cell] >= 0)
                {
                    continue;
                }

                int neighbor = GetAssignedNeighbor(assignment, cell % width, cell / width, width, height);
                if (neighbor < 0)
                {
                    continue;
                }

                assignment[cell] = neighbor;
                remaining--;
                changed = true;
            }

            if (!changed)
            {
                return;
            }
        }
    }

    private static void FillRemainingByNearestRegion(int[] assignment, int width, List<Region> kept)
    {
        for (int cell = 0; cell < assignment.Length; cell++)
        {
            if (assignment[cell] < 0)
            {
                assignment[cell] = GetNearestRegion(cell, width, kept);
            }
        }
    }

    private static void QueueNeighbor(int x, int y, int width, int height, bool value, bool[] mask, bool[] visited, Queue<int> pending)
    {
        if (x < 0 || x >= width || y < 0 || y >= height)
        {
            return;
        }

        int index = WoodCrumbleMaskGenerator.ToIndex(x, y, width);
        if (visited[index] || mask[index] != value)
        {
            return;
        }

        visited[index] = true;
        pending.Enqueue(index);
    }

    private static int CountUnassigned(int[] assignment)
    {
        int count = 0;
        foreach (int regionIndex in assignment)
        {
            if (regionIndex < 0)
            {
                count++;
            }
        }

        return count;
    }

    private static int GetAssignedNeighbor(int[] assignment, int x, int y, int width, int height)
    {
        int left = GetAssignedCell(assignment, x - 1, y, width, height);
        int right = GetAssignedCell(assignment, x + 1, y, width, height);
        int down = GetAssignedCell(assignment, x, y - 1, width, height);
        int up = GetAssignedCell(assignment, x, y + 1, width, height);

        if (left >= 0)
        {
            return left;
        }

        if (right >= 0)
        {
            return right;
        }

        return down >= 0 ? down : up;
    }

    private static int GetAssignedCell(int[] assignment, int x, int y, int width, int height)
    {
        if (x < 0 || x >= width || y < 0 || y >= height)
        {
            return -1;
        }

        return assignment[WoodCrumbleMaskGenerator.ToIndex(x, y, width)];
    }

    private static int GetNearestRegion(int cell, int width, List<Region> kept)
    {
        int x = cell % width;
        int y = cell / width;
        int nearest = 0;
        float nearestDistance = float.PositiveInfinity;

        for (int i = 0; i < kept.Count; i++)
        {
            float dx = kept[i].centerX - x;
            float dy = kept[i].centerY - y;
            float distance = dx * dx + dy * dy;

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = i;
            }
        }

        return nearest;
    }
}
