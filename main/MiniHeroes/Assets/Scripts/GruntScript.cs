using UnityEngine;

public class GruntScript : MonoBehaviour
{
    public GameObject John;
    public GameObject Bullet;
    private float LastShoot;
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

    public void Hit()
    {
        Health = Health - 1;
        if (Health == 0)
        {
            Destroy(gameObject);
        }
    }
}
