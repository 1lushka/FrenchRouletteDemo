using UnityEngine;
using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnemyAI enemyAI;
    [SerializeField] private GameObject barrier;
    [SerializeField] private Animator ropeAnimator;
    [SerializeField] private Animator handAnimator;
    [SerializeField] private TextMeshPro waveText;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] barrierOpenSounds;
    [SerializeField] private AudioClip[] barrierCloseSounds;
    [SerializeField] private AudioClip[] roundStartSounds;

    [Header("Settings")]
    [SerializeField] private float respawnDelay = 3f;
    [SerializeField] private float barrierMoveDistance = 2f;
    [SerializeField] private float barrierMoveDuration = 0.5f;
    [SerializeField] private float delayBeforeAttack = 1f;
    [SerializeField, Range(0.05f, 1f)] private float playerAttackSpeedMultiplier = 0.35f;

    [Header("Tutorial")]
    [SerializeField] private bool runTutorial = true;
    [SerializeField] private ShieldsRoundDirector shields;
    [SerializeField] private TapePiece tutorialTapeStep1;
    [SerializeField] private TapePiece tutorialTapeStep2;
    [SerializeField] private int tutorialAxeIndex = 0;
    [SerializeField] private bool hideUnusedAxes = true;
    [SerializeField] private float tutorialLandTimeout = 4f;
    [SerializeField] private float tutorialSettleDelay = 0.5f;
    [SerializeField] private float tutorialShieldDelay = 0.7f;
    [SerializeField] private float tutorialShowAllDelay = 0.8f;

    [Header("Enemy Shields")]
    [SerializeField] private ShieldsRoundDirector enemyShields;
    [SerializeField] private ShieldController shieldController;

    private bool waitingForAttack;
    private bool canAttack;
    private bool isBarrierDown = true;
    private bool roundInProgress;

    private int roundCount;
    private Vector3 barrierOriginalPosition;

    private void Start()
    {
        if (barrier != null)
            barrierOriginalPosition = barrier.transform.position;

        if (enemyAI == null)
            Debug.LogError("[GameManager] EnemyAI не назначен.");

        if (shields == null)
            Debug.LogError("[GameManager] Директор щитов игрока не назначен.");

        if (enemyShields == null)
            Debug.LogError("[GameManager] Директор щитов врага не назначен.");

        if (shieldController == null)
            Debug.LogError("[GameManager] ShieldController не назначен.");

        UpdateWaveText();

        StartCoroutine(GameLoop());
    }

    private IEnumerator GameLoop()
    {
        if (enemyAI == null || shields == null ||
            enemyShields == null || shieldController == null)
        {
            yield break;
        }

        if (runTutorial)
        {
            yield return TutorialRoutine();
        }
        else
        {
            shields.ShowAll(0f);
        }

        while (true)
        {
            roundCount++;
            UpdateWaveText();

            if (roundCount == 10)
            {
                Debug.Log("игрок победил");
                SceneManager.LoadScene("Win scene");
                yield break;
            }

            // ФАЗА 1: ИГРОК ЗАЩИЩАЕТСЯ

            yield return ShowBarrier();

            shields.ShowAll();

            enemyAI.MakeMove();

            canAttack = false;
            waitingForAttack = true;

            yield return new WaitUntil(() => canAttack);

            waitingForAttack = false;
            roundInProgress = true;
            canAttack = false;

            yield return new WaitForSeconds(delayBeforeAttack);

            // ВРАГ АТАКУЕТ ИГРОКА

            enemyAI.StartAttack();

            yield return HideBarrier();

            yield return WaitForAxesLanded();

            yield return new WaitForSeconds(respawnDelay);

            // СМЕНА РОЛЕЙ

            yield return ShowBarrier();

            shields.HideAll();

            enemyAI.ReturnAxesToPlayer();

            for (int i = 0; i < enemyAI.AxeCount; i++)
            {
                Axe axe = enemyAI.GetAxe(i);

                if (axe == null)
                    continue;

                Transform axeTransform = axe.transform;
                axeTransform.DOKill();

                axeTransform.DORotate(
                    axeTransform.eulerAngles + Vector3.up * 180f,
                    0.35f
                )
                .SetEase(Ease.OutQuad);
            }

            yield return new WaitForSeconds(0.4f);

            roundInProgress = false;

            enemyShields.RandomizePositions(
                shieldController.ShieldSegmentSize
            );

            enemyShields.ShowAll();

            yield return new WaitForSeconds(
                enemyShields.DropDuration +
                0.15f * (enemyShields.Count - 1) +
                0.3f
            );

            // ФАЗА 2: ИГРОК АТАКУЕТ ВРАГА

            yield return ShowBarrier();

            canAttack = false;
            waitingForAttack = true;

            yield return new WaitUntil(() => canAttack);

            waitingForAttack = false;
            roundInProgress = true;
            canAttack = false;

            yield return new WaitForSeconds(delayBeforeAttack);

            // НОЖИ АТАКУЮТ ВРАГА

            enemyAI.StartAttack(playerAttackSpeedMultiplier, true);

            yield return HideBarrier();

            yield return WaitForAxesLanded();

            yield return new WaitForSeconds(respawnDelay);

            // СНАЧАЛА ЗАКРЫВАЕМ БАРЬЕР

            yield return ShowBarrier();

            // ПОСЛЕ ПОЛНОГО ЗАКРЫТИЯ ПОЯВЛЯЮТСЯ ЩИТЫ ИГРОКА

            enemyShields.HideAll();
            shields.ShowAll();

            // ВОЗВРАЩАЕМ НОЖИ НА ИСХОДНУЮ СТОРОНУ

            enemyAI.ReturnAxes();

            for (int i = 0; i < enemyAI.AxeCount; i++)
            {
                Axe axe = enemyAI.GetAxe(i);

                if (axe == null)
                    continue;

                Transform axeTransform = axe.transform;
                axeTransform.DOKill();

                axeTransform.DORotate(
                    axeTransform.eulerAngles + Vector3.up * 180f,
                    0.35f
                )
                .SetEase(Ease.OutQuad);
            }

            yield return new WaitForSeconds(0.4f);

            roundInProgress = false;
        }
    }

    private IEnumerator WaitForAxesLanded()
    {
        float timer = 0f;

        while (timer < 10f)
        {
            bool allLanded = true;

            for (int i = 0; i < enemyAI.AxeCount; i++)
            {
                Axe axe = enemyAI.GetAxe(i);

                if (axe != null && axe.IsFlying)
                {
                    allLanded = false;
                    break;
                }
            }

            if (allLanded)
                yield break;

            timer += Time.deltaTime;
            yield return null;
        }

        Debug.LogWarning("[GameManager] Истекло время ожидания приземления ножей.");
    }

    private IEnumerator TutorialRoutine()
    {
        if (enemyAI == null)
            yield break;

        if (shields == null)
            yield break;

        TapePiece tape1 = ResolveTutorialTape(tutorialTapeStep1);
        TapePiece tape2 = ResolveTutorialTape(tutorialTapeStep2);

        if (tape1 == null || tape2 == null)
        {
            Debug.LogWarning(
                "[Tutorial] Не найдена верёвка для ножа — туториал пропущен."
            );

            yield break;
        }

        if (hideUnusedAxes)
            SetUnusedAxesActive(false);

        yield return TutorialThrow(tape1);

        yield return ResetBeforeShield(tape2);

        yield return new WaitForSeconds(tutorialShieldDelay);

        shields.Drop(0);

        yield return new WaitForSeconds(
            shields.DropDuration + 0.3f
        );

        yield return TutorialThrow(tape2, false);

        yield return ShowBarrier();

        enemyAI.ReturnAxes();

        if (hideUnusedAxes)
            SetUnusedAxesActive(true);

        yield return new WaitForSeconds(tutorialShowAllDelay);

        shields.ShowAll();

        yield return new WaitForSeconds(
            shields.DropDuration +
            0.15f * (shields.Count - 1) +
            0.3f
        );
    }

    private IEnumerator ResetBeforeShield(TapePiece nextTape)
    {
        yield return ShowBarrier();

        enemyAI.MakeMoveSingle(tutorialAxeIndex, nextTape);
    }

    private IEnumerator TutorialThrow(
        TapePiece tape,
        bool resetFirst = true)
    {
        roundInProgress = false;
        canAttack = false;

        if (resetFirst)
        {
            yield return ShowBarrier();

            enemyAI.MakeMoveSingle(tutorialAxeIndex, tape);
        }

        yield return new WaitUntil(() => canAttack);

        canAttack = false;

        yield return new WaitForSeconds(delayBeforeAttack);

        enemyAI.StartAttackSingle(tutorialAxeIndex);

        canAttack = false;

        yield return HideBarrier();

        yield return WaitLanded(
            tutorialAxeIndex,
            tutorialLandTimeout
        );

        yield return new WaitForSeconds(tutorialSettleDelay);
    }

    private IEnumerator WaitLanded(int axeIndex, float timeout)
    {
        Axe axe = enemyAI.GetAxe(axeIndex);
        float t = 0f;

        while (axe != null && axe.IsFlying && t < timeout)
        {
            t += Time.deltaTime;
            yield return null;
        }
    }

    private TapePiece ResolveTutorialTape(TapePiece tape)
    {
        if (tape != null)
            return tape;

        if (shields == null || enemyAI == null)
            return null;

        return enemyAI.GetTapeClosestToX(
            shields.GetShieldWorldX(0)
        );
    }

    private void SetUnusedAxesActive(bool active)
    {
        for (int i = 0; i < enemyAI.AxeCount; i++)
        {
            if (i == tutorialAxeIndex)
                continue;

            Axe axe = enemyAI.GetAxe(i);

            if (axe != null)
                axe.gameObject.SetActive(active);
        }
    }

    public void StartRound()
    {
        if (!isBarrierDown)
        {
            Debug.Log(
                "Нельзя начать раунд — барьер ещё не опущен!"
            );

            return;
        }

        if (roundInProgress)
        {
            Debug.Log(
                "Нельзя начать новый раунд — текущий ещё не закончился!"
            );

            return;
        }

        canAttack = true;

        if (ropeAnimator != null)
            ropeAnimator.SetTrigger("StartRound");

        if (handAnimator != null)
            handAnimator.SetTrigger("StartRound");

        PlayRandomSound(roundStartSounds);
    }

    private IEnumerator ShowBarrier()
    {
        isBarrierDown = true;

        if (barrier == null)
            yield break;

        PlayRandomSound(barrierCloseSounds);

        Tween tween = barrier.transform
            .DOMoveY(
                barrierOriginalPosition.y,
                barrierMoveDuration
            )
            .SetEase(Ease.InQuad);

        yield return tween.WaitForCompletion();
    }

    private IEnumerator HideBarrier()
    {
        isBarrierDown = false;

        if (barrier == null)
            yield break;

        PlayRandomSound(barrierOpenSounds);

        Tween tween = barrier.transform
            .DOMoveY(
                barrierOriginalPosition.y + barrierMoveDistance,
                barrierMoveDuration
            )
            .SetEase(Ease.OutQuad);

        yield return tween.WaitForCompletion();
    }

    private void UpdateWaveText()
    {
        if (waveText == null)
            return;

        waveText.text = $"ROUND: {roundCount}";

        waveText.DOFade(1f, 0.3f).From(0f);

        waveText.transform.DOPunchScale(
            Vector3.one * 0.1f,
            0.3f,
            6,
            0.5f
        );
    }

    private void PlayRandomSound(AudioClip[] clips)
    {
        if (audioSource == null || clips == null || clips.Length == 0)
            return;

        int start = Random.Range(0, clips.Length);

        for (int i = 0; i < clips.Length; i++)
        {
            AudioClip clip = clips[(start + i) % clips.Length];

            if (clip == null)
                continue;

            audioSource.PlayOneShot(clip);
            return;
        }
    }
}

