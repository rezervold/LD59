using System;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerMinigunBullet : MonoBehaviour
{
    private static readonly RaycastHit[] wallHitBuffer = new RaycastHit[16];

    [SerializeField] private SphereCollider hitCollider;
    [SerializeField] private float hitRadius = 0.4f;
    [SerializeField] private int pierceCount = 1;
    [SerializeField] private Juice wallImpactJuicePrefab;
    [SerializeField] private int wallImpactPoolSize = 10;
    [SerializeField] private float wallImpactLifetime = 1f;
    [SerializeField] private LayerMask wallLayers;
    [SerializeField] private string wallNamePrefix = "WALL";

    private float speed;
    private float lifetime;
    private float remainingDistance;
    private float killImpulse;
    private float killUpImpulse;
    private float killSpinImpulse;
    private float randomForce;
    private int remainingPierceCount;
    private bool isPlaying;

    private void Awake()
    {
        BindHitCollider();
        ApplyColliderSize();
    }

    private void OnValidate()
    {
        BindHitCollider();
        ApplyColliderSize();
    }

    private void OnDisable()
    {
        isPlaying = false;
    }

    private void Update()
    {
        if (!isPlaying)
            return;

        Vector3 previousPosition = transform.position;
        Vector3 direction = transform.forward;
        float step = speed * Time.deltaTime;
        Vector3 nextPosition = previousPosition + direction * step;
        float hitRadiusValue = GetWorldHitRadius();
        Vector3 blockerPoint = nextPosition;
        Vector3 blockerNormal = Vector3.up;
        bool hasBlockingHit = TryGetClosestBlockingHit(previousPosition, direction, step, hitRadiusValue, out blockerPoint, out blockerNormal);
        Vector3 enemyQueryEnd = nextPosition;

        if (hasBlockingHit)
        {
            float blockerDistance = Vector3.Distance(previousPosition, blockerPoint);
            float backoff = Mathf.Min(blockerDistance, Mathf.Max(0.01f, hitRadiusValue * 0.1f));
            enemyQueryEnd = blockerPoint - direction * backoff;
        }

        Vector3 enemyQueryStart = previousPosition;
        float enemyAdvance = Mathf.Max(0.02f, hitRadiusValue * 0.25f);

        while (remainingPierceCount > 0 && (enemyQueryEnd - enemyQueryStart).sqrMagnitude > 0.0001f)
        {
            if (!HordeCombatBridge.TryOverlapEnemyAlongSegment(enemyQueryStart, enemyQueryEnd, hitRadiusValue, out Unity.Entities.Entity hitEntity, out Vector3 hitPoint))
                break;

            if (HordeCombatBridge.TryKillEnemy(hitEntity, hitPoint, direction, killImpulse, killUpImpulse, killSpinImpulse, randomForce))
            {
                remainingPierceCount -= 1;
                transform.position = hitPoint;

                if (remainingPierceCount <= 0)
                {
                    gameObject.SetActive(false);
                    return;
                }
            }

            enemyQueryStart = hitPoint + direction * enemyAdvance;
        }

        transform.position = nextPosition;
        remainingDistance -= step;
        lifetime -= Time.deltaTime;

        if (hasBlockingHit)
        {
            transform.position = blockerPoint;
            PlayWallImpact(blockerPoint, blockerNormal);
            gameObject.SetActive(false);
            return;
        }

        if (remainingDistance <= 0f || lifetime <= 0f)
            gameObject.SetActive(false);
    }

    public void Play(Vector3 startPosition, Vector3 direction, float travelDistance, float travelSpeed, float maxLifetime, float impulse, float upImpulse, float spinImpulse, float randomForceValue)
    {
        transform.SetParent(null, true);
        transform.position = startPosition;
        transform.rotation = Quaternion.LookRotation(direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward, Vector3.up);
        remainingDistance = Mathf.Max(0f, travelDistance);
        speed = Mathf.Max(0.01f, travelSpeed);
        lifetime = Mathf.Max(0.01f, maxLifetime);
        killImpulse = impulse;
        killUpImpulse = upImpulse;
        killSpinImpulse = spinImpulse;
        randomForce = randomForceValue;
        remainingPierceCount = Mathf.Max(1, pierceCount);
        isPlaying = true;
        gameObject.SetActive(true);
    }

    private void BindHitCollider()
    {
        if (hitCollider != null)
            return;

        TryGetComponent(out hitCollider);
    }

    private float GetWorldHitRadius()
    {
        float radius = Mathf.Max(0.01f, hitRadius);

        if (hitCollider != null)
            radius *= Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);

        return radius;
    }

    private bool TryGetClosestBlockingHit(Vector3 startPoint, Vector3 direction, float distance, float radius, out Vector3 hitPoint, out Vector3 hitNormal)
    {
        hitPoint = startPoint + direction * distance;
        hitNormal = Vector3.up;
        float closestDistance = float.MaxValue;
        bool found = false;

        int hitCount = Physics.SphereCastNonAlloc(
            startPoint,
            radius,
            direction,
            wallHitBuffer,
            distance,
            GetWallCastMask(),
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit gameObjectHit = wallHitBuffer[i];

            if (!IsWallCollider(gameObjectHit.collider) || gameObjectHit.distance >= closestDistance)
                continue;

            closestDistance = gameObjectHit.distance;
            hitPoint = gameObjectHit.point;
            hitNormal = gameObjectHit.normal;
            found = true;
        }

        return found;
    }

    private void PlayWallImpact(Vector3 hitPoint, Vector3 hitNormal)
    {
        if (wallImpactJuicePrefab == null)
        {
            if (HordeDeathPresenter.Instance != null)
                HordeDeathPresenter.Instance.Play(hitPoint);

            return;
        }

        PlayerMinigunWallImpactPresenter.Play(
            wallImpactJuicePrefab,
            wallImpactPoolSize,
            wallImpactLifetime,
            hitPoint,
            hitNormal);
    }

    private int GetWallCastMask()
    {
        if (wallLayers.value != 0)
            return wallLayers.value;

        int mask = Physics.DefaultRaycastLayers;
        int playerLayer = LayerMask.NameToLayer("Player");

        if (playerLayer >= 0)
            mask &= ~(1 << playerLayer);

        if (gameObject.layer >= 0)
            mask &= ~(1 << gameObject.layer);

        return mask;
    }

    private bool IsWallCollider(Collider collider)
    {
        if (collider == null)
            return false;

        if (wallLayers.value != 0)
            return (wallLayers.value & (1 << collider.gameObject.layer)) != 0;

        if (string.IsNullOrEmpty(wallNamePrefix))
            return false;

        string colliderName = collider.transform.name;

        if (colliderName.StartsWith(wallNamePrefix, StringComparison.OrdinalIgnoreCase))
            return true;

        Transform root = collider.transform.root;
        return root != null && root.name.StartsWith(wallNamePrefix, StringComparison.OrdinalIgnoreCase);
    }

    private void ApplyColliderSize()
    {
        if (hitCollider == null)
            return;

        hitCollider.isTrigger = true;
        hitCollider.radius = Mathf.Max(0.01f, hitRadius);
        hitCollider.center = Vector3.zero;
    }
}
