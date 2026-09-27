using UnityEngine;

public class ProjectileBehaviour : MonoBehaviour
{
    public enum ProjectileState { Base, OnFire }
    public ProjectileState currentState = ProjectileState.Base;

    [Header("Sprites")]
    [SerializeField] private Sprite baseSprite;
    [SerializeField] private Sprite fireSprite;

    private SpriteRenderer sr;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();

        // Projectile starts with the base sprite
        if (sr != null && baseSprite != null)
        {
            sr.sprite = baseSprite;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Light"))
        {
            RAudio.PlayOneShot("Set Shot Alight");
            currentState = ProjectileState.OnFire;

            // Change the sprite to the fire sprite
            if (sr != null && fireSprite != null)
            {
                sr.sprite = fireSprite;
            }
        }
    }
}