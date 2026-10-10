using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Plays the UI click on every Button in every scene, so buttons don't need setting up one by one.
/// Buttons with a UISounds component are skipped since that handles its own click
/// </summary>
public static class UIButtonClicks
{
	public const string ClickSound = "UI Click";

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void Init()
	{
		SceneManager.sceneLoaded -= OnSceneLoaded;
		SceneManager.sceneLoaded += OnSceneLoaded;

		// first scene is already loaded by the time this runs
		HookButtons();
	}

	private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => HookButtons();

	private static void HookButtons()
	{
		foreach (Button button in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
		{
			if (button.GetComponent<UISounds>() != null) continue;

			// remove first so a button carried over between scenes doesn't get it twice
			button.onClick.RemoveListener(PlayClick);
			button.onClick.AddListener(PlayClick);
		}
	}

	private static void PlayClick() => RAudio.PlayOneShot(ClickSound);
}
