using UnityEngine;

public class MenuTransition : MonoBehaviour
{
    public GameObject[] menus;

    [Range(1, 5)]
    public int CurrentMenu = 1;
    public float MoveSpeed = 5f;
    public float ZoomSpeed = 5f;
    public float ZoomTarget = 3f;
    public float ZoomTravel = 7f;
    private Camera cam;
    private int lastMenu;
    private float travelDistance;

    private void Start()
    {
        cam = Camera.main;
        lastMenu = CurrentMenu;
        ResetDistance();
    }

    private void Update()
    {
        if (cam == null || menus == null || menus.Length == 0)
            return;

        CurrentMenu = Mathf.Clamp(CurrentMenu, 1, menus.Length);

        if (CurrentMenu != lastMenu)
        {
            lastMenu = CurrentMenu;
            ResetDistance();
        }

        Transform target = menus[CurrentMenu - 1].transform;

        MoveCamera(target.position);
        ZoomCamera(target.position);
    }

    public void ChangeMenu(int newMenu)
    {
        CurrentMenu = newMenu;
    }

    private void MoveCamera(Vector3 targetPosition)
    {
        Vector3 cameraPosition = cam.transform.position;

        Vector3 newPosition = Vector3.Lerp(
            cameraPosition,
            targetPosition,
            Time.deltaTime * MoveSpeed
        );

        cam.transform.position = new Vector3(
            newPosition.x,
            newPosition.y,
            cameraPosition.z
        );
    }

    private void ZoomCamera(Vector3 targetPosition)
    {
        float remainingDistance = Vector2.Distance(
            cam.transform.position,
            targetPosition
        );

        float progress = travelDistance > 0.1f
            ? Mathf.Clamp01(remainingDistance / travelDistance)
            : 0f;

        float zoomCurve = Mathf.Sin(progress * Mathf.PI);
        float targetZoom = Mathf.Lerp(ZoomTarget, ZoomTravel, zoomCurve);

        cam.orthographicSize = Mathf.Lerp(
            cam.orthographicSize,
            targetZoom,
            Time.deltaTime * ZoomSpeed
        );
    }

    private void ResetDistance()
    {
        if (cam == null || menus == null || menus.Length == 0)
            return;

        int index = Mathf.Clamp(CurrentMenu - 1, 0, menus.Length - 1);

        travelDistance = Vector2.Distance(
            cam.transform.position,
            menus[index].transform.position
        );
    }
}