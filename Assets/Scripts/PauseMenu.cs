using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject pauseMenuCanvas;
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private GameObject settingsPanel;

    // Track active players dynamically as they join
    private List<PlayerInput> activePlayers = new List<PlayerInput>();

    // Store original action maps so we can safely switch back to them on resume
    private Dictionary<PlayerInput, string> playerDefaultMaps = new Dictionary<PlayerInput, string>();

    public static bool MenuWasPressed;
    public static bool isPaused = false;

    void Start()
    {
        pauseMenuCanvas.SetActive(false);
        isPaused = false;
        Time.timeScale = 1f;
    }

    // Public method the Spawner script will call whenever a player spawns
    public void RegisterPlayerInput(PlayerInput pInput)
    {
        if (pInput != null && !activePlayers.Contains(pInput))
        {
            activePlayers.Add(pInput);

            // Save what map they were using when they spawned (e.g., "Player1" or "Player2")
            playerDefaultMaps[pInput] = pInput.currentActionMap.name;

            // SAFETY: If the game is already paused when a new player joins, force them into UI mode instantly
            if (isPaused)
            {
                pInput.SwitchCurrentActionMap("UI");
            }
        }
    }

    void Update()
    {
        MenuWasPressed = CheckForMenuInput();

        if (MenuWasPressed)
        {
            if (isPaused)
                ResumeGame();
            else
                PauseGame();
        }
    }

    // Global listener: Checks if ANY valid device triggers the "MenuOpen" equivalent action
    private bool CheckForMenuInput()
    {
        // 1. Keyboard Escape check
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            return true;
        }

        // 2. Controller Start/Menu button check (loops through all active gamepads)
        foreach (Gamepad pad in Gamepad.all)
        {
            if (pad.startButton.wasPressedThisFrame)
            {
                return true;
            }
        }

        return false;
    }

    public void PauseGame()
    {
        pauseMenuCanvas.SetActive(true);
        menuPanel.SetActive(true);
        settingsPanel.SetActive(false);

        Time.timeScale = 0f;
        isPaused = true;

        // Freeze controls for every player currently in the scene
        foreach (PlayerInput player in activePlayers)
        {
            if (player != null)
            {
                player.SwitchCurrentActionMap("UI");
            }
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ResumeGame()
    {
        pauseMenuCanvas.SetActive(false);
        Time.timeScale = 1f;
        isPaused = false;

        // Restore the original independent control schemes for each active player
        foreach (PlayerInput player in activePlayers)
        {
            if (player != null && playerDefaultMaps.TryGetValue(player, out string savedMap))
            {
                player.SwitchCurrentActionMap(savedMap);
            }
        }
    }

    public void OpenSettings()
    {
        menuPanel.SetActive(false);
        settingsPanel.SetActive(true);
    }

    public void CloseSettings()
    {
        settingsPanel.SetActive(false);
        menuPanel.SetActive(true);
    }

    public void MoveToScene(int sceneID)
    {
        pauseMenuCanvas.SetActive(false);
        isPaused = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneID);
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}