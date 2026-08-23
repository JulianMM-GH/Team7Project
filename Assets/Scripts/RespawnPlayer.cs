using SupanthaPaul;
using UnityEngine;

public class PlayerRespawn : MonoBehaviour
{
    [HideInInspector] public Transform currentCheckpoint;

    private PlayerController controller;

    void Awake()
    {
        controller = GetComponent<PlayerController>();
    }

    public void Respawn()
    {
        if (currentCheckpoint == null) return;

        transform.position = currentCheckpoint.position;

        if (controller != null)
            controller.ResetPhysicsState();
    }
}