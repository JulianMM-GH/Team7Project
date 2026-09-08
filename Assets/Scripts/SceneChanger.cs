using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneController : MonoBehaviour
{
    private ControlsSetupPopup controlsPopup;

    public void MoveToScene(int sceneID)
    {
        SceneManager.LoadScene(sceneID);
    }

    // Used by the main menu's Play button so players can assign controls before the scene loads.
    public void OpenControlsThenMoveToScene(int sceneID)
    {
        if (controlsPopup == null)
            controlsPopup = Object.FindFirstObjectByType<ControlsSetupPopup>();

        if (controlsPopup != null)
            controlsPopup.Open(sceneID);
        else
            MoveToScene(sceneID);
    }
}

