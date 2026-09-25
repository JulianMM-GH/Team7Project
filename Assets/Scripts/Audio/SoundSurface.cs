using UnityEngine;

/// <summary>
/// Marks ground as a surface type (Dirt, Wood, Water etc) for FootstepSounds
/// </summary>
[AddComponentMenu("Audio/Sound Surface")]
public class SoundSurface : MonoBehaviour
{
	[Tooltip("Needs to match a surface on Footstep Sounds (or a label on its surface parameter)")]
	public string surface = "Dirt";
}
