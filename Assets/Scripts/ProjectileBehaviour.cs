using UnityEngine;

public class ProjectileBehaviour : MonoBehaviour
{
    public enum ProjectileState { Base, OnFire }
    public ProjectileState currentState = ProjectileState.Base;

    [SerializeField] private Color fireColor = Color.red;

    [Header("Sounds")]
    [SerializeField] private AudioClip[] fireSoundClips;

    private void OnCollisionEnter2D(Collision2D collision)
    {
        Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Light"))
        {
            SFXManager.instance.PlayRandomSFXClip(fireSoundClips, transform, 1f, 0.75f);

            currentState = ProjectileState.OnFire;

            if (TryGetComponent<SpriteRenderer>(out var sr))
                sr.color = fireColor;
        }
    }
}
