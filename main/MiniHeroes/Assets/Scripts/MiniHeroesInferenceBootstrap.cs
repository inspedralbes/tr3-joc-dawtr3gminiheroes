using Unity.InferenceEngine;
using Unity.MLAgents.Policies;
using UnityEngine;

public class MiniHeroesInferenceBootstrap : MonoBehaviour
{
    public string ResourceModelPath = "MLAgents/MiniHeroesGrunt";
    public bool ForceInferenceOnly = true;

    public static bool HasLoadedModel { get; private set; }

    private void Awake()
    {
        HasLoadedModel = false;

        if (MiniHeroesRuntimeMode.IsTraining)
        {
            return;
        }

        ModelAsset model = Resources.Load<ModelAsset>(ResourceModelPath);
        if (model == null)
        {
            return;
        }

        HasLoadedModel = true;

        BehaviorParameters[] behaviorParameters = Object.FindObjectsByType<BehaviorParameters>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < behaviorParameters.Length; i++)
        {
            if (behaviorParameters[i].BehaviorName != "MiniHeroesGrunt")
            {
                continue;
            }

            behaviorParameters[i].Model = model;
            if (ForceInferenceOnly)
            {
                behaviorParameters[i].BehaviorType = BehaviorType.InferenceOnly;
            }
        }
    }
}
