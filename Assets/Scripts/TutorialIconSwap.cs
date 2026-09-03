using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Swaps a tutorial prompt's key icon to match the device the owning player is currently
/// using - keyboard, Xbox, PlayStation, or an unbranded/generic gamepad. Attach directly to
/// the icon object (needs a SpriteRenderer) - the owning player is found automatically via the
/// nearest UIPlayerFollow up the hierarchy (SilasTutorial / PhoenixTutorial).
///
/// Any of the gamepad sprites can be left unassigned until matching button art exists for that
/// brand - the icon just hides itself in that case, rather than showing the wrong key.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class TutorialIconSwap : MonoBehaviour
{
    [Tooltip("Shown when the owning player is on a keyboard. Leave empty to use whatever sprite is already on the SpriteRenderer.")]
    [SerializeField] private Sprite keyboardSprite;

    [Tooltip("Shown when the owning player is on an Xbox (or other XInput) controller. Leave empty until matching button art exists - the icon hides itself instead of showing the wrong key.")]
    [SerializeField] private Sprite xboxSprite;

    [Tooltip("Shown when the owning player is on a PlayStation controller. Leave empty until matching button art exists - the icon hides itself instead of showing the wrong key.")]
    [SerializeField] private Sprite playstationSprite;

    [Tooltip("Shown when the owning player is on any other gamepad. Leave empty until matching button art exists - the icon hides itself instead of showing the wrong key.")]
    [SerializeField] private Sprite genericGamepadSprite;

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

    // Lets an external owner that isn't under a UIPlayerFollow (e.g. a world-space interact
    // prompt that knows exactly which player triggered it) pin this icon to a specific player,
    // overriding the auto-detected index, then refresh immediately.
    public void SetTargetPlayer(int playerIndex)
    {
        EnsureInitialized();
        targetPlayerIndex = playerIndex;
        Refresh();
    }

    public void Refresh()
    {
        EnsureInitialized();

        if (spawner == null)
            spawner = Object.FindFirstObjectByType<LocalMultiplayerSpawner>();

        var slot = spawner?.GetSlot(targetPlayerIndex);
        Sprite sprite = keyboardSprite;

        if (slot?.device is Gamepad pad)
        {
            sprite = GamepadBrandUtility.GetBrand(pad) switch
            {
                GamepadBrand.Xbox => xboxSprite,
                GamepadBrand.PlayStation => playstationSprite,
                _ => genericGamepadSprite
            };
        }

        spriteRenderer.enabled = sprite != null;
        spriteRenderer.sprite = sprite;
    }
}
