using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class TutorialManager : MonoBehaviour
{
    [Serializable]
    private class TutorialStep
    {
        [SerializeField] private bool playAfterPrevious;
        [SerializeField] private bool showText;
        [SerializeField] [TextArea(2, 6)] private string text;
        [SerializeField] private bool focusUiObject;
        [SerializeField] private RectTransform focusTarget;
        [SerializeField] private bool showBlackBg;
        [SerializeField] private bool showFinger;
        [SerializeField] private Transform fingerTarget;
        [SerializeField] private Vector3 fingerOffset;
        [SerializeField] private bool animateFinger;
        [SerializeField] private Vector2 fingerMoveDirection = Vector2.up;
        [SerializeField] private float fingerMoveRange = 30f;
        [SerializeField] private float fingerMoveSpeed = 2f;
        [SerializeField] private bool autoSkipAfterTime;
        [SerializeField] private float autoSkipDelay = 1f;
        [SerializeField] private bool autoSkipUsesUnscaledTime = true;

        public bool PlayAfterPrevious => playAfterPrevious;
        public bool ShowText => showText;
        public string Text => text;
        public bool FocusUiObject => focusUiObject;
        public RectTransform FocusTarget => focusTarget;
        public bool ShowBlackBg => showBlackBg;
        public bool ShowFinger => showFinger;
        public Transform FingerTarget => fingerTarget;
        public Vector3 FingerOffset => fingerOffset;
        public bool AnimateFinger => animateFinger;
        public Vector2 FingerMoveDirection => fingerMoveDirection;
        public float FingerMoveRange => fingerMoveRange;
        public float FingerMoveSpeed => fingerMoveSpeed;
        public bool AutoSkipAfterTime => autoSkipAfterTime;
        public float AutoSkipDelay => autoSkipDelay;
        public bool AutoSkipUsesUnscaledTime => autoSkipUsesUnscaledTime;
    }

    private struct FocusRestoreData
    {
        public RectTransform target;
        public Transform parent;
        public int siblingIndex;
        public Vector2 anchorMin;
        public Vector2 anchorMax;
        public Vector2 anchoredPosition;
        public Vector2 sizeDelta;
        public Vector2 pivot;
        public Quaternion localRotation;
        public Vector3 localScale;
    }

    public static TutorialManager Instance { get; private set; }

    [SerializeField] private bool playFirstStepOnStart;
    [SerializeField] private GameObject textContainer;
    [SerializeField] private TMP_Text tutorialText;
    [SerializeField] private Transform focusedUiParent;
    [SerializeField] private GameObject blackBg;
    [SerializeField] private GameObject fingerObject;
    [SerializeField] private List<TutorialStep> steps = new List<TutorialStep>();

    public int CurrentStepIndex { get; private set; } = -1;

    private bool[] completedSteps;
    private Coroutine autoSkipCoroutine;
    private Coroutine fingerCoroutine;
    private bool hasFocusedTarget;
    private FocusRestoreData focusedTargetRestoreData;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        completedSteps = new bool[steps.Count];
        HideAllStepVisuals();
    }

    private void Start()
    {
        if (playFirstStepOnStart && steps.Count > 0)
            ShowStep(0);
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;

        StopStepRoutines();
        ClearFocusTarget();
        HideAllStepVisuals();
        Instance = null;
    }

    public void ShowStep(int stepIndex)
    {
        if (!IsStepIndexValid(stepIndex))
            return;

        StopStepRoutines();
        ClearFocusTarget();

        CurrentStepIndex = stepIndex;

        TutorialStep step = steps[stepIndex];

        if (step.ShowText)
        {
            textContainer.SetActive(true);
            tutorialText.text = step.Text;
        }
        else
        {
            textContainer.SetActive(false);
        }

        blackBg.SetActive(step.ShowBlackBg);

        if (step.FocusUiObject)
            FocusTarget(step.FocusTarget);

        if (step.ShowFinger)
        {
            fingerObject.SetActive(true);
            fingerCoroutine = StartCoroutine(FingerRoutine(step));
        }
        else
        {
            fingerObject.SetActive(false);
        }

        if (step.AutoSkipAfterTime)
            autoSkipCoroutine = StartCoroutine(AutoSkipRoutine(stepIndex, step.AutoSkipDelay, step.AutoSkipUsesUnscaledTime));
    }

    public void CompleteStep(int stepIndex)
    {
        if (CurrentStepIndex != stepIndex)
            return;

        CompleteCurrentStep();
    }

    public void CompleteCurrentStep()
    {
        if (!IsStepIndexValid(CurrentStepIndex))
            return;

        int completedStepIndex = CurrentStepIndex;
        completedSteps[completedStepIndex] = true;

        StopStepRoutines();
        ClearFocusTarget();
        HideAllStepVisuals();

        CurrentStepIndex = -1;

        int nextStepIndex = completedStepIndex + 1;

        if (IsStepIndexValid(nextStepIndex) && steps[nextStepIndex].PlayAfterPrevious)
            ShowStep(nextStepIndex);
    }

    public void StopTutorial()
    {
        StopStepRoutines();
        ClearFocusTarget();
        HideAllStepVisuals();
        CurrentStepIndex = -1;
    }

    public void ResetTutorialProgress()
    {
        completedSteps = new bool[steps.Count];
        StopTutorial();
    }

    public bool IsStepCompleted(int stepIndex)
    {
        if (!IsStepIndexValid(stepIndex))
            return false;

        return completedSteps[stepIndex];
    }

    private IEnumerator AutoSkipRoutine(int stepIndex, float delay, bool useUnscaledTime)
    {
        if (useUnscaledTime)
            yield return new WaitForSecondsRealtime(delay);
        else
            yield return new WaitForSeconds(delay);

        CompleteStep(stepIndex);
    }

    private IEnumerator FingerRoutine(TutorialStep step)
    {
        Transform fingerTransform = fingerObject.transform;
        Vector3 moveDirection = new Vector3(step.FingerMoveDirection.x, step.FingerMoveDirection.y, 0f);

        while (CurrentStepIndex >= 0 && steps[CurrentStepIndex] == step)
        {
            Vector3 targetPosition = step.FingerTarget.position + step.FingerOffset;

            if (step.AnimateFinger)
            {
                float wave = Mathf.Sin(Time.unscaledTime * step.FingerMoveSpeed) * step.FingerMoveRange;
                targetPosition += moveDirection.normalized * wave;
            }

            fingerTransform.position = targetPosition;
            yield return null;
        }
    }

    private void FocusTarget(RectTransform target)
    {
        focusedTargetRestoreData = new FocusRestoreData
        {
            target = target,
            parent = target.parent,
            siblingIndex = target.GetSiblingIndex(),
            anchorMin = target.anchorMin,
            anchorMax = target.anchorMax,
            anchoredPosition = target.anchoredPosition,
            sizeDelta = target.sizeDelta,
            pivot = target.pivot,
            localRotation = target.localRotation,
            localScale = target.localScale
        };

        hasFocusedTarget = true;

        target.SetParent(focusedUiParent, true);
        target.SetAsLastSibling();
    }

    private void ClearFocusTarget()
    {
        if (!hasFocusedTarget)
            return;

        RectTransform target = focusedTargetRestoreData.target;
        target.SetParent(focusedTargetRestoreData.parent, false);
        target.SetSiblingIndex(focusedTargetRestoreData.siblingIndex);
        target.anchorMin = focusedTargetRestoreData.anchorMin;
        target.anchorMax = focusedTargetRestoreData.anchorMax;
        target.anchoredPosition = focusedTargetRestoreData.anchoredPosition;
        target.sizeDelta = focusedTargetRestoreData.sizeDelta;
        target.pivot = focusedTargetRestoreData.pivot;
        target.localRotation = focusedTargetRestoreData.localRotation;
        target.localScale = focusedTargetRestoreData.localScale;

        hasFocusedTarget = false;
    }

    private void StopStepRoutines()
    {
        if (autoSkipCoroutine != null)
        {
            StopCoroutine(autoSkipCoroutine);
            autoSkipCoroutine = null;
        }

        if (fingerCoroutine != null)
        {
            StopCoroutine(fingerCoroutine);
            fingerCoroutine = null;
        }
    }

    private void HideAllStepVisuals()
    {
        textContainer.SetActive(false);
        blackBg.SetActive(false);
        fingerObject.SetActive(false);
    }

    private bool IsStepIndexValid(int stepIndex)
    {
        return stepIndex >= 0 && stepIndex < steps.Count;
    }
}
