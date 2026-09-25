using System.Collections.Generic;
using FMOD.Studio;
using UnityEngine;
using STOP_MODE = FMOD.Studio.STOP_MODE;

/// <summary>
/// Functions for animation events to play sounds by name. Needs to be on the same object as the Animator
/// </summary>
[AddComponentMenu("Audio/Animation Sounds")]
public class AnimationSounds : MonoBehaviour
{
	[Tooltip("Sounds follow this object while playing")]
	[SerializeField] private bool followThisObject = true;

	private readonly Dictionary<string, EventInstance> m_loops = new Dictionary<string, EventInstance>();

	public void PlaySound(string soundName)
	{
		if (!isActiveAndEnabled) return;

		AudioManager manager = AudioManager.Manager;
		if (manager == null || !manager.TryGetDescription(soundName, this, out EventDescription description)) return;
		manager.StartSound(description, Placement, transform.position, transform);
	}

	// same as PlaySound but ignores events from a clip that's blending out, stops sounds doubling up in transitions
	public void PlaySoundEvent(AnimationEvent animationEvent)
	{
		if (animationEvent.animatorClipInfo.clip != null && animationEvent.animatorClipInfo.weight < 0.5f) return;
		PlaySound(animationEvent.stringParameter);
	}

	public void StartLoop(string soundName)
	{
		if (!isActiveAndEnabled) return;

		if (m_loops.TryGetValue(soundName, out EventInstance existing) && existing.isValid())
		{
			existing.getPlaybackState(out PLAYBACK_STATE state);
			if (state != PLAYBACK_STATE.STOPPED && state != PLAYBACK_STATE.STOPPING) return;
			existing.release();
		}

		AudioManager manager = AudioManager.Manager;
		if (manager == null || !manager.TryGetDescription(soundName, this, out EventDescription description)) return;

		EventInstance instance = manager.StartSound(description, Placement, transform.position, transform, release: false);
		if (instance.isValid()) m_loops[soundName] = instance;
	}

	public void StopLoop(string soundName)
	{
		if (!m_loops.TryGetValue(soundName, out EventInstance instance)) return;
		StopAndRelease(instance);
		m_loops.Remove(soundName);
	}

	public void StopAllLoops()
	{
		foreach (EventInstance instance in m_loops.Values) StopAndRelease(instance);
		m_loops.Clear();
	}

	private void OnDisable() => StopAllLoops();

	private SoundPlacement Placement => followThisObject ? SoundPlacement.FollowObject : SoundPlacement.AtPosition;

	private static void StopAndRelease(EventInstance instance)
	{
		if (!instance.isValid()) return;
		instance.stop(STOP_MODE.ALLOWFADEOUT);
		instance.release();
	}
}
