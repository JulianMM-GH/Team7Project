using UnityEngine;

public class ProjectileBehaviour : MonoBehaviour
{
    public float speed = 10f;
    public float arcAngle = 45f;
    private Rigidbody2D rb;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();

        // Calculate direction based on angle
        Vector2 direction = new Vector2(Mathf.Cos(arcAngle * Mathf.Deg2Rad), Mathf.Sin(arcAngle * Mathf.Deg2Rad));

        // Apply force to create arc
        rb.AddForce(direction * speed, ForceMode2D.Impulse);
    }
    private void OnCollisionEnter2D(Collision2D collision)
    {
        Destroy(gameObject);
    }
}
