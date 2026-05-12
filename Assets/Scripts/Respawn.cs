using UnityEngine;

public class Respawn : MonoBehaviour
{
    public Vector2 startPos;

    private void Start()
    {
        startPos = transform.position;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Obstacle"))
        {
            RespawnPlayers();
        }
    }

    void RespawnPlayers()
    {
        TransformPosition();
    }

    void TransformPosition()
    {
        transform.position = startPos;
    }
}
