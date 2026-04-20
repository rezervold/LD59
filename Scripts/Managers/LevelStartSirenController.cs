using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class LevelStartSirenController : MonoBehaviour
{
    [SerializeField] private string gameplaySceneName = "ArenaPit";
    [SerializeField] private Juice sirenJuice;
    [SerializeField] private float startDelay = 0.5f;
    [SerializeField] private float callInterval = 1.25f;
    [SerializeField] private int sirenCallCount = 3;
    [SerializeField] private Color sirenColor = new Color(1f, 0.4f, 0.3f, 1f);
    [SerializeField] private float toSirenDuration = 0.18f;
    [SerializeField] private float holdDuration = 0.12f;
    [SerializeField] private float toDefaultDuration = 0.28f;
    [SerializeField] private bool useUnscaledTime = true;

    private Coroutine sirenRoutine;
    private Color defaultAmbientColor;
    private bool ambientCaptured;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        StopSirenRoutine(true);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
    {
        if (scene.name != ResolveGameplaySceneName())
        {
            StopSirenRoutine(true);
            return;
        }

        StartSirenRoutine();
    }

    private void StartSirenRoutine()
    {
        StopSirenRoutine(true);

        if (sirenCallCount <= 0)
            return;

        ambientCaptured = false;
        sirenRoutine = StartCoroutine(SirenRoutine());
    }

    private void StopSirenRoutine(bool restoreAmbient)
    {
        if (sirenRoutine != null)
        {
            StopCoroutine(sirenRoutine);
            sirenRoutine = null;
        }

        if (restoreAmbient && ambientCaptured)
            RenderSettings.ambientLight = defaultAmbientColor;

        ambientCaptured = false;
    }

    private IEnumerator SirenRoutine()
    {
        if (startDelay > 0f)
            yield return Wait(startDelay);

        defaultAmbientColor = RenderSettings.ambientLight;
        ambientCaptured = true;
        int totalCalls = Mathf.Max(0, sirenCallCount);

        for (int i = 0; i < totalCalls; i++)
        {
            if (sirenJuice != null)
                sirenJuice.Play();

            yield return LerpAmbient(defaultAmbientColor, sirenColor, toSirenDuration);

            if (holdDuration > 0f)
                yield return Wait(holdDuration);

            yield return LerpAmbient(sirenColor, defaultAmbientColor, toDefaultDuration);

            if (i < totalCalls - 1 && callInterval > 0f)
                yield return Wait(callInterval);
        }

        RenderSettings.ambientLight = defaultAmbientColor;
        ambientCaptured = false;
        sirenRoutine = null;
    }

    private IEnumerator LerpAmbient(Color from, Color to, float duration)
    {
        if (duration <= 0f)
        {
            RenderSettings.ambientLight = to;
            yield break;
        }

        float time = 0f;

        while (time < duration)
        {
            time += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float t = Mathf.Clamp01(time / duration);
            RenderSettings.ambientLight = Color.LerpUnclamped(from, to, t);
            yield return null;
        }

        RenderSettings.ambientLight = to;
    }

    private IEnumerator Wait(float duration)
    {
        if (useUnscaledTime)
            yield return new WaitForSecondsRealtime(duration);
        else
            yield return new WaitForSeconds(duration);
    }

    private string ResolveGameplaySceneName()
    {
        if (GameManager.Instance != null && !string.IsNullOrEmpty(GameManager.Instance.GameplaySceneName))
            return GameManager.Instance.GameplaySceneName;

        return gameplaySceneName;
    }
}
