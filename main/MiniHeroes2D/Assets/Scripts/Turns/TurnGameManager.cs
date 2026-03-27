using System.Collections.Generic;
using UnityEngine;
using MiniHeroes2D.UI;

namespace MiniHeroes2D.Turns
{
    public sealed class TurnGameManager : MonoBehaviour
    {
        [SerializeField] private List<TurnPlayerController> players = new();
        [SerializeField] private int startingPlayerIndex = 0;

        [Header("Projectile")]
        [SerializeField] private MiniHeroes2D.Gameplay.Projectile projectilePrefab;
        [SerializeField] private float projectileSpeed = 14f;

        private int currentPlayerIndex;
        private MiniHeroes2D.Gameplay.Projectile projectileInFlight;
        private bool gameOver;

        public IReadOnlyList<TurnPlayerController> Players => players;
        public TurnPlayerController CurrentPlayer => players.Count > 0 ? players[currentPlayerIndex] : null;
        public float ProjectileSpeedBase => projectileSpeed;

        public float ProjectileGravityScale
        {
            get
            {
                if (projectilePrefab == null) return 1f;
                Rigidbody2D rb = projectilePrefab.GetComponent<Rigidbody2D>();
                return rb != null ? rb.gravityScale : 1f;
            }
        }

        public void SetProjectilePrefab(MiniHeroes2D.Gameplay.Projectile prefab)
        {
            projectilePrefab = prefab;
        }

        public void SetProjectileSpeed(float speed)
        {
            projectileSpeed = speed;
        }

        private void Start()
        {
            currentPlayerIndex = players.Count == 0 ? 0 : Mathf.Clamp(startingPlayerIndex, 0, players.Count - 1);
            BeginTurn();
        }

        public void RegisterPlayer(TurnPlayerController player)
        {
            if (player == null) return;
            if (players.Contains(player)) return;
            players.Add(player);
        }

        public bool CanAct(TurnPlayerController player)
        {
            if (players.Count == 0) return false;
            if (projectileInFlight != null) return false;
            return player == players[currentPlayerIndex];
        }

        public void Fire(TurnPlayerController player, Vector2 direction, float speedMultiplier)
        {
            if (!CanAct(player)) return;
            if (projectilePrefab == null) return;

            projectileInFlight = Instantiate(projectilePrefab, player.FirePosition, Quaternion.identity);
            projectileInFlight.gameObject.SetActive(true);
            projectileInFlight.Exploded += OnProjectileExploded;

            Collider2D projectileCollider = projectileInFlight.GetComponent<Collider2D>();
            Collider2D shooterCollider = player.GetComponent<Collider2D>();
            if (projectileCollider != null && shooterCollider != null)
                Physics2D.IgnoreCollision(projectileCollider, shooterCollider, true);

            projectileInFlight.Launch(direction, projectileSpeed * Mathf.Clamp(speedMultiplier, 0.1f, 2.5f));

            foreach (TurnPlayerController p in players) p.SetInputEnabled(false);
        }

        private void OnProjectileExploded(MiniHeroes2D.Gameplay.Projectile projectile, Vector2 point)
        {
            if (projectileInFlight == projectile) projectileInFlight = null;

            if (CheckGameOver())
            {
                EndTurn();
                return;
            }

            EndTurn();
            AdvanceTurn();
            BeginTurn();
        }

        private void BeginTurn()
        {
            if (players.Count == 0) return;

            for (int i = 0; i < players.Count; i += 1)
                players[i].SetInputEnabled(i == currentPlayerIndex);
        }

        private void EndTurn()
        {
            foreach (TurnPlayerController p in players) p.SetInputEnabled(false);
        }

        private void AdvanceTurn()
        {
            if (players.Count == 0) return;
            currentPlayerIndex = (currentPlayerIndex + 1) % players.Count;
        }

        private bool CheckGameOver()
        {
            if (gameOver) return true;

            for (int i = 0; i < players.Count; i += 1)
            {
                MiniHeroes2D.Gameplay.Health health = players[i].GetComponent<MiniHeroes2D.Gameplay.Health>();
                if (health != null && health.IsDead)
                {
                    gameOver = true;
                    Debug.Log($"Game Over: {players[i].name} ha perdido.");
                    ShowEndOverlayForLoser(players[i]);
                    return true;
                }
            }

            return false;
        }

        private static void ShowEndOverlayForLoser(TurnPlayerController loser)
        {
            EndGameOverlay overlay = FindObjectOfType<EndGameOverlay>();
            if (overlay == null)
            {
                GameObject go = new("EndGameOverlay");
                overlay = go.AddComponent<EndGameOverlay>();
            }

            // Defeat if Player1 dies, otherwise victory.
            if (loser != null && loser.name.Equals("Player1"))
                overlay.ShowDefeat();
            else
                overlay.ShowVictory();
        }
    }
}
