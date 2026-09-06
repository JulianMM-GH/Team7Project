/// <summary>
/// A gameplay action a control prompt can teach - not a literal key, since the icon shown for
/// the same action differs per device (e.g. Movement shows a WASD cluster on keyboard but a
/// stick glyph on gamepad). Add a new value here, then add a matching row to whichever
/// ControlPromptIconSet asset should provide its art - no other code changes needed.
/// </summary>
public enum PromptAction
{
    Movement,
    Jump,
    WallJump,
    DoubleJump,
    Dash,
    Light,
    Shoot,
    Interact,
    MenuOpen
}
