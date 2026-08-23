using UnityEngine;

public class MovingTarget : MonoBehaviour
{
    public GameObject objectToDestroy;

    [Header("Sounds")]
    [SerializeField] private AudioClip bellSoundClip;

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.TryGetComponent<ProjectileBehaviour>(out var projectile))
        {
            if (projectile.currentState == ProjectileBehaviour.ProjectileState.OnFire)
            {
                SFXManager.instance.PlaySFXClip(bellSoundClip, transform, 1f);
                Destroy(objectToDestroy);
            }
            else
            {
                SFXManager.instance.PlaySFXClip(bellSoundClip, transform, 1f);
                Destroy(objectToDestroy);
            }

            Destroy(collision.gameObject);
        }
    }
}
