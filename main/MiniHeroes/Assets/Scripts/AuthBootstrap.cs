using UnityEngine;

public static class AuthBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureAuthManager()
    {
        if (MiniHeroesRuntimeMode.IsTraining || AuthManager.Exists())
        {
            return;
        }

        GameObject authObject = new GameObject("GlobalAuthManager");
        authObject.AddComponent<AuthManager>();
    }
}
