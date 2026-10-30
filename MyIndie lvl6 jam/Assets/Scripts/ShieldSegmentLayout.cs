
using System.Collections.Generic;
using UnityEngine;

public static class ShieldSegmentLayout
{
    private static readonly List<float> playerCenters = new List<float>();
    private static readonly List<float> enemyCenters = new List<float>();

    private static readonly List<TapePiece> playerSegments = new List<TapePiece>();
    private static readonly List<TapePiece> enemySegments = new List<TapePiece>();

    public static int PlayerCount => playerCenters.Count;
    public static int EnemyCount => enemyCenters.Count;

    public static float PlayerSpacing { get; private set; } = 0.1f;
    public static float EnemySpacing { get; private set; } = 0.1f;

    public static IReadOnlyList<TapePiece> Segments => playerSegments;
    public static IReadOnlyList<TapePiece> EnemySegments => enemySegments;

    public static void Build(TapePiece[] playerTapes, TapePiece[] enemyTapes)
    {
        BuildLine(playerTapes, playerCenters, playerSegments, out float playerSpacing);
        BuildLine(enemyTapes, enemyCenters, enemySegments, out float enemySpacing);

        PlayerSpacing = playerSpacing;
        EnemySpacing = enemySpacing;

        Debug.Log(
            $"[ShieldSegmentLayout] Player: {PlayerCount} segments, spacing {PlayerSpacing:0.###}. Enemy: {EnemyCount} segments, spacing {EnemySpacing:0.###}."
        );
    }

    private static void BuildLine(
        TapePiece[] tapes,
        List<float> centers,
        List<TapePiece> segments,
        out float spacing)
    {
        centers.Clear();
        segments.Clear();

        if (tapes != null)
        {
            List<TapePiece> sorted = new List<TapePiece>();

            foreach (TapePiece tape in tapes)
            {
                if (tape != null)
                    sorted.Add(tape);
            }

            sorted.Sort(
                (a, b) => a.transform.position.x.CompareTo(
                    b.transform.position.x
                )
            );

            foreach (TapePiece tape in sorted)
            {
                float x = tape.transform.position.x;

                if (centers.Count == 0 ||
                    Mathf.Abs(centers[centers.Count - 1] - x) > 0.001f)
                {
                    centers.Add(x);
                    segments.Add(tape);
                }
            }
        }

        if (centers.Count >= 2)
        {
            spacing = (centers[centers.Count - 1] - centers[0]) /
                      (centers.Count - 1);
        }
        else
        {
            spacing = 0.1f;
        }
    }

    public static float SlotCenter(int first, int size)
    {
        return GetSlotCenter(playerCenters, first, size);
    }

    public static float EnemySlotCenter(int first, int size)
    {
        return GetSlotCenter(enemyCenters, first, size);
    }

    private static float GetSlotCenter(List<float> centers, int first, int size)
    {
        if (centers.Count == 0)
            return 0f;

        size = Mathf.Clamp(size, 1, centers.Count);
        first = Mathf.Clamp(first, 0, centers.Count - size);

        if (size % 2 == 1)
            return centers[first + (size - 1) / 2];

        int a = first + size / 2 - 1;
        int b = first + size / 2;

        return (centers[a] + centers[b]) * 0.5f;
    }

    public static int SnapFirstIndex(float worldX, int size)
    {
        return GetSnapFirstIndex(playerCenters, worldX, size);
    }

    public static int EnemySnapFirstIndex(float worldX, int size)
    {
        return GetSnapFirstIndex(enemyCenters, worldX, size);
    }

    private static int GetSnapFirstIndex(
        List<float> centers,
        float worldX,
        int size)
    {
        if (centers.Count == 0)
            return 0;

        size = Mathf.Clamp(size, 1, centers.Count);

        int maxFirst = centers.Count - size;
        int best = 0;
        float bestDist = float.MaxValue;

        for (int f = 0; f <= maxFirst; f++)
        {
            float d = Mathf.Abs(GetSlotCenter(centers, f, size) - worldX);

            if (d < bestDist)
            {
                bestDist = d;
                best = f;
            }
        }

        return best;
    }

    public static void GetClampRange(int size, out float minX, out float maxX)
    {
        GetClampRangeFor(
            playerCenters,
            PlayerSpacing,
            size,
            out minX,
            out maxX
        );
    }

    public static void GetEnemyClampRange(
        int size,
        out float minX,
        out float maxX)
    {
        GetClampRangeFor(
            enemyCenters,
            EnemySpacing,
            size,
            out minX,
            out maxX
        );
    }

    private static void GetClampRangeFor(
        List<float> centers,
        float spacing,
        int size,
        out float minX,
        out float maxX)
    {
        if (centers.Count == 0)
        {
            minX = maxX = 0f;
            return;
        }

        size = Mathf.Clamp(size, 1, centers.Count);

        float width = size * spacing;

        minX = centers[0] - spacing * 0.5f + width * 0.5f;
        maxX = centers[centers.Count - 1] + spacing * 0.5f - width * 0.5f;

        if (minX > maxX)
        {
            float mid = (minX + maxX) * 0.5f;
            minX = maxX = mid;
        }
    }
}
