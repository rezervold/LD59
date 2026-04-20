using UnityEngine;
using UnityEngine.SceneManagement;

public static class MainMenuSceneBootstrap
{
    private const string MainMenuSceneName = "MainMenu";
    private static bool initialized;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        if (initialized)
            return;

        SceneManager.sceneLoaded += HandleSceneLoaded;
        initialized = true;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
    {
        if (scene.name != MainMenuSceneName)
            return;

        UpgradesScreen[] screens = Object.FindObjectsOfType<UpgradesScreen>(true);
        UpgradesScreen upgradesScreen = screens.Length > 0 ? screens[0] : null;

        if (upgradesScreen != null)
            upgradesScreen.Show();
    }
}
