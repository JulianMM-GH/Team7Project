using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SFXManager : MonoBehaviour
{
    public static SFXManager instance;

    [SerializeField] private AudioSource SFXObject;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
    }

    public void PlaySFXClip(AudioClip audioClip, Transform spawnTransform, float volume)
    {
        // Create the gameObject
        AudioSource audioSource = Instantiate(SFXObject, spawnTransform.position, Quaternion.identity);

        // Assign audioClip
        audioSource.clip = audioClip;

        // Assign volume
        audioSource.volume = volume;

        // Play audio
        audioSource.Play();

        // Get the lenght of SFXClip
        float clipLength = audioSource.clip.length;

        // Destroy the clip after audio has played
        Destroy(audioSource.gameObject, clipLength);
    }

    public void PlayRandomSFXClip(AudioClip[] audioClip, Transform spawnTransform, float volume)
    {
        // ASsign a random index
        int rand = Random.Range(0, audioClip.Length);

        // Create the gameObject
        AudioSource audioSource = Instantiate(SFXObject, spawnTransform.position, Quaternion.identity);

        // Assign audioClip
        audioSource.clip = audioClip[rand];

        // Assign volume
        audioSource.volume = volume;

        // Play audio
        audioSource.Play();

        // Get the lenght of SFXClip
        float clipLength = audioSource.clip.length;

        // Destroy the clip after audio has played
        Destroy(audioSource.gameObject, clipLength);
    }
}
