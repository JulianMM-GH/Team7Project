using System.Collections.Generic;
using UnityEngine.InputSystem;

/// <summary>
/// Carries the exact devices chosen in the main menu's controls popup into the next
/// scene, so LocalMultiplayerSpawner can join them immediately on load instead of
/// waiting for the player to press their input again.
/// </summary>
public static class PendingPlayerJoins
{
    public struct Entry
    {
        public string scheme;
        public InputDevice device;
    }

    public static readonly List<Entry> Entries = new List<Entry>();

    public static void Set(IEnumerable<Entry> entries)
    {
        Entries.Clear();
        Entries.AddRange(entries);
    }

    public static void Clear() => Entries.Clear();
}
