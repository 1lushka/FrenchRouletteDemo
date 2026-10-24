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
    [SerializeField] private AudioClip[] barrierOpenSounds;   // звуки открытия
    [SerializeField] private AudioClip[] barrierCloseSounds;  // звуки закрытия
    [SerializeField] private AudioClip[] roundStartSounds;    // звуки старта раунда

    [Header("Settings")]
    [SerializeField] private float respawnDelay = 3f;
    [SerializeField] private float barrierMoveDistance = 2f;
    [SerializeField] private float barrierMoveDuration = 0.5f;
    [SerializeField] private float delayBeforeAttack = 1f;

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

    private bool waitingForAttack = false;
    private bool canAttack = false;
    private bool isBarrierDown = true;
    private bool roundInProgress = false; // ✅ флаг, что раунд идёт
    private int roundCount = 0;
    private Vector3 barrierOriginalPosition;

    void Start()
    {
        if (barrier != null)
            barrierOriginalPosition = barrier.transform.position;

        UpdateWaveText();
        StartCoroutine(GameLoop());
    }

    private IEnumerator GameLoop()
    {
        if (runTutorial)
        {
            yield return TutorialRoutine();
        }
        else
        {
            // туториал выключен — сразу показываем все щиты
            ShieldsRoundDirector dir = shields != null ? shields : FindFirstObjectByType<ShieldsRoundDirector>();
            if (dir != null) dir.ShowAll(0f);
        }

        while (true)
        {
            roundCount++;
            UpdateWaveText();

            if (roundCount == 10)
            {
                print("игрок победил");
                Debug.Log("игрок победил");
                SceneManager.LoadScene("Win scene");
                yield break; // дальше цикл не идём — сцена победы загружена
            }

            yield return ShowBarrier();

            enemyAI.MakeMove();
            waitingForAttack = true;


            yield return new WaitUntil(() => canAttack);
            roundInProgress = true; 

            waitingForAttack = false;
            canAttack = false;

            yield return new WaitForSeconds(delayBeforeAttack);

            enemyAI.StartAttack();

            yield return HideBarrier();

            // ✅ Ждём, пока враг закончит атаку (если нужно — можно добавить событие окончания)
            yield return new WaitForSeconds(respawnDelay);

            roundInProgress = false; // ✅ теперь можно снова начать новый раунд
        }
    }

    // ============================ ТУТОРИАЛ ============================

    /// <summary>
    /// 1) щитов нет — один нож летит и попадает в верёвку;
    /// 2) по центру появляется щит;
    /// 3) второй нож летит по центру и бьётся о щит;
    /// 4) возвращаются остальные щиты и ножи — дальше обычная игра.
    /// </summary>
    private IEnumerator TutorialRoutine()
    {
        if (enemyAI == null) yield break;

        if (shields == null)
            shields = FindFirstObjectByType<ShieldsRoundDirector>();

        if (shields == null)
        {
            Debug.LogWarning("[Tutorial] ShieldsRoundDirector не найден — туториал пропущен.");
            yield break;
        }

        TapePiece tape1 = ResolveTutorialTape(tutorialTapeStep1);
        TapePiece tape2 = ResolveTutorialTape(tutorialTapeStep2);

        if (tape1 == null || tape2 == null)
        {
            Debug.LogWarning("[Tutorial] Не найдена верёвка для ножа — туториал пропущен.");
            yield break;
        }

        // прячем 2 лишних ножа (до первого yield корутина идёт синхронно внутри Start(),
        // поэтому в первом кадре их не будет видно)
        if (hideUnusedAxes)
            SetUnusedAxesActive(false);

        // 1) щитов нет — один нож
        yield return TutorialThrow(tape1);

        // 2) барьер опускается, нож возвращается — и ТОЛЬКО ПОТОМ падает щит
        yield return ResetBeforeShield(tape2);
        yield return new WaitForSeconds(tutorialShieldDelay);
        if (shields != null)
        {
            shields.Drop(0);
            yield return new WaitForSeconds(shields.DropDuration + 0.3f);
        }

        // 3) второй нож — барьер уже опущен, повторно его не опускаем
        yield return TutorialThrow(tape2, false);

        // 4) барьер опускается, ножи возвращаются — и ТОЛЬКО ПОТОМ 2 оставшихся щита
        yield return ShowBarrier();

        enemyAI.ReturnAxes();

        if (hideUnusedAxes)
            SetUnusedAxesActive(true);

        if (shields != null)
        {
            yield return new WaitForSeconds(tutorialShowAllDelay);
            shields.ShowAll();
            yield return new WaitForSeconds(
                shields.DropDuration + 0.15f * (shields.Count - 1) + 0.3f);
        }
    }

    /// Барьер опускается + нож возвращается на старт и целится под следующий бросок
    private IEnumerator ResetBeforeShield(TapePiece nextTape)
    {
        yield return ShowBarrier();

        enemyAI.MakeMoveSingle(tutorialAxeIndex, nextTape);
    }

    private IEnumerator TutorialThrow(TapePiece tape, bool resetFirst = true)
    {
        roundInProgress = false;
        canAttack = false;

        if (resetFirst)
        {
            yield return ShowBarrier();

            enemyAI.MakeMoveSingle(tutorialAxeIndex, tape);
        }

        // ждём, пока игрок дёрнет верёвку — как в обычном раунде
        yield return new WaitUntil(() => canAttack);
        canAttack = false;

        yield return new WaitForSeconds(delayBeforeAttack);

        enemyAI.StartAttackSingle(tutorialAxeIndex);
        canAttack = false; // сбрасываем, чтобы случайный клик не стартовал следующий раунд

        yield return HideBarrier();

        // ждём реальное попадание ножа
        yield return WaitLanded(tutorialAxeIndex, tutorialLandTimeout);
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
        if (tape != null) return tape;
        if (shields == null || enemyAI == null) return null;
        return enemyAI.GetTapeClosestToX(shields.GetShieldWorldX(0));
    }

    private void SetUnusedAxesActive(bool active)
    {
        for (int i = 0; i < enemyAI.AxeCount; i++)
        {
            if (i == tutorialAxeIndex) continue;

            Axe axe = enemyAI.GetAxe(i);
            if (axe != null)
                axe.gameObject.SetActive(active);
        }
    }

    public void StartRound()
    {
        // ✅ нельзя стартовать, если барьер не опущен или раунд уже идёт
        if (!isBarrierDown)
        {
            Debug.Log("⛔ Нельзя начать раунд — барьер ещё не опущен!");
            return;
        }

        if (roundInProgress)
        {
            Debug.Log("⚠️ Нельзя начать новый раунд — текущий ещё не закончился!");
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

        if (barrier == null) yield break;

        PlayRandomSound(barrierCloseSounds);

        Tween t = barrier.transform.DOMoveY(barrierOriginalPosition.y, barrierMoveDuration)
            .SetEase(Ease.InQuad);

        yield return t.WaitForCompletion();
    }


    private IEnumerator HideBarrier()
    {
        isBarrierDown = false;

        if (barrier == null) yield break;

        PlayRandomSound(barrierOpenSounds);

        Tween t = barrier.transform
            .DOMoveY(barrierOriginalPosition.y + barrierMoveDistance, barrierMoveDuration)
            .SetEase(Ease.OutQuad);

        yield return t.WaitForCompletion();
    }

    private void UpdateWaveText()
    {
        if (waveText != null)
        {
            waveText.text = $"ROUND: {roundCount}";
            waveText.DOFade(1f, 0.3f).From(0f);
            waveText.transform.DOPunchScale(Vector3.one * 0.1f, 0.3f, 6, 0.5f);
        }
    }

    private void PlayRandomSound(AudioClip[] clips)
    {
        if (audioSource == null || clips == null || clips.Length == 0)
            return;

        // пропускаем пустые слоты в массиве (в сцене может лежать null)
        int start = Random.Range(0, clips.Length);
        for (int i = 0; i < clips.Length; i++)
        {
            AudioClip clip = clips[(start + i) % clips.Length];
            if (clip == null) continue;
            audioSource.PlayOneShot(clip);
            return;
        }
    }
}
