using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The single shared library mapping each PromptAction to the icon for every PromptDevice.
/// Every ControlPromptIcon in the game reads from the same asset, so adding a new zone/prompt
/// never needs its own sprite wiring - and adding a new action just means one new list entry
/// here instead of touching any scene or prefab.
/// </summary>
[CreateAssetMenu(fileName = "ControlPromptIconSet", menuName = "Control Prompts/Icon Set")]
public class ControlPromptIconSet : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        public PromptAction action;

        [Tooltip("-1 = both players can see this prompt. 0 or 1 restricts it to just that player - e.g. an ability only one character has, like Silas's Wall Jump or Phoenix's Light.")]
        public int restrictToPlayer = -1;

        [Header("Icon per device")]
        public Sprite keyboardWASD;
        public Sprite keyboardArrows;
        public Sprite xbox;
        public Sprite playstation;
        public Sprite genericGamepad;

        public bool IsAllowedForPlayer(int playerIndex) => restrictToPlayer < 0 || restrictToPlayer == playerIndex;

        public Sprite GetSprite(PromptDevice device)
        {
            return device switch
            {
                PromptDevice.KeyboardWASD => keyboardWASD,
                PromptDevice.KeyboardArrows => keyboardArrows,
                PromptDevice.Xbox => xbox,
                PromptDevice.PlayStation => playstation,
                _ => genericGamepad
            };
        }
    }

    [SerializeField] private List<Entry> entries = new List<Entry>();

    // No sprite for a given (action, device) pair just hides the icon rather than showing the
    // wrong prompt - same graceful-degrade behaviour as the old per-object icon fields had. A
    // player-restricted action (see Entry.restrictToPlayer) hides the same way for the player it
    // doesn't apply to.
    public Sprite GetSprite(PromptAction action, PromptDevice device, int playerIndex)
    {
        foreach (Entry entry in entries)
        {
            if (entry.action == action)
                return entry.IsAllowedForPlayer(playerIndex) ? entry.GetSprite(device) : null;
        }

        return null;
    }
}
