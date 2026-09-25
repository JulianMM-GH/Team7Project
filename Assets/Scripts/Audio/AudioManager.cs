using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using FMODUnity;
using FMOD.Studio;
using STOP_MODE = FMOD.Studio.STOP_MODE;
using UnityEngine.SceneManagement;

public enum SoundPlacement
{
	AtListener, // sits on the camera so it doesn't pan or get quieter
	AtPosition,
	FollowObject,
}

/// <summary>
/// Handles all the FMOD playback. Creates itself if a scene doesn't have one - use RAudio instead of calling this directly
/// </summary>
[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
[AddComponentMenu("Audio/Audio Manager")]
public class AudioManager : MonoBehaviour
{
	private static AudioManager Instance { get; set; }
	private static bool s_quitting;

	// makes one if there isn't one yet (play mode only)
	public static AudioManager Manager
	{
		get
		{
			if (Instance != null) return Instance;
			if (!Application.isPlaying || s_quitting) return null;

			new GameObject("AudioManager").AddComponent<AudioManager>();
			return Instance;
		}
	}

	// same as Manager but doesn't create one, for editor stuff
	public static AudioManager Current => Instance;

	[Tooltip("Events in these banks can't be looked up by name")]
	[FMODUnity.BankRef]
	public string[] banksToIgnore;

	private enum SoundKind { Event, Snapshot }

	private sealed class OwnedSound
	{
		public string path;
		public FMOD.GUID guid;
		public EventDescription description;
		public EventInstance instance;
	}

	private struct PositionedInstance
	{
		public EventInstance instance;
		public Transform follow;
		public bool followsListener;
		public int startFrame;
	}

	// used by the live tab in the sound library window
	public struct ActiveSound
	{
		public string role;
		public string path;
		public PLAYBACK_STATE state;
	}

	public struct SoundLogEntry
	{
		public float time;
		public string path;
		public string note;
	}

	private const int MaxPendingCalls = 64;
	private const int MaxLogEntries = 40;

	private bool m_ready;
	private bool m_initFailed;
	private readonly List<Action> m_pending = new List<Action>();

	private readonly Dictionary<string, EventDescription> m_eventsByPath = new Dictionary<string, EventDescription>(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, string> m_bankByPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, List<string>> m_eventNames = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, List<string>> m_snapshotNames = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
	private int m_catalogBankCount = -1;

	private readonly Dictionary<string, OwnedSound> m_loops = new Dictionary<string, OwnedSound>(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, OwnedSound> m_snapshots = new Dictionary<string, OwnedSound>(StringComparer.OrdinalIgnoreCase);
	private readonly OwnedSound m_music = new OwnedSound();
	private readonly OwnedSound m_ambience = new OwnedSound();

	private readonly List<PositionedInstance> m_positioned = new List<PositionedInstance>();
	private readonly Dictionary<FMOD.GUID, float> m_lastPlayed = new Dictionary<FMOD.GUID, float>();
	private readonly HashSet<string> m_warned = new HashSet<string>();
	private readonly List<SoundLogEntry> m_log = new List<SoundLogEntry>();

	private SoundListener2D m_fallbackListener;
	private Bus m_masterBus;
	private bool m_hasMasterBus;

	public bool IsReady => m_ready;
	public bool IsGamePaused { get; private set; }
	public IReadOnlyList<SoundLogEntry> RecentSounds => m_log;

	// Lifecycle

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
	private static void ResetStatics()
	{
		Instance = null;
		s_quitting = false;
		Application.quitting -= OnQuitting;
		Application.quitting += OnQuitting;
	}

	private static void OnQuitting() => s_quitting = true;

	private void Awake()
	{
		if (Instance != null && Instance != this)
		{
			Destroy(gameObject);
			return;
		}

		Instance = this;
		transform.SetParent(null);
		DontDestroyOnLoad(gameObject);
		SceneManager.activeSceneChanged += OnActiveSceneChanged;

		// Set up here (not Start) so it's ready before any other object's Start() calls RAudio this frame -
		// Unity doesn't guarantee Start() order across GameObjects.
		TryInitialize();
	}

	// If the master banks aren't loaded yet (this is the first thing to touch FMOD) keep retrying from Update
	// instead of being broken for the rest of the session
	private void TryInitialize()
	{
		if (m_ready || m_initFailed) return;

		try
		{
			if (!RuntimeManager.HaveMasterBanksLoaded) return;

			ApplySavedVolumes();
			BuildCatalog();
			EnsureFallbackListener();
			m_ready = true;
		}
		catch (Exception e)
		{
			m_initFailed = true;
			Debug.LogError($"[Audio] FMOD failed to initialise: {e.Message}");
			return;
		}

		List<Action> pending = new List<Action>(m_pending);
		m_pending.Clear();
		foreach (Action call in pending) call();
	}

	private void Update()
	{
		if (!m_ready) TryInitialize();
	}

	private void LateUpdate()
	{
		if (!m_ready) return;
		UpdateFallbackListener();
		UpdatePositions();
	}

	private void OnDestroy()
	{
		if (Instance != this) return;

		SceneManager.activeSceneChanged -= OnActiveSceneChanged;
		Instance = null;

		foreach (OwnedSound sound in AllOwnedSounds()) Release(sound, STOP_MODE.IMMEDIATE);
		m_loops.Clear();
		m_snapshots.Clear();
	}

	private void OnActiveSceneChanged(Scene from, Scene to)
	{
		if (!m_ready) return;

		// stop loops from the old scene unless they're set to keep playing
		foreach (OwnedSound loop in m_loops.Values.ToList())
		{
			if (SoundLibrary.SettingsFor(loop.guid).keepPlayingOnSceneChange && IsActive(loop.instance)) continue;
			Release(loop, STOP_MODE.ALLOWFADEOUT);
			m_loops.Remove(loop.path);
		}

		BuildCatalog();

		// old scene's listener is gone, LateUpdate turns ours back off if the new scene has one
		if (m_fallbackListener != null) m_fallbackListener.gameObject.SetActive(true);

		StartCoroutine(StopMusicIfSceneHasNoLevelAudio());
	}

	private IEnumerator StopMusicIfSceneHasNoLevelAudio()
	{
		yield return null;

		SoundLibrary library = SoundLibrary.Instance;
		if (library != null && !library.stopMusicInScenesWithoutLevelAudio) yield break;
		if (FindAnyObjectByType<LevelAudio>() != null) yield break;

		StopMusic(true);
		StopAmbience(true);
	}

	// queues the call if FMOD isn't ready yet, returns true if it got queued
	private bool Defer(Action call)
	{
		if (m_ready) return false;
		TryInitialize();
		if (m_ready) return false;

		if (m_pending.Count < MaxPendingCalls) m_pending.Add(call);
		return true;
	}

	// One-shots

	public void PlayOneShot(string id) => PlayOneShot(id, SoundPlacement.AtListener, Vector3.zero, null);
	public void PlayOneShot(string id, Vector3 position) => PlayOneShot(id, SoundPlacement.AtPosition, position, null);
	public void PlayOneShot(string id, GameObject follow) => PlayOneShot(id, follow != null ? SoundPlacement.FollowObject : SoundPlacement.AtListener, Vector3.zero, follow != null ? follow.transform : null);

	public void PlayOneShot(EventReference sound) => PlayOneShot(sound, SoundPlacement.AtListener, Vector3.zero, null);
	public void PlayOneShot(EventReference sound, Vector3 position) => PlayOneShot(sound, SoundPlacement.AtPosition, position, null);
	public void PlayOneShot(EventReference sound, GameObject follow) => PlayOneShot(sound, follow != null ? SoundPlacement.FollowObject : SoundPlacement.AtListener, Vector3.zero, follow != null ? follow.transform : null);

	private void PlayOneShot(string id, SoundPlacement placement, Vector3 position, Transform follow)
	{
		if (Defer(() => PlayOneShot(id, placement, position, follow))) return;
		if (!TryResolve(id, SoundKind.Event, true, null, out EventDescription description, out _)) return;
		StartSound(description, placement, position, follow);
	}

	private void PlayOneShot(EventReference sound, SoundPlacement placement, Vector3 position, Transform follow)
	{
		if (Defer(() => PlayOneShot(sound, placement, position, follow))) return;
		if (!TryResolve(sound, null, out EventDescription description)) return;
		StartSound(description, placement, position, follow);
	}

	// Used by RAudio and the components. Returns an invalid instance if it got skipped (muted/cooldown).
	// If release is false whoever called this needs to release it
	public EventInstance StartSound(EventDescription description, SoundPlacement placement = SoundPlacement.AtListener,
		Vector3 position = default, Transform follow = null, IList<SoundParameter> parameters = null, bool release = true)
	{
		if (!m_ready || !description.isValid()) return default;

		description.getID(out FMOD.GUID guid);
		description.getPath(out string path);
		SoundSettings settings = SoundLibrary.SettingsFor(guid);

		if (settings.muted)
		{
			Record(path, "muted");
			return default;
		}

		if (settings.cooldown > 0f)
		{
			float now = Time.unscaledTime;
			if (m_lastPlayed.TryGetValue(guid, out float last) && now - last < settings.cooldown)
			{
				Record(path, "cooldown");
				return default;
			}
			m_lastPlayed[guid] = now;
		}

		FMOD.RESULT result = description.createInstance(out EventInstance instance);
		if (result != FMOD.RESULT.OK)
		{
			Warn("create:" + path, $"[Audio] Couldn't create instance of {path} ({result})");
			return default;
		}

		Configure(instance, description, settings, placement, position, follow);
		if (parameters != null)
		{
			foreach (SoundParameter parameter in parameters) parameter?.ApplyTo(instance);
		}

		instance.start();
		if (release) instance.release();
		Record(path, null);
		return instance;
	}

	private void Configure(EventInstance instance, EventDescription description, SoundSettings settings,
		SoundPlacement placement, Vector3 position, Transform follow)
	{
		instance.setVolume(settings.RollVolume());
		instance.setPitch(settings.RollPitch());

		description.is3D(out bool is3D);
		if (!is3D) return;

		if (placement == SoundPlacement.FollowObject && follow == null) placement = SoundPlacement.AtListener;

		Vector3 start = placement == SoundPlacement.AtPosition ? position
			: placement == SoundPlacement.FollowObject ? follow.position
			: ListenerPosition;
		instance.set3DAttributes(RuntimeUtils.To3DAttributes(start));

		if (placement == SoundPlacement.AtPosition) return;

		m_positioned.RemoveAll(p => p.instance.handle == instance.handle);
		m_positioned.Add(new PositionedInstance
		{
			instance = instance,
			follow = follow,
			followsListener = placement == SoundPlacement.AtListener,
			startFrame = Time.frameCount,
		});
	}

	private void UpdatePositions()
	{
		Vector3 listener = ListenerPosition;

		for (int i = m_positioned.Count - 1; i >= 0; i--)
		{
			PositionedInstance entry = m_positioned[i];
			entry.instance.getPlaybackState(out PLAYBACK_STATE state);

			// new instances can still say STOPPED for a frame after start()
			if (!entry.instance.isValid() || (state == PLAYBACK_STATE.STOPPED && Time.frameCount > entry.startFrame + 1))
			{
				m_positioned.RemoveAt(i);
				continue;
			}

			if (entry.followsListener)
				entry.instance.set3DAttributes(RuntimeUtils.To3DAttributes(listener));
			else if (entry.follow != null)
				entry.instance.set3DAttributes(RuntimeUtils.To3DAttributes(entry.follow.position));
			// followed object got destroyed - just leave the sound where it was
		}
	}

	public static Vector3 ListenerPosition
	{
		get
		{
			if (!RuntimeManager.IsInitialized) return Vector3.zero;
			RuntimeManager.StudioSystem.getListenerAttributes(0, out FMOD.ATTRIBUTES_3D attributes);
			return new Vector3(attributes.position.x, attributes.position.y, attributes.position.z);
		}
	}

	// Looping sounds

	public void Play(string id, bool restartIfPlaying = true)
	{
		if (Defer(() => Play(id, restartIfPlaying))) return;

		OwnedSound loop = GetOrCreateLoop(id);
		if (loop == null) return;
		if (!restartIfPlaying && IsActive(loop.instance)) return;

		SoundSettings settings = SoundLibrary.SettingsFor(loop.guid);
		if (settings.muted)
		{
			Record(loop.path, "muted");
			return;
		}

		Configure(loop.instance, loop.description, settings, SoundPlacement.AtListener, Vector3.zero, null);
		loop.instance.start();
		Record(loop.path, "loop started");
	}

	public void Stop(string id, STOP_MODE mode = STOP_MODE.ALLOWFADEOUT)
	{
		if (Defer(() => Stop(id, mode))) return;
		if (!TryResolve(id, SoundKind.Event, true, null, out _, out string path)) return;

		foreach (OwnedSound sound in OwnedSoundsFor(path)) sound.instance.stop(mode);
	}

	public bool IsPlaying(string id)
	{
		if (!m_ready || !TryResolve(id, SoundKind.Event, false, null, out _, out string path)) return false;

		foreach (OwnedSound sound in OwnedSoundsFor(path))
		{
			sound.instance.getPlaybackState(out PLAYBACK_STATE state);
			if (state != PLAYBACK_STATE.STOPPED) return true;
		}
		return false;
	}

	public bool Exists(string id) => m_ready && TryResolve(id, SoundKind.Event, false, null, out _, out _);

	public void StopAllFromBanks(STOP_MODE mode = STOP_MODE.IMMEDIATE, params string[] banks)
	{
		if (Defer(() => StopAllFromBanks(mode, banks))) return;
		if (banks == null || banks.Length == 0) return;

		foreach (KeyValuePair<string, EventDescription> entry in m_eventsByPath)
		{
			if (!m_bankByPath.TryGetValue(entry.Key, out string bank) || !banks.Contains(bank)) continue;
			if (entry.Value.getInstanceList(out EventInstance[] instances) != FMOD.RESULT.OK) continue;
			foreach (EventInstance instance in instances) instance.stop(mode);
		}
	}

	public void StopAllSounds(bool fadeOut = true)
	{
		if (Defer(() => StopAllSounds(fadeOut))) return;
		if (TryGetBus(SONIC_AUDIO_BUS.Master, out Bus master)) master.stopAllEvents(fadeOut ? STOP_MODE.ALLOWFADEOUT : STOP_MODE.IMMEDIATE);
	}

	public EventInstance CreateInstance(string id)
	{
		if (!m_ready || !TryResolve(id, SoundKind.Event, true, null, out EventDescription description, out _)) return default;

		description.createInstance(out EventInstance instance);
		description.getID(out FMOD.GUID guid);
		SoundSettings settings = SoundLibrary.SettingsFor(guid);
		instance.setVolume(settings.RollVolume());
		instance.setPitch(settings.RollPitch());
		instance.set3DAttributes(RuntimeUtils.To3DAttributes(ListenerPosition));
		return instance;
	}

	private OwnedSound GetOrCreateLoop(string id)
	{
		if (!TryResolve(id, SoundKind.Event, true, null, out EventDescription description, out string path)) return null;
		if (m_loops.TryGetValue(path, out OwnedSound loop) && loop.instance.isValid()) return loop;

		if (description.createInstance(out EventInstance instance) != FMOD.RESULT.OK) return null;
		instance.set3DAttributes(RuntimeUtils.To3DAttributes(ListenerPosition));
		description.getID(out FMOD.GUID guid);

		loop = new OwnedSound { path = path, guid = guid, description = description, instance = instance };
		m_loops[path] = loop;
		return loop;
	}

	// the shared loop, plus the music/ambience slots if they're playing this event
	private IEnumerable<OwnedSound> OwnedSoundsFor(string path)
	{
		if (m_loops.TryGetValue(path, out OwnedSound loop) && loop.instance.isValid()) yield return loop;
		if (m_music.instance.isValid() && string.Equals(m_music.path, path, StringComparison.OrdinalIgnoreCase)) yield return m_music;
		if (m_ambience.instance.isValid() && string.Equals(m_ambience.path, path, StringComparison.OrdinalIgnoreCase)) yield return m_ambience;
	}

	private IEnumerable<OwnedSound> AllOwnedSounds()
	{
		foreach (OwnedSound loop in m_loops.Values) yield return loop;
		foreach (OwnedSound snapshot in m_snapshots.Values) yield return snapshot;
		yield return m_music;
		yield return m_ambience;
	}

	private static void Release(OwnedSound sound, STOP_MODE mode)
	{
		if (!sound.instance.isValid()) return;
		sound.instance.stop(mode);
		sound.instance.release();
		sound.instance.clearHandle();
	}

	private static bool IsActive(EventInstance instance)
	{
		if (!instance.isValid()) return false;
		instance.getPlaybackState(out PLAYBACK_STATE state);
		return state == PLAYBACK_STATE.PLAYING || state == PLAYBACK_STATE.STARTING || state == PLAYBACK_STATE.SUSTAINING;
	}

	// Music and ambience

	public void PlayMusic(string id, bool restartIfSame = false) => PlayInSlot(m_music, id, restartIfSame);
	public void PlayMusic(EventReference music, bool restartIfSame = false) => PlayInSlot(m_music, music, restartIfSame);
	public void StopMusic(bool fadeOut = true) => StopSlot(m_music, fadeOut);

	public void PlayAmbience(string id, bool restartIfSame = false) => PlayInSlot(m_ambience, id, restartIfSame);
	public void PlayAmbience(EventReference ambience, bool restartIfSame = false) => PlayInSlot(m_ambience, ambience, restartIfSame);
	public void StopAmbience(bool fadeOut = true) => StopSlot(m_ambience, fadeOut);

	public void SetMusicParam(string parameter, float value)
	{
		if (Defer(() => SetMusicParam(parameter, value))) return;
		if (m_music.instance.isValid()) CheckParam(m_music.instance.setParameterByName(parameter, value), m_music.path, parameter);
	}

	public void SetMusicLabeledParam(string parameter, string label)
	{
		if (Defer(() => SetMusicLabeledParam(parameter, label))) return;
		if (m_music.instance.isValid()) CheckParam(m_music.instance.setParameterByNameWithLabel(parameter, label), m_music.path, parameter);
	}

	private void PlayInSlot(OwnedSound slot, string id, bool restartIfSame)
	{
		if (Defer(() => PlayInSlot(slot, id, restartIfSame))) return;
		if (TryResolve(id, SoundKind.Event, true, null, out EventDescription description, out _)) PlayInSlot(slot, description, restartIfSame);
	}

	private void PlayInSlot(OwnedSound slot, EventReference sound, bool restartIfSame)
	{
		if (Defer(() => PlayInSlot(slot, sound, restartIfSame))) return;
		if (TryResolve(sound, null, out EventDescription description)) PlayInSlot(slot, description, restartIfSame);
	}

	private void PlayInSlot(OwnedSound slot, EventDescription description, bool restartIfSame)
	{
		description.getID(out FMOD.GUID guid);

		// already playing this track (probably carried over from the last scene) so leave it
		if (!restartIfSame && slot.instance.isValid() && slot.guid.Equals(guid) && IsActive(slot.instance)) return;

		StopSlot(slot, true);

		EventInstance instance = StartSound(description, release: false);
		if (!instance.isValid()) return;

		description.getPath(out string path);
		slot.instance = instance;
		slot.description = description;
		slot.guid = guid;
		slot.path = path;
	}

	private void StopSlot(OwnedSound slot, bool fadeOut)
	{
		if (Defer(() => StopSlot(slot, fadeOut))) return;
		Release(slot, fadeOut ? STOP_MODE.ALLOWFADEOUT : STOP_MODE.IMMEDIATE);
		slot.path = null;
		slot.guid = default;
	}

	// Parameters

	public void SetLabeledParam(string ID, string Parameter, string value)
	{
		if (Defer(() => SetLabeledParam(ID, Parameter, value))) return;
		foreach (OwnedSound sound in OwnedOrNewLoop(ID))
			CheckParam(sound.instance.setParameterByNameWithLabel(Parameter, value), sound.path, Parameter);
	}

	public void SetValueParam(string ID, string Parameter, float value)
	{
		if (Defer(() => SetValueParam(ID, Parameter, value))) return;
		foreach (OwnedSound sound in OwnedOrNewLoop(ID))
			CheckParam(sound.instance.setParameterByName(Parameter, value), sound.path, Parameter);
	}

	public float GetParam(string ID, string Parameter)
	{
		if (!m_ready || !TryResolve(ID, SoundKind.Event, true, null, out _, out string path)) return 0f;

		foreach (OwnedSound sound in OwnedSoundsFor(path))
		{
			sound.instance.getParameterByName(Parameter, out float value);
			return value;
		}
		return 0f;
	}

	public void GSetLabeledParam(string Parameter, string value)
	{
		if (Defer(() => GSetLabeledParam(Parameter, value))) return;
		CheckParam(RuntimeManager.StudioSystem.setParameterByNameWithLabel(Parameter, value), null, Parameter);
	}

	public void GSetValueParam(string Parameter, float value)
	{
		if (Defer(() => GSetValueParam(Parameter, value))) return;
		CheckParam(RuntimeManager.StudioSystem.setParameterByName(Parameter, value), null, Parameter);
	}

	public float GGetParam(string Parameter)
	{
		if (!m_ready) return 0f;
		RuntimeManager.StudioSystem.getParameterByName(Parameter, out float value);
		return value;
	}

	// if nothing's playing yet, make the loop so the param is already set when Play gets called
	private List<OwnedSound> OwnedOrNewLoop(string id)
	{
		List<OwnedSound> result = new List<OwnedSound>();
		if (!TryResolve(id, SoundKind.Event, true, null, out _, out string path)) return result;

		result.AddRange(OwnedSoundsFor(path));
		if (result.Count == 0)
		{
			OwnedSound loop = GetOrCreateLoop(path);
			if (loop != null) result.Add(loop);
		}
		return result;
	}

	private void CheckParam(FMOD.RESULT result, string path, string parameter)
	{
		if (result == FMOD.RESULT.OK) return;
		string owner = path != null ? $"on {path}" : "(global)";
		Warn($"param:{path}:{parameter}:{result}",
			$"[Audio] Couldn't set param \"{parameter}\" {owner} ({result}) - check the name matches FMOD and whether it's local or global");
	}

	// Snapshots

	public void StartSnapshot(string name)
	{
		if (Defer(() => StartSnapshot(name))) return;
		if (TryResolve(name, SoundKind.Snapshot, true, null, out EventDescription description, out _)) StartSnapshot(description);
	}

	public void StartSnapshot(EventReference snapshot)
	{
		if (Defer(() => StartSnapshot(snapshot))) return;
		if (TryResolve(snapshot, null, out EventDescription description)) StartSnapshot(description);
	}

	private void StartSnapshot(EventDescription description)
	{
		description.getPath(out string path);
		if (m_snapshots.TryGetValue(path, out OwnedSound existing) && IsActive(existing.instance)) return;
		if (existing != null) Release(existing, STOP_MODE.IMMEDIATE);

		if (description.createInstance(out EventInstance instance) != FMOD.RESULT.OK) return;
		description.getID(out FMOD.GUID guid);
		instance.start();
		m_snapshots[path] = new OwnedSound { path = path, guid = guid, description = description, instance = instance };
		Record(path, "snapshot on");
	}

	public void StopSnapshot(string name, bool fadeOut = true)
	{
		if (Defer(() => StopSnapshot(name, fadeOut))) return;
		if (TryResolve(name, SoundKind.Snapshot, true, null, out _, out string path)) StopSnapshotByPath(path, fadeOut);
	}

	public void StopSnapshot(EventReference snapshot, bool fadeOut = true)
	{
		if (Defer(() => StopSnapshot(snapshot, fadeOut))) return;
		if (!TryResolve(snapshot, null, out EventDescription description)) return;
		description.getPath(out string path);
		StopSnapshotByPath(path, fadeOut);
	}

	private void StopSnapshotByPath(string path, bool fadeOut)
	{
		if (!m_snapshots.TryGetValue(path, out OwnedSound snapshot)) return;
		Release(snapshot, fadeOut ? STOP_MODE.ALLOWFADEOUT : STOP_MODE.IMMEDIATE);
		m_snapshots.Remove(path);
		Record(path, "snapshot off");
	}

	public bool IsSnapshotActive(string name)
	{
		if (!m_ready || !TryResolve(name, SoundKind.Snapshot, false, null, out _, out string path)) return false;
		return m_snapshots.TryGetValue(path, out OwnedSound snapshot) && IsActive(snapshot.instance);
	}

	// Sound lookup

	private void BuildCatalog()
	{
		m_eventsByPath.Clear();
		m_bankByPath.Clear();
		m_eventNames.Clear();
		m_snapshotNames.Clear();

		RuntimeManager.StudioSystem.getBankList(out Bank[] banks);
		m_catalogBankCount = banks?.Length ?? 0;
		if (banks == null) return;

		foreach (Bank bank in banks)
		{
			bank.getPath(out string bankPath);
			string bankName = string.IsNullOrEmpty(bankPath) ? "" : bankPath.Split('/')[^1];
			if (banksToIgnore != null && banksToIgnore.Contains(bankName)) continue;
			if (bank.getEventList(out EventDescription[] events) != FMOD.RESULT.OK) continue;

			foreach (EventDescription description in events)
			{
				if (description.getPath(out string path) != FMOD.RESULT.OK || string.IsNullOrEmpty(path)) continue;
				if (m_eventsByPath.ContainsKey(path)) continue;

				m_eventsByPath[path] = description;
				m_bankByPath[path] = bankName;

				Dictionary<string, List<string>> names = path.StartsWith("snapshot:") ? m_snapshotNames : m_eventNames;
				string name = path.Substring(path.LastIndexOf('/') + 1);
				if (!names.TryGetValue(name, out List<string> paths)) names[name] = paths = new List<string>();
				paths.Add(path);
			}
		}
	}

	private bool RefreshCatalogIfBanksChanged()
	{
		RuntimeManager.StudioSystem.getBankCount(out int count);
		if (count == m_catalogBankCount) return false;
		BuildCatalog();
		return true;
	}

	// takes "Jump", "event:/SFX/Jump" or a guid string
	private bool TryResolve(string id, SoundKind kind, bool warn, UnityEngine.Object context, out EventDescription description, out string path)
	{
		description = default;
		path = null;

		if (string.IsNullOrWhiteSpace(id))
		{
			if (warn) Warn("empty-name", "[Audio] Tried to play a sound with an empty name", context);
			return false;
		}

		id = id.Trim();

		if (id.StartsWith("{"))
		{
			if (FMOD.Studio.Util.parseID(id, out FMOD.GUID guid) == FMOD.RESULT.OK &&
			    RuntimeManager.StudioSystem.getEventByID(guid, out description) == FMOD.RESULT.OK)
			{
				description.getPath(out path);
				return true;
			}
		}
		else if (id.Contains(":/"))
		{
			if ((m_eventsByPath.TryGetValue(id, out description) && description.isValid()) ||
			    (RuntimeManager.StudioSystem.getEvent(id, out description) == FMOD.RESULT.OK && description.isValid()))
			{
				description.getPath(out path);
				return true;
			}
		}
		else
		{
			if (TryResolveName(id, kind, warn, out description, out path)) return true;
			if (RefreshCatalogIfBanksChanged() && TryResolveName(id, kind, warn, out description, out path)) return true;
		}

		if (warn) WarnMissing(id, kind, context);
		return false;
	}

	private bool TryResolveName(string name, SoundKind kind, bool warn, out EventDescription description, out string path)
	{
		description = default;
		path = null;

		Dictionary<string, List<string>> names = kind == SoundKind.Snapshot ? m_snapshotNames : m_eventNames;
		if (!names.TryGetValue(name, out List<string> paths) || paths.Count == 0) return false;

		if (paths.Count > 1 && warn)
		{
			Warn("ambiguous:" + name,
				$"[Audio] More than one {(kind == SoundKind.Snapshot ? "snapshot" : "event")} called \"{name}\" " +
				$"({string.Join(", ", paths)}), using {paths[0]}. Use the full path if that's the wrong one");
		}

		path = paths[0];
		return m_eventsByPath.TryGetValue(path, out description) && description.isValid();
	}

	private bool TryResolve(EventReference sound, UnityEngine.Object context, out EventDescription description)
	{
		description = default;

		if (sound.IsNull)
		{
			Warn("no-sound:" + (context != null ? context.GetInstanceID() : 0), $"[Audio] {DescribeContext(context)} has no sound set", context);
			return false;
		}

		if (RuntimeManager.StudioSystem.getEventByID(sound.Guid, out description) == FMOD.RESULT.OK && description.isValid()) return true;

		RefreshCatalogIfBanksChanged();
		if (RuntimeManager.StudioSystem.getEventByID(sound.Guid, out description) == FMOD.RESULT.OK && description.isValid()) return true;

#if UNITY_EDITOR
		string name = string.IsNullOrEmpty(sound.Path) ? sound.Guid.ToString() : sound.Path;
#else
		string name = sound.Guid.ToString();
#endif
		Warn("missing-ref:" + sound.Guid,
			$"[Audio] {DescribeContext(context)} uses {name} which isn't in the banks - renamed/deleted in FMOD or banks need rebuilding", context);
		Record(name, "not found");
		return false;
	}

	// for components - the warning links back to the component that asked
	public bool TryGetDescription(EventReference sound, UnityEngine.Object context, out EventDescription description)
	{
		description = default;
		return m_ready && TryResolve(sound, context, out description);
	}

	public bool TryGetDescription(string id, UnityEngine.Object context, out EventDescription description)
	{
		description = default;
		return m_ready && TryResolve(id, SoundKind.Event, true, context, out description, out _);
	}

	private static string DescribeContext(UnityEngine.Object context)
	{
		if (context is Component component) return $"\"{component.gameObject.name}\" ({component.GetType().Name})";
		return context != null ? $"\"{context.name}\"" : "Something";
	}

	private void WarnMissing(string id, SoundKind kind, UnityEngine.Object context)
	{
		Record(id, "not found");

		SoundLibrary library = SoundLibrary.Instance;
		if (library != null && !library.warnAboutMissingSounds) return;

		Dictionary<string, List<string>> names = kind == SoundKind.Snapshot ? m_snapshotNames : m_eventNames;
		string suggestion = ClosestName(id, names.Keys);
		string hint = suggestion != null ? $" Did you mean \"{suggestion}\"?" : " (if it's new the banks might need building)";
		string noun = kind == SoundKind.Snapshot ? "snapshot" : "sound";

		Warn("missing:" + id, $"[Audio] No FMOD {noun} called \"{id}\".{hint}", context);
	}

	private static string ClosestName(string name, IEnumerable<string> candidates)
	{
		string best = null;
		int bestDistance = Mathf.Max(2, name.Length / 3) + 1;

		foreach (string candidate in candidates)
		{
			int distance = EditDistance(name.ToLowerInvariant(), candidate.ToLowerInvariant());
			if (distance < bestDistance)
			{
				best = candidate;
				bestDistance = distance;
			}
		}
		return best;
	}

	private static int EditDistance(string a, string b)
	{
		int[] previous = new int[b.Length + 1];
		int[] current = new int[b.Length + 1];
		for (int j = 0; j <= b.Length; j++) previous[j] = j;

		for (int i = 1; i <= a.Length; i++)
		{
			current[0] = i;
			for (int j = 1; j <= b.Length; j++)
			{
				int cost = a[i - 1] == b[j - 1] ? 0 : 1;
				current[j] = Mathf.Min(Mathf.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
			}
			(previous, current) = (current, previous);
		}
		return previous[b.Length];
	}

	private void Warn(string key, string message, UnityEngine.Object context = null)
	{
		if (m_warned.Add(key)) Debug.LogWarning(message, context);
	}

	private void Record(string path, string note)
	{
		if (m_log.Count >= MaxLogEntries) m_log.RemoveAt(0);
		m_log.Add(new SoundLogEntry { time = Time.unscaledTime, path = path, note = note });

		SoundLibrary library = SoundLibrary.Instance;
		if (library != null && library.logEverySound)
			Debug.Log(note == null ? $"[Audio] Playing {path}" : $"[Audio] {path} - {note}");
	}

	// Buses

	public void SetMasterVolume(float level) => SetBusVolume(SONIC_AUDIO_BUS.Master, SONIC_VOLUME_PREFS.Master, level);
	public void SetSFXVolume(float level) => SetBusVolume(SONIC_AUDIO_BUS.SFX, SONIC_VOLUME_PREFS.SFX, level);
	public void SetMusicVolume(float level) => SetBusVolume(SONIC_AUDIO_BUS.Music, SONIC_VOLUME_PREFS.Music, level);
	public void SetAmbienceVolume(float level) => SetBusVolume(SONIC_AUDIO_BUS.Ambience, SONIC_VOLUME_PREFS.Ambience, level);

	private void SetBusVolume(string busPath, string prefKey, float level)
	{
		PlayerPrefs.SetFloat(prefKey, level);
		PlayerPrefs.Save();

		if (Defer(() => SetBusVolume(busPath, prefKey, level))) return;
		if (TryGetBus(busPath, out Bus bus)) bus.setVolume(level);
	}

	// Re-applies the saved slider levels to the FMOD buses - called once when this singleton is created,
	// so the saved settings take effect immediately on boot rather than only once a slider is next touched.
	private void ApplySavedVolumes()
	{
		if (TryGetBus(SONIC_AUDIO_BUS.Master, out Bus master)) master.setVolume(RAudio.GetMasterVolume());
		if (TryGetBus(SONIC_AUDIO_BUS.SFX, out Bus sfx)) sfx.setVolume(RAudio.GetSFXVolume());
		if (TryGetBus(SONIC_AUDIO_BUS.Music, out Bus music)) music.setVolume(RAudio.GetMusicVolume());
		if (TryGetBus(SONIC_AUDIO_BUS.Ambience, out Bus ambience)) ambience.setVolume(RAudio.GetAmbienceVolume());
	}

	public void SetBusMuted(string busPath, bool muted)
	{
		if (Defer(() => SetBusMuted(busPath, muted))) return;
		if (TryGetBus(busPath, out Bus bus)) bus.setMute(muted);
	}

	public void SetGlobalPause(bool paused)
	{
		if (Defer(() => SetGlobalPause(paused))) return;

		SoundLibrary library = SoundLibrary.Instance;
		string[] buses = library != null ? library.pauseBuses ?? Array.Empty<string>() : new[] { SONIC_AUDIO_BUS.Master };
		foreach (string busPath in buses)
		{
			if (TryGetBus(busPath, out Bus bus)) bus.setPaused(paused);
		}

		if (library != null && !library.pauseSnapshot.IsNull)
		{
			if (paused) StartSnapshot(library.pauseSnapshot);
			else StopSnapshot(library.pauseSnapshot);
		}

		IsGamePaused = paused;
	}

	// for the live tab
	public List<string> GetBusPaths()
	{
		List<string> paths = new List<string>();
		if (!m_ready) return paths;

		RuntimeManager.StudioSystem.getBankList(out Bank[] banks);
		foreach (Bank bank in banks ?? Array.Empty<Bank>())
		{
			if (bank.getBusList(out Bus[] buses) != FMOD.RESULT.OK) continue;
			foreach (Bus bus in buses)
			{
				if (bus.getPath(out string path) == FMOD.RESULT.OK && !paths.Contains(path)) paths.Add(path);
			}
		}
		paths.Sort(StringComparer.OrdinalIgnoreCase);
		return paths;
	}

	// warns once instead of throwing like RuntimeManager.GetBus does
	public bool TryGetBus(string busPath, out Bus bus)
	{
		bus = default;
		if (string.IsNullOrEmpty(busPath)) return false;

		if (busPath == SONIC_AUDIO_BUS.Master)
		{
			bus = ResolveMasterBus();
			return bus.isValid();
		}

		if (RuntimeManager.StudioSystem.getBus(busPath, out bus) == FMOD.RESULT.OK && bus.isValid()) return true;

		Warn("bus:" + busPath, $"[Audio] No bus called \"{busPath}\" (should look like bus:/SFX)");
		return false;
	}

	// FMOD's own Unity integration treats System::getBus("bus:/") returning
	// ERR_EVENT_NOTFOUND as a known, benign quirk of looking up the root bus
	// by path (see RuntimeManager's ERROR_CALLBACK filter). Fetch the root bus
	// from the Master bank's own bus list instead, which works.
	private Bus ResolveMasterBus()
	{
		if (m_hasMasterBus && m_masterBus.isValid()) return m_masterBus;

		RuntimeManager.StudioSystem.getBankList(out Bank[] banks);
		foreach (Bank bank in banks ?? Array.Empty<Bank>())
		{
			bank.getPath(out string bankPath);
			if (bankPath != "bank:/Master") continue;

			// The Master bank's bus list holds every bus in the mixer (not just the root),
			// so match on path rather than assuming the root bus is first.
			bank.getBusList(out Bus[] buses);
			foreach (Bus bus in buses)
			{
				bus.getPath(out string candidatePath);
				if (candidatePath != SONIC_AUDIO_BUS.Master) continue;

				m_masterBus = bus;
				m_hasMasterBus = true;
				break;
			}
			break;
		}

		return m_masterBus;
	}

	// Listener

	// no listener in the scene = use our own that follows the main camera
	private void EnsureFallbackListener()
	{
		if (m_fallbackListener != null) return;

		GameObject listener = new GameObject("Sound Listener (automatic)");
		listener.transform.SetParent(transform, false);
		m_fallbackListener = listener.AddComponent<SoundListener2D>();
		m_fallbackListener.followMainCamera = true;
	}

	private void UpdateFallbackListener()
	{
		if (m_fallbackListener == null) return;

		int ours = m_fallbackListener.gameObject.activeInHierarchy ? 1 : 0;
		bool sceneHasListener = StudioListener.ListenerCount - ours > 0;
		if (m_fallbackListener.gameObject.activeSelf == sceneHasListener) m_fallbackListener.gameObject.SetActive(!sceneHasListener);
	}

	public bool UsingAutomaticListener => m_fallbackListener != null && m_fallbackListener.gameObject.activeInHierarchy;

	// Debugging

	public List<ActiveSound> GetActiveSounds()
	{
		List<ActiveSound> result = new List<ActiveSound>();
		AddActive(result, "Music", m_music);
		AddActive(result, "Ambience", m_ambience);
		foreach (OwnedSound loop in m_loops.Values) AddActive(result, "Loop", loop);
		foreach (OwnedSound snapshot in m_snapshots.Values) AddActive(result, "Snapshot", snapshot);
		return result;
	}

	private static void AddActive(List<ActiveSound> result, string role, OwnedSound sound)
	{
		if (!sound.instance.isValid()) return;
		sound.instance.getPlaybackState(out PLAYBACK_STATE state);
		if (state == PLAYBACK_STATE.STOPPED) return;
		result.Add(new ActiveSound { role = role, path = sound.path, state = state });
	}

	public void StopActiveSound(string role, string path)
	{
		switch (role)
		{
			case "Music": StopMusic(true); break;
			case "Ambience": StopAmbience(true); break;
			case "Snapshot": StopSnapshotByPath(path, true); break;
			default: Stop(path, STOP_MODE.ALLOWFADEOUT); break;
		}
	}
}
