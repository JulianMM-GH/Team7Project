using SupanthaPaul;
using System.Collections;
using UnityEngine;

public class InteractableObject : MonoBehaviour
{
    [SerializeField] private GameObject timeCapsule;
    [SerializeField] private GameObject uiImage;

    
    void Start()
    {
        uiImage.SetActive(false);
    }
    

    private void OnTriggerEnter2D(Collider2D other)
    {
        //Debug.Log("Player has touched the object!");

        uiImage.SetActive(true);

        StartCoroutine(ActivateAndDeactivate(2.0f));
    }

    IEnumerator ActivateAndDeactivate(float delay)
    {
        uiImage.SetActive(true);

        yield return new WaitForSeconds(delay);

        uiImage.SetActive(false);
        timeCapsule.SetActive(false);
    }
}
