using UnityEngine;
using UnityEngine.SceneManagement;
using SupanthaPaul;

public class TempWinScreen : MonoBehaviour
{
    [Header("Scene Configuration")]
    [Tooltip("The exact build index of the scene you want to load")]
    [SerializeField] private int targetSceneIndex;

    private bool hasTriggered = false; // Prevents double-triggering (both players interact)

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasTriggered) return;

        // Object entering the trigger is a player
        PlayerController player = other.GetComponentInParent<PlayerController>();

        if (player != null)
        {
            hasTriggered = true;

            Time.timeScale = 1f;

            // Load the next level
            SceneManager.LoadScene(targetSceneIndex);
        }
    }
}
