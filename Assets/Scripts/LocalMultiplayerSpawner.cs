using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Multiplayer Setup
/// Supports: shared keyboard (WASD + Arrows), keyboard + gamepad, and two gamepads, at once.
/// Each joined player gets a stable slot (0 or 1) plus a record of the exact device they joined with
/// so a UI menu can display and reassign who is Player 1 / Player 2.
/// The chosen layout auto-saves (PlayerPrefs) and is re-applied when players rejoin.
/// </summary>
[RequireComponent(typeof(PlayerInputManager))]
public class LocalMultiplayerSpawner : MonoBehaviour
{
    [Header("Unique Player Prefabs")]
    [SerializeField] private GameObject player1Prefab;
    [SerializeField] private GameObject player2Prefab;

    [Header("Spawn Locations")]
    [SerializeField] private Transform player1SpawnPoint;
    [SerializeField] private Transform player2SpawnPoint;

    [Header("Control Scheme Names (must EXACTLY match the Input Actions asset)")]
    [SerializeField] private string wasdScheme = "KeyboardWASD";
    [SerializeField] private string arrowsScheme = "KeyboardArrows";
    [SerializeField] private string gamepadScheme = "Gamepad";

    [Header("Layout Saving")]
    [Tooltip("Re-apply the last saved layout automatically once both players have joined.")]
    [SerializeField] private bool applySavedLayoutOnJoin = true;

    public string WasdScheme => wasdScheme;
    public string ArrowsScheme => arrowsScheme;
    public string GamepadScheme => gamepadScheme;

    /// <summary>One selectable input configuration (a scheme + the device it runs on).</summary>
    public struct InputOption
    {
        public string scheme;
        public InputDevice device;
        public string label;

        public bool Matches(string otherScheme, InputDevice otherDevice) =>
            scheme == otherScheme && device == otherDevice;
    }

    /// <summary>Everything a UI needs to know about one joined player.</summary>
    public class PlayerSlot
    {
        // 0 = Player 1, 1 = Player 2
        public int playerIndex;
        public PlayerInput playerInput;
        public InputDevice device;
        public string controlScheme;

        public string DeviceLabel
        {
            get
            {
                if (device is Keyboard)
                    return controlScheme == "KeyboardArrows" ? "Keyboard (Arrows)" : "Keyboard (WASD)";
                if (device is Gamepad pad)
                {
                    int padNumber = Gamepad.all.IndexOf(p => p == pad) + 1;
                    return $"{pad.displayName} #{padNumber}";
                }
                return device != null ? device.displayName : "None";
            }
        }
    }

    private readonly PlayerSlot[] slots = new PlayerSlot[2];

    // Gamepads claimed by device ID (Gamepad.all ordering is unreliable).
    private readonly HashSet<int> claimedGamepadIds = new HashSet<int>();

    // Keyboard halves claimed by scheme (the keyboard device itself is shared).
    private readonly HashSet<string> claimedSchemes = new HashSet<string>();

    private PlayerInputManager inputManager;
    private CameraLimits cameraLimitsScript;
    private PauseMenu pauseMenuScript;
    private int joinedCount = 0;

    public int JoinedCount => joinedCount;

    public PlayerSlot GetSlot(int playerIndex) =>
        (playerIndex >= 0 && playerIndex < slots.Length) ? slots[playerIndex] : null;

    void Awake()
    {
        inputManager = GetComponent<PlayerInputManager>();
        cameraLimitsScript = Object.FindFirstObjectByType<CameraLimits>();
        pauseMenuScript = Object.FindFirstObjectByType<PauseMenu>();

        // Manual joining stops the PlayerInputManager auto-pairing devices behind our back,
        // but joining must stay enabled or JoinPlayer() fails.
        inputManager.joinBehavior = PlayerJoinBehavior.JoinPlayersManually;
        inputManager.EnableJoining();
    }

