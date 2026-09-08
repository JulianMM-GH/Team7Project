using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Main menu popup for assigning each player's controller before the game starts.
/// Detects devices the same dynamic way LocalMultiplayerSpawner does in-level (press
/// W / Up Arrow / a gamepad's south button to claim a slot), so newly-woken wireless
/// pads that haven't sent an input yet still show up once pressed. Confirm hands the
/// exact joined devices to PendingPlayerJoins and fades into the scene - no need to
/// press anything again once it loads.
/// </summary>
public class ControlsSetupPopup : MonoBehaviour
{
    // Control scheme names, must EXACTLY match the ones in the Input Actions asset
    // (same values LocalMultiplayerSpawner uses).
    private const string WasdScheme = "KeyboardWASD";
    private const string ArrowsScheme = "KeyboardArrows";
    private const string GamepadScheme = "Gamepad";

    private class Slot
    {
        public bool joined;
        public string scheme;
        public InputDevice device;
    }

    [Header("Panels")]
    [SerializeField] private GameObject popupRoot;
    [SerializeField] private GameObject firstSelected;
    [Tooltip("Hidden when opened from the Settings screen (OpenSettingsOnly) - there's no scene to confirm into there.")]
    [SerializeField] private GameObject confirmButton;

    [Header("Player Rows (index 0 = Player 1, 1 = Player 2)")]
    [SerializeField] private TMP_Text[] playerLabels = new TMP_Text[2];

    [Header("Fade Transition")]
    [SerializeField] private CanvasGroup fadeOverlay;
    [SerializeField] private float fadeTime = 1f;

    private readonly Slot[] slots = { new Slot(), new Slot() };

    // Gamepads claimed by device ID (Gamepad.all ordering is unreliable).
    private readonly HashSet<int> claimedGamepadIds = new HashSet<int>();

    // Keyboard halves claimed by scheme (the keyboard device itself is shared).
    private readonly HashSet<string> claimedSchemes = new HashSet<string>();

    private int sceneToLoad;
    private bool isOpen;

    void Awake()
    {
        popupRoot.SetActive(false);

        if (fadeOverlay != null)
        {
            fadeOverlay.alpha = 0f;
            fadeOverlay.blocksRaycasts = false;
        }
    }

    public void Open(int targetSceneIndex)
    {
        sceneToLoad = targetSceneIndex;

        slots[0] = new Slot();
        slots[1] = new Slot();
        claimedGamepadIds.Clear();
        claimedSchemes.Clear();

        TryRestoreSavedLayout();

        if (confirmButton != null)
            confirmButton.SetActive(true);

        isOpen = true;
        popupRoot.SetActive(true);
        RefreshRow(0);
        RefreshRow(1);

        // Re-seed the EventSystem so controllers have something selected to navigate from.
        if (EventSystem.current != null && firstSelected != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(firstSelected);
        }
    }

    // For opening this same popup from the Main Menu's Settings screen instead of the
    // pre-Play flow - Join/Cycle already save on every change, so there's no scene to load;
    // hide Confirm since there's nothing to confirm into, wire a "Back" button to
    // CloseSettingsOnly() instead.
    public void OpenSettingsOnly()
    {
        Open(-1);

        if (confirmButton != null)
            confirmButton.SetActive(false);
    }

    public void CloseSettingsOnly()
    {
        isOpen = false;
        popupRoot.SetActive(false);
    }

    // Pre-fills each slot from whatever was last saved (via Join/Cycle here, or in-level via
    // DeviceSelectMenu), so reopening this popup shows the current assignment instead of
    // resetting to "Not Joined" every time.
    private void TryRestoreSavedLayout()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (!LocalMultiplayerSpawner.TryLoadPreJoinLayout(i, GamepadScheme, out string scheme, out InputDevice device))
                continue;

            bool alreadyClaimed = device is Gamepad pad ? claimedGamepadIds.Contains(pad.deviceId) : claimedSchemes.Contains(scheme);
            if (alreadyClaimed) continue;

