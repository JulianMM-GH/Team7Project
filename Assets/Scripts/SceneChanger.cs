using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneController : MonoBehaviour
{
    public void MoveToScene(int sceneID)
    {
        SceneManager.LoadScene(sceneID);
    }
}

