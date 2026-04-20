using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerMinigunWallImpactPresenter : MonoBehaviour
{
    private static PlayerMinigunWallImpactPresenter instance;

    private readonly List<Juice> pool = new List<Juice>();

    private Juice effectPrefab;
    private float effectLifetime;

    public static void Play(Juice prefab, int initialPoolSize, float lifetime, Vector3 worldPosition, Vector3 worldNormal)
    {
        if (prefab == null)
            return;

        GetOrCreateInstance().PlayInternal(prefab, initialPoolSize, lifetime, worldPosition, worldNormal);
    }

    private static PlayerMinigunWallImpactPresenter GetOrCreateInstance()
    {
        if (instance != null)
            return instance;

        GameObject presenterObject = new GameObject("PlayerMinigunWallImpactPresenter");
        instance = presenterObject.AddComponent<PlayerMinigunWallImpactPresenter>();
        return instance;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void PlayInternal(Juice prefab, int initialPoolSize, float lifetime, Vector3 worldPosition, Vector3 worldNormal)
    {
        Configure(prefab, initialPoolSize, lifetime);

        Juice effect = GetAvailableEffect();
        Transform effectTransform = effect.transform;
        effectTransform.SetPositionAndRotation(
            worldPosition,
            worldNormal.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(worldNormal, Vector3.up) : Quaternion.identity);
        effectTransform.localScale = effectPrefab.transform.localScale;

        GameObject effectObject = effect.gameObject;

        if (!effectObject.activeSelf)
            effectObject.SetActive(true);

        effect.Play();
        StartCoroutine(ReleaseRoutine(effectObject, effectLifetime));
    }

    private void Configure(Juice prefab, int initialPoolSize, float lifetime)
    {
        effectLifetime = Mathf.Max(0f, lifetime);

        if (effectPrefab != prefab)
        {
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i] != null)
                    Destroy(pool[i].gameObject);
            }

            pool.Clear();
            effectPrefab = prefab;
        }

        int targetPoolSize = Mathf.Max(1, initialPoolSize);

        for (int i = pool.Count; i < targetPoolSize; i++)
            CreateEffectInstance();
    }

    private IEnumerator ReleaseRoutine(GameObject effectObject, float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (effectObject != null)
            effectObject.SetActive(false);
    }

    private Juice GetAvailableEffect()
    {
        for (int i = 0; i < pool.Count; i++)
        {
            if (!pool[i].gameObject.activeSelf)
                return pool[i];
        }

        return CreateEffectInstance();
    }

    private Juice CreateEffectInstance()
    {
        Juice effect = Instantiate(effectPrefab, transform);
        effect.gameObject.SetActive(false);
        pool.Add(effect);
        return effect;
    }
}
