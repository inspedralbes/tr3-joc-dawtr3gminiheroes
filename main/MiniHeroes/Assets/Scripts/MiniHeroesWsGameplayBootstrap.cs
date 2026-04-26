using UnityEngine;
using UnityEngine.SceneManagement;

public static class MiniHeroesWsGameplayBootstrap
{
    private const string MultiplayerActivePrefKey = "mh_ws_mp_active";
    private static bool subscribed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        if (subscribed)
        {
            return;
        }

        subscribed = true;
        SceneManager.sceneLoaded += OnSceneLoaded;
        TryEnsureClient();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryEnsureClient();
    }

    private static void TryEnsureClient()
    {
        if (PlayerPrefs.GetInt(MultiplayerActivePrefKey, 0) != 1)
        {
            return;
        }

        if (Object.FindFirstObjectByType<JohnMovement>() == null)
        {
            return;
        }

        if (Object.FindFirstObjectByType<MiniHeroesWsGameplayClient>() != null)
        {
            return;
        }

        GameObject go = new GameObject("MiniHeroesWsGameplayClient");
        go.AddComponent<MiniHeroesWsGameplayClient>();
    }
}