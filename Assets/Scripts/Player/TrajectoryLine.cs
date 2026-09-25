using System.Collections;
using UnityEngine;

public class TrajectoryLine : MonoBehaviour
{
    public LineRenderer lineRenderer;
    public int linePoints = 30;
    public float speed = 10f;
    public float arcAngle = 45f;

    void Update()
    {
        float offset = Time.time * -2f;
        lineRenderer.material.mainTextureOffset = new Vector2(offset, 0);

        RenderTrajectory();
    }

    void RenderTrajectory()
    {
        lineRenderer.positionCount = linePoints;
        Vector2 startPosition = transform.position;
        Vector2 startVelocity = new Vector2(Mathf.Cos(arcAngle * Mathf.Deg2Rad), Mathf.Sin(arcAngle * Mathf.Deg2Rad)) * speed;

        for (int i = 0; i < linePoints; i++)
        {
            float t = i * 0.1f;
            Vector2 displacement = startVelocity * t + 0.5f * Physics2D.gravity * t * t;
            lineRenderer.SetPosition(i, startPosition + displacement);
        }
    }
}
