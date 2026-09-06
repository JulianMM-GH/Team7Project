using UnityEngine;

/// <summary>
/// Shows the icon for one PromptAction, matching whichever device its target player is
/// currently using. Attach directly to the icon object (needs a SpriteRenderer). The target
/// player is found automatically via the nearest UIPlayerFollow up the hierarchy (for head
/// prompts that follow a specific player around) - or set explicitly with SetTargetPlayer for
/// icons that aren't under one, like a world-space interact prompt that already knows exactly
/// which player triggered it.
///
/// If followTargetPlayer is on, the icon repositions itself above the target player every frame
/// (world-space, ignoring its own parent) instead of relying on a moving parent to carry it -
/// this is what makes a ControlPromptTrigger's icon show up over the player's head rather than
/// sitting still at the trigger's own location. Leave it off for icons that are already carried
/// by a moving parent (a head-prompt canvas) or that are meant to stay put (an item pickup
/// prompt sitting at the item's position).
///
/// Every icon renders at the same world-space size (iconWorldSize) no matter what resolution its
/// source art is or how its parent hierarchy is scaled - each icon set entry can come from a
/// completely different pack without ever needing a matching import setting. The icon's rotation
/// is likewise locked to whatever it was at Awake, so it stays upright even if its parent spins
/// (e.g. a slowly rotating pickup).
///
/// Visibility defaults to fully shown the moment it has a sprite, matching plain SetActive-driven
/// callers (e.g. PlayerHeadPrompts). Callers that want a fade - like ControlPromptTrigger - use
/// SetVisible/SetVisibleImmediate instead; fading in and out use separate, independently tunable
/// durations.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class ControlPromptIcon : MonoBehaviour
{
    [SerializeField] private PromptAction action;
    [SerializeField] private ControlPromptIconSet iconSet;

    [Tooltip("World-space size (in units) the icon's longer edge renders at, regardless of the source art's resolution or any parent scaling.")]
    [SerializeField] private float iconWorldSize = 1.1f;

    [Tooltip("How long a SetVisible(true) takes to reach full opacity.")]
    [SerializeField] private float fadeInDuration = 0.12f;

    [Tooltip("How long a SetVisible(false) takes to reach zero opacity.")]
    [SerializeField] private float fadeOutDuration = 0.6f;

    [Header("Follow Player")]
    [Tooltip("Reposition this icon above the target player every frame, instead of relying on a moving parent (e.g. a ControlPromptTrigger's icon needs this; a head-prompt canvas icon or a stationary pickup prompt doesn't).")]
    [SerializeField] private bool followTargetPlayer = false;

    [Tooltip("World-space offset from the target player's position when Follow Target Player is on.")]
    [SerializeField] private Vector3 followOffset = new Vector3(0f, 2f, 0f);

    private SpriteRenderer spriteRenderer;
    private LocalMultiplayerSpawner spawner;
    private int targetPlayerIndex;
    private bool initialized;
    private Quaternion lockedRotation;
    private float currentAlpha = 1f;
    private float targetAlpha = 1f;

    void Awake()
    {
        EnsureInitialized();
    }

    void OnEnable()
    {
        Refresh();
    }

    void Update()
    {
        if (Mathf.Approximately(currentAlpha, targetAlpha)) return;

        float duration = targetAlpha > currentAlpha ? fadeInDuration : fadeOutDuration;
        float speed = duration > 0f ? 1f / duration : float.MaxValue;
        currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, speed * Time.deltaTime);
        ApplyAlpha();
    }

    // Runs after every Update (including a spinning parent's or the target player's movement),
    // so both the follow position and the rotation lock always win.
    void LateUpdate()
    {
        if (followTargetPlayer)
        {
            Transform target = GetTargetPlayerTransform();
            if (target != null)
                transform.position = target.position + followOffset;
        }

        transform.rotation = lockedRotation;
    }

    // PlayerHeadPrompts (on a parent) can call Refresh() from its own Awake(), which Unity doesn't
    // guarantee runs after this component's Awake() - so Refresh() can't assume Awake() already ran.
    private void EnsureInitialized()
    {
        if (initialized) return;
        initialized = true;

        spriteRenderer = GetComponent<SpriteRenderer>();
        lockedRotation = transform.rotation;

        UIPlayerFollow owner = GetComponentInParent<UIPlayerFollow>();
        targetPlayerIndex = owner != null ? owner.TargetPlayerIndex : 0;
    }

    // Lets an external owner that isn't under a UIPlayerFollow (e.g. a world-space interact
    // prompt that knows exactly which player triggered it) pin this icon to a specific player,
    // overriding the auto-detected index, then refresh immediately.
    public void SetTargetPlayer(int playerIndex)
    {
        EnsureInitialized();
        targetPlayerIndex = playerIndex;
        Refresh();
    }

    // Animates toward fully shown/hidden - fast in, slow out (fadeInDuration/fadeOutDuration).
    public void SetVisible(bool visible)
    {
        targetAlpha = visible ? 1f : 0f;
    }

    // Snaps to fully shown/hidden with no animation - for establishing initial state.
    public void SetVisibleImmediate(bool visible)
    {
        EnsureInitialized();
        targetAlpha = visible ? 1f : 0f;
        currentAlpha = targetAlpha;
        ApplyAlpha();
    }

    private void ApplyAlpha()
    {
        Color c = spriteRenderer.color;
        c.a = currentAlpha;
        spriteRenderer.color = c;
    }

    private Transform GetTargetPlayerTransform()
    {
        if (spawner == null)
            spawner = Object.FindFirstObjectByType<LocalMultiplayerSpawner>();

        var slot = spawner?.GetSlot(targetPlayerIndex);
        return slot?.playerInput != null ? slot.playerInput.transform : null;
    }

    public void Refresh()
    {
        EnsureInitialized();

        if (spawner == null)
            spawner = Object.FindFirstObjectByType<LocalMultiplayerSpawner>();

        var slot = spawner?.GetSlot(targetPlayerIndex);
        Sprite sprite = null;

        if (slot != null && iconSet != null)
        {
            PromptDevice device = PromptDeviceUtility.GetDevice(slot.device, slot.controlScheme, spawner.ArrowsScheme);
            sprite = iconSet.GetSprite(action, device, targetPlayerIndex);
        }

        spriteRenderer.enabled = sprite != null;
        spriteRenderer.sprite = sprite;

        if (sprite != null)
            ApplyWorldSize(sprite);
    }

    // Rescales this transform so the sprite always renders at iconWorldSize regardless of its
    // native pixel resolution/PPU, compensating for whatever the parent hierarchy's own scale is
    // (e.g. a head-prompt canvas authored at 1/100th scale with icons counter-scaled back up).
    private void ApplyWorldSize(Sprite sprite)
    {
        float nativeSize = Mathf.Max(sprite.rect.width, sprite.rect.height) / sprite.pixelsPerUnit;
        if (nativeSize <= 0f) return;

        float rawScale = iconWorldSize / nativeSize;
        Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;

        transform.localScale = new Vector3(
            !Mathf.Approximately(parentScale.x, 0f) ? rawScale / parentScale.x : rawScale,
            !Mathf.Approximately(parentScale.y, 0f) ? rawScale / parentScale.y : rawScale,
            1f);
    }
}
