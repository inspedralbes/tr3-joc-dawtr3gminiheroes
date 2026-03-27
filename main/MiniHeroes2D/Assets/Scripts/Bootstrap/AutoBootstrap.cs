using UnityEngine;
using UnityEngine.SceneManagement;

namespace MiniHeroes2D.Bootstrap
{
    public static class AutoBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureBootstrap()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.name != "SampleScene") return;

            if (Object.FindObjectOfType<BattleBootstrap>() != null) return;

            GameObject bootstrap = new("BattleBootstrap (Auto)");
            bootstrap.AddComponent<BattleBootstrap>();
        }
    }
}
