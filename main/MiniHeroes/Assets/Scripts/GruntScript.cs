using UnityEngine;

public class GruntScript : MonoBehaviour
{
    public GameObject John;
    public GameObject Bullet;
    private float LastShoot;
    private int MaxHealth = 5;
    private int Health = 5;

    private void Update()
    {
        if(John == null) return;

        Vector3 direction = John.transform.position - transform.position;
        if(direction.x >= 0.0f) transform.localScale = new Vector3(1,1,1);
        else transform.localScale = new Vector3(-1,1,1);

        float distance = Mathf.Abs(John.transform.position.x - transform.position.x);

        if(distance < 1.0f && Time.time > LastShoot + 0.75f)
        {
            Shoot();
            LastShoot = Time.time;
        }
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

    public void Hit(int damage = 1)
    {
        Health = Health - damage;
        if (Health <= 0)
        {
            if (John != null)
            {
                JohnMovement johnScript = John.GetComponent<JohnMovement>();
                if (johnScript != null)
                {
                    johnScript.AddExperience(5);
                }
            }
            Destroy(gameObject);
        }
    }

    private void OnGUI()
    {
        if (Camera.main != null)
        {
            Vector2 screenPosition = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 0.6f);
            
            float barWidth = 40f;
            float barHeight = 10f;
            Rect rect = new Rect(screenPosition.x - barWidth / 2, Screen.height - screenPosition.y - barHeight, barWidth, barHeight);
            
            // Background
            GUI.color = Color.black;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            
            // Foreground (Health)
            GUI.color = Color.red; // Grunts might have red health bars
            Rect healthRect = new Rect(rect.x, rect.y, rect.width * ((float)Health / MaxHealth), rect.height);
            GUI.DrawTexture(healthRect, Texture2D.whiteTexture);
            
            // Reset GUI color
            GUI.color = Color.white;
        }
    }
}
