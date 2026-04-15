using UnityEngine;

public class JohnMovement : MonoBehaviour
{
    public GameObject Bullet;
    public float Speed;
    public float JumpForce;

    private Rigidbody2D Rigidbody2D;
    private Animator Animator;
    private float Horizontal;
    private bool Grounded;
    private float LastShoot;
    private int MaxHealth = 10;
    private int Health = 10;
    
    void Start()
    {
        Rigidbody2D = GetComponent<Rigidbody2D>();
        Animator = GetComponent<Animator>();
    }

    void Update()
    {
        Horizontal = Input.GetAxis("Horizontal");

        if(Horizontal < 0.0f) transform.localScale = new Vector3(-1, 1, 1);
        else if(Horizontal > 0.0f) transform.localScale = new Vector3(1, 1, 1);

        Animator.SetBool("running", Horizontal != 0.0f);

        Debug.DrawRay(transform.position, Vector3.down, Color.red);

        if(Physics2D.Raycast(transform.position, Vector3.down, 0.1f))
        {
            Grounded = true;
        }
        else
        {
            Grounded = false;
        }

        if (Input.GetKeyDown(KeyCode.W) && Grounded)
        {
            Jump();
        }

        if (Input.GetKeyDown(KeyCode.Space) && Time.time > LastShoot + 0.25f)
        {
            Shoot();
            LastShoot = Time.time;
        }
        
    }

    private void Jump()
    {
        Rigidbody2D.AddForce(Vector2.up * JumpForce);
    }

    private void Shoot()
    {
        Vector3 direction;
        if(transform.localScale.x == 1)
        {
            direction = Vector3.right;
        }
        else
        {
            direction = Vector3.left;
        }
        GameObject bullet = Instantiate(Bullet, transform.position + direction * 0.1f, Quaternion.identity);
        bullet.GetComponent<BulletScript>().SetDirection(direction);
    }

    private void FixedUpdate()
    {
        Rigidbody2D.linearVelocity = new Vector2(Horizontal, Rigidbody2D.linearVelocity.y);
    }
    
    public void Hit()
    {
        Health = Health - 1;
        if (Health == 0)
        {
            Destroy(gameObject);
        }
    }

    private void OnGUI()
    {
        if (Camera.main != null)
        {
            Vector2 screenPosition = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 0.6f);
            
            float barWidth = 50f;
            float barHeight = 12f;
            Rect rect = new Rect(screenPosition.x - barWidth / 2, Screen.height - screenPosition.y - barHeight, barWidth, barHeight);
            
            // Background
            GUI.color = Color.black;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            
            // Foreground (Health)
            GUI.color = Color.green;
            Rect healthRect = new Rect(rect.x, rect.y, rect.width * ((float)Health / MaxHealth), rect.height);
            GUI.DrawTexture(healthRect, Texture2D.whiteTexture);
            
            // Reset GUI color
            GUI.color = Color.white;
        }
    }
}