            slots[i].joined = true;
            slots[i].scheme = scheme;
            slots[i].device = device;
            Claim(slots[i]);
        }
    }

    void Update()
    {
        if (!isOpen || (slots[0].joined && slots[1].joined)) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.wKey.wasPressedThisFrame && !claimedSchemes.Contains(WasdScheme))
            {
                Join(WasdScheme, keyboard);
                return;
            }
            if (keyboard.upArrowKey.wasPressedThisFrame && !claimedSchemes.Contains(ArrowsScheme))
            {
                Join(ArrowsScheme, keyboard);
                return;
            }
        }

        foreach (var pad in Gamepad.all)
        {
            if (claimedGamepadIds.Contains(pad.deviceId)) continue;

            if (pad.buttonSouth.wasPressedThisFrame)
            {
                Join(GamepadScheme, pad);
                return;
            }
        }
    }

    private void Join(string scheme, InputDevice device)
    {
        int index = slots[0].joined ? 1 : 0;
        if (slots[index].joined) return;

        slots[index].joined = true;
        slots[index].scheme = scheme;
        slots[index].device = device;
        Claim(slots[index]);

        LocalMultiplayerSpawner.SavePreJoinLayout(index, scheme, device);
        RefreshRow(index);
    }

    public void CyclePlayer1Left()  => Cycle(0, -1);
    public void CyclePlayer1Right() => Cycle(0, +1);
    public void CyclePlayer2Left()  => Cycle(1, -1);
    public void CyclePlayer2Right() => Cycle(1, +1);

    // Lets an already-joined player switch to a different currently connected device.
    private void Cycle(int playerIndex, int direction)
    {
        if (!slots[playerIndex].joined) return;

        List<(string scheme, InputDevice device)> options = BuildOptionsList();
        if (options.Count == 0) return;

        int otherIndex = 1 - playerIndex;
        int current = options.FindIndex(o => o.scheme == slots[playerIndex].scheme && o.device == slots[playerIndex].device);
        if (current < 0) current = 0;

        // Step through the list (wrapping), skipping whatever the other player already has.
        for (int step = 1; step <= options.Count; step++)
        {
            int next = ((current + direction * step) % options.Count + options.Count) % options.Count;
            var candidate = options[next];

            bool heldByOther = slots[otherIndex].joined
                && slots[otherIndex].scheme == candidate.scheme
                && slots[otherIndex].device == candidate.device;
            if (heldByOther) continue;

            Release(slots[playerIndex]);
            slots[playerIndex].scheme = candidate.scheme;
            slots[playerIndex].device = candidate.device;
            Claim(slots[playerIndex]);

            LocalMultiplayerSpawner.SavePreJoinLayout(playerIndex, candidate.scheme, candidate.device);
            RefreshRow(playerIndex);
            return;
        }
    }

    private List<(string, InputDevice)> BuildOptionsList()
    {
        var list = new List<(string, InputDevice)>();

        if (Keyboard.current != null)
        {
            list.Add((WasdScheme, Keyboard.current));
            list.Add((ArrowsScheme, Keyboard.current));
        }

        foreach (var pad in Gamepad.all)
            list.Add((GamepadScheme, pad));

        return list;
    }

    private void Claim(Slot slot)
    {
        if (slot.device is Gamepad pad) claimedGamepadIds.Add(pad.deviceId);
        else claimedSchemes.Add(slot.scheme);
    }

    private void Release(Slot slot)
    {
        if (slot.device is Gamepad pad) claimedGamepadIds.Remove(pad.deviceId);
        else claimedSchemes.Remove(slot.scheme);
    }

    private void RefreshRow(int playerIndex)
    {
        if (playerIndex >= playerLabels.Length || playerLabels[playerIndex] == null) return;

        Slot slot = slots[playerIndex];
        playerLabels[playerIndex].text = slot.joined ? DeviceLabel(slot) : "Not Joined - Press a Button";
    }

    private string DeviceLabel(Slot slot)
    {
        if (slot.device is Keyboard)
            return slot.scheme == ArrowsScheme ? "Keyboard (Arrows)" : "Keyboard (WASD)";

        if (slot.device is Gamepad pad)
        {
            int number = 0;
            for (int i = 0; i < Gamepad.all.Count; i++)
            {
                if (Gamepad.all[i] == pad) { number = i + 1; break; }
            }
            return $"{pad.displayName} #{number}";
        }

        return "Unknown Device";
    }

    public void Confirm()
    {
        if (!slots[0].joined) return;

        var entries = new List<PendingPlayerJoins.Entry>();
        for (int i = 0; i < slots.Length; i++)
        {
            if (!slots[i].joined) continue;
            entries.Add(new PendingPlayerJoins.Entry { scheme = slots[i].scheme, device = slots[i].device });
            LocalMultiplayerSpawner.SavePreJoinLayout(i, slots[i].scheme, slots[i].device);
        }
        PendingPlayerJoins.Set(entries);

        isOpen = false;
        StartCoroutine(FadeAndLoad());
    }

    private IEnumerator FadeAndLoad()
    {
        if (fadeOverlay != null)
        {
            fadeOverlay.blocksRaycasts = true;
            float timer = 0f;

            while (timer < fadeTime)
            {
                timer += Time.deltaTime;
                fadeOverlay.alpha = Mathf.Clamp01(timer / fadeTime);
                yield return null;
            }

            fadeOverlay.alpha = 1f;
        }

        SceneManager.LoadScene(sceneToLoad);
    }
}
