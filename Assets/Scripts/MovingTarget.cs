using UnityEngine;

public class MovingTarget : MonoBehaviour
{
    public GameObject objectToDestroy;

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.TryGetComponent<ProjectileBehaviour>(out var projectile))
        {
            RAudio.PlayOneShot("Bell");
            Destroy(objectToDestroy);

            Destroy(collision.gameObject);
        }
    }
}
