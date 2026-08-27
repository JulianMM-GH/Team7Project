using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Swaps a tutorial prompt's key icon between its keyboard sprite and a controller sprite,
/// based on the device the owning player is currently using. Attach directly to the icon
/// object (needs a SpriteRenderer) - the owning player is found automatically via the nearest
/// UIPlayerFollow up the hierarchy (SilasTutorial / PhoenixTutorial).
///
/// controllerSprite can be left unassigned until matching button art exists - the icon just
/// hides itself while on a gamepad in that case, rather than showing the wrong keyboard key.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class TutorialIconSwap : MonoBehaviour
{
    [Tooltip("Shown when the owning player is on a keyboard. Leave empty to use whatever sprite is already on the SpriteRenderer.")]
    [SerializeField] private Sprite keyboardSprite;

    [Tooltip("Shown when the owning player is on a gamepad. Leave empty until matching button art exists - the icon hides itself instead of showing the wrong key.")]
    [SerializeField] private Sprite controllerSprite;

    private SpriteRenderer spriteRenderer;
    private LocalMultiplayerSpawner spawner;
    private int targetPlayerIndex;
    private bool initialized;

    void Awake()
    {
        EnsureInitialized();
    }

    void OnEnable()
    {
        Refresh();
    }

    // PlayerHeadPrompts (on a parent) can call Refresh() from its own Awake(), which Unity doesn't
    // guarantee runs after this component's Awake() - so Refresh() can't assume Awake() already ran.
    private void EnsureInitialized()
    {
        if (initialized) return;
        initialized = true;

        spriteRenderer = GetComponent<SpriteRenderer>();

        if (keyboardSprite == null)
            keyboardSprite = spriteRenderer.sprite;

        UIPlayerFollow owner = GetComponentInParent<UIPlayerFollow>();
        targetPlayerIndex = owner != null ? owner.TargetPlayerIndex : 0;
    }

    public void Refresh()
    {
        EnsureInitialized();

        if (spawner == null)
            spawner = Object.FindFirstObjectByType<LocalMultiplayerSpawner>();

        var slot = spawner?.GetSlot(targetPlayerIndex);
        bool isGamepad = slot != null && slot.device is Gamepad;

        Sprite sprite = isGamepad ? controllerSprite : keyboardSprite;
        spriteRenderer.enabled = sprite != null;
        spriteRenderer.sprite = sprite;
    }
}
