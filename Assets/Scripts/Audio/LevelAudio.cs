using FMODUnity;
using UnityEngine;

/// <summary>
/// Drop on one GameObject per scene to set the music and ambience. Same track carries over between scenes
/// </summary>
[AddComponentMenu("Audio/Level Audio")]
public class LevelAudio : MonoBehaviour
{
	[SerializeField] private EventReference music;
	[SerializeField] private EventReference ambience;
	[Tooltip("Restart the music even if the last scene was already playing it")]
	[SerializeField] private bool restartIfAlreadyPlaying;
	[Tooltip("If music/ambience is empty, keep whatever the last scene was playing")]
	[SerializeField] private bool keepPreviousSceneAudioIfEmpty;
	[SerializeField] private SoundParameter[] musicParameters = new SoundParameter[0];

	// old tick boxes - still work but the inspector has an upgrade button for them
	[SerializeField, HideInInspector] private bool playMusic;
	[SerializeField, HideInInspector] private bool playAmbience;
	[SerializeField, HideInInspector] private bool playUIMusic;

	public const string LegacyMusic = "Music";
	public const string LegacyAmbience = "Ambience";
	public const string LegacyUIMusic = "UI Music";

	public bool UsesLegacySetup => playMusic || playAmbience || playUIMusic;

	private void Start()
	{
		StartMusic();
		StartAmbience();
	}

	private void StartMusic()
	{
		if (!music.IsNull) RAudio.PlayMusic(music, restartIfAlreadyPlaying);
		else if (playUIMusic) RAudio.PlayMusic(LegacyUIMusic, restartIfAlreadyPlaying);
		else if (playMusic) RAudio.PlayMusic(LegacyMusic, restartIfAlreadyPlaying);
		else if (!keepPreviousSceneAudioIfEmpty) RAudio.StopMusic();

		// old setup could play both at once
		if (music.IsNull && playUIMusic && playMusic) RAudio.Play(LegacyMusic, false);

		foreach (SoundParameter parameter in musicParameters)
		{
			if (parameter == null || string.IsNullOrEmpty(parameter.name)) continue;
			if (string.IsNullOrEmpty(parameter.label)) RAudio.SetMusicParam(parameter.name, parameter.value);
			else RAudio.SetMusicLabeledParam(parameter.name, parameter.label);
		}
	}

	private void StartAmbience()
	{
		if (!ambience.IsNull) RAudio.PlayAmbience(ambience, restartIfAlreadyPlaying);
		else if (playAmbience) RAudio.PlayAmbience(LegacyAmbience, restartIfAlreadyPlaying);
		else if (!keepPreviousSceneAudioIfEmpty) RAudio.StopAmbience();
	}

	// for UnityEvents
	public void ChangeMusic(string soundName) => RAudio.PlayMusic(soundName);
	public void FadeOutMusic() => RAudio.StopMusic();
}
