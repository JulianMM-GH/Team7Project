using System.Collections.Generic;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;
using UnityEngine.Events;
using STOP_MODE = FMOD.Studio.STOP_MODE;

/// <summary>
/// Trigger area that plays a loop, turns on a snapshot and/or sets a global param while the player is inside
/// </summary>
[RequireComponent(typeof(Collider2D))]
[AddComponentMenu("Audio/Sound Zone")]
public class SoundZone : MonoBehaviour
{
	public enum LoopPosition
	{
		Everywhere,
		AtThisObject, // gets louder towards the middle, needs a 3D event
	}

	[Tooltip("Leave empty to react to anything")]
	[SerializeField] private string onlyReactToTag = "Player";

	[Header("While inside")]
	[SerializeField] private EventReference loopWhileInside;
	[SerializeField] private LoopPosition loopPosition = LoopPosition.Everywhere;
	[SerializeField] private EventReference snapshotWhileInside;
	[Tooltip("Leave empty for none")]
	[SerializeField] private string globalParameter;
	[SerializeField] private float valueInside = 1f;
	[SerializeField] private float valueOutside;

	[Header("On the way in and out")]
	[SerializeField] private EventReference playOnEnter;
	[SerializeField] private EventReference playOnExit;
	[Tooltip("Fade the loop/snapshot out on exit instead of cutting them off")]
	[SerializeField] private bool fadeOut = true;

	[Header("Events")]
	[SerializeField] private UnityEvent onEnter = new UnityEvent();
	[SerializeField] private UnityEvent onExit = new UnityEvent();

	private readonly HashSet<Collider2D> m_inside = new HashSet<Collider2D>();
	private EventInstance m_loop;
	private EventInstance m_snapshot;

	public bool IsOccupied => m_inside.Count > 0;

	private void OnTriggerEnter2D(Collider2D other)
	{
		if (!Reacts(other)) return;

		m_inside.RemoveWhere(c => c == null || !c.isActiveAndEnabled);
		bool wasEmpty = m_inside.Count == 0;
		m_inside.Add(other);

		if (wasEmpty) Enter();
	}

	private void OnTriggerExit2D(Collider2D other)
	{
		if (!m_inside.Remove(other)) return;

		m_inside.RemoveWhere(c => c == null || !c.isActiveAndEnabled);
		if (m_inside.Count == 0) Exit(true);
	}

	private void OnDisable()
	{
		bool wasOccupied = m_inside.Count > 0;
		m_inside.Clear();
		if (wasOccupied) Exit(gameObject.scene.isLoaded);
	}

	private bool Reacts(Collider2D other) => string.IsNullOrEmpty(onlyReactToTag) || other.CompareTag(onlyReactToTag);

	private void Enter()
	{
		AudioManager manager = AudioManager.Manager;
		if (manager != null)
		{
			if (!playOnEnter.IsNull) RAudio.PlayOneShot(playOnEnter, transform.position);

			if (!loopWhileInside.IsNull && manager.TryGetDescription(loopWhileInside, this, out EventDescription loop))
			{
				SoundPlacement placement = loopPosition == LoopPosition.AtThisObject ? SoundPlacement.FollowObject : SoundPlacement.AtListener;
				m_loop = manager.StartSound(loop, placement, transform.position, transform, release: false);
			}

			// each zone gets its own snapshot instance so overlapping zones don't cancel each other out
			if (!snapshotWhileInside.IsNull && manager.TryGetDescription(snapshotWhileInside, this, out EventDescription snapshot) &&
			    snapshot.createInstance(out m_snapshot) == FMOD.RESULT.OK)
			{
				m_snapshot.start();
			}

			if (!string.IsNullOrEmpty(globalParameter)) RAudio.GSetValueParam(globalParameter, valueInside);
		}

		onEnter.Invoke();
	}

	// sceneStillRunning is false when the zone is getting unloaded with the scene
	private void Exit(bool sceneStillRunning)
	{
		StopAndRelease(ref m_loop);
		StopAndRelease(ref m_snapshot);

		if (AudioManager.Manager != null)
		{
			if (!string.IsNullOrEmpty(globalParameter)) RAudio.GSetValueParam(globalParameter, valueOutside);
			if (sceneStillRunning && !playOnExit.IsNull) RAudio.PlayOneShot(playOnExit, transform.position);
		}

		if (sceneStillRunning) onExit.Invoke();
	}

	private void StopAndRelease(ref EventInstance instance)
	{
		if (!instance.isValid()) return;
		instance.stop(fadeOut ? STOP_MODE.ALLOWFADEOUT : STOP_MODE.IMMEDIATE);
		instance.release();
		instance.clearHandle();
	}

	private void OnDrawGizmos()
	{
		Collider2D area = GetComponent<Collider2D>();
		if (area == null) return;

		Gizmos.color = IsOccupied ? new Color(0.3f, 1f, 0.5f, 0.25f) : new Color(0.3f, 0.7f, 1f, 0.12f);
		Bounds bounds = area.bounds;
		Gizmos.DrawCube(bounds.center, bounds.size);
	}
}
