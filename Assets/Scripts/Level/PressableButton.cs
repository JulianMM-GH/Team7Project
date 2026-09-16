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
        // Physics2D.OverlapBox is a spatial query, not a collision callback, so unlike
        // OnCollisionStay2D it keeps detecting the player even if their Rigidbody2D has
        // gone to sleep from standing still - which was causing the plate (and doors) to
        // randomly reset while the player was idle on top of it.
        bool touchingTarget = plateCollider != null &&
            Physics2D.OverlapBox(plateCollider.bounds.center, plateCollider.bounds.size, 0f, 1 << targetLayer);

        if (touchingTarget)
        {
            if (!isPressed)
                RAudio.PlayOneShot("Pressure Plate Click");

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
        if (isPressed)
            SinkProgress += SinkSpeed * Time.deltaTime;
        else
            SinkProgress -= SinkSpeed * Time.deltaTime;

        SinkProgress = Mathf.Clamp01(SinkProgress);

        transform.localPosition =
            StartPosition + new Vector2(0f, -SinkProgress * SinkDepth);
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