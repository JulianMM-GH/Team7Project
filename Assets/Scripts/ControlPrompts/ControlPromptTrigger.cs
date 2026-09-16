using SupanthaPaul;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// A fully configurable "show a control prompt on collision" trigger. Drop this on any 2D
/// trigger collider, wire up its two icons (one per player), and set each icon's Action in the
/// Inspector - Movement, Jump, WallJump, Interact, or any future PromptAction. No sprite wiring
/// is needed on the icons themselves; each looks up its own art from the shared
/// ControlPromptIconSet based on whichever player it's tracking.
///
/// Player 1 and Player 2 each get their own icon, shown/hidden independently - if both players
/// are standing in the trigger at once, both prompts are visible at once (each following its own
/// player - see ControlPromptIcon's Follow Target Player). Both icons are hidden the moment the
/// game starts and only fade in while that specific player is standing in the trigger - fading in
/// fast, and fading back out slowly once they leave (see ControlPromptIcon's
/// fadeInDuration/fadeOutDuration).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class ControlPromptTrigger : MonoBehaviour
{
    [Tooltip("Shown for Player 1. Its Action field decides what it shows.")]
    [SerializeField] private ControlPromptIcon player1Icon;

    [Tooltip("Shown for Player 2. Its Action field decides what it shows.")]
    [SerializeField] private ControlPromptIcon player2Icon;

    void Awake()
    {
        // Hidden until each specific player actually stands in the trigger.
        player1Icon?.SetVisibleImmediate(false);
        player2Icon?.SetVisibleImmediate(false);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        int playerIndex = GetPlayerIndex(other);
        if (playerIndex == -1) return;

        ControlPromptIcon icon = IconFor(playerIndex);
        if (icon == null) return;

        icon.SetTargetPlayer(playerIndex);
        icon.SetVisible(true);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        int playerIndex = GetPlayerIndex(other);
        if (playerIndex == -1) return;

        IconFor(playerIndex)?.SetVisible(false);
    }

    private ControlPromptIcon IconFor(int playerIndex) => playerIndex == 0 ? player1Icon : player2Icon;

    private int GetPlayerIndex(Collider2D other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null) return -1;

        PlayerInput input = player.GetComponent<PlayerInput>();
        return input != null ? input.playerIndex : 0;
    }
}
