using UnityEngine;

public class WoodenTarget : MonoBehaviour
{
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.TryGetComponent<ProjectileBehaviour>(out var projectile))
        {
            if (projectile.currentState == ProjectileBehaviour.ProjectileState.OnFire)
            {
                Debug.Log("Hit by fire projectile");
                Destroy(gameObject);
            }
            else
            {
                Debug.Log("Hit by base projectile.");
            }

            Destroy(collision.gameObject);
        }
    }
}
