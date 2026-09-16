using System;
using System.Collections.Generic;
using FMODUnity;
using UnityEngine;

/// <summary>
/// Global sound settings + per sound tweaks. Lives in Resources, edit it through Audio > Sound Library
/// </summary>
[CreateAssetMenu(menuName = "Audio/Sound Library", fileName = "SoundLibrary")]
public class SoundLibrary : ScriptableObject
{
	public const string ResourcesName = "SoundLibrary";
	public const string DefaultAssetPath = "Assets/Resources/SoundLibrary.asset";

	[Header("Pausing")]
	[Tooltip("Buses that get paused with the game. bus:/ is master (pauses menu sounds too)")]
	public string[] pauseBuses = { SONIC_AUDIO_BUS.Master };

	[Tooltip("Optional snapshot that's on while paused")]
	public EventReference pauseSnapshot;

	[Header("Scene changes")]
	[Tooltip("Fade out music/ambience when loading a scene that has no Level Audio")]
	public bool stopMusicInScenesWithoutLevelAudio = true;

	[Header("Debugging")]
	[Tooltip("Warn in the console when something asks for a sound that doesn't exist")]
	public bool warnAboutMissingSounds = true;

	[Tooltip("Logs every sound that plays (spammy, just for debugging)")]
	public bool logEverySound;

	// only sounds that have actually been changed get stored so the asset doesn't cause merge conflicts
	public List<SoundSettings> sounds = new List<SoundSettings>();

	private static SoundLibrary s_instance;
	private static bool s_searched;
	private Dictionary<FMOD.GUID, SoundSettings> m_lookup;

	public static SoundLibrary Instance
	{
		get
		{
			// only load once, not every time a sound plays
			bool destroyed = !ReferenceEquals(s_instance, null) && s_instance == null;
			if (!s_searched || destroyed)
			{
				s_instance = Resources.Load<SoundLibrary>(ResourcesName);
				s_searched = true;
			}
			return s_instance;
		}
	}

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
	public static void ClearCache()
	{
		s_instance = null;
		s_searched = false;
	}

	public static SoundSettings SettingsFor(FMOD.GUID guid)
	{
		SoundLibrary library = Instance;
		if (library != null && library.TryGet(guid, out SoundSettings settings)) return settings;
		return SoundSettings.Defaults;
	}

	public bool TryGet(FMOD.GUID guid, out SoundSettings settings)
	{
		if (m_lookup == null)
		{
			m_lookup = new Dictionary<FMOD.GUID, SoundSettings>();
			foreach (SoundSettings entry in sounds)
			{
				if (entry != null && !entry.sound.Guid.IsNull) m_lookup[entry.sound.Guid] = entry;
			}
		}
		return m_lookup.TryGetValue(guid, out settings);
	}

	// call after adding/removing entries
	public void InvalidateLookup() => m_lookup = null;

	private void OnValidate() => InvalidateLookup();
	private void OnEnable() => InvalidateLookup();
}

// volume is in dB and pitch in semitones to match FMOD
[Serializable]
public class SoundSettings
{
	public static readonly SoundSettings Defaults = new SoundSettings();

	public EventReference sound;

	[Tooltip("0 = same as FMOD")]
	[Range(-40f, 10f)] public float volumeDb;

	[Tooltip("Randomly up to this much quieter each play")]
	[Range(0f, 12f)] public float randomVolumeDb;

	[Tooltip("12 = an octave up")]
	[Range(-24f, 24f)] public float pitchSemitones;

	[Tooltip("Random pitch up/down each play")]
	[Range(0f, 12f)] public float randomPitchSemitones;

	[Tooltip("Min seconds between plays, anything sooner gets skipped")]
	[Min(0f)] public float cooldown;

	[Tooltip("Loops started with RAudio.Play don't get stopped when the scene changes")]
	public bool keepPlayingOnSceneChange;

	[Tooltip("For testing")]
	public bool muted;

	[TextArea(2, 5)] public string notes;

	public bool HasTweaks =>
		!Mathf.Approximately(volumeDb, 0f) || !Mathf.Approximately(randomVolumeDb, 0f) ||
		!Mathf.Approximately(pitchSemitones, 0f) || !Mathf.Approximately(randomPitchSemitones, 0f) ||
		!Mathf.Approximately(cooldown, 0f) || keepPlayingOnSceneChange || muted || !string.IsNullOrEmpty(notes);

	public float RollVolume()
	{
		float db = volumeDb - (randomVolumeDb > 0f ? UnityEngine.Random.Range(0f, randomVolumeDb) : 0f);
		return DecibelsToLinear(db);
	}

	public float RollPitch()
	{
		float semitones = pitchSemitones + (randomPitchSemitones > 0f ? UnityEngine.Random.Range(-randomPitchSemitones, randomPitchSemitones) : 0f);
		return SemitonesToPitch(semitones);
	}

	public static float DecibelsToLinear(float db) => db <= -80f ? 0f : Mathf.Pow(10f, db / 20f);
	public static float SemitonesToPitch(float semitones) => Mathf.Pow(2f, semitones / 12f);
}

// parameter to set when a sound starts. label is used instead of value if it's filled in
[Serializable]
public class SoundParameter
{
	public string name;
	public float value;
	public string label;

	public SoundParameter() { }

	public SoundParameter(string name, float value)
	{
		this.name = name;
		this.value = value;
	}

	public SoundParameter(string name, string label)
	{
		this.name = name;
		this.label = label;
	}

	public void ApplyTo(FMOD.Studio.EventInstance instance)
	{
		if (string.IsNullOrEmpty(name) || !instance.isValid()) return;

		FMOD.RESULT result = string.IsNullOrEmpty(label)
			? instance.setParameterByName(name, value)
			: instance.setParameterByNameWithLabel(name, label);

		if (result != FMOD.RESULT.OK)
		{
			instance.getDescription(out FMOD.Studio.EventDescription description);
			description.getPath(out string path);
			Debug.LogWarning($"[Audio] Couldn't set param \"{name}\" on {path} ({result})");
		}
	}
}
