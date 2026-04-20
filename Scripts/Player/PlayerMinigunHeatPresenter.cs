using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PlayerMinigunHeatPresenter : MonoBehaviour
{
    public static PlayerMinigunHeatPresenter Instance { get; private set; }

    [SerializeField] private GameObject visibilityTarget;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Image heatFill;
    [SerializeField] private RectTransform feedbackTarget;
    [SerializeField] private Vector3 overheatPunchScale = new Vector3(0.22f, 0.22f, 0f);
    [SerializeField] private Vector3 overheatPunchRotation = new Vector3(0f, 0f, 18f);
    [SerializeField] private float overheatPunchDuration = 0.28f;
    [SerializeField] private int overheatPunchVibrato = 12;
    [SerializeField] private float overheatPunchElasticity = 0.85f;

    private bool requestedVisible;
    private Tween scaleTween;
    private Tween rotationTween;
    private Vector3 baseFeedbackScale = Vector3.one;
    private Quaternion baseFeedbackRotation = Quaternion.identity;
    private bool feedbackBaseCaptured;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        heatFill.fillAmount = 0f;
        requestedVisible = false;
        CaptureFeedbackBaseState();
        SetVisible(false);
    }

    private void OnDestroy()
    {
        KillFeedbackTweens();

        if (Instance == this)
            Instance = null;
    }

    public void SetVisible(bool visible)
    {
        requestedVisible = visible;
        bool actualVisible = visible && UpgradeManager.Instance != null && UpgradeManager.Instance.IsActive(PlayerUpgradeType.Minigun);
        GameObject target = visibilityTarget != null ? visibilityTarget : gameObject;

        if (target.activeSelf != actualVisible)
            target.SetActive(actualVisible);

        if (!actualVisible)
            ResetFeedbackTarget();

        if (canvasGroup != null)
        {
            canvasGroup.alpha = actualVisible ? 1f : 0f;
            canvasGroup.interactable = actualVisible;
            canvasGroup.blocksRaycasts = actualVisible;
            return;
        }

        heatFill.enabled = actualVisible;
    }

    public void SetHeatNormalized(float normalized)
    {
        heatFill.fillAmount = Mathf.Clamp01(normalized);
    }

    public void PlayOverheatFeedback()
    {
        RectTransform target = GetFeedbackTarget();

        if (target == null)
            return;

        CaptureFeedbackBaseState();
        KillFeedbackTweens();
        target.localScale = baseFeedbackScale;
        target.localRotation = baseFeedbackRotation;
        scaleTween = target.DOPunchScale(overheatPunchScale, overheatPunchDuration, overheatPunchVibrato, overheatPunchElasticity);
        rotationTween = target.DOPunchRotation(overheatPunchRotation, overheatPunchDuration, overheatPunchVibrato, overheatPunchElasticity);
    }

    private void OnEnable()
    {
        if (UpgradeManager.Instance != null)
            UpgradeManager.Instance.UpgradeStateChanged += HandleUpgradeStateChanged;

        SetVisible(requestedVisible);
    }

    private void OnDisable()
    {
        if (UpgradeManager.Instance != null)
            UpgradeManager.Instance.UpgradeStateChanged -= HandleUpgradeStateChanged;

        ResetFeedbackTarget();
    }

    private void HandleUpgradeStateChanged(PlayerUpgradeType upgradeType, bool isActive)
    {
        if (upgradeType != PlayerUpgradeType.Minigun)
            return;

        SetVisible(requestedVisible);
    }

    private RectTransform GetFeedbackTarget()
    {
        if (feedbackTarget != null)
            return feedbackTarget;

        if (visibilityTarget != null)
            visibilityTarget.TryGetComponent(out feedbackTarget);

        if (feedbackTarget != null)
            return feedbackTarget;

        TryGetComponent(out feedbackTarget);
        return feedbackTarget;
    }

    private void ResetFeedbackTarget()
    {
        RectTransform target = GetFeedbackTarget();

        if (target == null)
            return;

        CaptureFeedbackBaseState();
        KillFeedbackTweens();
        target.localScale = baseFeedbackScale;
        target.localRotation = baseFeedbackRotation;
    }

    private void CaptureFeedbackBaseState()
    {
        RectTransform target = GetFeedbackTarget();

        if (target == null || feedbackBaseCaptured)
            return;

        baseFeedbackScale = target.localScale;
        baseFeedbackRotation = target.localRotation;
        feedbackBaseCaptured = true;
    }

    private void KillFeedbackTweens()
    {
        if (scaleTween != null && scaleTween.IsActive())
            scaleTween.Kill();

        if (rotationTween != null && rotationTween.IsActive())
            rotationTween.Kill();

        scaleTween = null;
        rotationTween = null;
    }
}
