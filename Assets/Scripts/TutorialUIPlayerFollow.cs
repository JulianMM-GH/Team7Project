using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One instance sits on each tutorial trigger zone. While a player is inside, it asks that
/// player's PlayerHeadPrompts (on SilasTutorial / PhoenixTutorial) to show this zone's prompt
/// canvas instead of the default one; on exit it releases that request. The canvas itself no
/// longer needs a separate keyboard/controller version - TutorialIconSwap on its icon handles
/// that, refreshed automatically whenever the zone's prompt is (re)shown.
/// </summary>
public class TutorialUIPlayerFollow : MonoBehaviour
{
    [Header("Player Tutorial Canvas (Specific to this Zone)")]
    [SerializeField] private GameObject player1TutorialCanvas;
    [SerializeField] private GameObject player2TutorialCanvas;

    private LocalMultiplayerSpawner spawner;
    private readonly PlayerHeadPrompts[] headPromptsCache = new PlayerHeadPrompts[2];

    // Track which player indices (0 for Player 1, 1 for Player 2) are inside the box collision zone
    private readonly HashSet<int> playerIndicesInside = new HashSet<int>();

    private void Awake()
    {
        spawner = Object.FindFirstObjectByType<LocalMultiplayerSpawner>();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        int playerIndex = GetPlayerIndexFromCollider(collision);
        if (playerIndex != -1 && playerIndicesInside.Add(playerIndex))
        {
            GetHeadPrompts(playerIndex)?.ShowZonePrompt(CanvasFor(playerIndex));
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        int playerIndex = GetPlayerIndexFromCollider(collision);
        if (playerIndex != -1 && !IsAnyColliderOfPlayerInTrigger(playerIndex))
        {
            playerIndicesInside.Remove(playerIndex);
            GetHeadPrompts(playerIndex)?.HideZonePrompt(CanvasFor(playerIndex));
        }
    }

    private void OnDisable()
    {
        foreach (int playerIndex in playerIndicesInside)
            GetHeadPrompts(playerIndex)?.HideZonePrompt(CanvasFor(playerIndex));

        playerIndicesInside.Clear();
    }

    // Called by PauseMenu.ResumeGame() so every player's prompt icon reflects a control change
    // made while paused, whether it's showing a zone-specific prompt or the default one.
    public static void RefreshActiveTutorials()
    {
        foreach (PlayerHeadPrompts prompts in Object.FindObjectsByType<PlayerHeadPrompts>(FindObjectsSortMode.None))
            prompts.RefreshIcons();
    }

    private GameObject CanvasFor(int playerIndex) => playerIndex == 0 ? player1TutorialCanvas : player2TutorialCanvas;

    private PlayerHeadPrompts GetHeadPrompts(int playerIndex)
    {
        if (headPromptsCache[playerIndex] != null)
            return headPromptsCache[playerIndex];

        foreach (UIPlayerFollow follower in Object.FindObjectsByType<UIPlayerFollow>(FindObjectsSortMode.None))
        {
            if (follower.TargetPlayerIndex == playerIndex)
            {
                headPromptsCache[playerIndex] = follower.GetComponent<PlayerHeadPrompts>();
                break;
            }
        }

        return headPromptsCache[playerIndex];
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

        // Grab all colliders directly attached to the player root and its children
        Collider2D[] playerColliders = pObj.GetComponentsInChildren<Collider2D>();

        foreach (var pCollider in playerColliders)
        {
            // Skip triggers or inactive objects to avoid false tracking positives
            if (pCollider.isTrigger || !pCollider.gameObject.activeInHierarchy) continue;

            // Geometric verification: check if player bounds intersect trigger bounds
            if (myTrigger.bounds.Intersects(pCollider.bounds))
            {
                return true;
            }
        }
        return false;
    }
}
