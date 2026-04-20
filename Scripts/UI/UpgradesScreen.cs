using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class UpgradesScreen : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Button continueButton;
    [SerializeField] private CanvasGroup continueCanvasGroup;
    [SerializeField] private float fadeDuration = 0.35f;
    [SerializeField] private string gameplaySceneName = "ArenaPit";

    private Tween fadeTween;
    private bool continueVisible = true;

    private void Awake()
    {
        continueButton.onClick.RemoveListener(HandleContinuePressed);
        continueButton.onClick.AddListener(HandleContinuePressed);
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        ApplyContinueVisibility();
    }

    private void Start()
    {
        Show();
    }

    private void OnDestroy()
    {
        if (continueButton != null)
            continueButton.onClick.RemoveListener(HandleContinuePressed);

        if (fadeTween != null && fadeTween.IsActive())
            fadeTween.Kill();
    }

    public void Show()
    {
        if (fadeTween != null && fadeTween.IsActive())
            fadeTween.Kill();

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
        ApplyContinueVisibility();
        fadeTween = canvasGroup.DOFade(1f, fadeDuration).SetUpdate(true);
    }

    public void SetContinueVisible(bool visible)
    {
        continueVisible = visible;
        ApplyContinueVisibility();
    }

    public bool IsContinueVisible()
    {
        return continueVisible;
    }

    private void HandleContinuePressed()
    {
        continueButton.interactable = false;
        canvasGroup.blocksRaycasts = false;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.LoadGameplayScene();
            return;
        }

        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.LoadScene(gameplaySceneName);
            return;
        }

        SceneManager.LoadScene(gameplaySceneName);
    }

    private void ApplyContinueVisibility()
    {
        continueCanvasGroup.alpha = continueVisible ? 1f : 0f;
        continueCanvasGroup.interactable = continueVisible;
        continueCanvasGroup.blocksRaycasts = continueVisible;
        continueButton.interactable = continueVisible && canvasGroup != null && canvasGroup.interactable;
    }
}
