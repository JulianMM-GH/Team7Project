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

    void Start()
    {
        StartPosition = (Vector2)transform.localPosition;
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        if (collision.gameObject.layer == targetLayer)
        {
            isPressed = true;

            releaseTimer = 0.1f;
        }
    }



    void Update()
    {
        if (releaseTimer > 0f)
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