using FMOD.Studio;
using STOP_MODE = FMOD.Studio.STOP_MODE;
using FMODUnity;
using UnityEngine;

/// <summary>
/// Global audio engine. Sounds can be referred to by event name ("Jump") or full path ("event:/SFX/Jump")
/// </summary>
public static class RAudio
{
	// One shots - new instance every call, released automatically
	public static void PlayOneShot(string ID) => AudioManager.Manager?.PlayOneShot(ID);
	public static void PlayOneShot(string ID, Vector3 position) => AudioManager.Manager?.PlayOneShot(ID, position);
	public static void PlayOneShot(string ID, GameObject follow) => AudioManager.Manager?.PlayOneShot(ID, follow);

	public static void PlayOneShot(EventReference sound) => AudioManager.Manager?.PlayOneShot(sound);
	public static void PlayOneShot(EventReference sound, Vector3 position) => AudioManager.Manager?.PlayOneShot(sound, position);
	public static void PlayOneShot(EventReference sound, GameObject follow) => AudioManager.Manager?.PlayOneShot(sound, follow);

	// Loops - one shared instance per sound, stopped on scene change unless set otherwise in the Sound Library
	public static void Play(string ID, bool restartIfPlaying = true) => AudioManager.Manager?.Play(ID, restartIfPlaying);
	public static void Stop(string ID) => AudioManager.Manager?.Stop(ID, STOP_MODE.ALLOWFADEOUT);
	public static void Stop(string ID, STOP_MODE mode) => AudioManager.Manager?.Stop(ID, mode);
	public static bool IsPlaying(string ID) => AudioManager.Manager != null && AudioManager.Manager.IsPlaying(ID);

	public static void StopAllFromBanks(STOP_MODE mode = STOP_MODE.IMMEDIATE, params string[] banks) => AudioManager.Manager?.StopAllFromBanks(mode, banks);
	public static void StopAllSounds(bool fadeOut = true) => AudioManager.Manager?.StopAllSounds(fadeOut);
	public static bool Exists(string ID) => AudioManager.Manager != null && AudioManager.Manager.Exists(ID);

	// Music + ambience - one of each at a time, same track carries over between scenes
	public static void PlayMusic(string ID, bool restartIfSame = false) => AudioManager.Manager?.PlayMusic(ID, restartIfSame);
	public static void PlayMusic(EventReference music, bool restartIfSame = false) => AudioManager.Manager?.PlayMusic(music, restartIfSame);
	public static void StopMusic(bool fadeOut = true) => AudioManager.Manager?.StopMusic(fadeOut);
	public static void SetMusicParam(string parameter, float value) => AudioManager.Manager?.SetMusicParam(parameter, value);
	public static void SetMusicLabeledParam(string parameter, string label) => AudioManager.Manager?.SetMusicLabeledParam(parameter, label);

	public static void PlayAmbience(string ID, bool restartIfSame = false) => AudioManager.Manager?.PlayAmbience(ID, restartIfSame);
	public static void PlayAmbience(EventReference ambience, bool restartIfSame = false) => AudioManager.Manager?.PlayAmbience(ambience, restartIfSame);
	public static void StopAmbience(bool fadeOut = true) => AudioManager.Manager?.StopAmbience(fadeOut);

	//Param settings for local parameters

	public static void SetLabeledParam(string ID, string Parameter, string value) => AudioManager.Manager?.SetLabeledParam(ID, Parameter, value);
	public static void SetValueParam(string ID, string Parameter, float value) => AudioManager.Manager?.SetValueParam(ID, Parameter, value);
	public static float GetValueParam(string ID, string Parameter) => AudioManager.Manager != null ? AudioManager.Manager.GetParam(ID, Parameter) : 0f;

	//Param settings for global parameters

	public static void GSetLabeledParam(string Parameter, string value) => AudioManager.Manager?.GSetLabeledParam(Parameter, value);
	public static void GSetValueParam(string Parameter, float value) => AudioManager.Manager?.GSetValueParam(Parameter, value);
	public static float GGetValueParam(string Parameter) => AudioManager.Manager != null ? AudioManager.Manager.GGetParam(Parameter) : 0f;

	// Snapshots - "Underwater" or "snapshot:/Underwater"
	public static void StartSnapshot(string name) => AudioManager.Manager?.StartSnapshot(name);
	public static void StartSnapshot(EventReference snapshot) => AudioManager.Manager?.StartSnapshot(snapshot);
	public static void StopSnapshot(string name, bool fadeOut = true) => AudioManager.Manager?.StopSnapshot(name, fadeOut);
	public static void StopSnapshot(EventReference snapshot, bool fadeOut = true) => AudioManager.Manager?.StopSnapshot(snapshot, fadeOut);
	public static bool IsSnapshotActive(string name) => AudioManager.Manager != null && AudioManager.Manager.IsSnapshotActive(name);

	// Bus volume (options menu sliders) - persists across scenes and sessions via PlayerPrefs

	public static void SetMasterVolume(float level) => AudioManager.Manager?.SetMasterVolume(level);
	public static void SetSFXVolume(float level) => AudioManager.Manager?.SetSFXVolume(level);
	public static void SetMusicVolume(float level) => AudioManager.Manager?.SetMusicVolume(level);
	public static void SetAmbienceVolume(float level) => AudioManager.Manager?.SetAmbienceVolume(level);

	public static float GetMasterVolume() => PlayerPrefs.GetFloat(SONIC_VOLUME_PREFS.Master, 1f);
	public static float GetSFXVolume() => PlayerPrefs.GetFloat(SONIC_VOLUME_PREFS.SFX, 1f);
	public static float GetMusicVolume() => PlayerPrefs.GetFloat(SONIC_VOLUME_PREFS.Music, 1f);
	public static float GetAmbienceVolume() => PlayerPrefs.GetFloat(SONIC_VOLUME_PREFS.Ambience, 1f);

	public static void SetBusMuted(string busPath, bool muted) => AudioManager.Manager?.SetBusMuted(busPath, muted);

	// Pauses/resumes the pause buses from the Sound Library (Master by default) - used when the game is paused
	public static void SetGlobalPause(bool paused) => AudioManager.Manager?.SetGlobalPause(paused);

	// Raw instance with library tweaks applied. Caller has to start, position, stop and release it
	public static EventInstance CreateInstance(string ID) => AudioManager.Manager != null ? AudioManager.Manager.CreateInstance(ID) : default;
}

/// <summary>
/// FMOD mixer bus paths for the options menu volume sliders
/// </summary>
public static class SONIC_AUDIO_BUS
{
	public const string Master = "bus:/";
	public const string SFX = "bus:/SFX";
	public const string Music = "bus:/MSC";
	public const string Ambience = "bus:/AMBNC";
}

/// <summary>
/// PlayerPrefs keys the saved slider volumes are stored under
/// </summary>
public static class SONIC_VOLUME_PREFS
{
	public const string Master = "Volume_Master";
	public const string SFX = "Volume_SFX";
	public const string Music = "Volume_Music";
	public const string Ambience = "Volume_Ambience";
}
