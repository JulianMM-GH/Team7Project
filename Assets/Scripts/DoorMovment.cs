using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;

public class DoorMovment : MonoBehaviour
{
    private Vector2 StartPosition;
    public float raiseHeight;
    public float raiseSpeed;
    private float releaseTimer;
    private bool isOpening;
    private bool hasPlayedParticles = false;


    void Start()
    {
        StartPosition = (Vector2)transform.localPosition;
    }

    public void OpenDoor()
    {
        isOpening = true;
        releaseTimer = 0.1f;
        hasPlayedParticles = false;

    }

    // Update is called once per frame
    void Update()
    {
        if (releaseTimer > 0f)
        {
            releaseTimer -= Time.deltaTime;
        }
        else
        {
            isOpening = false;
        }

        if (isOpening)
        {
            float d = Vector2.Distance(transform.localPosition, new Vector2(0, raiseHeight));
            transform.localPosition = Vector2.Lerp(transform.localPosition, new Vector2(0, raiseHeight), Time.deltaTime * raiseSpeed * Mathf.Lerp(0.15f, 1f, d / (d + 1f)));
        }
        else
        {
            float d = Vector2.Distance(transform.localPosition, StartPosition);
            float t = d / (d + 3f); 
            transform.localPosition = Vector2.Lerp(transform.localPosition, StartPosition, Time.deltaTime * raiseSpeed * Mathf.Lerp(30f, 4f, t * t));
            if (Vector2.Distance(transform.localPosition, StartPosition) < 0.5f && !hasPlayedParticles)
            {
                ParticleSystem[] Particles = GetComponentsInChildren<ParticleSystem>();
                foreach (ParticleSystem particle in Particles) {
                    particle.Play();
                }
                hasPlayedParticles = true;
            }
        }
    }
}
