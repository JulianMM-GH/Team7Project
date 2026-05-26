using UnityEngine;

public class MovingTarget : MonoBehaviour
{
    public GameObject objectToDestroy;

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.TryGetComponent<ProjectileBehaviour>(out var projectile))
        {
            if (projectile.currentState == ProjectileBehaviour.ProjectileState.OnFire)
            {
                Destroy(objectToDestroy);
            }
            else
            {
                Destroy(objectToDestroy);
            }

            Destroy(collision.gameObject);
        }
    }
}
