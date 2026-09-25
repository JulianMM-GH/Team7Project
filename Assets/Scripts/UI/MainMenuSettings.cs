using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Panel navigation for the Main Menu's Settings page - the physical page the camera pans to
/// (HomeMenu > Settings). The Controls / Audio buttons and the volume sliders live directly on
/// that page instead of in a screen overlay, and the page's own "Back" button calls Back():
/// Audio -> hub, hub -> first menu. No Time.timeScale, player input, or pause state involved.
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

    [Header("Navigation")]
    [SerializeField] private MenuTransition menuTransition;
    [SerializeField] private int returnMenu = 1;            // MenuTransition page Back returns to

    [Header("First Selected Buttons (for controller navigation)")]
    [SerializeField] private GameObject hubFirstSelected;
    [SerializeField] private GameObject audioFirstSelected;

    void Awake()
    {
        // The page is always visible in the world, so start on the hub without grabbing selection
        settingsHubPanel.SetActive(true);
        audioPanel.SetActive(false);
    }

    // Resets the page to the hub - wired to the Main Menu's "Settings" button
    public void Open() => ShowHub();

    // Wired to the Settings page's "Back" button
    public void Back()
    {
        if (audioPanel.activeSelf)
        {
            ShowHub();
            return;
        }

        menuTransition.ChangeMenu(returnMenu);
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
