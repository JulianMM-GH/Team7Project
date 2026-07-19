using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.Text;
using UnityEngine.UI;

public class PauseMenu : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject pauseMenuCanvas;
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private GameObject settingsPanel;

    // Player Input
    // To ensure that the players can't receive input when the game is paused, their action maps are changed to "UI", preventing inputs such as movement or jumping
    [SerializeField] private PlayerInput Player1Input;
    [SerializeField] private PlayerInput Player2Input;
    private InputAction menuAction;
    public static bool MenuWasPressed;

    // Pause
    public static bool isPaused = false;

    private void Awake()
    {
        menuAction = Player1Input.actions["MenuOpen"];
    }

    void Start()
    {
        // Menu is hidden
        pauseMenuCanvas.SetActive(false);
        isPaused = false;
        Time.timeScale = 1f;
    }

    void Update()
    {
        MenuWasPressed = menuAction.WasPressedThisFrame();

        // Toggle pause menu
        if (MenuWasPressed)
        {
            if (isPaused)
                ResumeGame();
            else
                PauseGame();
        }
    }

    public void PauseGame()
    {
        pauseMenuCanvas.SetActive(true);
        menuPanel.SetActive(true);
        settingsPanel.SetActive(false);

        Time.timeScale = 0f; // Pause
        //AudioListener.pause = true;
        isPaused = true;

        Player1Input.SwitchCurrentActionMap("UI");
        Player2Input.SwitchCurrentActionMap("UI");

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ResumeGame()
    {
        pauseMenuCanvas.SetActive(false);
        Time.timeScale = 1f; // Unpause
        //AudioListener.pause = false;
        isPaused = false;

        Player1Input.SwitchCurrentActionMap("Player1");
        Player2Input.SwitchCurrentActionMap("Player2");
    }

    // Pause Menu Navigation
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

    // Change Scene
    public void MoveToScene(int sceneID)
    {
        pauseMenuCanvas.SetActive(false);
        isPaused = false;
        Time.timeScale = 1f;

        SceneManager.LoadScene(sceneID);
    }

    // Quit
    public void QuitGame()
    {
        Application.Quit();
    }
}
