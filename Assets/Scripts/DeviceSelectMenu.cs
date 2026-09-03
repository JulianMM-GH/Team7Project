using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Drives the "Player Controls" panel in the settings menu.
/// Each player row has <' and '>' arrows that cycle through every available input option
/// (Keyboard WASD, Keyboard Arrows, and each connected gamepad).
/// Changes apply instantly through LocalMultiplayerSpawner.
/// </summary>
public class DeviceSelectMenu : MonoBehaviour
{
    [System.Serializable]
    public class PlayerRow
    {
        public TMP_Text deviceNameText;
        public Image deviceIcon;
    }

    [SerializeField] private LocalMultiplayerSpawner spawner;
    [SerializeField] private PlayerRow[] playerRows = new PlayerRow[2];

    [Header("Device Icons")]
    [SerializeField] private Sprite keyboardSprite;
    [SerializeField] private Sprite arrowKeysSprite;
    [SerializeField] private Sprite playstationSprite;
    [SerializeField] private Sprite xboxSprite;
    [SerializeField] private Sprite genericGamepadSprite;

    [Header("Optional: text shown when a player hasn't joined")]
    [SerializeField] private string notJoinedText = "Not Joined";

    private List<LocalMultiplayerSpawner.InputOption> options = new List<LocalMultiplayerSpawner.InputOption>();

    void OnEnable()
    {
        if (spawner == null)
            spawner = Object.FindFirstObjectByType<LocalMultiplayerSpawner>();

        InputSystem.onDeviceChange += OnDeviceChange;
        RefreshEverything();
    }

    void OnDisable()
    {
        InputSystem.onDeviceChange -= OnDeviceChange;
    }

    // Keeps the menu accurate if a controller is plugged in / unplugged while it's open
    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (change == InputDeviceChange.Added || change == InputDeviceChange.Removed)
            RefreshEverything();
    }

    private void RefreshEverything()
    {
        if (spawner == null) return;
        options = spawner.GetAvailableOptions();
        RefreshRow(0);
        RefreshRow(1);
    }
    
    public void CyclePlayer1Left()  => Cycle(0, -1);
    public void CyclePlayer1Right() => Cycle(0, +1);
    public void CyclePlayer2Left()  => Cycle(1, -1);
    public void CyclePlayer2Right() => Cycle(1, +1);

    public void SwapPlayers()
    {
        if (spawner == null) return;
        spawner.SwapPlayerDevices();
        RefreshRow(0);
        RefreshRow(1);
    }

    private void Cycle(int playerIndex, int direction)
    {
        if (spawner == null || options.Count == 0) return;

        var slot = spawner.GetSlot(playerIndex);
        if (slot == null) return;

        int current = options.FindIndex(o => o.Matches(slot.controlScheme, slot.device));

        // Step through the list (wrapping) until an assignable option is found.
        // AssignOptionToPlayer rejects options held by the other player, so those get skipped.
        for (int step = 1; step <= options.Count; step++)
        {
            int next = ((current + direction * step) % options.Count + options.Count) % options.Count;
            if (spawner.AssignOptionToPlayer(playerIndex, options[next]))
            {
                RefreshRow(playerIndex);
                return;
            }
        }
    }

    private void RefreshRow(int playerIndex)
    {
        if (playerIndex >= playerRows.Length || playerRows[playerIndex] == null) return;

        PlayerRow row = playerRows[playerIndex];
        var slot = spawner != null ? spawner.GetSlot(playerIndex) : null;

        if (slot == null)
        {
            if (row.deviceNameText != null) row.deviceNameText.text = notJoinedText;
            if (row.deviceIcon != null) row.deviceIcon.enabled = false;
            return;
        }

        if (row.deviceNameText != null)
            row.deviceNameText.text = slot.DeviceLabel;

        if (row.deviceIcon != null)
        {
            row.deviceIcon.enabled = true;
            row.deviceIcon.sprite = GetIconFor(slot.device, slot.controlScheme);
        }
    }

    private Sprite GetIconFor(InputDevice device, string controlScheme)
    {
        // Both keyboard halves are the same physical device, so the scheme decides the icon
        if (device is Keyboard)
            return (spawner != null && controlScheme == spawner.ArrowsScheme && arrowKeysSprite != null)
                ? arrowKeysSprite
                : keyboardSprite;

        if (device is Gamepad pad)
        {
            return GamepadBrandUtility.GetBrand(pad) switch
            {
                GamepadBrand.PlayStation => playstationSprite,
                GamepadBrand.Xbox => xboxSprite,
                _ => genericGamepadSprite
            };
        }

        return genericGamepadSprite;
    }
}