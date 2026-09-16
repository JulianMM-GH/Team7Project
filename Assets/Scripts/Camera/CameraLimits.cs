using UnityEngine;
using System.Collections.Generic;

public class CameraLimits : MonoBehaviour
{
    private List<GameObject> players = new List<GameObject>();
    private Camera cam;

    public float smoothSpeed = 0.125f;
    public Vector3 Offset;

    public Vector3 boundsCenter;
    public Vector3 boundsSize = new Vector3(10f, 10f, 0f);

    void Start()
    {
        cam = GetComponent<Camera>();
    }

    public void RegisterPlayer(GameObject player)
    {
        if (player != null && !players.Contains(player))
        {
            players.Add(player);
        }
    }

    private Vector3 AveragePositions(List<GameObject> positions)
    {
        if (positions.Count == 0) return Vector3.zero;

        Vector3 sum = Vector3.zero;
        foreach (GameObject p in positions)
        {
            // Null check
            if (p != null) sum += p.transform.position;
        }

        return sum / positions.Count;
    }

    private void ClampPlayersToBounds()
    {
        Vector3 min = boundsCenter - boundsSize / 2f;
        Vector3 max = boundsCenter + boundsSize / 2f;

        foreach (GameObject player in players)
        {
            if (player == null) continue;
            Vector3 pos = player.transform.position;

            pos.x = Mathf.Clamp(pos.x, min.x, max.x);
            pos.y = Mathf.Clamp(pos.y, min.y, max.y);

            player.transform.position = pos;
        }
    }

    private void ClampCameraToBounds()
    {
        Vector3 min = boundsCenter - boundsSize / 2f;
        Vector3 max = boundsCenter + boundsSize / 2f;

        Vector3 pos = transform.position;

        float camHeight = cam.orthographicSize;
        float camWidth = camHeight * cam.aspect;

        // Check if bounds are wide enough to accommodate camera size
        if ((max.x - camWidth) > (min.x + camWidth))
        {
            pos.x = Mathf.Clamp(pos.x, min.x + camWidth, max.x - camWidth);
        }
        else
        {
            pos.x = boundsCenter.x; // Force center if bounds are too small
        }

        // Check if bounds are tall enough to accommodate camera size
        if ((max.y - camHeight) > (min.y + camHeight))
        {
            pos.y = Mathf.Clamp(pos.y, min.y + camHeight, max.y - camHeight);
        }
        else
        {
            pos.y = boundsCenter.y; // Force center if bounds are too small
        }

        transform.position = pos;
    }

    private void ClampPlayersToCamera()
    {
        foreach (GameObject player in players)
        {
            if (player == null) continue;
            Vector3 viewportPos = cam.WorldToViewportPoint(player.transform.position);

            bool outside =
                viewportPos.x < 0 || viewportPos.x > 1 ||
                viewportPos.y < 0 || viewportPos.y > 1;

            if (outside)
            {
                viewportPos.x = Mathf.Clamp01(viewportPos.x);
                viewportPos.y = Mathf.Clamp01(viewportPos.y);

                Vector3 clampedWorldPos = cam.ViewportToWorldPoint(viewportPos);

                player.transform.position = new Vector3(
                    clampedWorldPos.x,
                    clampedWorldPos.y,
                    player.transform.position.z
                );
            }
        }
    }

    private void FixedUpdate()
    {
        // If no players are spawned yet, do nothing and prevent errors
        if (players.Count == 0) return;

        Vector3 meanPosition = AveragePositions(players);
        Vector3 desiredPosition = meanPosition + Offset;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);

        ClampCameraToBounds();
        ClampPlayersToBounds();
        ClampPlayersToCamera();
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(boundsCenter, boundsSize);
    }
}