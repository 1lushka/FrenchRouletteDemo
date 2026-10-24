using System.Collections.Generic;
using UnityEngine;

public static class ShieldSegmentLayout
{
    private static readonly List<float> _centers = new List<float>();
    private static readonly List<TapePiece> _segments = new List<TapePiece>();

    public static int Count => _centers.Count;
    public static float Spacing { get; private set; } = 0.1f;
    public static IReadOnlyList<TapePiece> Segments => _segments;

    public static void Build()
    {
        _centers.Clear();
        _segments.Clear();

        TapePiece[] found = Object.FindObjectsByType<TapePiece>(FindObjectsSortMode.None);
        var sorted = new List<TapePiece>(found);
        sorted.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));

        foreach (TapePiece tape in sorted)
        {
            if (tape == null) continue;
            _segments.Add(tape);
            _centers.Add(tape.transform.position.x);
        }

        if (_centers.Count >= 2)
            Spacing = (_centers[_centers.Count - 1] - _centers[0]) / (_centers.Count - 1);
        else
            Spacing = 0.1f;
    }

    public static float SlotCenter(int first, int size)
    {
        if (_centers.Count == 0) return 0f;

        size = Mathf.Clamp(size, 1, _centers.Count);
        first = Mathf.Clamp(first, 0, _centers.Count - size);

        if (size % 2 == 1)
            return _centers[first + (size - 1) / 2];

        int a = first + size / 2 - 1; 
        int b = first + size / 2;
        return (_centers[a] + _centers[b]) * 0.5f;
    }

    public static int SnapFirstIndex(float worldX, int size)
    {
        if (_centers.Count == 0) return 0;

        size = Mathf.Clamp(size, 1, _centers.Count);
        int maxFirst = _centers.Count - size;

        int best = 0;
        float bestDist = float.MaxValue;
        for (int f = 0; f <= maxFirst; f++)
        {
            float d = Mathf.Abs(SlotCenter(f, size) - worldX);
            if (d < bestDist - 1e-7f)
            {
                bestDist = d;
                best = f;
            }
        }
        return best;
    }

    public static void GetClampRange(int size, out float minX, out float maxX)
    {
        if (_centers.Count == 0)
        {
            minX = maxX = 0f;
            return;
        }

        size = Mathf.Clamp(size, 1, _centers.Count);
        float width = size * Spacing;

        minX = _centers[0] - Spacing * 0.5f + width * 0.5f;
        maxX = _centers[_centers.Count - 1] + Spacing * 0.5f - width * 0.5f;
        if (minX > maxX)
        {
            float mid = (minX + maxX) * 0.5f;
            minX = maxX = mid;
        }
    }
}
