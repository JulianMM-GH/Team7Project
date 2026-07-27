using UnityEngine;
using UnityEngine.InputSystem;

public class LocalMultiplayerSpawner : MonoBehaviour
{
    [Header("Unique Player Prefabs")]
    [SerializeField] private GameObject player1Prefab;
    [SerializeField] private GameObject player2Prefab;

    [Header("Spawn Locations")]
    [SerializeField] private Transform player1SpawnPoint;
    [SerializeField] private Transform player2SpawnPoint;

    [Header("Tutorial Prompt Prefabs")]
    [SerializeField] private GameObject player1KeyboardTutorial;
    [SerializeField] private GameObject player1ControllerTutorial;

    [SerializeField] private GameObject player2KeyboardTutorial;
    [SerializeField] private GameObject player2ControllerTutorial;


    private PlayerInputManager inputManager;
    private CameraLimits cameraLimitsScript;
    private PauseMenu pauseMenuScript;
    private int playerIndex = 0;

    // Track what device Player 1 grabbed so Player 2 doesn't accidentally steal it
    private InputDevice player1Device;

    void Awake()
    {
        inputManager = GetComponent<PlayerInputManager>();
        cameraLimitsScript = Object.FindFirstObjectByType<CameraLimits>();
        pauseMenuScript = Object.FindFirstObjectByType<PauseMenu>();

        //Hide all tutorial prompts
        player1KeyboardTutorial.SetActive(false);
        player1ControllerTutorial.SetActive(false);

        player2KeyboardTutorial.SetActive(false);
        player2ControllerTutorial.SetActive(false);
    }

    void Update()
    {
        if (playerIndex >= 2) return;

        // Player 1
        if (playerIndex == 0)
        {
            // Join via W key -> gets KeyboardWASD profile
            if (Keyboard.current != null && Keyboard.current.wKey.wasPressedThisFrame)
            {
                //player1KeyboardTutorial.SetActive(true);
                SpawnPlayer(Keyboard.current, "KeyboardWASD");
            }
            // Join via Gamepad 1 button -> gets Gamepad profile
            else if (Gamepad.all.Count > 0 && Gamepad.all[0].buttonSouth.wasPressedThisFrame)
            {
                //player1ControllerTutorial.SetActive(true);
                SpawnPlayer(Gamepad.all[0], "Gamepad");
            }
        }
        // Player 2
        else if (playerIndex == 1)
        {
            // Scenario A: Shared Keyboard (P1 used WASD, P2 presses Up Arrow)
            if (Keyboard.current != null && Keyboard.current.upArrowKey.wasPressedThisFrame)
            {
                // Only allow if Player 1 isn't using a controller exclusively
                //player2KeyboardTutorial.SetActive(true);
                SpawnPlayer(Keyboard.current, "KeyboardArrows");
            }
            // Scenario B: Gamepad assignment
            else if (Gamepad.all.Count > 0)
            {
                // If there are two gamepads connected, check if the second one presses a button
                if (Gamepad.all.Count > 1 && Gamepad.all[1].buttonSouth.wasPressedThisFrame)
                {
                    //player2ControllerTutorial.SetActive(true);
                    SpawnPlayer(Gamepad.all[1], "Gamepad");
                }
                // If there is only one gamepad, but Player 1 used the keyboard, Player 2 can take Gamepad 0
                else if (Gamepad.all[0].buttonSouth.wasPressedThisFrame && player1Device is Keyboard)
                {
                    //player2ControllerTutorial.SetActive(true);
                    SpawnPlayer(Gamepad.all[0], "Gamepad");
                }
            }
        }
    }

    private void SpawnPlayer(InputDevice device, string controlScheme)
    {
        GameObject selectedPrefab = (playerIndex == 0) ? player1Prefab : player2Prefab;
        Transform spawnPoint = (playerIndex == 0) ? player1SpawnPoint : player2SpawnPoint;

        if (selectedPrefab == null) return;

        // Instruct Unity to pair this exact control subset scheme to this instance
        PlayerInput newPlayer = inputManager.JoinPlayer(playerIndex, pairWithDevice: device, controlScheme: controlScheme);

        if (newPlayer != null)
        {
            if (playerIndex == 0) player1Device = device;

            if (spawnPoint != null)
            {
                newPlayer.transform.position = spawnPoint.position;
                newPlayer.transform.rotation = spawnPoint.rotation;
            }

            if (cameraLimitsScript != null)
            {
                cameraLimitsScript.RegisterPlayer(newPlayer.gameObject);
            }

            if (pauseMenuScript != null)
            {
                pauseMenuScript.RegisterPlayerInput(newPlayer);
            }

            // Find all UI elements following a player and link them
            UIPlayerFollow[] uiFollowers = Object.FindObjectsByType<UIPlayerFollow>(FindObjectsSortMode.None);
            foreach (UIPlayerFollow ui in uiFollowers)
            {
                ui.SetupTarget(newPlayer.transform, playerIndex);
            }

            playerIndex++;

            if (playerIndex == 1 && player2Prefab != null)
            {
                inputManager.playerPrefab = player2Prefab;
            }
        }
    }
}