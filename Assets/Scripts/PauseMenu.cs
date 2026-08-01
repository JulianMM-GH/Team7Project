using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    [Header("UI Panels")]
    [SerializeField] private GameObject pauseMenuCanvas;
    [SerializeField] private GameObject menuPanel;      // Main pause menu; Resume, Settings, Quit
    [SerializeField] private GameObject settingsPanel;  // Hub: Controls, Audio, Back
    [SerializeField] private GameObject controlsPanel;  // Device select menu
    [SerializeField] private GameObject audioPanel;     // Audio settings

    [Header("First Selected Buttons (for controller navigation)")]
    [SerializeField] private GameObject menuFirstSelected;
    [SerializeField] private GameObject settingsFirstSelected;
    [SerializeField] private GameObject controlsFirstSelected;
    [SerializeField] private GameObject audioFirstSelected;

    private List<PlayerInput> activePlayers = new List<PlayerInput>();

    // Original action maps so we can safely switch back on resume
    private Dictionary<PlayerInput, string> playerDefaultMaps = new Dictionary<PlayerInput, string>();

    public static bool MenuWasPressed;
    public static bool isPaused = false;

    void Start()
    {
        pauseMenuCanvas.SetActive(false);
        isPaused = false;
        Time.timeScale = 1f;
    }

    public void RegisterPlayerInput(PlayerInput pInput)
    {
        if (pInput != null && !activePlayers.Contains(pInput))
        {
            activePlayers.Add(pInput);
            playerDefaultMaps[pInput] = pInput.currentActionMap.name;

            // If the game is already paused when a new player joins, force them into UI mode
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

    private bool CheckForMenuInput()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            return true;
        }

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
        ShowPanel(menuPanel, menuFirstSelected);

        Time.timeScale = 0f;
        isPaused = true;

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

        foreach (PlayerInput player in activePlayers)
        {
            if (player != null && playerDefaultMaps.TryGetValue(player, out string savedMap))
            {
                player.SwitchCurrentActionMap(savedMap);
            }
        }

        TutorialUIPlayerFollow.RefreshActiveTutorials();
    }

    // Panel navigation
    public void OpenSettings()   => ShowPanel(settingsPanel, settingsFirstSelected);  // "Settings" button on main pause panel
    public void CloseSettings()  => ShowPanel(menuPanel, menuFirstSelected);          // "Back" button on settings hub -> main pause panel

    public void OpenControls()   => ShowPanel(controlsPanel, controlsFirstSelected);  // "Controls" button on settings hub
    public void OpenAudio()      => ShowPanel(audioPanel, audioFirstSelected);        // "Audio" button on settings hub
    public void BackToSettings() => ShowPanel(settingsPanel, settingsFirstSelected);  // "Back" button on Controls AND Audio panels

    private void ShowPanel(GameObject panel, GameObject firstSelected)
    {
        menuPanel.SetActive(panel == menuPanel);
        settingsPanel.SetActive(panel == settingsPanel);
        if (controlsPanel != null) controlsPanel.SetActive(panel == controlsPanel);
        if (audioPanel != null) audioPanel.SetActive(panel == audioPanel);

        // Give controllers something selected to navigate from
        if (firstSelected != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(firstSelected);
        }
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