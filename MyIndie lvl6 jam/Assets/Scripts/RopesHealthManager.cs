using System;
using System.Collections.Generic;
using UnityEngine;

public class RopesHealthManager : MonoBehaviour
{
    [Header("������ ��� ������������")]
    [SerializeField] private TapePiece[] ropes;

    // ������� "���� ��� �� ����"
    public event Action<int, TapePiece> OnFirstThresholdHit; // (�����, ������)
    public event Action<TapePiece> OnFirstHit2;
    public event Action<TapePiece> OnFirstHit1;
    public event Action<TapePiece> OnFirstHit0;

    // ������ ��������� �������� �� ������ ������ ��� ������� "�������� ����� �����"
    private readonly Dictionary<TapePiece, int> _lastHealth = new();

    // ��������� ����������� ��� ����������� ������ (����� ���� �������)
    private static readonly HashSet<int> _firedThresholdsGlobal = new HashSet<int>();

    private static readonly int[] _thresholds = { 2, 1, 0 };

    private void OnEnable()
    {
        if (ropes == null) return;

        foreach (var rope in ropes)
        {
            if (rope == null) continue;

            if (!_lastHealth.ContainsKey(rope))
                _lastHealth[rope] = rope.Health; // ������ ��������� ��������

            rope.HealthChanged += HandleHealthChanged;
        }
    }

    private void OnDisable()
    {
        if (ropes == null) return;

        foreach (var rope in ropes)
        {
            if (rope == null) continue;
            rope.HealthChanged -= HandleHealthChanged;
        }
    }

    private void HandleHealthChanged(TapePiece rope, int newHealth)
    {
        if (!_lastHealth.TryGetValue(rope, out var prev))
            prev = newHealth;

        // ��������� ����������� ������� ������ ������ ����
        foreach (var t in _thresholds)
        {
            if (_firedThresholdsGlobal.Contains(t))
                continue; // ��� �������� ���� ����� ����� � ���� ����

            // "�������� ����� �����": ���� > t, ����� <= t
            if (prev > t && newHealth <= t)
            {
                _firedThresholdsGlobal.Add(t);

                OnFirstThresholdHit?.Invoke(t, rope);

                switch (t)
                {
                    case 2: OnFirstHit2?.Invoke(rope); break;
                    case 1: OnFirstHit1?.Invoke(rope); break;
                    case 0: OnFirstHit0?.Invoke(rope); break;
                }

                // ����� �������� � �� ���� ������ �������� �������� ���� ���������� ����� �����������
                break;
            }
        }

        _lastHealth[rope] = newHealth;
    }

    // �����������: ����� ��� ������� ������ ����� ��������/���������
    public static void ResetGlobalThresholds()
    {
        _firedThresholdsGlobal.Clear();
    }
    void Start()
    {
        ResetGlobalThresholds(); // сброс статических порогов при (пере)запуске сцены
        HookRopesHealthDebug(this);
    }

    public static void HookRopesHealthDebug(RopesHealthManager mgr)
    {
        if (mgr == null)
        {
            Debug.LogWarning("[RopesHealth] Manager not found.");
            return;
        }

        mgr.OnFirstThresholdHit += (t, rope) =>
            Debug.Log($"[RopesHealth] GLOBAL threshold {t} hit by '{rope?.name}'");

        mgr.OnFirstHit2 += rope =>
            Debug.Log($"[RopesHealth] FIRST 2 hit by '{rope?.name}'");

        mgr.OnFirstHit1 += rope =>
            Debug.Log($"[RopesHealth] FIRST 1 hit by '{rope?.name}'");

        mgr.OnFirstHit0 += rope =>
            Debug.Log($"[RopesHealth] FIRST 0 hit by '{rope?.name}'");
    }

}
