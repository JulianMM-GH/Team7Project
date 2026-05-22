using UnityEngine;
using UnityEngine.Video;
using UnityEngine.UI;

public class DestroyVideo : MonoBehaviour
{
    [SerializeField] public GameObject skipButtonObject;

    private VideoPlayer videoPlayer;

    void Start()
    {
        videoPlayer = GetComponent<VideoPlayer>();
        videoPlayer.loopPointReached += EndReached;
    }

    void EndReached(VideoPlayer vp)
    {
        EndVideo();
    }

    public void SkipVideo()
    {
        EndVideo();
    }

    private void EndVideo()
    {
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= EndReached;
            videoPlayer.Stop();
        }

        if (skipButtonObject != null)
        {
            Destroy(skipButtonObject);
        }

        Destroy(gameObject);
    }
}