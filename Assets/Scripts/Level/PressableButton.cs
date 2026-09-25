using UnityEngine;

public class PressableButton : MonoBehaviour
{
    public int targetLayer;
    private float SinkProgress;
    public float SinkSpeed;
    public float SinkDepth;
    public bool SingleUse;
    private bool HasBeenUsed = false;
    private Vector2 StartPosition;
    private bool isPressed;
    private float releaseTimer;
    public DoorMovment[] DoorMovmentScripts;
    private Collider2D plateCollider;

    void Start()
    {
        StartPosition = (Vector2)transform.localPosition;
        plateCollider = GetComponent<Collider2D>();
    }

    void Update()
    {
        // 1. Check every object on the target layer inside the zone. Phoenix's light is a trigger
        // collider on the Player layer too, so a single OverlapBox could return the light instead of
        // the player standing on the plate and wrongly release it - only solid Player bodies count.
        Collider2D[] hitColliders = Physics2D.OverlapBoxAll(
            plateCollider.bounds.center,
            plateCollider.bounds.size,
            0f,
            1 << targetLayer
        );

        // 2. Verify that a solid (non-trigger) collider with the "Player" tag was hit
        bool touchingTarget = false;
        foreach (Collider2D hitCollider in hitColliders)
        {
            if (!hitCollider.isTrigger && hitCollider.CompareTag("Player"))
            {
                touchingTarget = true;
                break;
            }
        }

        if (touchingTarget)
        {
            if (!isPressed) RAudio.PlayOneShot("Pressure Plate Click");
            isPressed = true;
            releaseTimer = 0.1f;
        }
        else if (releaseTimer > 0f)
        {
            releaseTimer -= Time.deltaTime;
        }
        else
        {
            isPressed = false;
        }

        if (SingleUse && HasBeenUsed)
        {
            SinkProgress = 1f;
            isPressed = true;
            releaseTimer = .1f;
        }

        // movement 
        if (isPressed) SinkProgress += SinkSpeed * Time.deltaTime;
        else SinkProgress -= SinkSpeed * Time.deltaTime;

        SinkProgress = Mathf.Clamp01(SinkProgress);
        transform.localPosition = StartPosition + new Vector2(0f, -SinkProgress * SinkDepth);

        if (SinkProgress == 1f)
        {
            foreach (DoorMovment Door in DoorMovmentScripts)
            {
                Door.OpenDoor();
                HasBeenUsed = true;
            }
        }
    }
}
