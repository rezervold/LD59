using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum PlayerUpgradeType
{
    Engine = 0,
    Tires = 1,
    Hull = 2,
    Minigun = 3
}

[DisallowMultipleComponent]
public class UpgradeManager : MonoBehaviour
{
    public static UpgradeManager Instance { get; private set; }

    public event Action<PlayerUpgradeType, bool> UpgradeStateChanged;

    private readonly HashSet<PlayerUpgradeType> purchasedUpgrades = new HashSet<PlayerUpgradeType>();
    private bool gameplayApplyPending = true;

    public static UpgradeManager EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        GameObject root = new GameObject("UpgradeManager");
        return root.AddComponent<UpgradeManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        Instance = null;
    }

    private void Update()
    {
        if (!gameplayApplyPending)
            return;

        if (TryApplyGameplayUpgrades())
            gameplayApplyPending = false;
    }

    public bool IsPurchased(PlayerUpgradeType upgradeType)
    {
        return purchasedUpgrades.Contains(upgradeType);
    }

    public bool IsActive(PlayerUpgradeType upgradeType)
    {
        if (IsPurchased(upgradeType))
            return true;

        return upgradeType == PlayerUpgradeType.Minigun &&
            GameManager.Instance != null &&
            GameManager.Instance.ActiveGameplayLevel >= 3;
    }

    public bool TryPurchase(PlayerUpgradeType upgradeType, int cost)
    {
        if (IsPurchased(upgradeType))
            return false;

        if (SoftManager.Instance == null || !SoftManager.Instance.TrySpend(cost))
            return false;

        purchasedUpgrades.Add(upgradeType);
        UpgradeStateChanged?.Invoke(upgradeType, true);
        gameplayApplyPending = true;
        return true;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
    {
        gameplayApplyPending = true;
    }

    private bool TryApplyGameplayUpgrades()
    {
        World world = World.DefaultGameObjectInjectionWorld;

        if (world == null || !world.IsCreated)
            return false;

        EntityManager entityManager = world.EntityManager;
        EntityQuery playerQuery = entityManager.CreateEntityQuery(
            ComponentType.ReadWrite<PlayerCar>(),
            ComponentType.ReadWrite<PlayerCarState>());

        if (playerQuery.IsEmptyIgnoreFilter)
        {
            playerQuery.Dispose();
            return false;
        }

        Entity playerEntity = playerQuery.GetSingletonEntity();
        PlayerCar playerCar = entityManager.GetComponentData<PlayerCar>(playerEntity);
        PlayerCarState playerState = entityManager.GetComponentData<PlayerCarState>(playerEntity);
        bool hasAnyUpgrade = false;

        if (IsPurchased(PlayerUpgradeType.Engine))
        {
            playerCar.ForwardAcceleration = 70f;
            playerCar.MaxForwardSpeed = 45f;
            playerCar.MaxReverseSpeed = 45f;
            hasAnyUpgrade = true;
        }

        if (IsPurchased(PlayerUpgradeType.Tires))
        {
            playerCar.Grip = 4f;
            hasAnyUpgrade = true;
        }

        if (IsPurchased(PlayerUpgradeType.Hull))
        {
            playerCar.MaxHealth = 300f;
            playerState.CurrentHealth = playerCar.MaxHealth;
            hasAnyUpgrade = true;
        }

        if (hasAnyUpgrade)
        {
            entityManager.SetComponentData(playerEntity, playerCar);
            entityManager.SetComponentData(playerEntity, playerState);
        }

        if (PlayerHealthPresenter.Instance != null)
        {
            PlayerHealthPresenter.Instance.SetShakeWidthMultiplier(IsPurchased(PlayerUpgradeType.Hull) ? 3f : 1f);

            if (IsPurchased(PlayerUpgradeType.Hull))
                PlayerHealthPresenter.Instance.ResetHealth(playerCar.MaxHealth);
        }

        playerQuery.Dispose();
        return true;
    }
}
