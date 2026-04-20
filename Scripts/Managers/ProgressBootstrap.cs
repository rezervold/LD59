using UnityEngine;

public static class ProgressBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        SoftManager.EnsureInstance();
        UpgradeManager.EnsureInstance();
    }
}
