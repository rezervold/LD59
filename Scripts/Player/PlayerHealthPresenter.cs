using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PlayerHealthPresenter : MonoBehaviour
{
    public static PlayerHealthPresenter Instance { get; private set; }

    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private Image healthFill;
    [SerializeField] private RectTransform shakeTarget;
    [SerializeField] private RectTransform cntr;
    [SerializeField] private Juice damageJuice;
    [SerializeField] private float fillTweenDuration = 0.12f;
    [SerializeField] private float shakeRotationZ = 14f;
    [SerializeField] private float shakeDuration = 0.22f;
    [SerializeField] private int shakeVibrato = 12;
    [SerializeField] private float shakeElasticity = 0.8f;

    public float CurrentHealth { get; private set; }
    public float MaxHealth => maxHealth;

    private Tween fillTween;
    private Tween shakeTween;
    private Vector2 baseShakeSizeDelta;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        CurrentHealth = maxHealth;
        baseShakeSizeDelta = cntr.sizeDelta;
        healthFill.fillAmount = 1f;
        SetShakeWidthMultiplier(UpgradeManager.Instance != null && UpgradeManager.Instance.IsPurchased(PlayerUpgradeType.Hull) ? 3f : 1f);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (fillTween != null && fillTween.IsActive())
            fillTween.Kill();

        if (shakeTween != null && shakeTween.IsActive())
            shakeTween.Kill();
    }

    public void ApplyDamage(float currentHealth, float targetMaxHealth)
    {
        maxHealth = Mathf.Max(1f, targetMaxHealth);
        CurrentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        float normalized = CurrentHealth / maxHealth;

        if (CurrentHealth <= 0f && normalized <= 0f)
        {
            if (fillTween != null && fillTween.IsActive())
                fillTween.Kill();

            healthFill.fillAmount = 0f;
        }
        else
        {
            if (fillTween != null && fillTween.IsActive())
                fillTween.Kill();

            fillTween = healthFill.DOFillAmount(normalized, fillTweenDuration);
        }

        if (shakeTween != null && shakeTween.IsActive())
            shakeTween.Kill();

        shakeTarget.localRotation = Quaternion.identity;
        shakeTween = shakeTarget.DOPunchRotation(new Vector3(0f, 0f, shakeRotationZ), shakeDuration, shakeVibrato, shakeElasticity);

        if (damageJuice != null)
            damageJuice.Play();
    }

    public void SetHealth(float currentHealth, float targetMaxHealth)
    {
        maxHealth = Mathf.Max(1f, targetMaxHealth);
        CurrentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        float normalized = CurrentHealth / maxHealth;

        if (fillTween != null && fillTween.IsActive())
            fillTween.Kill();

        healthFill.fillAmount = normalized;
    }

    public void ResetHealth(float targetMaxHealth)
    {
        maxHealth = Mathf.Max(1f, targetMaxHealth);
        CurrentHealth = maxHealth;

        if (fillTween != null && fillTween.IsActive())
            fillTween.Kill();

        if (shakeTween != null && shakeTween.IsActive())
            shakeTween.Kill();

        healthFill.fillAmount = 1f;
        shakeTarget.localRotation = Quaternion.identity;
    }

    public void SetShakeWidthMultiplier(float multiplier)
    {
        Vector2 sizeDelta = baseShakeSizeDelta;
        sizeDelta.x *= Mathf.Max(0.01f, multiplier);
        cntr.sizeDelta = sizeDelta;
    }
}
