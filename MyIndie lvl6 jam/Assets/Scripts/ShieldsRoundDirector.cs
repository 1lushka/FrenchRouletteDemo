using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;

public class ShieldsRoundDirector : MonoBehaviour
{
    [SerializeField] private List<Transform> shields = new List<Transform>();

    [Header("Hide / Show")]
    [SerializeField] private float hideOffsetY = 8f;
    [SerializeField] private float dropDuration = 0.45f;
    [SerializeField] private Ease dropEase = Ease.OutQuad;
    [SerializeField] private float showStagger = 0.15f;

    [SerializeField] private bool toggleColliders = true;

    private float[] baseY;
    private Collider[][] colliders;
    private bool[] visible;

    public float DropDuration => dropDuration;
    public int Count => shields.Count;

    private void Awake()
    {
        if (shields.Count == 0)
        {
            foreach (Transform child in transform)
                shields.Add(child);
        }

        baseY = new float[shields.Count];
        colliders = new Collider[shields.Count][];
        visible = new bool[shields.Count];

        for (int i = 0; i < shields.Count; i++)
        {
            if (shields[i] == null)
                continue;

            baseY[i] = shields[i].position.y;

            colliders[i] = toggleColliders
                ? shields[i].GetComponentsInChildren<Collider>(true)
                : null;
        }

        HideAll();
    }

    public float GetShieldWorldX(int index)
    {
        if (index < 0 || index >= shields.Count || shields[index] == null)
            return 0f;

        return shields[index].position.x;
    }

    public void HideAll()
    {
        for (int i = 0; i < shields.Count; i++)
            SetHidden(i, true, 0f);
    }

    public Tween Drop(int index, float delay = 0f)
    {
        if (index < 0 || index >= shields.Count || shields[index] == null)
            return null;

        if (visible[index])
            return null;

        return SetHidden(index, false, delay);
    }

    public void ShowAll(float stagger = -1f)
    {
        if (stagger < 0f)
            stagger = showStagger;

        for (int i = 0; i < shields.Count; i++)
            Drop(i, i * stagger);
    }

    public void RandomizePositions(int segmentSize)
    {
        int totalSegments = ShieldSegmentLayout.EnemyCount;

        if (totalSegments == 0)
        {
            Debug.LogWarning("[EnemyShields] Enemy Tapes не назначены в ShieldController.");
            return;
        }

        segmentSize = Mathf.Clamp(segmentSize, 1, totalSegments);

        if (shields.Count * segmentSize > totalSegments)
        {
            Debug.LogWarning(
                $"[EnemyShields] Недостаточно места: {shields.Count} щитов по {segmentSize} сегмента, всего {totalSegments}."
            );
            return;
        }

        List<int> candidates = new List<int>();

        for (int i = 0; i <= totalSegments - segmentSize; i++)
            candidates.Add(i);

        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);
            (candidates[i], candidates[randomIndex]) =
                (candidates[randomIndex], candidates[i]);
        }

        List<int> selected = new List<int>();

        if (!FindPositions(candidates, selected, 0, segmentSize))
        {
            Debug.LogWarning("[EnemyShields] Не удалось найти свободные позиции.");
            return;
        }

        for (int i = 0; i < shields.Count; i++)
        {
            if (shields[i] == null)
                continue;

            Vector3 position = shields[i].position;
            position.x = ShieldSegmentLayout.EnemySlotCenter(
                selected[i],
                segmentSize
            );

            shields[i].position = position;
        }
    }

    private bool FindPositions(
        List<int> candidates,
        List<int> selected,
        int startIndex,
        int segmentSize)
    {
        if (selected.Count == shields.Count)
            return true;

        for (int i = startIndex; i < candidates.Count; i++)
        {
            int candidate = candidates[i];
            bool overlaps = false;

            foreach (int selectedStart in selected)
            {
                if (Mathf.Abs(candidate - selectedStart) < segmentSize)
                {
                    overlaps = true;
                    break;
                }
            }

            if (overlaps)
                continue;

            selected.Add(candidate);

            if (FindPositions(candidates, selected, i + 1, segmentSize))
                return true;

            selected.RemoveAt(selected.Count - 1);
        }

        return false;
    }

    private Tween SetHidden(int index, bool hidden, float delay)
    {
        Transform tr = shields[index];

        tr.DOKill();

        SetColliders(index, !hidden);
        visible[index] = !hidden;

        if (hidden)
        {
            tr.position = new Vector3(
                tr.position.x,
                baseY[index] + hideOffsetY,
                tr.position.z
            );

            return null;
        }

        tr.position = new Vector3(
            tr.position.x,
            baseY[index] + hideOffsetY,
            tr.position.z
        );

        return tr.DOMoveY(baseY[index], dropDuration)
            .SetEase(dropEase)
            .SetDelay(delay)
            .SetLink(tr.gameObject, LinkBehaviour.KillOnDestroy);
    }

    private void SetColliders(int index, bool enabled)
    {
        if (!toggleColliders || colliders == null || colliders[index] == null)
            return;

        foreach (Collider c in colliders[index])
        {
            if (c != null)
                c.enabled = enabled;
        }
    }
}