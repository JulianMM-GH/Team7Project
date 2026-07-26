using UnityEngine;
using UnityEngine.InputSystem;

public class UIPlayerFollow : MonoBehaviour
{
    [Header("Target Identity")]
    [Tooltip("0 for Player 1, 1 for Player 2")]
    [SerializeField] private int targetPlayerIndex = 0;

    [Header("Position Tuning")]
    [SerializeField] private Vector3 offset = new Vector3(0, 2f, 0);

    private Transform targetPlayerTransform;

    public void SetupTarget(Transform playerTransform, int index)
    {
        if (index == targetPlayerIndex)
        {
            targetPlayerTransform = playerTransform;
        }
    }

    void LateUpdate()
    {
        // If the specific player isn't in the game yet, do nothing
        if (targetPlayerTransform != null)
        {
            transform.position = targetPlayerTransform.position + offset;
        }
    }
}