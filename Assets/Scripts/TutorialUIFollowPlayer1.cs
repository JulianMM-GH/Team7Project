using UnityEngine;

public class TutorialUIPlayerFollow : MonoBehaviour
{
    [SerializeField] public GameObject canvas1;
    [SerializeField] public GameObject canvas2;

    void Awake()
    {
        canvas1.SetActive(false);
        canvas2.SetActive(false);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            canvas1.SetActive(true);
            canvas2.SetActive(true);
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            canvas1.SetActive(false);
            canvas2.SetActive(false);
        }
    }
}