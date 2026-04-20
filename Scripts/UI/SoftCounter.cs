using DG.Tweening;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class SoftCounter : MonoBehaviour
{
    [SerializeField] private RectTransform counterContainer;
    [SerializeField] private TMP_Text valueText;
    [SerializeField] private float addScale = 1.2f;
    [SerializeField] private float removeScale = 0.8f;
    [SerializeField] private float scaleDuration = 0.12f;

    private Tween scaleTween;
    private int currentValue = -1;

    private void OnEnable()
    {
        if (SoftManager.Instance != null)
        {
            SoftManager.Instance.BalanceChanged += HandleBalanceChanged;
            Refresh(SoftManager.Instance.Balance, 0);
        }
    }

    private void OnDisable()
    {
        if (SoftManager.Instance != null)
            SoftManager.Instance.BalanceChanged -= HandleBalanceChanged;

        if (scaleTween != null && scaleTween.IsActive())
            scaleTween.Kill();
    }

    private void HandleBalanceChanged(int balance, int delta)
    {
        Refresh(balance, delta);
    }

    private void Refresh(int balance, int delta)
    {
        currentValue = balance;
        valueText.text = currentValue.ToString();

        if (delta == 0)
            return;

        float targetScale = delta > 0 ? addScale : removeScale;

        if (scaleTween != null && scaleTween.IsActive())
            scaleTween.Kill();

        counterContainer.localScale = Vector3.one;
        scaleTween = DOTween.Sequence()
            .Append(counterContainer.DOScale(Vector3.one * targetScale, scaleDuration))
            .Append(counterContainer.DOScale(Vector3.one, scaleDuration))
            .SetUpdate(true);
    }
}
