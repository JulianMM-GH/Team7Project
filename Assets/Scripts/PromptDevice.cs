using UnityEngine.InputSystem;

/// <summary>
/// Which visual style of control prompt icon should be shown - the keyboard half of this
/// distinguishes WASD from Arrow Keys (since they're different physical keys), while every
/// gamepad brand collapses to whichever ones have matching art (Xbox/PlayStation) or
/// GenericGamepad as a fallback.
/// </summary>
public enum PromptDevice
{
    KeyboardWASD,
    KeyboardArrows,
    Xbox,
    PlayStation,
    GenericGamepad
}

public static class PromptDeviceUtility
{
    public static PromptDevice GetDevice(InputDevice device, string controlScheme, string arrowsSchemeName)
    {
        if (device is Gamepad pad)
        {
            return GamepadBrandUtility.GetBrand(pad) switch
            {
                GamepadBrand.Xbox => PromptDevice.Xbox,
                GamepadBrand.PlayStation => PromptDevice.PlayStation,
                _ => PromptDevice.GenericGamepad
            };
        }

        return controlScheme == arrowsSchemeName ? PromptDevice.KeyboardArrows : PromptDevice.KeyboardWASD;
    }
}
