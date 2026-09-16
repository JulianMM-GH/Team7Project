// Made this because i fucking hate the animator and debugging it grr - skullyy

using UnityEngine;

public class DebugTime : MonoBehaviour
{
    [Header("Time Scales")]
    public float f1Scale = 1.0f;
    public float f2Scale = 0.5f;
    public float f3Scale = 0.25f;
    public float f4Scale = 0.1f;

    [Header("Options")]
    public bool showOnScreen = true;
    public bool logChanges   = true;

    private void Update()
    {
        if      (Input.GetKeyDown(KeyCode.F1)) SetScale(f1Scale);
        else if (Input.GetKeyDown(KeyCode.F2)) SetScale(f2Scale);
        else if (Input.GetKeyDown(KeyCode.F3)) SetScale(f3Scale);
        else if (Input.GetKeyDown(KeyCode.F4)) SetScale(f4Scale);
    }

    private void SetScale(float scale)
    {
        Time.timeScale      = scale;
        Time.fixedDeltaTime = 0.02f * scale;

        if (logChanges)
            Debug.Log($"<color=#ffd27f>[TimeScaler]</color> Time.timeScale = {scale}");
    }

    private void OnGUI()
    {
        if (!showOnScreen) return;
        GUI.Label(new Rect(10, 10, 300, 20), $"TimeScale: {Time.timeScale:F2}  (F1-F4)");
    }

    private void OnDisable()
    {
        Time.timeScale      = 1f;
        Time.fixedDeltaTime = 0.02f;
    }
}