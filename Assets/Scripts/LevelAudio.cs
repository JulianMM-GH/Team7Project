using UnityEngine;

/// <summary>
/// Drop on one GameObject per scene to start the looping FMOD music/ambience beds on scene load.
/// </summary>
public class LevelAudio : MonoBehaviour
{
    [Header("Play on scene start")]
    [SerializeField] private bool playMusic;
    [SerializeField] private bool playAmbience;
    [SerializeField] private bool playUIMusic;

    private void Start()
    {
        if (playMusic)
            RAudio.Play("Music");

        if (playAmbience)
            RAudio.Play("Ambience");

        if (playUIMusic)
            RAudio.Play("UI Music");
    }
}