    void Update()
    {
        if (joinedCount >= 2) return;

        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.wKey.wasPressedThisFrame && !claimedSchemes.Contains(wasdScheme))
            {
                SpawnPlayer(keyboard, wasdScheme);
                return;
            }
            if (keyboard.upArrowKey.wasPressedThisFrame && !claimedSchemes.Contains(arrowsScheme))
            {
                SpawnPlayer(keyboard, arrowsScheme);
                return;
            }
        }

        foreach (var pad in Gamepad.all)
        {
            if (claimedGamepadIds.Contains(pad.deviceId)) continue;

            if (pad.buttonSouth.wasPressedThisFrame)
            {
                SpawnPlayer(pad, gamepadScheme);
                return;
            }
        }
    }

    private void SpawnPlayer(InputDevice device, string controlScheme)
    {
        int index = joinedCount;
        GameObject selectedPrefab = (index == 0) ? player1Prefab : player2Prefab;
        Transform spawnPoint = (index == 0) ? player1SpawnPoint : player2SpawnPoint;

        if (selectedPrefab == null) return;

        // JoinPlayer() always instantiates inputManager.playerPrefab, so set it right before joining.
        inputManager.playerPrefab = selectedPrefab;

        PlayerInput newPlayer = inputManager.JoinPlayer(index, -1, controlScheme, device);
        if (newPlayer == null)
        {
            Debug.LogError($"[Spawner] JoinPlayer failed for scheme '{controlScheme}'. " +
                           "Check that the scheme name exactly matches the Input Actions asset.");
            return;
        }

        newPlayer.neverAutoSwitchControlSchemes = true;

        if (device is Gamepad) claimedGamepadIds.Add(device.deviceId);
        else claimedSchemes.Add(controlScheme);

        slots[index] = new PlayerSlot
        {
            playerIndex = index,
            playerInput = newPlayer,
            device = device,
            controlScheme = controlScheme
        };

        if (spawnPoint != null)
        {
            newPlayer.transform.position = spawnPoint.position;
            newPlayer.transform.rotation = spawnPoint.rotation;
        }

        if (cameraLimitsScript != null)
            cameraLimitsScript.RegisterPlayer(newPlayer.gameObject);

        if (pauseMenuScript != null)
            pauseMenuScript.RegisterPlayerInput(newPlayer);

        UIPlayerFollow[] uiFollowers = Object.FindObjectsByType<UIPlayerFollow>(FindObjectsSortMode.None);
        foreach (UIPlayerFollow ui in uiFollowers)
            ui.SetupTarget(newPlayer.transform, index);

        joinedCount++;

        if (applySavedLayoutOnJoin && joinedCount == 2)
            ApplySavedLayout();
    }
    
    //  Public API for the controller-assignment settings menu

    /// <summary>
    /// Every input option a player could switch to right now:
    /// both keyboard halves plus every connected gamepad.
    /// </summary>
    public List<InputOption> GetAvailableOptions()
    {
        var options = new List<InputOption>();

        if (Keyboard.current != null)
        {
            options.Add(new InputOption { scheme = wasdScheme, device = Keyboard.current, label = "Keyboard (WASD)" });
            options.Add(new InputOption { scheme = arrowsScheme, device = Keyboard.current, label = "Keyboard (Arrows)" });
        }

        for (int i = 0; i < Gamepad.all.Count; i++)
        {
            Gamepad pad = Gamepad.all[i];
            options.Add(new InputOption
            {
                scheme = gamepadScheme,
                device = pad,
                label = $"{pad.displayName} #{i + 1}"
            });
        }

        return options;
    }

    /// <summary>
    /// Move a player onto a different scheme/device. Returns false if the option is
    /// invalid, already theirs, or currently held by the other player.
    /// </summary>
    public bool AssignOptionToPlayer(int playerIndex, InputOption option)
    {
        if (TryAssign(playerIndex, option))
        {
            SaveLayout();
            return true;
        }
        return false;
    }

    private bool TryAssign(int playerIndex, InputOption option)
    {
        PlayerSlot slot = GetSlot(playerIndex);
        if (slot == null || option.device == null || !option.device.added) return false;

        if (option.Matches(slot.controlScheme, slot.device)) return false;

        PlayerSlot other = GetSlot(1 - playerIndex);
        if (other != null && option.Matches(other.controlScheme, other.device)) return false;

        // Release old claims, then claim the new option.
        if (slot.device is Gamepad oldPad) claimedGamepadIds.Remove(oldPad.deviceId);
        else claimedSchemes.Remove(slot.controlScheme);

        slot.playerInput.SwitchCurrentControlScheme(option.scheme, option.device);

        slot.device = option.device;
        slot.controlScheme = option.scheme;
        if (option.device is Gamepad newPad) claimedGamepadIds.Add(newPad.deviceId);
        else claimedSchemes.Add(option.scheme);
        return true;
    }

    /// <summary>Swap which device/scheme controls Player 1 vs Player 2.</summary>
    public void SwapPlayerDevices()
    {
        if (slots[0] == null || slots[1] == null) return;

        PlayerSlot a = slots[0];
        PlayerSlot b = slots[1];

        a.playerInput.SwitchCurrentControlScheme(b.controlScheme, b.device);
        b.playerInput.SwitchCurrentControlScheme(a.controlScheme, a.device);

        (a.device, b.device) = (b.device, a.device);
        (a.controlScheme, b.controlScheme) = (b.controlScheme, a.controlScheme);

        SaveLayout();
    }
    
    //  Layout saving (PlayerPrefs)

    private const string PrefPrefix = "InputLayout_P";

    public void SaveLayout()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;
            PlayerPrefs.SetString($"{PrefPrefix}{i}_Scheme", slots[i].controlScheme);
            PlayerPrefs.SetString($"{PrefPrefix}{i}_Device", GetDeviceKey(slots[i].device));
        }
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Re-apply the saved layout to whoever has joined. Devices that are no longer
    /// connected are skipped, leaving that player on whatever they joined with.
    /// </summary>
    public void ApplySavedLayout()
    {
        // Two passes: if the saved layout is the reverse of the current one, the first
        // pass can be blocked by the other player still holding the wanted device.
        for (int pass = 0; pass < 2; pass++)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null) continue;
                if (!PlayerPrefs.HasKey($"{PrefPrefix}{i}_Scheme")) continue;

                string scheme = PlayerPrefs.GetString($"{PrefPrefix}{i}_Scheme");
                string deviceKey = PlayerPrefs.GetString($"{PrefPrefix}{i}_Device");

                InputDevice device = ResolveDeviceKey(scheme, deviceKey);
                if (device == null) continue;

                TryAssign(i, new InputOption { scheme = scheme, device = device });
            }
        }
    }

    public void ClearSavedLayout()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            PlayerPrefs.DeleteKey($"{PrefPrefix}{i}_Scheme");
            PlayerPrefs.DeleteKey($"{PrefPrefix}{i}_Device");
        }
    }

    // Device IDs change between sessions, so gamepads are identified by their
    // display name plus their ordinal among same-named pads (e.g. "DualSense|0").
    private string GetDeviceKey(InputDevice device)
    {
        if (device is Keyboard) return "Keyboard";

        if (device is Gamepad pad)
        {
            int ordinal = 0;
            foreach (var p in Gamepad.all)
            {
                if (p == pad) break;
                if (p.displayName == pad.displayName) ordinal++;
            }
            return $"{pad.displayName}|{ordinal}";
        }

        return device != null ? device.displayName : "";
    }

    private InputDevice ResolveDeviceKey(string scheme, string deviceKey)
    {
        if (scheme != gamepadScheme)
            return Keyboard.current;

        foreach (var pad in Gamepad.all)
        {
            if (GetDeviceKey(pad) == deviceKey)
                return pad;
        }
        return null;
    }
}

/// <summary>Small helper because ReadOnlyArray has no IndexOf with predicates.</summary>
internal static class GamepadListExtensions
{
    public static int IndexOf(this UnityEngine.InputSystem.Utilities.ReadOnlyArray<Gamepad> list,
                              System.Predicate<Gamepad> match)
    {
        for (int i = 0; i < list.Count; i++)
            if (match(list[i])) return i;
        return -1;
    }
}