using UnityEngine;

public class PlatformAwake : MonoBehaviour
{
    private void Awake()
    {
        if (TryGetComponent<BoxCollider2D>(out BoxCollider2D boxCollider))
        {
            boxCollider.enabled = false;
        }
    }
}