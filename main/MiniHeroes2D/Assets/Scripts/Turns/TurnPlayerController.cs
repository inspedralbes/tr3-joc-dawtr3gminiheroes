using UnityEngine;

namespace MiniHeroes2D.Turns
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class TurnPlayerController : MonoBehaviour
    {
        [SerializeField] private TurnGameManager gameManager;
        [SerializeField] private Transform firePoint;
        [SerializeField] private int horizontalDirection = 1;
        [SerializeField] private bool isAi;

        [Header("Aim")]
        [SerializeField] private float minAngle = 15f;
        [SerializeField] private float maxAngle = 75f;
        [SerializeField] private float aimSpeedDegreesPerSecond = 90f;
        [SerializeField] private float powerChargePerSecond = 0.75f;

        [Header("AI")]
        [SerializeField] private float aiThinkDelaySeconds = 0.85f;
        [SerializeField] private float aiAngleStepDegrees = 2f;
        [SerializeField] private float aiPowerStep = 0.05f;
        [SerializeField] private Vector2 aiSpeedMultiplierRange = new(0.35f, 1.5f);
        [SerializeField] private float aiAimJitterDegrees = 1.5f;
        [SerializeField] private float aiMaxFlightTimeSeconds = 5.5f;

        private bool inputEnabled;
        private float currentAngleDeg = 45f;
        private float currentPower = 1f;
        private bool isCharging;
        private float aiTimer;
        private bool aiShotQueued;

        public Vector2 FirePosition => firePoint != null ? (Vector2)firePoint.position : (Vector2)transform.position;
        public float MinAngle => minAngle;
        public float MaxAngle => maxAngle;
        public int HorizontalDirection => horizontalDirection;

        private void Reset()
        {
            gameManager = FindObjectOfType<TurnGameManager>();
        }

        private void Awake()
        {
            if (gameManager == null) gameManager = FindObjectOfType<TurnGameManager>();
            if (gameManager != null) gameManager.RegisterPlayer(this);
        }

        public void SetInputEnabled(bool enabled)
        {
            inputEnabled = enabled;
            isCharging = false;
            aiTimer = 0f;
            aiShotQueued = false;

            if (enabled && isAi)
            {
                MiniHeroes2D.ML.MlAgentsTurnShooterAgent agent = GetComponent<MiniHeroes2D.ML.MlAgentsTurnShooterAgent>();
                if (agent != null) agent.NotifyTurnBegan();
            }
        }

        public void SetFirePoint(Transform point)
        {
            firePoint = point;
        }

        public void SetHorizontalDirection(int dir)
        {
            horizontalDirection = dir >= 0 ? 1 : -1;
        }

        public void SetIsAi(bool value)
        {
            isAi = value;
        }

        private void Update()
        {
            if (!inputEnabled) return;
            if (gameManager == null) return;
            if (!gameManager.CanAct(this)) return;

            if (isAi)
            {
                if (GetComponent<MiniHeroes2D.ML.MlAgentsTurnShooterAgent>() != null)
                    return;

                UpdateAi();
                return;
            }

            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector2 toMouse = GetMouseWorld(cam) - FirePosition;
                if (toMouse.sqrMagnitude > 0.0001f)
                {
                    Vector2 dir = toMouse.normalized;
                    float signedAngle = Mathf.Atan2(dir.y, dir.x * horizontalDirection) * Mathf.Rad2Deg;
                    currentAngleDeg = Mathf.Clamp(signedAngle, minAngle, maxAngle);
                }
            }

            float aimDelta = 0f;
            if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) aimDelta -= 1f;
            if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) aimDelta += 1f;

            currentAngleDeg = Mathf.Clamp(
                currentAngleDeg + aimDelta * aimSpeedDegreesPerSecond * Time.deltaTime,
                minAngle,
                maxAngle
            );

            if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
                isCharging = true;

            if (isCharging)
            {
                currentPower = Mathf.Clamp01(currentPower + powerChargePerSecond * Time.deltaTime);
            }

            if (Input.GetKeyUp(KeyCode.Space) || Input.GetMouseButtonUp(0))
            {
                isCharging = false;
                FireTowardMouseOrAngle();
                currentPower = 0.25f;
            }
        }

        private static Vector2 GetMouseWorld(Camera cam)
        {
            Vector3 mouse = Input.mousePosition;
            float distance = Mathf.Abs(cam.transform.position.z);
            Vector3 world = cam.ScreenToWorldPoint(new Vector3(mouse.x, mouse.y, distance));
            return new Vector2(world.x, world.y);
        }

        private void FireTowardMouseOrAngle()
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector2 toMouse = GetMouseWorld(cam) - FirePosition;
                if (toMouse.sqrMagnitude > 0.0001f)
                {
                    Vector2 direction = toMouse.normalized;
                    // Avoid shooting "behind" the character; clamp to forward hemisphere.
                    if ((direction.x * horizontalDirection) < 0.05f)
                        direction = new Vector2(0.05f * horizontalDirection, Mathf.Sign(direction.y) * 0.999f).normalized;

                    gameManager.Fire(this, direction, Mathf.Lerp(0.35f, 1.5f, currentPower));
                    return;
                }
            }

            Fire();
        }

        public void FireWithAngleAndPower01(float angle01, float power01)
        {
            if (gameManager == null) return;
            if (!gameManager.CanAct(this)) return;

            float angleDeg = Mathf.Lerp(minAngle, maxAngle, Mathf.Clamp01(angle01));
            float speedMultiplier = Mathf.Lerp(0.35f, 1.5f, Mathf.Clamp01(power01));

            float radians = angleDeg * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(radians) * horizontalDirection, Mathf.Sin(radians));
            gameManager.Fire(this, direction, speedMultiplier);
        }

        private void UpdateAi()
        {
            if (aiShotQueued) return;

            aiTimer += Time.deltaTime;
            if (aiTimer < aiThinkDelaySeconds) return;

            TurnPlayerController opponent = FindOpponent();
            if (opponent == null) return;

            Vector2 start = FirePosition;
            Vector2 target = opponent.transform.position;

            if (!TrySolveShot(start, target, out float angleDeg, out float speedMultiplier))
            {
                angleDeg = 45f;
                speedMultiplier = 1f;
            }

            angleDeg = Mathf.Clamp(angleDeg + Random.Range(-aiAimJitterDegrees, aiAimJitterDegrees), minAngle, maxAngle);
            currentAngleDeg = angleDeg;

            float radians = currentAngleDeg * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(radians) * horizontalDirection, Mathf.Sin(radians));

            aiShotQueued = true;
            gameManager.Fire(this, direction, speedMultiplier);
        }

        private TurnPlayerController FindOpponent()
        {
            if (gameManager == null) return null;

            TurnPlayerController best = null;
            float bestDistance = float.PositiveInfinity;

            var list = gameManager.Players;
            for (int i = 0; i < list.Count; i += 1)
            {
                TurnPlayerController other = list[i];
                if (other == null) continue;
                if (other == this) continue;

                float d = Vector2.Distance(other.transform.position, transform.position);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = other;
                }
            }

            return best;
        }

        public TurnPlayerController FindOpponent(TurnGameManager manager)
        {
            if (manager == null) return null;

            TurnPlayerController best = null;
            float bestDistance = float.PositiveInfinity;

            var list = manager.Players;
            for (int i = 0; i < list.Count; i += 1)
            {
                TurnPlayerController other = list[i];
                if (other == null) continue;
                if (other == this) continue;

                float d = Vector2.Distance(other.transform.position, transform.position);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = other;
                }
            }

            return best;
        }

        public bool TryComputeShotToTarget(TurnGameManager manager, Vector2 targetPosition, out float angle01, out float power01)
        {
            angle01 = 0.55f;
            power01 = 0.75f;
            if (manager == null) return false;
            if (gameManager == null) return false;
            if (manager != gameManager) return false;

            Vector2 start = FirePosition;
            Vector2 target = targetPosition;

            if (!TrySolveShot(start, target, out float angleDeg, out float speedMultiplier))
                return false;

            angle01 = Mathf.InverseLerp(minAngle, maxAngle, angleDeg);
            power01 = Mathf.InverseLerp(0.35f, 1.5f, speedMultiplier);
            return true;
        }

        private bool TrySolveShot(Vector2 start, Vector2 target, out float angleDeg, out float speedMultiplier)
        {
            angleDeg = 45f;
            speedMultiplier = 1f;

            float baseSpeed = gameManager.ProjectileSpeedBase;
            float gravityScale = gameManager.ProjectileGravityScale;
            float g = Physics2D.gravity.y * gravityScale;
            if (Mathf.Abs(g) < 0.0001f) return false;

            float dx = Mathf.Abs(target.x - start.x);
            float dy = target.y - start.y;
            if (dx < 0.15f) return false;

            float bestScore = float.PositiveInfinity;
            float bestAngle = 45f;
            float bestMult = 1f;

            float minMult = Mathf.Min(aiSpeedMultiplierRange.x, aiSpeedMultiplierRange.y);
            float maxMult = Mathf.Max(aiSpeedMultiplierRange.x, aiSpeedMultiplierRange.y);

            float stepAngle = Mathf.Max(0.25f, aiAngleStepDegrees);
            float stepPower = Mathf.Max(0.01f, aiPowerStep);

            for (float a = minAngle; a <= maxAngle; a += stepAngle)
            {
                float rad = a * Mathf.Deg2Rad;
                float cos = Mathf.Cos(rad);
                float sin = Mathf.Sin(rad);
                if (cos <= 0.001f) continue;

                for (float mult = minMult; mult <= maxMult; mult += stepPower)
                {
                    float v = baseSpeed * mult;
                    float t = dx / (v * cos);
                    if (t <= 0f || t > aiMaxFlightTimeSeconds) continue;

                    float yAtX = (v * sin * t) + (0.5f * g * t * t);
                    float error = Mathf.Abs(yAtX - dy);
                    float score = error + (0.08f * t); // prefer faster shots if similar error

                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestAngle = a;
                        bestMult = mult;
                    }
                }
            }

            if (float.IsInfinity(bestScore)) return false;

            angleDeg = bestAngle;
            speedMultiplier = bestMult;
            return true;
        }

        private void Fire()
        {
            float radians = currentAngleDeg * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(radians) * horizontalDirection, Mathf.Sin(radians));
            gameManager.Fire(this, direction, Mathf.Lerp(0.35f, 1.5f, currentPower));
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = inputEnabled ? Color.green : Color.gray;
            Vector2 origin = firePoint != null ? (Vector2)firePoint.position : (Vector2)transform.position;

            float radians = currentAngleDeg * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(radians) * horizontalDirection, Mathf.Sin(radians));
            Gizmos.DrawLine(origin, origin + dir * 2f);
        }
    }
}
