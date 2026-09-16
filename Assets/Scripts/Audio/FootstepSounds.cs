using System;
using System.Collections.Generic;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;

/// <summary>
/// Footsteps that change based on the SoundSurface underneath. Call Footstep() from an animation event
/// or turn on stepAutomatically to use a timer instead
/// </summary>
[AddComponentMenu("Audio/Footstep Sounds")]
public class FootstepSounds : MonoBehaviour
{
	[Serializable]
	public class SurfaceSound
	{
		public string surface;
		public EventReference sound;
	}

	[Tooltip("Used when there's no SoundSurface or it isn't in the list")]
	[SerializeField] private EventReference defaultSound;
	[SerializeField] private List<SurfaceSound> surfaceSounds = new List<SurfaceSound>();
	[Tooltip("Instead of the list - labeled parameter on the default sound, gets set to the surface name")]
	[SerializeField] private string surfaceParameter;

	[Header("Ground detection")]
	[Tooltip("Uses this object's position if empty")]
	[SerializeField] private Transform feet;
	[SerializeField] private float groundCheckDistance = 0.4f;
	[SerializeField] private LayerMask groundLayers = ~0;
	[Tooltip("Skip steps when there's no ground (mid air frames in run animations etc)")]
	[SerializeField] private bool onlyWhenGrounded = true;

	[Header("Automatic steps (instead of Animation Events)")]
	[SerializeField] private bool stepAutomatically;
	[SerializeField] private float secondsBetweenSteps = 0.32f;
	[Tooltip("Min horizontal speed that counts as walking")]
	[SerializeField] private float minimumSpeed = 0.5f;
	[Tooltip("Finds one in parents if empty")]
	[SerializeField] private Rigidbody2D body;

	private readonly RaycastHit2D[] m_hits = new RaycastHit2D[8];
	private readonly SoundParameter[] m_surfaceLabel = { new SoundParameter() };
	private float m_stepTimer;

	public string CurrentSurface { get; private set; } = "";

	private void Awake()
	{
		if (body == null) body = GetComponentInParent<Rigidbody2D>();
	}

	private void Update()
	{
		if (!stepAutomatically || body == null) return;

		if (Mathf.Abs(body.linearVelocity.x) < minimumSpeed)
		{
			m_stepTimer = 0f; // so the first step plays straight away when you start moving
			return;
		}

		m_stepTimer -= Time.deltaTime;
		if (m_stepTimer > 0f) return;

		m_stepTimer = secondsBetweenSteps;
		Footstep();
	}

	public void Footstep()
	{
		bool grounded = FindSurface(out string surface);
		if (onlyWhenGrounded && !grounded) return;
		CurrentSurface = surface;

		EventReference sound = defaultSound;
		IList<SoundParameter> parameters = null;

		if (!string.IsNullOrEmpty(surfaceParameter))
		{
			if (!string.IsNullOrEmpty(surface))
			{
				m_surfaceLabel[0].name = surfaceParameter;
				m_surfaceLabel[0].label = surface;
				parameters = m_surfaceLabel;
			}
		}
		else
		{
			SurfaceSound match = surfaceSounds.Find(s => string.Equals(s.surface, surface, StringComparison.OrdinalIgnoreCase));
			if (match != null && !match.sound.IsNull) sound = match.sound;
		}

		AudioManager manager = AudioManager.Manager;
		if (manager == null || !manager.TryGetDescription(sound, this, out EventDescription description)) return;
		manager.StartSound(description, SoundPlacement.AtPosition, FeetPosition, null, parameters);
	}

	public void PlayFootstep() => Footstep();

	private Vector3 FeetPosition => feet != null ? feet.position : transform.position;

	private bool FindSurface(out string surface)
	{
		surface = "";

		ContactFilter2D filter = new ContactFilter2D { useTriggers = false, useLayerMask = true, layerMask = groundLayers };
		int count = Physics2D.Raycast(FeetPosition, Vector2.down, filter, m_hits, groundCheckDistance);

		for (int i = 0; i < count; i++)
		{
			// skip our own colliders
			Collider2D ground = m_hits[i].collider;
			if (body != null && ground.attachedRigidbody == body) continue;
			if (ground.transform.IsChildOf(transform)) continue;

			SoundSurface tag = ground.GetComponentInParent<SoundSurface>();
			if (tag != null) surface = tag.surface;
			return true;
		}
		return false;
	}

	private void OnDrawGizmosSelected()
	{
		Gizmos.color = Color.cyan;
		Vector3 start = FeetPosition;
		Gizmos.DrawLine(start, start + Vector3.down * groundCheckDistance);
	}
}
