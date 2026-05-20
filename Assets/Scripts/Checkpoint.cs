using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            PlayerRespawn player = collision.GetComponent<PlayerRespawn>();
            if (player != null)
            {
                player.currentCheckpoint = transform;
            }
        }
    }
}