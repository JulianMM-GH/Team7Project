using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lives on the same object as UIPlayerFollow (SilasTutorial / PhoenixTutorial) and owns every
/// tutorial prompt canvas that follows this player's head. Exactly one is ever visible: the
/// most recently entered tutorial zone's canvas, or defaultPrompt when no zone is active.
/// TutorialUIPlayerFollow (on each trigger zone) drives this via ShowZonePrompt/HideZonePrompt.
/// </summary>
public class PlayerHeadPrompts : MonoBehaviour
{
    [Tooltip("Shown whenever no tutorial zone is currently overriding it (e.g. SilasControls / PhoenixControls).")]
    [SerializeField] private GameObject defaultPrompt;

    private readonly List<Canvas> allPrompts = new List<Canvas>();

    // Zone requests, most recent last, so exiting one zone can fall back to another still-active one.
    private readonly List<GameObject> activeRequests = new List<GameObject>();

    void Awake()
    {
        GetComponentsInChildren(true, allPrompts);
        ApplyState();
    }

    public void ShowZonePrompt(GameObject prompt)
    {
        if (prompt == null) return;

        activeRequests.Remove(prompt);
        activeRequests.Add(prompt);
        ApplyState();
    }

    public void HideZonePrompt(GameObject prompt)
    {
        activeRequests.Remove(prompt);
        ApplyState();
    }

    // Called after a control scheme change so the visible prompt's icon reflects the new device.
    public void RefreshIcons()
    {
        GameObject current = CurrentTarget();
        if (current == null) return;

        foreach (TutorialIconSwap icon in current.GetComponentsInChildren<TutorialIconSwap>(true))
            icon.Refresh();
    }

    private GameObject CurrentTarget()
    {
        return activeRequests.Count > 0 ? activeRequests[activeRequests.Count - 1] : defaultPrompt;
    }

    private void ApplyState()
    {
        GameObject target = CurrentTarget();

        foreach (Canvas canvas in allPrompts)
        {
            if (canvas != null)
                canvas.gameObject.SetActive(canvas.gameObject == target);
        }

        RefreshIcons();
    }
}
