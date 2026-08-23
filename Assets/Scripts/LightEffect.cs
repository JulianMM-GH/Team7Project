using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class LightEffect : MonoBehaviour
{
    [SerializeField] private PlayerInput playerInput;

    private InputAction lightAction;

    [SerializeField] public bool canLight = false;

    public float rotationSpeed = 100f;
    public float scaleSpeed = 5f;
    public float minScale = 1f;
    public float maxScale = 3f;
    private float ChargePower = 0f;
    public float MaxChargePower = 100f;

    public float animationSpeed = 10f;
    public float lightRadius = 0.5f;
    public float hideDelay = 0.15f;
    public GameObject MaskObj;
    private Vector2 MaskPos;


    public Sprite[] lightFrames;
    public Sprite[] emptyFrames;
    public LayerMask affectedLayers;
    private bool lightEnabled;

    SpriteMask spriteMask;
    SpriteRenderer spriteRenderer;

    List<Collider2D> litObjects = new List<Collider2D>();
    List<(SpriteRenderer sprite, Collider2D collider)> lightableObjects = new List<(SpriteRenderer, Collider2D)>();

    int currentFrame;
    float animationTimer;
    float hideTimer;

    void Awake()
    {
        ColliderToTrigger();

        spriteMask = GetComponent<SpriteMask>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        transform.localScale = Vector3.one * minScale;
    }

    void Start()
    {
        MaskPos = MaskObj.transform.localPosition;
        
        // Find the PlayerInput component on the parent
        playerInput = GetComponentInParent<PlayerInput>();

        if (playerInput != null)
        {
            // Extract the reference for the "Light" command
            lightAction = playerInput.actions["Light"];
        }
        else
        {
            Debug.LogError("Cannot find a PlayerInput component");
        }

        CacheLightableObjects();
    }

    // Scans the scene once instead of every frame - was causing stutter while lighting was active
    void CacheLightableObjects()
    {
        SpriteRenderer[] allSprites = FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);

        foreach (SpriteRenderer sprite in allSprites)
        {
            if (sprite == spriteRenderer) continue;
            if ((affectedLayers & (1 << sprite.gameObject.layer)) == 0) continue;
            if (!sprite.TryGetComponent(out Collider2D objectCollider)) continue;

            lightableObjects.Add((sprite, objectCollider));
        }
    }

    void OnEnable()
    {
        ColliderToTrigger();
    }

    void ColliderToTrigger()
    {
        Collider2D[] ownColliders = GetComponentsInChildren<Collider2D>(true);

        foreach (Collider2D col in ownColliders)
        {
            if (col != null)
                col.isTrigger = true;
        }
    }



    void Update()
    {
        MaskObj.transform.localPosition = Vector2.Lerp(Vector2.zero, MaskPos, ChargePower / MaxChargePower);

        if (lightAction == null)
            return;

        if (lightAction.WasPressedThisFrame())
            lightEnabled = !lightEnabled;

        if (lightEnabled)
        {
            ChargePower -= (float)(0.5 * Time.deltaTime);
            if (ChargePower <= 0f)
            {
                ChargePower = 0f;
                lightEnabled = false; 
            }
        }
        else
        {
            ChargePower += (float)(1 * Time.deltaTime);
            if (ChargePower > MaxChargePower)
                ChargePower = MaxChargePower;
        }

        bool lightIsActive = lightEnabled && ChargePower > 0f;

        var sr = MaskObj.transform.parent.GetComponent<SpriteRenderer>();
        sr.enabled = lightIsActive || ChargePower < MaxChargePower;
        sr = MaskObj.transform.parent.GetChild(1).GetComponent<SpriteRenderer>();
        sr.enabled = lightIsActive || ChargePower < MaxChargePower;

        RotateLight();
        ResizeLight(lightIsActive);
        UpdateVisibility(lightIsActive);

        TurnOffOldLitObjects();

        if (!spriteRenderer.enabled)
            return;

        AnimateLight();

        if (lightIsActive)
            LightObjectsNearby();
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

    void LightObjectsNearby()
    {
        float radius = lightRadius * transform.localScale.x;
        Vector2 lightPosition = transform.position;

        foreach (var (sprite, objectCollider) in lightableObjects)
        {
            if (sprite == null) continue;

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