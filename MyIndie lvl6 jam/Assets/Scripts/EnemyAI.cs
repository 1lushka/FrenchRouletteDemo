
using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;

public class EnemyAI : MonoBehaviour
{
    [Header("Player Tapes")]
    [SerializeField] private TapePiece[] playerTapePieces;

    [Header("Enemy Tapes")]
    [SerializeField] private TapePiece[] enemyTapePieces;

    [Header("Axes")]
    [SerializeField] private Transform[] axes;
    [SerializeField] private Transform[] playerAxePositions;

    private Vector3[] startPositions;

    private void Awake()
    {
        if (axes != null && axes.Length > 0)
        {
            startPositions = new Vector3[axes.Length];

            for (int i = 0; i < axes.Length; i++)
            {
                if (axes[i] != null)
                    startPositions[i] = axes[i].position;
            }
        }
    }

    public void MakeMove()
    {
        MoveAxesToRandomTapes(playerTapePieces);
    }

    public void MakeMoveEnemy()
    {
        MoveAxesToRandomTapes(enemyTapePieces);
    }

    private void MoveAxesToRandomTapes(TapePiece[] tapes)
    {
        if (tapes == null || tapes.Length == 0 ||
            axes == null || axes.Length == 0)
        {
            Debug.LogWarning("Не назначены верёвки или топоры.");
            return;
        }

        List<TapePiece> availablePieces = new List<TapePiece>();

        foreach (TapePiece tape in tapes)
        {
            if (tape != null)
                availablePieces.Add(tape);
        }

        for (int i = 0; i < axes.Length; i++)
        {
            Transform axe = axes[i];

            if (axe == null)
                continue;

            if (availablePieces.Count == 0)
            {
                Debug.LogWarning("Не хватает TapePiece для всех топоров!");
                break;
            }

            int randIndex = Random.Range(0, availablePieces.Count);
            TapePiece chosenPiece = availablePieces[randIndex];
            availablePieces.RemoveAt(randIndex);

            float startY = startPositions != null && startPositions.Length > i
                ? startPositions[i].y : axe.position.y;

            float startZ = startPositions != null && startPositions.Length > i
                ? startPositions[i].z : axe.position.z;

            Vector3 targetPos = new Vector3(
                chosenPiece.transform.position.x,
                startY,
                startZ
            );

            ObjectMover.MoveTo(axe, targetPos);
        }
    }

    public void StartAttack(float speedMultiplier = 1f, bool attackEnemy = false)
    {
        if (axes == null)
            return;

        for (int i = 0; i < axes.Length; i++)
        {
            Transform axe = axes[i];

            if (axe == null)
                continue;

            Axe axeCtrl = axe.GetComponent<Axe>();

            if (axeCtrl != null)
                axeCtrl.Throw(speedMultiplier, attackEnemy);
        }
    }

    public int AxeCount => axes == null ? 0 : axes.Length;

    public int PlayerTapeCount => playerTapePieces == null ? 0 : playerTapePieces.Length;

    public int EnemyTapeCount => enemyTapePieces == null ? 0 : enemyTapePieces.Length;

    public Axe GetAxe(int index)
    {
        if (axes == null || index < 0 || index >= axes.Length || axes[index] == null)
            return null;

        return axes[index].GetComponent<Axe>();
    }

    public TapePiece GetTapeClosestToX(float x)
    {
        return GetTapeClosestToX(playerTapePieces, x);
    }

    public TapePiece GetEnemyTapeClosestToX(float x)
    {
        return GetTapeClosestToX(enemyTapePieces, x);
    }

    private TapePiece GetTapeClosestToX(TapePiece[] tapes, float x)
    {
        if (tapes == null || tapes.Length == 0)
            return null;

        TapePiece best = null;
        float bestDist = float.MaxValue;

        foreach (TapePiece tape in tapes)
        {
            if (tape == null)
                continue;

            float d = Mathf.Abs(tape.transform.position.x - x);

            if (d < bestDist)
            {
                bestDist = d;
                best = tape;
            }
        }

        return best;
    }

    public void MakeMoveSingle(int axeIndex, TapePiece tape)
    {
        if (axes == null || axeIndex < 0 || axeIndex >= axes.Length)
            return;

        if (tape == null)
            return;

        Transform axe = axes[axeIndex];

        if (axe == null)
            return;

        float startY = startPositions != null && startPositions.Length > axeIndex
            ? startPositions[axeIndex].y : axe.position.y;

        float startZ = startPositions != null && startPositions.Length > axeIndex
            ? startPositions[axeIndex].z : axe.position.z;

        Vector3 targetPos = new Vector3(
            tape.transform.position.x,
            startY,
            startZ
        );

        ObjectMover.MoveTo(axe, targetPos);
    }

    public void ReturnAxes()
    {
        if (startPositions == null || axes == null)
            return;

        for (int i = 0; i < axes.Length && i < startPositions.Length; i++)
        {
            if (axes[i] != null)
                axes[i].position = startPositions[i];
        }
    }

    public void ReturnAxesToPlayer()
    {
        if (axes == null || playerAxePositions == null)
            return;

        int count = Mathf.Min(axes.Length, playerAxePositions.Length);

        for (int i = 0; i < count; i++)
        {
            if (axes[i] == null || playerAxePositions[i] == null)
                continue;

            Axe axe = axes[i].GetComponent<Axe>();

            axes[i].DOKill();

            if (axe != null)
                axe.ResetFlight();

            axes[i].SetPositionAndRotation(
                playerAxePositions[i].position,
                playerAxePositions[i].rotation
            );
        }
    }

    public void StartAttackSingle(int axeIndex)
    {
        Axe axeCtrl = GetAxe(axeIndex);

        if (axeCtrl != null)
            axeCtrl.Throw();
    }
}
