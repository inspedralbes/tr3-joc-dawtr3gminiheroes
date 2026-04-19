using UnityEngine;

public class BulletScript : MonoBehaviour
{
    public AudioClip Sound;
    public float Speed = 6f;
    public float Lifetime = 4f;
    public int Damage = 1;

    private Rigidbody2D body;
    private Vector2 direction;
    private DamageTeam ownerTeam = DamageTeam.Neutral;
    private GameObject owner;

    private void Start()
    {
        body = GetComponent<Rigidbody2D>();
        PlayShotSound();
        Destroy(gameObject, Lifetime);
    }

    private void FixedUpdate()
    {
        if (body == null)
        {
            return;
        }

        body.linearVelocity = direction * Speed;
    }

    public void Configure(Vector2 travelDirection, DamageTeam team, GameObject ownerObject)
    {
        direction = travelDirection.normalized;
        ownerTeam = team;
        owner = ownerObject;
    }

    public void SetDirection(Vector2 travelDirection)
    {
        Configure(travelDirection, DamageTeam.Neutral, null);
    }

    public void DestroyBullet()
    {
        Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (owner != null && collision.transform.root == owner.transform.root)
        {
            return;
        }

        IDamageable damageable = collision.GetComponentInParent<IDamageable>();
        if (damageable != null)
        {
            if (damageable.Team == ownerTeam)
            {
                return;
            }

            damageable.ReceiveDamage(Damage, owner, ownerTeam);
            DestroyBullet();
            return;
        }

        if (!collision.isTrigger)
        {
            DestroyBullet();
        }
    }

    private void PlayShotSound()
    {
        if (Sound == null)
        {
            return;
        }

        AudioSource source = null;
        if (Camera.main != null)
        {
            source = Camera.main.GetComponent<AudioSource>();
        }

        if (source == null)
        {
            source = Object.FindFirstObjectByType<AudioSource>();
        }

        if (source != null)
        {
            source.PlayOneShot(Sound);
        }
    }
}
