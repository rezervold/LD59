using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class HordeDeathPresenter : MonoBehaviour
{
    public static HordeDeathPresenter Instance { get; private set; }

    [SerializeField] private Juice effectPrefab;
    [SerializeField] private int initialPoolSize = 12;
    [SerializeField] private float effectLifetime = 2f;

    private readonly List<Juice> pool = new List<Juice>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        for (int i = 0; i < initialPoolSize; i++)
            CreateEffectInstance();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void Play(Vector3 worldPosition)
    {
        if (effectPrefab == null)
            return;

        Juice effect = GetAvailableEffect();
        GameObject effectObject = effect.gameObject;
        effectObject.transform.SetPositionAndRotation(worldPosition, Quaternion.identity);
        effectObject.transform.localScale = effectPrefab.transform.localScale;
        effectObject.SetActive(true);
        effect.Play();
        StartCoroutine(ReleaseRoutine(effectObject, effectLifetime));
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
