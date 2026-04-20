using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class LevelTutorialController : MonoBehaviour
{
    [SerializeField] private string gameplaySceneName = "ArenaPit";
    [SerializeField] private float showDelay = 0.5f;
    [SerializeField] private float confirmHoldDuration = 0.1f;
    [SerializeField] private bool useFade = true;
    [SerializeField] private float fadeDuration = 0.15f;
    [SerializeField] private RectTransform tutorialRoot;
    [SerializeField] private CanvasGroup tutorialCanvasGroup;
    [SerializeField] private GameObject gasVisual;
    [SerializeField] private GameObject turnVisual;
    [SerializeField] private GameObject minigunVisual;

    private Coroutine tutorialRoutine;
    private Tween fadeTween;
    private bool levelOneTutorialCompleted;
    private bool levelThreeTutorialCompleted;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        StopTutorialRoutine(true);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
    {
        StopTutorialRoutine(true);

        if (scene.name != ResolveGameplaySceneName())
            return;

        int currentLevel = GameManager.Instance != null ? GameManager.Instance.CurrentLevel : 1;

        if (currentLevel == 1 && !levelOneTutorialCompleted)
        {
            tutorialRoutine = StartCoroutine(LevelOneTutorialRoutine());
            return;
        }

        if (currentLevel == 3 && !levelThreeTutorialCompleted)
            tutorialRoutine = StartCoroutine(LevelThreeTutorialRoutine());
    }

    private IEnumerator LevelOneTutorialRoutine()
    {
        EnsureVisuals();

        if (!CanRunLevelOneTutorial())
        {
            tutorialRoutine = null;
            yield break;
        }

        yield return new WaitForSecondsRealtime(showDelay);
        yield return ShowStep(gasVisual);
        yield return WaitForHold(() => Input.GetKey(KeyCode.W));
        yield return ShowStep(turnVisual);
        yield return WaitForHold(() => Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.D));
        levelOneTutorialCompleted = true;
        yield return HideTutorial();
        tutorialRoutine = null;
    }

    private IEnumerator LevelThreeTutorialRoutine()
    {
        EnsureVisuals();

        if (!CanRunLevelThreeTutorial())
        {
            tutorialRoutine = null;
            yield break;
        }

        yield return new WaitForSecondsRealtime(showDelay);
        yield return ShowStep(minigunVisual);
        yield return WaitForHold(() => Input.GetKey(KeyCode.Space));
        levelThreeTutorialCompleted = true;
        yield return HideTutorial();
        tutorialRoutine = null;
    }

    private IEnumerator WaitForHold(Func<bool> isHeld)
    {
        float holdTime = 0f;

        while (true)
        {
            if (SceneLoader.Instance != null && SceneLoader.Instance.IsLoading)
            {
                holdTime = 0f;
                yield return null;
                continue;
            }

            if (GameManager.Instance != null && GameManager.Instance.IsPaused)
            {
                holdTime = 0f;
                yield return null;
                continue;
            }

            if (isHeld())
            {
                holdTime += Time.unscaledDeltaTime;

                if (holdTime >= confirmHoldDuration)
                    yield break;
            }
            else
            {
                holdTime = 0f;
            }

            yield return null;
        }
    }

    private IEnumerator ShowStep(GameObject activeVisual)
    {
        if (activeVisual == null || tutorialRoot == null)
            yield break;

        tutorialRoot.gameObject.SetActive(true);
        SetAllVisuals(false);
        activeVisual.SetActive(true);

        if (!useFade || tutorialCanvasGroup == null)
        {
            if (tutorialCanvasGroup != null)
                tutorialCanvasGroup.alpha = 1f;

            yield break;
        }

        KillFadeTween();
        tutorialCanvasGroup.alpha = 0f;
        fadeTween = tutorialCanvasGroup.DOFade(1f, fadeDuration).SetUpdate(true);
        yield return fadeTween.WaitForCompletion();
        fadeTween = null;
    }

    private IEnumerator HideTutorial()
    {
        if (tutorialRoot == null)
            yield break;

        if (!useFade || tutorialCanvasGroup == null)
        {
            SetAllVisuals(false);
            tutorialRoot.gameObject.SetActive(false);
            yield break;
        }

        KillFadeTween();
        fadeTween = tutorialCanvasGroup.DOFade(0f, fadeDuration).SetUpdate(true);
        yield return fadeTween.WaitForCompletion();
        fadeTween = null;
        SetAllVisuals(false);
        tutorialRoot.gameObject.SetActive(false);
    }

    private void StopTutorialRoutine(bool hideImmediately)
    {
        if (tutorialRoutine != null)
        {
            StopCoroutine(tutorialRoutine);
            tutorialRoutine = null;
        }

        KillFadeTween();

        if (!hideImmediately || tutorialRoot == null)
            return;

        if (tutorialCanvasGroup != null)
            tutorialCanvasGroup.alpha = 0f;

        SetAllVisuals(false);
        tutorialRoot.gameObject.SetActive(false);
    }

    private void EnsureVisuals()
    {
        if (tutorialRoot == null)
            return;

        if (tutorialCanvasGroup != null)
        {
            tutorialCanvasGroup.alpha = 0f;
            tutorialCanvasGroup.interactable = false;
            tutorialCanvasGroup.blocksRaycasts = false;
        }

        SetAllVisuals(false);
        tutorialRoot.gameObject.SetActive(false);
    }

    private void SetAllVisuals(bool visible)
    {
        if (gasVisual != null)
            gasVisual.SetActive(visible);

        if (turnVisual != null)
            turnVisual.SetActive(visible);

        if (minigunVisual != null)
            minigunVisual.SetActive(visible);
    }

    private void KillFadeTween()
    {
        if (fadeTween != null && fadeTween.IsActive())
            fadeTween.Kill();

        fadeTween = null;
    }

    private bool CanRunLevelOneTutorial()
    {
        return tutorialRoot != null && gasVisual != null && turnVisual != null;
    }

    private bool CanRunLevelThreeTutorial()
    {
        return tutorialRoot != null && minigunVisual != null;
    }

    private string ResolveGameplaySceneName()
    {
        if (GameManager.Instance != null && !string.IsNullOrEmpty(GameManager.Instance.GameplaySceneName))
            return GameManager.Instance.GameplaySceneName;

        return gameplaySceneName;
    }
}
