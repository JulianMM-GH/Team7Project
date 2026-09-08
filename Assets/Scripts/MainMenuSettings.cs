using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Panel navigation for the Main Menu's Settings screen (Controls + Audio), mirroring
/// PauseMenu's settings hub but standalone - no Time.timeScale, player input, or pause state
/// involved here.
///
/// Audio reuses AudioSettingsUI as-is (the sliders call it directly). Controls reuses the
/// existing ControlsSetupPopup - it already saves every change via PlayerPrefs
/// (LocalMultiplayerSpawner.SavePreJoinLayout), so whatever's picked here is what a level
/// picks up on join; this script just opens/closes it via OpenSettingsOnly()/CloseSettingsOnly()
/// instead of its normal Confirm-and-load-scene flow.
/// </summary>
public class MainMenuSettings : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject settingsHubPanel;   // "Controls" / "Audio" buttons
    [SerializeField] private GameObject audioPanel;         // Hosts the volume sliders
    [SerializeField] private ControlsSetupPopup controlsSetupPopup;

    [Header("First Selected Buttons (for controller navigation)")]
    [SerializeField] private GameObject hubFirstSelected;
    [SerializeField] private GameObject audioFirstSelected;

    void OnEnable()
    {
        ShowHub();
    }

    public void OpenControls()
    {
        settingsHubPanel.SetActive(false);
        audioPanel.SetActive(false);
        controlsSetupPopup.OpenSettingsOnly();
    }

    public void BackFromControls()
    {
        controlsSetupPopup.CloseSettingsOnly();
        ShowHub();
    }

    public void OpenAudio()
    {
        settingsHubPanel.SetActive(false);
        audioPanel.SetActive(true);

        if (audioFirstSelected != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(audioFirstSelected);
        }

        SyncAudioSliders();
    }

    public void BackFromAudio() => ShowHub();

    // Closes the whole Settings overlay - wired to the hub's own "Back" button
    public void Close() => gameObject.SetActive(false);

    // Opens the Settings overlay - wired to the Main Menu's "Settings" button
    public void Open() => gameObject.SetActive(true);

    private void ShowHub()
    {
        settingsHubPanel.SetActive(true);
        audioPanel.SetActive(false);

        if (hubFirstSelected != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(hubFirstSelected);
        }
    }

    // Reflects the saved bus volumes on the sliders so they don't show 100% every time the
    // panel opens. Same slider path convention as PauseMenu.SyncAudioSliders().
    private void SyncAudioSliders()
    {
        if (audioPanel == null) return;

        SetSliderWithoutNotify("MasterVolume/Slider", RAudio.GetMasterVolume());
        SetSliderWithoutNotify("SFXVolume/Slider", RAudio.GetSFXVolume());
        SetSliderWithoutNotify("MusicVolume/Slider", RAudio.GetMusicVolume());
        SetSliderWithoutNotify("AmbienceVolume/Slider", RAudio.GetAmbienceVolume());
    }

    private void SetSliderWithoutNotify(string path, float value)
    {
        Transform t = audioPanel.transform.Find(path);
        if (t != null && t.TryGetComponent(out Slider slider))
            slider.SetValueWithoutNotify(value);
    }
}
