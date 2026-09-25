using UnityEngine;

public class PingPongMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float speed = 2.0f;
    public float range = 3.0f;

    private Vector2 startPosition;

    private void Start()
    {
        // Initial position stored as the center point
        startPosition = transform.position;
    }

    private void Update()
    {
        // Calculate the horizontal offset for the ping-pong movement
        float offset = Mathf.PingPong(Time.time * speed, range * 2) - range;

        // Apply the offset to the starting position
        transform.position = new Vector2(startPosition.x + offset, startPosition.y);
    }
}
