using UnityEngine;

public class CameraLimits : MonoBehaviour
{
    public GameObject[] Players;
    private Camera cam;

    public float smoothSpeed = 0.125f;
    public Vector3 Offset;

    public Vector3 boundsCenter;
    public Vector3 boundsSize = new Vector3(10f, 10f, 0f);

    void Start()
    {
        cam = GetComponent<Camera>();
    }

    private Vector3 AveragePositions(GameObject[] positions)
    {
        if (positions.Length == 0) return Vector3.zero;

        Vector3 sum = Vector3.zero;
        foreach (GameObject p in positions)
        {
            sum += p.transform.position;
        }

        return sum / positions.Length;
    }

    private void ClampPlayersToBounds()
    {
        Vector3 min = boundsCenter - boundsSize / 2f;
        Vector3 max = boundsCenter + boundsSize / 2f;

        foreach (GameObject player in Players)
        {
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

        pos.x = Mathf.Clamp(pos.x, min.x + camWidth, max.x - camWidth);
        pos.y = Mathf.Clamp(pos.y, min.y + camHeight, max.y - camHeight);

        transform.position = pos;
    }

    private void ClampPlayersToCamera()
    {
        foreach (GameObject player in Players)
        {
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
        Vector3 meanPosition = AveragePositions(Players);
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