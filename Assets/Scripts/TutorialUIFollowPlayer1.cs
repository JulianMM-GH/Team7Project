using UnityEngine;

public class TutorialUIPlayerFollow : MonoBehaviour
{
    [SerializeField] public GameObject silasTutorialCanvas;
    [SerializeField] public GameObject phoenixTutorialCanvas;

    void Awake()
    {
        silasTutorialCanvas.SetActive(false);
        phoenixTutorialCanvas.SetActive(false);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            silasTutorialCanvas.SetActive(true);
            phoenixTutorialCanvas.SetActive(true);
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            silasTutorialCanvas.SetActive(false);
            phoenixTutorialCanvas.SetActive(false);
        }
    }
}