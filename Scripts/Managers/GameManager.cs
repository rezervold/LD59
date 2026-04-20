using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private bool dontDestroyOnLoad = true;
    [SerializeField] private int startLevel = 1;
    [SerializeField] private float levelCompleteDelay = 2f;
    [SerializeField] private string gameplaySceneName = "ArenaPit";
    [SerializeField] private string upgradesSceneName = "MainMenu";

    public bool IsPaused { get; private set; }
    public int CurrentLevel { get; private set; }
    public int ActiveGameplayLevel { get; private set; }
    public bool IsLevelTransitionInProgress { get; private set; }
    public string GameplaySceneName => gameplaySceneName;
    public string UpgradesSceneName => upgradesSceneName;

    public event Action<bool> PauseStateChanged;

    private float cachedTimeScale = 1f;
    private float cachedFixedDeltaTime = 0.02f;
    private Coroutine levelTransitionRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        CurrentLevel = Mathf.Max(1, startLevel);
        SceneManager.sceneLoaded += HandleSceneLoaded;
        HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);

        if (dontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 144;
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;

        SceneManager.sceneLoaded -= HandleSceneLoaded;

        if (levelTransitionRoutine != null)
            StopCoroutine(levelTransitionRoutine);

        if (IsPaused)
            ResumeGame();

        Instance = null;
    }

    public void PauseGame()
    {
        if (IsPaused)
            return;

        cachedTimeScale = Time.timeScale;
        cachedFixedDeltaTime = Time.fixedDeltaTime;

        Time.timeScale = 0f;
        Time.fixedDeltaTime = 0f;
        IsPaused = true;
        PauseStateChanged?.Invoke(true);
    }

    public void ResumeGame()
    {
        if (!IsPaused)
            return;

        Time.timeScale = cachedTimeScale > 0f ? cachedTimeScale : 1f;
        Time.fixedDeltaTime = cachedFixedDeltaTime > 0f ? cachedFixedDeltaTime : 0.02f;
        IsPaused = false;
        PauseStateChanged?.Invoke(false);
    }

    public void TogglePause()
    {
        if (IsPaused)
            ResumeGame();
        else
            PauseGame();
    }

    public void CompleteCurrentLevel()
    {
        if (IsLevelTransitionInProgress)
            return;

        CurrentLevel = Mathf.Max(1, CurrentLevel + 1);
        levelTransitionRoutine = StartCoroutine(LoadUpgradesSceneRoutine());
    }

    public void LoadGameplayScene()
    {
        CancelLevelTransition();

        if (IsPaused)
            ResumeGame();

        LoadScene(gameplaySceneName);
    }

    public int GetWrappedLevelIndex(int levelCount)
    {
        if (levelCount <= 0)
            return 0;

        return (Mathf.Max(1, CurrentLevel) - 1) % levelCount;
    }

    private IEnumerator LoadUpgradesSceneRoutine()
    {
        IsLevelTransitionInProgress = true;

        if (IsPaused)
            ResumeGame();

        if (levelCompleteDelay > 0f)
            yield return new WaitForSecondsRealtime(levelCompleteDelay);

        levelTransitionRoutine = null;
        LoadScene(upgradesSceneName);
    }

    private void CancelLevelTransition()
    {
        if (levelTransitionRoutine != null)
            StopCoroutine(levelTransitionRoutine);

        levelTransitionRoutine = null;
        IsLevelTransitionInProgress = false;
    }

    private void LoadScene(string sceneName)
    {
        if (SceneLoader.Instance != null)
        {
            if (!SceneLoader.Instance.IsLoading)
                SceneLoader.Instance.LoadScene(sceneName);

            return;
        }

        SceneManager.LoadScene(sceneName);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
    {
        if (scene.name == gameplaySceneName)
            ActiveGameplayLevel = Mathf.Max(1, CurrentLevel);

        if (scene.name != gameplaySceneName && scene.name != upgradesSceneName)
            return;

        IsLevelTransitionInProgress = false;
        levelTransitionRoutine = null;
    }
}
