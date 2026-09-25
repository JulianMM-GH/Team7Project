using System.Collections.Generic;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;
using STOP_MODE = FMOD.Studio.STOP_MODE;

/// <summary>
/// Plays an FMOD event from this object. One shots can overlap, loops only ever have one instance
/// </summary>
[AddComponentMenu("Audio/Sound Emitter")]
public class SoundEmitter : MonoBehaviour
{
	public enum PlayWhen
	{
		Manually,
		SceneStarts,
		ObjectEnabled,
		SomethingEntersTrigger,
		SomethingLeavesTrigger,
		SomethingCollides,
		ObjectDisabled,  // not when the scene unloads
		ObjectDestroyed, // not when the scene unloads
	}

	public enum StopWhen
	{
		Manually,
		ObjectDisabled,
		EverythingLeavesTrigger,
	}

	public enum Position
	{
		FollowThisObject,
		StayWhereItStarted,
		NoPosition,
	}

	[SerializeField] private EventReference sound;
	[SerializeField] private PlayWhen playWhen = PlayWhen.SceneStarts;
	[Tooltip("Mostly matters for loops, one shots stop on their own")]
	[SerializeField] private StopWhen stopWhen = StopWhen.ObjectDisabled;
	[Tooltip("Leave empty to react to anything")]
	[SerializeField] private string onlyReactToTag = "Player";
	[SerializeField] private Position position = Position.FollowThisObject;

	[Tooltip("Uses the event's AHDSR release instead of cutting off")]
	[SerializeField] private bool fadeOutWhenStopped = true;
	[SerializeField] private bool playOnlyOnce;
	[Tooltip("Loops only")]
	[SerializeField] private bool restartIfAlreadyPlaying;
	[SerializeField] private List<SoundParameter> parameters = new List<SoundParameter>();

	private readonly List<EventInstance> m_instances = new List<EventInstance>();
	private int m_lastPlayFrame = -1;
	private int m_occupants;
	private bool m_hasPlayed;
	private bool m_started;

	public EventReference Sound
	{
		get => sound;
		set => sound = value;
	}

	public bool IsPlaying
	{
		get
		{
			PruneFinished();
			return m_instances.Count > 0;
		}
	}

	// Triggers

	private void Start()
	{
		m_started = true;
		if (playWhen == PlayWhen.SceneStarts || playWhen == PlayWhen.ObjectEnabled) Play();
	}

	private void OnEnable()
	{
		// Start handles the first enable so it doesn't play twice on spawn
		if (m_started && playWhen == PlayWhen.ObjectEnabled) Play();
	}

	private void OnDisable()
	{
		m_occupants = 0;
		if (stopWhen == StopWhen.ObjectDisabled) StopInternal(fadeOutWhenStopped);
		if (!IsSceneUnloading && playWhen == PlayWhen.ObjectDisabled) Play();
	}

	private void OnDestroy()
	{
		if (!IsSceneUnloading && playWhen == PlayWhen.ObjectDestroyed) Play();
		// one shots can finish, but don't leave loops running with nothing to stop them
		ReleaseAndStopLoops();
	}

	private void OnTriggerEnter2D(Collider2D other)
	{
		if (!Reacts(other.gameObject)) return;

		m_occupants++;
		if (playWhen == PlayWhen.SomethingEntersTrigger) Play();
	}

	private void OnTriggerExit2D(Collider2D other)
	{
		if (!Reacts(other.gameObject)) return;

		m_occupants = Mathf.Max(0, m_occupants - 1);
		if (playWhen == PlayWhen.SomethingLeavesTrigger) Play();
		if (stopWhen == StopWhen.EverythingLeavesTrigger && m_occupants == 0) Stop();
	}

	private void OnCollisionEnter2D(Collision2D collision)
	{
		if (playWhen == PlayWhen.SomethingCollides && Reacts(collision.gameObject)) Play();
	}

	private bool Reacts(GameObject other) => string.IsNullOrEmpty(onlyReactToTag) || other.CompareTag(onlyReactToTag);

	private bool IsSceneUnloading => !gameObject.scene.isLoaded;

	// Public controls

	public void Play()
	{
		if (playOnlyOnce && m_hasPlayed) return;

		AudioManager manager = AudioManager.Manager;
		if (manager == null || !manager.TryGetDescription(sound, this, out EventDescription description)) return;

		description.isOneshot(out bool isOneShot);
		PruneFinished();

		if (!isOneShot && m_instances.Count > 0)
		{
			if (!restartIfAlreadyPlaying) return;
			StopInternal(false);
		}

		SoundPlacement placement = position == Position.NoPosition ? SoundPlacement.AtListener
			: position == Position.FollowThisObject ? SoundPlacement.FollowObject
			: SoundPlacement.AtPosition;

		EventInstance instance = manager.StartSound(description, placement, transform.position, transform, parameters, release: false);
		if (!instance.isValid()) return;

		// released instances still play (and can still be stopped), FMOD cleans them up after
		if (isOneShot) instance.release();

		m_instances.Add(instance);
		m_lastPlayFrame = Time.frameCount;
		m_hasPlayed = true;
	}

	public void Stop() => StopInternal(fadeOutWhenStopped);

	public void StopImmediately() => StopInternal(false);

	public void TogglePlay()
	{
		if (IsPlaying) Stop();
		else Play();
	}

	// also remembered for next time Play gets called
	public void SetParameter(string parameterName, float value)
	{
		SoundParameter stored = Remember(parameterName);
		stored.value = value;
		stored.label = null;
		foreach (EventInstance instance in m_instances) stored.ApplyTo(instance);
	}

	public void SetParameterLabel(string parameterName, string label)
	{
		SoundParameter stored = Remember(parameterName);
		stored.label = label;
		foreach (EventInstance instance in m_instances) stored.ApplyTo(instance);
	}

	private SoundParameter Remember(string parameterName)
	{
		SoundParameter stored = parameters.Find(p => p.name == parameterName);
		if (stored == null)
		{
			stored = new SoundParameter { name = parameterName };
			parameters.Add(stored);
		}
		return stored;
	}

	private void StopInternal(bool fadeOut)
	{
		foreach (EventInstance instance in m_instances)
		{
			if (!instance.isValid()) continue;
			instance.stop(fadeOut ? STOP_MODE.ALLOWFADEOUT : STOP_MODE.IMMEDIATE);
			instance.release();
		}
		m_instances.Clear();
	}

	private void ReleaseAndStopLoops()
	{
		foreach (EventInstance instance in m_instances)
		{
			if (!instance.isValid()) continue;
			instance.getDescription(out EventDescription description);
			description.isOneshot(out bool isOneShot);
			if (!isOneShot) instance.stop(fadeOutWhenStopped ? STOP_MODE.ALLOWFADEOUT : STOP_MODE.IMMEDIATE);
			instance.release();
		}
		m_instances.Clear();
	}

	private void PruneFinished()
	{
		// something started this frame can still say STOPPED until FMOD updates
		if (m_lastPlayFrame >= Time.frameCount - 1) return;

		for (int i = m_instances.Count - 1; i >= 0; i--)
		{
			EventInstance instance = m_instances[i];
			if (instance.isValid())
			{
				instance.getPlaybackState(out PLAYBACK_STATE state);
				if (state != PLAYBACK_STATE.STOPPED) continue;
				instance.release();
			}
			m_instances.RemoveAt(i);
		}
	}
}
