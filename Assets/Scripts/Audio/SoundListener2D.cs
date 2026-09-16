using FMODUnity;
using UnityEngine;

/// <summary>
/// Listener for 2D. Studio Listener sits on the camera which is ~10 units back from the level so everything sounds
/// further away than it is - this one uses the camera's x/y but the level's z
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Audio/Sound Listener 2D")]
public class SoundListener2D : MonoBehaviour
{
	public bool flattenDepth = true;
	[Tooltip("Z the level sits on")]
	public float levelDepth;

	// the AudioManager's fallback listener follows whatever Camera.main is
	[HideInInspector] public bool followMainCamera;

	private GameObject m_ears;

	private void OnEnable()
	{
		if (m_ears == null)
		{
			// actual StudioListener lives on a child that gets moved into place every frame
			m_ears = new GameObject("FMOD Listener (positioned by Sound Listener 2D)");
			m_ears.transform.SetParent(transform, false);
			m_ears.AddComponent<StudioListener>();
		}

		m_ears.SetActive(true);
		MoveEars();
	}

	private void OnDisable()
	{
		if (m_ears != null) m_ears.SetActive(false);
	}

	private void LateUpdate() => MoveEars();

	private void MoveEars()
	{
		Transform source = transform;
		if (followMainCamera)
		{
			Camera main = Camera.main;
			if (main == null) return;
			source = main.transform;
		}

		Vector3 position = source.position;
		if (flattenDepth) position.z = levelDepth;

		m_ears.transform.SetPositionAndRotation(position, Quaternion.identity);
	}
}
