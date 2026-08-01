using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class TutorialUIPlayerFollow : MonoBehaviour
{
    [Header("Player 1 Tutorial Canvases (Specific to this Zone)")]
    [SerializeField] private GameObject player1KeyboardTutorialCanvas;
    [SerializeField] private GameObject player1ControllerTutorialCanvas;

    [Header("Player 2 Tutorial Canvases (Specific to this Zone)")]
    [SerializeField] private GameObject player2KeyboardTutorialCanvas;
    [SerializeField] private GameObject player2ControllerTutorialCanvas;

    private LocalMultiplayerSpawner spawner;

    // Track which player indices (0 for Player 1, 1 for Player 2) are inside the box collision zone
    private readonly HashSet<int> playerIndicesInside = new HashSet<int>();

    // Global list of all active box collision trigger zones so they can update when unpausing
    private static readonly HashSet<TutorialUIPlayerFollow> activeInstances = new HashSet<TutorialUIPlayerFollow>();

    private void Awake()
    {
        spawner = Object.FindFirstObjectByType<LocalMultiplayerSpawner>();
        HideAllCanvases();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        int playerIndex = GetPlayerIndexFromCollider(collision);
        if (playerIndex != -1)
        {
            playerIndicesInside.Add(playerIndex);
            activeInstances.Add(this);
            UpdateTutorialVisibility();
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        int playerIndex = GetPlayerIndexFromCollider(collision);
        if (playerIndex != -1)
        {
            if (!IsAnyColliderOfPlayerInTrigger(playerIndex))
            {
                playerIndicesInside.Remove(playerIndex);
            }

            if (playerIndicesInside.Count == 0)
            {
                activeInstances.Remove(this);
                HideAllCanvases();
            }
            else
            {
                UpdateTutorialVisibility();
            }
        }
    }

    private void OnDisable()
    {
        activeInstances.Remove(this);
        HideAllCanvases();
    }

    // Called by PauseMenu.ResumeGame() to update active zones after changing controls.
    // This ensures that even if you change your controls when tutorial prompts are already being displayed, they will remain accurate.
    public static void RefreshActiveTutorials()
    {
        foreach (var instance in activeInstances)
        {
            if (instance != null && instance.playerIndicesInside.Count > 0)
            {
                instance.UpdateTutorialVisibility();
            }
        }
    }

    public void UpdateTutorialVisibility()
    {
        if (spawner == null)
            spawner = Object.FindFirstObjectByType<LocalMultiplayerSpawner>();

        // Player 1
        var slot1 = spawner?.GetSlot(0);
        bool p1Here = playerIndicesInside.Contains(0) && slot1 != null;

        if (p1Here)
        {
            bool isGamepad1 = slot1.device is Gamepad;
            if (player1KeyboardTutorialCanvas != null) player1KeyboardTutorialCanvas.SetActive(!isGamepad1);
            if (player1ControllerTutorialCanvas != null) player1ControllerTutorialCanvas.SetActive(isGamepad1);
        }
        else
        {
            if (player1KeyboardTutorialCanvas != null) player1KeyboardTutorialCanvas.SetActive(false);
            if (player1ControllerTutorialCanvas != null) player1ControllerTutorialCanvas.SetActive(false);
        }

        // Player 2
        var slot2 = spawner?.GetSlot(1);
        bool p2Here = playerIndicesInside.Contains(1) && slot2 != null;

        if (p2Here)
        {
            bool isGamepad2 = slot2.device is Gamepad;
            if (player2KeyboardTutorialCanvas != null) player2KeyboardTutorialCanvas.SetActive(!isGamepad2);
            if (player2ControllerTutorialCanvas != null) player2ControllerTutorialCanvas.SetActive(isGamepad2);
        }
        else
        {
            if (player2KeyboardTutorialCanvas != null) player2KeyboardTutorialCanvas.SetActive(false);
            if (player2ControllerTutorialCanvas != null) player2ControllerTutorialCanvas.SetActive(false);
        }
    }

    private int GetPlayerIndexFromCollider(Collider2D collision)
    {
        if (!collision.CompareTag("Player")) return -1;

        if (spawner == null)
            spawner = Object.FindFirstObjectByType<LocalMultiplayerSpawner>();

        for (int i = 0; i < 2; i++)
        {
            var slot = spawner?.GetSlot(i);
            if (slot?.playerInput != null)
            {
                GameObject pObj = slot.playerInput.gameObject;
                // Check if the collider belongs to this player root or any of its children
                if (collision.gameObject == pObj || collision.transform.IsChildOf(pObj.transform))
                {
                    return i;
                }
            }
        }
        return -1;
    }

    private bool IsAnyColliderOfPlayerInTrigger(int playerIndex)
    {
        var slot = spawner?.GetSlot(playerIndex);
        if (slot?.playerInput == null) return false;

        GameObject pObj = slot.playerInput.gameObject;
        Collider2D myTrigger = GetComponent<Collider2D>();
        if (myTrigger == null) return false;

        // Overlap check to see if any part of the player intersects this trigger area
        Collider2D[] results = new Collider2D[10];
        ContactFilter2D filter = new ContactFilter2D();
        filter.NoFilter();
        filter.useTriggers = true;

        int count = Physics2D.OverlapCollider(myTrigger, filter, results);
        for (int i = 0; i < count; i++)
        {
            if (results[i] != null && (results[i].gameObject == pObj || results[i].transform.IsChildOf(pObj.transform)))
            {
                return true;
            }
        }
        return false;
    }

    private void HideAllCanvases()
    {
        if (player1KeyboardTutorialCanvas != null) player1KeyboardTutorialCanvas.SetActive(false);
        if (player1ControllerTutorialCanvas != null) player1ControllerTutorialCanvas.SetActive(false);
        if (player2KeyboardTutorialCanvas != null) player2KeyboardTutorialCanvas.SetActive(false);
        if (player2ControllerTutorialCanvas != null) player2ControllerTutorialCanvas.SetActive(false);
    }
}