using UnityEngine;

[DisallowMultipleComponent]
public class PlayerCarVfxPresenter : MonoBehaviour
{
    public static PlayerCarVfxPresenter Instance { get; private set; }

    [SerializeField] private ParticleSystem smokeParticle;
    [SerializeField] private Juice explodeJuice;
    [SerializeField] private Vector3 smokeOffset;

    private bool smokeActive;
    private bool smokeInitialized;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        SetSmokeActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void SetState(Vector3 worldPosition, bool smokeEnabled)
    {
        if (smokeParticle != null)
            smokeParticle.transform.position = worldPosition + smokeOffset;

        SetSmokeActive(smokeEnabled);
    }

    public void PlayExplosion(Vector3 worldPosition)
    {
        SetSmokeActive(false);

        if (explodeJuice == null)
            return;

        explodeJuice.transform.position = worldPosition;
        explodeJuice.Play();
    }

    private void SetSmokeActive(bool active)
    {
        if (smokeParticle == null)
            return;

        if (smokeInitialized && smokeActive == active)
            return;

        smokeInitialized = true;
        smokeActive = active;

        if (active)
        {
            smokeParticle.gameObject.SetActive(true);
            smokeParticle.Play();
            return;
        }

        smokeParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        smokeParticle.gameObject.SetActive(false);
    }
}
