using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class UpgradeButton : MonoBehaviour
{
    [SerializeField] private PlayerUpgradeType upgradeType;
    [SerializeField] private string displayName;
    [SerializeField, TextArea] private string description;
    [SerializeField] private Sprite displayIcon;
    [SerializeField] private int cost = 5;
    [SerializeField] private GameObject unaccesibleVisuals;
    [SerializeField] private TMP_Text descText;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private Image icon;
    [SerializeField] private Button buyButton;
    [SerializeField] private TMP_Text costText;

    private void Awake()
    {
        buyButton.onClick.RemoveListener(HandleBuyPressed);
        buyButton.onClick.AddListener(HandleBuyPressed);
        ApplyStaticData();
    }

    private void OnEnable()
    {
        if (SoftManager.Instance != null)
            SoftManager.Instance.BalanceChanged += HandleBalanceChanged;

        if (UpgradeManager.Instance != null)
            UpgradeManager.Instance.UpgradeStateChanged += HandleUpgradeStateChanged;

        RefreshState();
    }

    private void OnDisable()
    {
        if (SoftManager.Instance != null)
            SoftManager.Instance.BalanceChanged -= HandleBalanceChanged;

        if (UpgradeManager.Instance != null)
            UpgradeManager.Instance.UpgradeStateChanged -= HandleUpgradeStateChanged;
    }

    private void OnDestroy()
    {
        if (buyButton != null)
            buyButton.onClick.RemoveListener(HandleBuyPressed);
    }

    private void HandleBuyPressed()
    {
        if (UpgradeManager.Instance == null)
            return;

        if (UpgradeManager.Instance.TryPurchase(upgradeType, cost))
            RefreshState();
    }

    private void HandleBalanceChanged(int balance, int delta)
    {
        RefreshState();
    }

    private void HandleUpgradeStateChanged(PlayerUpgradeType changedType, bool isActive)
    {
        if (changedType != upgradeType)
            return;

        RefreshState();
    }

    private void ApplyStaticData()
    {
        nameText.text = displayName;
        icon.sprite = displayIcon;
    }

    private void RefreshState()
    {
        bool purchased = UpgradeManager.Instance != null && UpgradeManager.Instance.IsPurchased(upgradeType);
        int balance = SoftManager.Instance != null ? SoftManager.Instance.Balance : 0;
        bool canAfford = balance >= cost;

        costText.text = purchased ? "BOUGHT" : cost.ToString();
        buyButton.interactable = !purchased && canAfford;
        unaccesibleVisuals.SetActive(!purchased && !canAfford);
    }
}
