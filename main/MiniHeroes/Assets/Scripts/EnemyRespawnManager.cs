using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyRespawnManager : MonoBehaviour
{
    public float RespawnDelay = 2.5f;
    public bool EnableMlAgents = true;

    private readonly List<GruntScript> trackedEnemies = new List<GruntScript>();
    private GameObject player;
    private RuntimeLevelExtender levelExtender;
    private bool initialized;

    public void Configure(GameObject playerObject, RuntimeLevelExtender extender)
    {
        player = playerObject;
        levelExtender = extender;
        TryBootstrap();
    }

    private void Start()
    {
        TryBootstrap();
    }

    private void Update()
    {
        if (!initialized)
        {
            TryBootstrap();
        }
    }

    public void HandleEnemyDeath(GruntScript enemy)
    {
        if (enemy == null)
        {
            return;
        }

        StartCoroutine(RespawnEnemyRoutine(enemy));
    }

    private IEnumerator RespawnEnemyRoutine(GruntScript enemy)
    {
        yield return new WaitForSecondsRealtime(RespawnDelay);

        if (enemy == null)
        {
            yield break;
        }

        enemy.ResetEnemy();
    }

    private void TryBootstrap()
    {
        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                player = playerObject;
            }
        }

        if (player == null)
        {
            return;
        }

        if (levelExtender != null)
        {
            levelExtender.GenerateIfNeeded();
        }

        RegisterSceneEnemies();
        SpawnGeneratedEnemies();
        initialized = trackedEnemies.Count > 0;
    }

    private void RegisterSceneEnemies()
    {
        GruntScript[] enemies = FindObjectsByType<GruntScript>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (GruntScript enemy in enemies)
        {
            if (trackedEnemies.Contains(enemy))
            {
                continue;
            }

            enemy.Initialize(player, this, enemy.transform.position, EnableMlAgents);
            trackedEnemies.Add(enemy);
        }
    }

    private void SpawnGeneratedEnemies()
    {
        if (levelExtender == null || trackedEnemies.Count == 0)
        {
            return;
        }

        GameObject template = trackedEnemies[0].gameObject;
        foreach (Vector3 spawnPoint in levelExtender.GeneratedSpawnPoints)
        {
            bool occupied = false;
            foreach (GruntScript existingEnemy in trackedEnemies)
            {
                if (Vector3.Distance(existingEnemy.SpawnPoint, spawnPoint) < 0.25f)
                {
                    occupied = true;
                    break;
                }
            }

            if (occupied)
            {
                continue;
            }

            GameObject clone = Instantiate(template, spawnPoint, Quaternion.identity);
            clone.name = "Grunt";

            GruntScript cloneScript = clone.GetComponent<GruntScript>();
            cloneScript.Initialize(player, this, spawnPoint, EnableMlAgents);
            trackedEnemies.Add(cloneScript);
        }
    }
}
