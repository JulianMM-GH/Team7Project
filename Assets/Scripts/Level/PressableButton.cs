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
        // 1. Check if an object on the target layer is inside the zone
        Collider2D hitCollider = Physics2D.OverlapBox(
            plateCollider.bounds.center,
            plateCollider.bounds.size,
            0f,
            1 << targetLayer
        );

        // 2. Verify that an object was hit AND it has the "Player" tag
        bool touchingTarget = plateCollider != null &&
                             hitCollider != null &&
                             hitCollider.CompareTag("Player");

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
