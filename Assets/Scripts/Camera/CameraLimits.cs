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

    [Header("Dynamic zoom")]
    [Tooltip("Pull the camera back a little once a player is pushed right up against the edge of the screen.")]
    public bool dynamicZoom = true;

    [Tooltip("How far back the camera is allowed to go. 1 = never zooms, 1.2 = shows 20% more when players are at their limit.")]
    [Range(1f, 2f)] public float maxZoomOut = 1.2f;

    [Tooltip("Slice of the screen kept clear along each edge. The zoom only kicks in once a player pushes into it.")]
    [Range(0f, 0.3f)] public float edgeMargin = 0.08f;

    [Tooltip("How fast the camera pulls back when a player hits the edge.")]
    [Range(0.01f, 1f)] public float zoomOutSpeed = 0.1f;

    [Tooltip("How fast it settles back in once the players are together again. Slower than zooming out so it doesn't pop.")]
    [Range(0.01f, 1f)] public float zoomInSpeed = 0.03f;

    [Tooltip("Stops players being clamped with their pivot exactly on the screen edge (which cuts them in half). Set to 0 for the old behaviour.")]
    [Range(0f, 0.2f)] public float playerScreenMargin = 0.03f;

    // 1 = the framing set up in the inspector, grows towards maxZoomOut
    private float zoom = 1f;

    // The orthographic size the scene was authored with. Also the reference the bounds
    // clamp has always used, so it stays in play even on a perspective camera.
    private float baseOrthoSize;

    public float CurrentZoom => zoom;

    void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam != null) baseOrthoSize = cam.orthographicSize;
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

    // Zooming

    /// <summary>
    /// Half the height the camera can see at zoom 1, in world units. Orthographic cameras
    /// state it outright, perspective ones get it from the FOV and how far the offset sits them back.
    /// </summary>
    private float BaseHalfHeight()
    {
        if (cam == null) return 0f;
        if (cam.orthographic) return baseOrthoSize;

        return Mathf.Abs(Offset.z) * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
    }

    private void UpdateZoom()
    {
        float baseHalf = BaseHalfHeight();

        if (!dynamicZoom || baseHalf <= 0.001f)
        {
            zoom = Mathf.Lerp(zoom, 1f, zoomInSpeed);
            return;
        }

        // How far the furthest player is from the middle of the screen right now.
        Vector3 center = transform.position;
        float halfWidthNeeded = 0f;
        float halfHeightNeeded = 0f;

        foreach (GameObject player in players)
        {
            if (player == null) continue;
            Vector3 pos = player.transform.position;
            halfWidthNeeded = Mathf.Max(halfWidthNeeded, Mathf.Abs(pos.x - center.x));
            halfHeightNeeded = Mathf.Max(halfHeightNeeded, Mathf.Abs(pos.y - center.y));
        }

        // Leave the edge margin clear, so nothing starts happening until they're genuinely near the edge.
        float usable = Mathf.Max(0.1f, 1f - 2f * edgeMargin);
        halfWidthNeeded /= usable;
        halfHeightNeeded /= usable;

        // The camera only zooms in one dimension, so the width requirement becomes a height one.
        float aspect = cam.aspect > 0.01f ? cam.aspect : 1f;
        float needed = Mathf.Max(halfHeightNeeded, halfWidthNeeded / aspect);

        float target = Mathf.Clamp(needed / baseHalf, 1f, maxZoomOut);
        zoom = Mathf.Lerp(zoom, target, target > zoom ? zoomOutSpeed : zoomInSpeed);
    }

    private void ApplyZoom()
    {
        // Orthographic zooms by size, perspective by backing the camera off (see ZoomedOffset).
        if (cam != null && cam.orthographic)
            cam.orthographicSize = baseOrthoSize * zoom;
    }

    private Vector3 ZoomedOffset()
    {
        Vector3 offset = Offset;
        if (cam != null && !cam.orthographic) offset.z *= zoom;
        return offset;
    }

    /// <summary>
    /// Half height the bounds clamp works off. At zoom 1 this is exactly what it always was,
    /// so levels stay framed the way they were tuned, and it only grows with the zoom.
    /// </summary>
    private float ClampHalfHeight()
    {
        if (cam.orthographic) return cam.orthographicSize; // zoom is already baked in there
        return baseOrthoSize + (zoom - 1f) * BaseHalfHeight();
    }

    // Clamping

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

        float camHeight = ClampHalfHeight();
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
        float margin = Mathf.Clamp(playerScreenMargin, 0f, 0.45f);

        foreach (GameObject player in players)
        {
            if (player == null) continue;
            Vector3 viewportPos = cam.WorldToViewportPoint(player.transform.position);

            bool outside =
                viewportPos.x < margin || viewportPos.x > 1f - margin ||
                viewportPos.y < margin || viewportPos.y > 1f - margin;

            if (outside)
            {
                viewportPos.x = Mathf.Clamp(viewportPos.x, margin, 1f - margin);
                viewportPos.y = Mathf.Clamp(viewportPos.y, margin, 1f - margin);

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
        if (players.Count == 0 || cam == null) return;

        UpdateZoom();
        ApplyZoom();

        Vector3 meanPosition = AveragePositions(players);
        Vector3 desiredPosition = meanPosition + ZoomedOffset();
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
