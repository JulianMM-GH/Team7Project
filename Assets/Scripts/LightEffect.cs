using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class LightEffect : MonoBehaviour
{
    [SerializeField] public static PlayerInput PlayerInput;

    private InputAction lightAction;

    [SerializeField] public bool canLight = false;

    public float rotationSpeed = 100f;
    public float scaleSpeed = 5f;
    public float minScale = 1f;
    public float maxScale = 3f;

    public float animationSpeed = 10f;
    public float lightRadius = 0.5f;
    public float hideDelay = 0.15f;

    public Sprite[] lightFrames;
    public Sprite[] emptyFrames;
    public LayerMask affectedLayers;

    SpriteMask spriteMask;
    SpriteRenderer spriteRenderer;

    List<Collider2D> litObjects = new List<Collider2D>();

    int currentFrame;
    float animationTimer;
    float hideTimer;

    void Awake()
    {
        spriteMask = GetComponent<SpriteMask>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        transform.localScale = Vector3.one * minScale;

        PlayerInput = GetComponent<PlayerInput>();

        lightAction = PlayerInput.actions["Light"];
    }

    void Update()
    {
        bool isHoldingLight = lightAction.IsPressed();

        RotateLight();
        ResizeLight(isHoldingLight && canLight);
        UpdateVisibility(isHoldingLight && canLight);

        TurnOffOldLitObjects();

        if (!spriteRenderer.enabled)
            return;

        AnimateLight();
        LightObjectsNearby(isHoldingLight && canLight);
    }

    void RotateLight()
    {
        transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);
    }

    void ResizeLight(bool isHoldingLight)
    {
        float targetScale = isHoldingLight ? maxScale : minScale;

        transform.localScale = Vector3.Lerp(
            transform.localScale,
            Vector3.one * targetScale,
            scaleSpeed * Time.deltaTime
        );
    }

    void UpdateVisibility(bool isHoldingLight)
    {
        bool isStillShrinking = transform.localScale.x > minScale + 0.08f;

        if (isHoldingLight || isStillShrinking)
            hideTimer = 0f;
        else
            hideTimer += Time.deltaTime;

        bool shouldShow = isHoldingLight || isStillShrinking || hideTimer < hideDelay;

        spriteMask.enabled = shouldShow;
        spriteRenderer.enabled = shouldShow;
    }

    void TurnOffOldLitObjects()
    {
        foreach (Collider2D objectCollider in litObjects)
        {
            if (objectCollider != null)
                objectCollider.enabled = false;
        }

        litObjects.Clear();
    }

    void AnimateLight()
    {
        FadeLightInAndOut();
        ChangeAnimationFrame();
    }

    void FadeLightInAndOut()
    {
        Color color = spriteRenderer.color;
        color.a = Mathf.Lerp(0.3f, 1f, Mathf.Sin(Time.time * 2f) * 0.5f + 0.5f);
        spriteRenderer.color = color;
    }

    void ChangeAnimationFrame()
    {
        if (lightFrames.Length == 0)
            return;

        animationTimer += Time.deltaTime;

        if (animationTimer < 1f / animationSpeed)
            return;

        animationTimer = 0f;
        currentFrame = (currentFrame + 1) % lightFrames.Length;

        spriteMask.sprite = lightFrames[currentFrame];

        if (currentFrame < emptyFrames.Length)
            spriteRenderer.sprite = emptyFrames[currentFrame];
    }

    void LightObjectsNearby(bool isHoldingLight)
    {
        float radius = isHoldingLight
            ? lightRadius * transform.localScale.x
            : lightRadius * maxScale;

        Vector2 lightPosition = transform.position;

        SpriteRenderer[] sprites = FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);

        foreach (SpriteRenderer sprite in sprites)
        {
            if (sprite == spriteRenderer)
                continue;

            if ((affectedLayers & (1 << sprite.gameObject.layer)) == 0)
                continue;

            if (!sprite.TryGetComponent(out Collider2D objectCollider))
                continue;

            float distance = Vector2.Distance(
                lightPosition,
                sprite.bounds.ClosestPoint(lightPosition)
            );

            if (distance > radius)
                continue;

            objectCollider.enabled = true;
            litObjects.Add(objectCollider);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, lightRadius * transform.localScale.x);
    }
}