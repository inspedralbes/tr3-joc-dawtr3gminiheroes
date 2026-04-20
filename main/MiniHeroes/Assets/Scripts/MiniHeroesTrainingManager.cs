using System.Collections;
using UnityEngine;

public class MiniHeroesTrainingManager : MonoBehaviour
{
    public float TrainingTimeScale = 8f;
    public float PlayerRespawnDelay = 0.5f;

    private JohnMovement player;

    private void Awake()
    {
        if (!MiniHeroesRuntimeMode.IsTraining)
        {
            enabled = false;
            return;
        }

        Time.timeScale = TrainingTimeScale;
    }

    private void Start()
    {
        if (!MiniHeroesRuntimeMode.IsTraining)
        {
            return;
        }

        player = Object.FindFirstObjectByType<JohnMovement>();
        if (player != null && player.GetComponent<TrainingPlayerBot>() == null)
        {
            player.gameObject.AddComponent<TrainingPlayerBot>();
        }
    }

    public void HandlePlayerDeath()
    {
        if (!MiniHeroesRuntimeMode.IsTraining)
        {
            return;
        }

        StopAllCoroutines();
        StartCoroutine(RespawnPlayerRoutine());
    }

    private IEnumerator RespawnPlayerRoutine()
    {
        yield return new WaitForSecondsRealtime(PlayerRespawnDelay);

        if (player == null)
        {
            player = Object.FindFirstObjectByType<JohnMovement>();
        }

        if (player != null)
        {
            player.ResetForTraining();
        }

        GruntScript[] grunts = Object.FindObjectsByType<GruntScript>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < grunts.Length; i++)
        {
            grunts[i].ResetEnemy();
        }
    }
}
