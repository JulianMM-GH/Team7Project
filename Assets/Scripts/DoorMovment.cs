using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;

public class DoorMovment : MonoBehaviour
{
    private Vector2 StartPosition;
    public float raiseHeight;
    public float raiseSpeed;
    public float sidePushSpeed = 8f;
    private float releaseTimer;
    private bool isOpening;
    private bool hasPlayedParticles = false;

    private BoxCollider2D doorCollider;
    private LayerMask playerMask;

    void Awake()
    {
        // A Collider2D with no Rigidbody2D is treated as static by the physics engine. Moving its
        // Transform every frame (as this script does below) then forces the engine to tear down and
        // rebuild the collider each frame, which is expensive and was the cause of the stutter while
        // doors move. A Kinematic body lets Unity move the same collider cheaply instead.
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody2D>();

        rb.bodyType = RigidbodyType2D.Kinematic;

        doorCollider = GetComponent<BoxCollider2D>();
        playerMask = LayerMask.GetMask("Player");
    }

    void Start()
    {
        StartPosition = (Vector2)transform.localPosition;
    }

    public void OpenDoor()
    {
        isOpening = true;
        releaseTimer = 0.1f;
        hasPlayedParticles = false;

    }

    // Update is called once per frame
    void Update()
    {
        if (releaseTimer > 0f)
        {
            releaseTimer -= Time.deltaTime;
        }
        else
        {
            isOpening = false;
        }

        if (isOpening)
        {
            float d = Vector2.Distance(transform.localPosition, new Vector2(0, raiseHeight));
            transform.localPosition = Vector2.Lerp(transform.localPosition, new Vector2(0, raiseHeight), Time.deltaTime * raiseSpeed * Mathf.Lerp(0.15f, 1f, d / (d + 1f)));
        }
        else
        {
            float d = Vector2.Distance(transform.localPosition, StartPosition);
            float t = d / (d + 3f);
            transform.localPosition = Vector2.Lerp(transform.localPosition, StartPosition, Time.deltaTime * raiseSpeed * Mathf.Lerp(30f, 4f, t * t));
            if (Vector2.Distance(transform.localPosition, StartPosition) < 0.5f && !hasPlayedParticles)
            {
                ParticleSystem[] Particles = GetComponentsInChildren<ParticleSystem>();
                foreach (ParticleSystem particle in Particles) {
                    particle.Play();
                }
                hasPlayedParticles = true;
            }

            PushClearOfClosingDoor();
        }
    }

    // Nudges anyone caught under the closing door out to whichever side they're
    // already leaning toward, instead of letting them get squeezed underneath it.
    void PushClearOfClosingDoor()
    {
        if (doorCollider == null)
            return;

        Bounds bounds = doorCollider.bounds;
        Collider2D[] overlaps = Physics2D.OverlapBoxAll(bounds.center, bounds.size, 0f, playerMask);
        foreach (Collider2D overlap in overlaps)
        {
            Rigidbody2D playerBody = overlap.attachedRigidbody;
            if (playerBody == null)
                continue;

            float pushDirection = playerBody.position.x >= bounds.center.x ? 1f : -1f;
            playerBody.position += new Vector2(pushDirection * sidePushSpeed * Time.deltaTime, 0f);
        }
    }
}
