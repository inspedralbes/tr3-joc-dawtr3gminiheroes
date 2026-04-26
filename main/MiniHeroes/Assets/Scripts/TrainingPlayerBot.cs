using UnityEngine;

public class TrainingPlayerBot : MonoBehaviour
{
    public float PreferredDistance = 3f;
    public float RetreatDistance = 1.4f;

    private JohnMovement player;

    private void Awake()
    {
        player = GetComponent<JohnMovement>();
    }

    private void Update()
    {
        if (!MiniHeroesRuntimeMode.IsTraining || player == null || player.IsDead)
        {
            return;
        }

        GruntScript target = FindClosestGrunt(transform.position);
        if (target == null)
        {
            player.SetExternalControlState(0f, false, false);
            return;
        }

        float horizontalDelta = target.transform.position.x - transform.position.x;
        float horizontalDistance = Mathf.Abs(horizontalDelta);
        float moveInput = 0f;

        if (horizontalDistance > PreferredDistance)
        {
            moveInput = Mathf.Sign(horizontalDelta);
        }
        else if (horizontalDistance < RetreatDistance)
        {
            moveInput = -Mathf.Sign(horizontalDelta);
        }

        bool shouldShoot = horizontalDistance <= 5f;
        player.SetExternalControlState(moveInput, false, shouldShoot);
    }

    private static GruntScript FindClosestGrunt(Vector3 origin)
    {
        GruntScript[] grunts = Object.FindObjectsByType<GruntScript>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        GruntScript closest = null;
        float closestDistance = float.MaxValue;

        for (int i = 0; i < grunts.Length; i++)
        {
            if (grunts[i].IsDead)
            {
                continue;
            }

            float distance = Vector2.Distance(grunts[i].transform.position, origin);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = grunts[i];
            }
        }

        return closest;
    }
}
