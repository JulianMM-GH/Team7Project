using UnityEngine;

/// <summary>
/// FMOD YAY
/// </summary>
public class AudioSettingsUI : MonoBehaviour
{
    public void SetMasterVolume(float level) => RAudio.SetMasterVolume(level);
    public void SetSFXVolume(float level) => RAudio.SetSFXVolume(level);
    public void SetMusicVolume(float level) => RAudio.SetMusicVolume(level);
    public void SetAmbienceVolume(float level) => RAudio.SetAmbienceVolume(level);
}
