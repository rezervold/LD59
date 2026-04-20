using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance { get; private set; }

    [SerializeField] private Image fadeImage;
    [SerializeField] private float fadeDuration = 0.35f;
    [SerializeField] private bool dontDestroyOnLoad = true;
    [SerializeField] private int overlaySortingOrder = 5000;

    public bool IsLoading { get; private set; }

    private Color fadeColor = Color.black;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (dontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);

        SetFade(0f);
        SetFadeRaycastEnabled(false);
    }

    public void LoadScene(int buildIndex)
    {
        if (IsLoading)
            return;

        StartCoroutine(LoadSceneRoutine(buildIndex));
    }

    public void LoadScene(string sceneName)
    {
        if (IsLoading)
            return;

        StartCoroutine(LoadSceneRoutine(sceneName));
    }

    public void LoadNextScene()
    {
        int nextSceneIndex = SceneManager.GetActiveScene().buildIndex + 1;

        if (nextSceneIndex >= SceneManager.sceneCountInBuildSettings)
            return;

        LoadScene(nextSceneIndex);
    }

    public void ReloadCurrentScene()
    {
        LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private IEnumerator LoadSceneRoutine(int buildIndex)
    {
        IsLoading = true;

        yield return FadeRoutine(1f);

        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(buildIndex);

        while (!loadOperation.isDone)
            yield return null;

        yield return FadeRoutine(0f);

        IsLoading = false;
    }

    private IEnumerator LoadSceneRoutine(string sceneName)
    {
        IsLoading = true;

        yield return FadeRoutine(1f);

        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(sceneName);

        while (!loadOperation.isDone)
            yield return null;

        yield return FadeRoutine(0f);

        IsLoading = false;
    }

    private IEnumerator FadeRoutine(float targetAlpha)
    {
        if (!fadeImage)
            yield break;

        fadeImage.enabled = true;
        SetFadeRaycastEnabled(targetAlpha > 0f);

        float startAlpha = fadeImage.color.a;

        if (Mathf.Approximately(fadeDuration, 0f))
        {
            SetFade(targetAlpha);
            if (targetAlpha <= 0f)
            {
                SetFadeRaycastEnabled(false);
                fadeImage.enabled = false;
            }
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);
            SetFade(Mathf.Lerp(startAlpha, targetAlpha, t));
            yield return null;
        }

        SetFade(targetAlpha);

        if (targetAlpha <= 0f)
        {
            SetFadeRaycastEnabled(false);
            fadeImage.enabled = false;
        }
    }

    private void SetFade(float alpha)
    {
        if (!fadeImage)
            return;

        fadeColor.a = alpha;
        fadeImage.color = fadeColor;
    }

    private void SetFadeRaycastEnabled(bool enabled)
    {
        if (!fadeImage)
            return;

        fadeImage.raycastTarget = enabled;
    }
}
