using UnityEngine;

public class PlayerRespawn : MonoBehaviour
{
    [HideInInspector] public Transform currentCheckpoint;

    public void Respawn()
    {
        if (currentCheckpoint != null)
        {
            transform.position = currentCheckpoint.position;
        }
    }
}