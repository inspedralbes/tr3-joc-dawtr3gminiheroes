using MiniHeroes2D.Turns;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace MiniHeroes2D.ML
{
    [DisallowMultipleComponent]
    public sealed class MlAgentsTurnShooterAgent : Agent
    {
        [SerializeField] private TurnPlayerController controller;
        [SerializeField] private float thinkDelaySeconds = 0.85f;

        private TurnGameManager gameManager;
        private bool shotQueued;
        private float timer;

        public override void Initialize()
        {
            if (controller == null) controller = GetComponent<TurnPlayerController>();
            gameManager = FindObjectOfType<TurnGameManager>();
        }

        public void NotifyTurnBegan()
        {
            shotQueued = false;
            timer = 0f;
            RequestDecision();
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            if (controller == null || gameManager == null)
            {
                sensor.AddObservation(0f);
                sensor.AddObservation(0f);
                sensor.AddObservation(0f);
                return;
            }

            TurnPlayerController opponent = controller.FindOpponent(gameManager);
            if (opponent == null)
            {
                sensor.AddObservation(0f);
                sensor.AddObservation(0f);
                sensor.AddObservation(0f);
                return;
            }

            Vector2 delta = (Vector2)opponent.transform.position - (Vector2)controller.transform.position;
            sensor.AddObservation(Mathf.Clamp(delta.x / 20f, -1f, 1f));
            sensor.AddObservation(Mathf.Clamp(delta.y / 12f, -1f, 1f));
            sensor.AddObservation(Mathf.Clamp(delta.magnitude / 25f, 0f, 1f));
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            if (controller == null || gameManager == null) return;
            if (shotQueued) return;
            if (!gameManager.CanAct(controller)) return;

            timer += Time.deltaTime;
            if (timer < thinkDelaySeconds) return;

            float angle01 = Mathf.Clamp01(actions.ContinuousActions[0]);
            float power01 = Mathf.Clamp01(actions.ContinuousActions[1]);

            controller.FireWithAngleAndPower01(angle01, power01);
            shotQueued = true;
        }

        public override void Heuristic(in ActionBuffers actionsOut)
        {
            if (controller == null || gameManager == null) return;

            TurnPlayerController opponent = controller.FindOpponent(gameManager);
            if (opponent == null) return;

            if (!controller.TryComputeShotToTarget(gameManager, opponent.transform.position, out float angle01, out float power01))
            {
                angle01 = 0.55f;
                power01 = 0.75f;
            }

            ActionSegment<float> a = actionsOut.ContinuousActions;
            a[0] = angle01;
            a[1] = power01;
        }
    }
}

