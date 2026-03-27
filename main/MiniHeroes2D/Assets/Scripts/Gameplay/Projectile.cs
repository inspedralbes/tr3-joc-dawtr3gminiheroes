using System;
using System.Collections.Generic;
using UnityEngine;
using MiniHeroes2D.Terrain;

namespace MiniHeroes2D.Gameplay
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class Projectile : MonoBehaviour
    {
        [Header("Flight")]
        [SerializeField] private float lifetimeSeconds = 10f;

        [Header("Explosion")]
        [SerializeField] private float explosionRadius = 2.25f;
        [SerializeField, Range(0.01f, 1f)] private float damagePercentPerHit = 0.15f;
        [SerializeField] private float explosionForce = 8f;
        [SerializeField] private bool knockbackAffectsPlayers = false;
        [SerializeField] private LayerMask damageMask = ~0;

        public event Action<Projectile, Vector2> Exploded;

        private Rigidbody2D body;
        private Transform cachedTransform;
        private bool hasExploded;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            cachedTransform = transform;
        }

        public void Launch(Vector2 direction, float speed)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.AddForce(direction.normalized * speed, ForceMode2D.Impulse);

            Destroy(gameObject, Mathf.Max(0.25f, lifetimeSeconds));
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (hasExploded) return;

            Vector2 point = collision.contactCount > 0 ? collision.GetContact(0).point : (Vector2)transform.position;
            Explode(point);
        }

        private void FixedUpdate()
        {
            if (hasExploded) return;
            if (body == null) return;

            Vector2 v = body.linearVelocity;
            if (v.sqrMagnitude < 0.05f) return;

            float angle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
            cachedTransform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }

        private void Explode(Vector2 point)
        {
            hasExploded = true;

            DestructibleTileTerrain terrain = FindObjectOfType<DestructibleTileTerrain>();
            if (terrain != null) terrain.DigCircle(point, explosionRadius);

            Collider2D[] hits = Physics2D.OverlapCircleAll(point, explosionRadius, damageMask);
            HashSet<Health> damaged = new();
            foreach (Collider2D hit in hits)
            {
                float distance = Vector2.Distance(hit.ClosestPoint(point), point);
                float t = Mathf.Clamp01(distance / Mathf.Max(0.001f, explosionRadius));

                Health health = hit.GetComponentInParent<Health>();
                if (health != null && damaged.Add(health))
                {
                    int damage = Mathf.CeilToInt(health.MaxHealth * Mathf.Clamp01(damagePercentPerHit));
                    health.TakeDamage(damage);
                }

                Rigidbody2D hitBody = hit.attachedRigidbody;
                if (hitBody != null && hitBody.bodyType == RigidbodyType2D.Dynamic)
                {
                    if (!knockbackAffectsPlayers && health != null)
                        continue;

                    Vector2 dir = ((Vector2)hitBody.worldCenterOfMass - point).normalized;
                    hitBody.AddForce(dir * (explosionForce * (1f - t)), ForceMode2D.Impulse);
                }
            }

            Exploded?.Invoke(this, point);
            Destroy(gameObject);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.65f, 0f, 0.25f);
            Gizmos.DrawSphere(transform.position, explosionRadius);
        }
    }
}
