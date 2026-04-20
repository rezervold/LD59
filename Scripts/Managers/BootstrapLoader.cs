using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BootstrapLoader : MonoBehaviour
{
    [SerializeField] private float delayBeforeLoad = 1f;
    [SerializeField] private bool useUnscaledTime = true;

    private void Start()
    {
        if (SceneManager.GetActiveScene().name != "Bootstrap")
            return;

        StartCoroutine(LoadNextSceneRoutine());
    }

    private IEnumerator LoadNextSceneRoutine()
    {
        if (delayBeforeLoad > 0f)
        {
            if (useUnscaledTime)
                yield return new WaitForSecondsRealtime(delayBeforeLoad);
            else
                yield return new WaitForSeconds(delayBeforeLoad);
        }

        int nextSceneIndex = SceneManager.GetActiveScene().buildIndex + 1;

        if (nextSceneIndex >= SceneManager.sceneCountInBuildSettings)
            yield break;

        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadScene(nextSceneIndex);
        else
            SceneManager.LoadScene(nextSceneIndex);
    }
}
