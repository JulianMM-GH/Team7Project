using UnityEngine.InputSystem;

/// <summary>
/// Which brand of gamepad a connected device is, so UI can pick the matching button-prompt
/// icon (Xbox face buttons vs PlayStation face buttons vs a generic/unbranded pad).
/// </summary>
public enum GamepadBrand
{
    Xbox,
    PlayStation,
    Generic
}

public static class GamepadBrandUtility
{
    public static GamepadBrand GetBrand(Gamepad pad)
    {
        if (pad == null) return GamepadBrand.Generic;

        string name = (pad.displayName ?? "").ToLowerInvariant();

        if (pad is UnityEngine.InputSystem.DualShock.DualShockGamepad
            || name.Contains("dualsense") || name.Contains("dualshock") || name.Contains("wireless controller"))
            return GamepadBrand.PlayStation;

        if (pad is UnityEngine.InputSystem.XInput.XInputController || name.Contains("xbox"))
            return GamepadBrand.Xbox;

        return GamepadBrand.Generic;
    }
}
