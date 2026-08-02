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

    public void PlaySFXClip(AudioClip audioClip, Transform spawnTransform, float volume, float fadeDuration = 0f)
    {
        // Create the gameObject
        AudioSource audioSource = Instantiate(SFXObject, spawnTransform.position, Quaternion.identity);

        // Assign audioClip
        audioSource.clip = audioClip;

        // Assign volume
        audioSource.volume = volume;

        // Play audio
        audioSource.Play();

        // Get the length of SFXClip
        float clipLength = audioSource.clip.length;

        // Added optional fadeDuration parameter
        if (fadeDuration > 0f)
        {
            // Start the fade out coroutine directly on the manager
            StartCoroutine(FadeOutAndDestroy(audioSource, clipLength, fadeDuration));
        }
        else
        {
            // Destroy the clip normally after audio has played
            Destroy(audioSource.gameObject, clipLength);
        }
    }

    // Added optional fadeDuration parameter
    public void PlayRandomSFXClip(AudioClip[] audioClip, Transform spawnTransform, float volume, float fadeDuration = 0f)
    {
        // Assign a random index
        int rand = Random.Range(0, audioClip.Length);

        // Create the gameObject
        AudioSource audioSource = Instantiate(SFXObject, spawnTransform.position, Quaternion.identity);

        // Assign audioClip
        audioSource.clip = audioClip[rand];

        // Assign volume
        audioSource.volume = volume;

        // Play audio
        audioSource.Play();

        // Get the length of SFXClip
        float clipLength = audioSource.clip.length;

        // Check if we need to fade out
        if (fadeDuration > 0f)
        {
            // Start the fade out coroutine directly on the manager
            StartCoroutine(FadeOutAndDestroy(audioSource, clipLength, fadeDuration));
        }
        else
        {
            // Destroy the clip normally after audio has played
            Destroy(audioSource.gameObject, clipLength);
        }
    }

    // Coroutine that handles the volume calculation and destruction
    private IEnumerator FadeOutAndDestroy(AudioSource audioSource, float clipLength, float fadeDuration)
    {
        // Ensure fade duration isn't longer than the clip itself
        fadeDuration = Mathf.Min(fadeDuration, clipLength);

        // Wait until it is time to start fading
        float delayBeforeFade = clipLength - fadeDuration;
        yield return new WaitForSeconds(delayBeforeFade);

        float startVolume = audioSource.volume;
        float currentTime = 0;

        // Smoothly reduce volume to 0
        while (currentTime < fadeDuration)
        {
            // Safeguard in case the object was destroyed externally mid-fade
            if (audioSource == null) yield break;

            currentTime += Time.deltaTime;
            audioSource.volume = Mathf.Lerp(startVolume, 0f, currentTime / fadeDuration);
            yield return null;
        }

        // Clean up the object once faded out
        if (audioSource != null)
        {
            Destroy(audioSource.gameObject);
        }
    }
}