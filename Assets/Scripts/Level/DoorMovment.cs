using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;

public class DoorMovment : MonoBehaviour
{
    private Vector2 StartPosition;
    public float raiseHeight;
    public float raiseSpeed;
    public float sidePushSpeed = 8f;
    [Tooltip("Gap left between a pushed-out player and the edge of the door")]
    public float pushClearance = 0.05f;
    private float releaseTimer;
    private bool isOpening;
    private bool hasPlayedParticles = false;

    private BoxCollider2D doorCollider;
    private Rigidbody2D doorBody;
    private LayerMask playerMask;
    private LayerMask solidMask;
    private readonly List<Rigidbody2D> pushedBodies = new List<Rigidbody2D>();

    // Shrinks the "is this spot free?" test box a touch so a player simply standing on the floor
    // or brushing a wall doesn't count as blocked.
    private const float TestBoxInset = 0.1f;
    // Extra distances (in player-widths) to try if the spot right beside the door is blocked.
    private static readonly float[] ExtraPushSteps = { 0f, 0.5f, 1f, 1.5f };

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
        doorBody = rb;

        doorCollider = GetComponent<BoxCollider2D>();
        playerMask = LayerMask.GetMask("Player");
        // anything solid that isn't a player - ground, walls, other doors, plates...
        solidMask = ~playerMask;
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
            // Clear everyone out of the door's path first. While anyone is still in the way the door
            // holds where it is, so it can never come down on a player and crush them into the floor.
            bool playerInTheWay = PushClearOfClosingDoor();

            if (!playerInTheWay)
            {
                float d = Vector2.Distance(transform.localPosition, StartPosition);
                float t = d / (d + 3f);
                transform.localPosition = Vector2.Lerp(transform.localPosition, StartPosition, Time.deltaTime * raiseSpeed * Mathf.Lerp(30f, 4f, t * t));
            }

            if (Vector2.Distance(transform.localPosition, StartPosition) < 0.5f && !hasPlayedParticles)
            {
                ParticleSystem[] Particles = GetComponentsInChildren<ParticleSystem>();
                foreach (ParticleSystem particle in Particles) {
                    particle.Play();
                }
                hasPlayedParticles = true;
            }
        }
    }

    // Moves anyone standing in the space the door still has to close through out to the side,
    // only ever into a spot that's actually free (never into the ground or a wall). Prefers the
    // side they're already leaning toward, falls back to the other side, and if neither side has
    // room the door just waits. Returns true while any player is still in the door's path.
    bool PushClearOfClosingDoor()
    {
        if (doorCollider == null)
            return false;

        Bounds door = doorCollider.bounds;

        // the whole column between where the door is now and where it'll be once shut
        Vector3 closedWorldPos = transform.parent != null
            ? transform.parent.TransformPoint(StartPosition)
            : (Vector3)StartPosition;
        Vector3 toClosed = closedWorldPos - transform.position;
        Bounds sweep = door;
        sweep.Encapsulate(door.min + toClosed);
        sweep.Encapsulate(door.max + toClosed);

        bool playerInTheWay = false;
        pushedBodies.Clear();

        Collider2D[] overlaps = Physics2D.OverlapBoxAll(sweep.center, sweep.size, 0f, playerMask);
        foreach (Collider2D overlap in overlaps)
        {
            // Phoenix's light is a trigger on the Player layer - only real bodies get pushed
            if (overlap.isTrigger)
                continue;

            Rigidbody2D body = overlap.attachedRigidbody;
            if (body == null || pushedBodies.Contains(body))
                continue;

            Bounds player = overlap.bounds;

            // standing on top of the door, or just brushing its sides - not in the way
            if (player.min.y >= door.max.y - 0.05f)
                continue;
            if (player.max.x <= sweep.min.x + 0.01f || player.min.x >= sweep.max.x - 0.01f)
                continue;

            pushedBodies.Add(body);
            playerInTheWay = true;

            float preferredSide = body.position.x >= sweep.center.x ? 1f : -1f;
            if (!TryFindClearSpot(player, body, sweep, preferredSide, out float targetCenterX))
                continue;   // nowhere safe yet - door keeps waiting until they move

            float direction = Mathf.Sign(targetCenterX - player.center.x);
            // cancel out walking back against the push so it always wins
            float resist = Mathf.Max(0f, -body.linearVelocity.x * direction);
            float step = Mathf.Min((sidePushSpeed + resist) * Time.deltaTime, Mathf.Abs(targetCenterX - player.center.x));

            body.position += new Vector2(direction * step, 0f);
        }

        return playerInTheWay;
    }

    // Finds the nearest free spot beside the door's path that the player can be slid to, checking
    // both the destination and the route there so they can't be pushed through or into anything.
    bool TryFindClearSpot(Bounds player, Rigidbody2D body, Bounds sweep, float preferredSide, out float targetCenterX)
    {
        foreach (float extra in ExtraPushSteps)
        {
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? preferredSide : -preferredSide;
                float edgeX = side > 0f ? sweep.max.x + player.extents.x : sweep.min.x - player.extents.x;
                float candidateX = edgeX + side * (pushClearance + extra * player.size.x);

                if (IsPathClear(player, candidateX, body))
                {
                    targetCenterX = candidateX;
                    return true;
                }
            }
        }

        targetCenterX = player.center.x;
        return false;
    }

    // Sweeps the player's box from where they are to targetX, ignoring this door and the player
    // themselves - true if nothing solid is in the way or at the destination.
    bool IsPathClear(Bounds player, float targetX, Rigidbody2D body)
    {
        Vector2 size = new Vector2(
            Mathf.Max(0.01f, player.size.x - TestBoxInset),
            Mathf.Max(0.01f, player.size.y - TestBoxInset));
        Vector2 from = player.center;
        Vector2 to = new Vector2(targetX, player.center.y);

        // sample every half box-width along the route (plus the destination itself)
        float distance = Mathf.Abs(to.x - from.x);
        int samples = Mathf.Max(1, Mathf.CeilToInt(distance / (size.x * 0.5f)));
        for (int s = 1; s <= samples; s++)
        {
            Vector2 point = Vector2.Lerp(from, to, (float)s / samples);
            Collider2D[] hits = Physics2D.OverlapBoxAll(point, size, 0f, solidMask);
            foreach (Collider2D hit in hits)
            {
                if (hit.isTrigger || hit == doorCollider || hit.attachedRigidbody == body || hit.attachedRigidbody == doorBody)
                    continue;
                return false;
            }
        }
        return true;
    }
}
