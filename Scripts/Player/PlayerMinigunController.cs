using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerMinigunController : MonoBehaviour
{
    [Serializable]
    private class Barrel
    {
        [SerializeField] private GameObject visualsRoot;
        [SerializeField] private Transform muzzle;
        [SerializeField] private Juice shotJuice;

        public GameObject VisualsRoot => visualsRoot;
        public Transform Muzzle => muzzle;
        public Juice ShotJuice => shotJuice;
    }

    [SerializeField] private Transform ownerRoot;
    [SerializeField] private Barrel leftBarrel = new Barrel();
    [SerializeField] private Barrel rightBarrel = new Barrel();
    [SerializeField] private Transform[] spinTargets = Array.Empty<Transform>();
    [SerializeField] private Vector3 spinAxis = Vector3.right;
    [SerializeField] private float spinSpeed = 1080f;
    [SerializeField] private PlayerMinigunBullet bulletPrefab;
    [SerializeField] private int initialBulletPoolSize = 24;
    [SerializeField] private float bulletSpeed = 80f;
    [SerializeField] private float bulletMaxDistance = 50f;
    [SerializeField] private float bulletLifetime = 1.2f;
    [SerializeField] private float shootInterval = 0.08f;
    [SerializeField] private float spreadAngle = 0.35f;
    [SerializeField] private float raycastStartOffset = 1f;
    [SerializeField] private float overheatDuration = 3f;
    [SerializeField] private float killImpulse = 11f;
    [SerializeField] private float killUpImpulse = 1.5f;
    [SerializeField] private float killSpinImpulse = 6f;
    [SerializeField] private float randomForce = 4f;
    [SerializeField] private bool followPlayerEntity = true;

    private readonly List<PlayerMinigunBullet> bulletPool = new List<PlayerMinigunBullet>();

    private bool unlocked;
    private bool blockedUntilRelease;
    private float shotTimer;
    private float heatNormalized;

    private void Awake()
    {
        WarmupBulletPool();
        RefreshUnlockedState();
    }

    private void OnEnable()
    {
        if (UpgradeManager.Instance != null)
            UpgradeManager.Instance.UpgradeStateChanged += HandleUpgradeStateChanged;

        SyncHeatPresenter();
    }

    private void OnDisable()
    {
        if (UpgradeManager.Instance != null)
            UpgradeManager.Instance.UpgradeStateChanged -= HandleUpgradeStateChanged;

        if (PlayerMinigunHeatPresenter.Instance != null)
            PlayerMinigunHeatPresenter.Instance.SetVisible(false);
    }

    private void Update()
    {
        RefreshUnlockedState();
        SyncToPlayerEntity();
        RotateMiniguns();

        if (!unlocked)
            return;

        bool isFireHeld = Input.GetKey(KeyCode.Space);

        if (!isFireHeld)
            blockedUntilRelease = false;

        bool canShoot = isFireHeld &&
            !blockedUntilRelease &&
            (GameManager.Instance == null || !GameManager.Instance.IsPaused) &&
            HordeCombatBridge.IsPlayerAlive();
        float deltaTime = Time.deltaTime;
        float heatDelta = deltaTime / Mathf.Max(0.01f, overheatDuration);
        float coolDelta = deltaTime / Mathf.Max(0.01f, overheatDuration * 2f);
        bool hitOverheatThisFrame = false;

        if (canShoot)
        {
            shotTimer -= deltaTime;

            while (shotTimer <= 0f)
            {
                FireAllBarrels();
                shotTimer += Mathf.Max(0.01f, shootInterval);
            }

            float previousHeat = heatNormalized;
            heatNormalized = Mathf.Clamp01(heatNormalized + heatDelta);
            hitOverheatThisFrame = previousHeat < 1f && heatNormalized >= 1f;

            if (heatNormalized >= 1f)
            {
                blockedUntilRelease = true;
                shotTimer = 0f;
            }
        }
        else
        {
            shotTimer = 0f;
            heatNormalized = Mathf.Clamp01(heatNormalized - coolDelta);
        }

        SyncHeatPresenter();

        if (hitOverheatThisFrame && PlayerMinigunHeatPresenter.Instance != null)
            PlayerMinigunHeatPresenter.Instance.PlayOverheatFeedback();
    }

    public void SetUnlocked(bool value)
    {
        if (unlocked == value)
        {
            SyncHeatPresenter();
            return;
        }

        unlocked = value;

        if (!unlocked)
        {
            blockedUntilRelease = false;
            shotTimer = 0f;
            heatNormalized = 0f;
        }

        SetBarrelVisuals(leftBarrel, unlocked);
        SetBarrelVisuals(rightBarrel, unlocked);
        SyncHeatPresenter();
    }

    private void HandleUpgradeStateChanged(PlayerUpgradeType upgradeType, bool isActive)
    {
        if (upgradeType != PlayerUpgradeType.Minigun)
            return;

        RefreshUnlockedState();
    }

    private void FireAllBarrels()
    {
        FireBarrel(leftBarrel);
        FireBarrel(rightBarrel);
    }

    private void FireBarrel(Barrel barrel)
    {
        Transform muzzle = barrel.Muzzle;

        if (muzzle == null)
            return;

        Vector2 spread = UnityEngine.Random.insideUnitCircle * spreadAngle;
        Vector3 direction = muzzle.forward;
        direction = Quaternion.AngleAxis(spread.x, muzzle.up) * direction;
        direction = Quaternion.AngleAxis(spread.y, muzzle.right) * direction;
        float spawnOffset = Mathf.Max(0f, raycastStartOffset);
        Vector3 startPosition = muzzle.position + direction * spawnOffset;
        float travelDistance = Mathf.Max(0.01f, bulletMaxDistance - spawnOffset);
        SpawnBullet(startPosition, direction, travelDistance);

        if (barrel.ShotJuice != null)
            barrel.ShotJuice.Play();
    }

    private void SpawnBullet(Vector3 startPosition, Vector3 direction, float distance)
    {
        if (bulletPrefab == null)
            return;

        PlayerMinigunBullet bullet = GetBullet();
        bullet.Play(startPosition, direction, distance, bulletSpeed, bulletLifetime, killImpulse, killUpImpulse, killSpinImpulse, randomForce);
    }

    private PlayerMinigunBullet GetBullet()
    {
        for (int i = 0; i < bulletPool.Count; i++)
        {
            if (!bulletPool[i].gameObject.activeSelf)
                return bulletPool[i];
        }

        return CreateBullet();
    }

    private void WarmupBulletPool()
    {
        if (bulletPrefab == null)
            return;

        for (int i = bulletPool.Count; i < initialBulletPoolSize; i++)
            CreateBullet();
    }

    private PlayerMinigunBullet CreateBullet()
    {
        PlayerMinigunBullet bullet = Instantiate(bulletPrefab);
        bullet.gameObject.SetActive(false);
        bulletPool.Add(bullet);
        return bullet;
    }

    private void RotateMiniguns()
    {
        if (!unlocked)
            return;

        Vector3 axis = spinAxis.sqrMagnitude > 0.0001f ? spinAxis.normalized : Vector3.right;
        float angle = spinSpeed * Time.deltaTime;

        for (int i = 0; i < spinTargets.Length; i++)
        {
            if (spinTargets[i] == null)
                continue;

            spinTargets[i].Rotate(axis, angle, Space.Self);
        }
    }

    private void SetBarrelVisuals(Barrel barrel, bool active)
    {
        if (barrel.VisualsRoot != null)
            barrel.VisualsRoot.SetActive(active);
    }

    private void SyncHeatPresenter()
    {
        if (PlayerMinigunHeatPresenter.Instance == null)
            return;

        PlayerMinigunHeatPresenter.Instance.SetVisible(unlocked);
        PlayerMinigunHeatPresenter.Instance.SetHeatNormalized(heatNormalized);
    }

    private void SyncToPlayerEntity()
    {
        if (!followPlayerEntity)
            return;

        if (!HordeCombatBridge.TryGetPlayerPose(out Vector3 position, out Quaternion rotation))
            return;

        Transform target = GetFollowTarget();
        target.SetPositionAndRotation(position, rotation);
    }

    private Transform GetFollowTarget()
    {
        if (ownerRoot != null)
            return ownerRoot;

        Transform leftRoot = leftBarrel.VisualsRoot != null ? leftBarrel.VisualsRoot.transform : null;
        Transform rightRoot = rightBarrel.VisualsRoot != null ? rightBarrel.VisualsRoot.transform : null;

        if (leftRoot != null && rightRoot != null && leftRoot.parent != null && leftRoot.parent == rightRoot.parent)
            return leftRoot.parent;

        if (leftRoot != null && leftRoot.parent != null)
            return leftRoot.parent;

        if (rightRoot != null && rightRoot.parent != null)
            return rightRoot.parent;

        return transform;
    }

    private void RefreshUnlockedState()
    {
        bool active = UpgradeManager.Instance != null && UpgradeManager.Instance.IsActive(PlayerUpgradeType.Minigun);
        SetUnlocked(active);
    }
}
