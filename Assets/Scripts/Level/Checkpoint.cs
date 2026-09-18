using System.Collections.Generic;
using FMODUnity;
using UnityEngine;

/// <summary>
/// Two player checkpoint. It only counts once every player has passed through it, then it
/// becomes the respawn point for both of them. Walking back out again doesn't undo it, and
/// each checkpoint only ever fires once.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Checkpoint : MonoBehaviour
{
    [Header("Activation")]
    [Tooltip("How many players have to pass through. 0 = however many are actually in the level.")]
    public int requiredPlayers = 0;

    [Tooltip("Optional ordering. This won't take over if a checkpoint with a higher number is already active. Leave everything at 0 to ignore ordering.")]
    public int order = 0;

    [Header("Respawn points")]
    [Tooltip("Where each player comes back, one per player. Leave empty to use this object's position, spread out by the spacing below.")]
    public Transform[] respawnPoints;

    [Tooltip("Gap between players respawning on this object's position, so they don't land inside each other. Ignored when respawn points are set.")]
    public float respawnSpacing = 1.2f;

    [Header("Feedback")]
    [Tooltip("FMOD event played when the checkpoint activates. Leave it empty and it just logs to the console instead.")]
    public EventReference activationSound;

    [Tooltip("Optional object switched on when the checkpoint activates (flag, light, particles...).")]
    public GameObject activatedVisual;

    /// <summary>The checkpoint players are currently respawning at.</summary>
    public static Checkpoint Active { get; private set; }

    public bool IsActivated => activated;

    // Passing through is a latch - a player who steps back out has still passed it.
    private readonly HashSet<PlayerRespawn> passed = new HashSet<PlayerRespawn>();
    private bool activated;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (activated) return;
        if (!collision.CompareTag("Player")) return;

        PlayerRespawn player = collision.GetComponentInParent<PlayerRespawn>();
        if (player == null) return;

        passed.Add(player);

        if (passed.Count >= PlayersNeeded())
            Activate();
    }

    private int PlayersNeeded()
    {
        if (requiredPlayers > 0) return requiredPlayers;

        int joined = 0;
        foreach (PlayerRespawn player in PlayerRespawn.All)
            if (player != null) joined++;

        // Solo playtesting still needs checkpoints to work.
        return Mathf.Max(1, joined);
    }

    private void Activate()
    {
        activated = true;

        // A checkpoint further on already counts, so don't drag the players back to this one.
        if (Active != null && Active != this && order < Active.order) return;

        Active = this;

        foreach (PlayerRespawn player in PlayerRespawn.All)
            AssignTo(player);

        if (activatedVisual != null) activatedVisual.SetActive(true);

        PlayActivationSound();
    }

    /// <summary>Points one player's respawn at this checkpoint. Also used by players who join after it fired.</summary>
    public void AssignTo(PlayerRespawn player)
    {
        if (player == null) return;

        int playerCount = 0;
        foreach (PlayerRespawn other in PlayerRespawn.All)
            if (other != null) playerCount++;

        player.SetCheckpoint(transform, RespawnPointFor(player.PlayerIndex, playerCount));
    }

    private Vector3 RespawnPointFor(int playerIndex, int playerCount)
    {
        if (respawnPoints != null && respawnPoints.Length > 0)
        {
            Transform point = respawnPoints[Mathf.Clamp(playerIndex, 0, respawnPoints.Length - 1)];
            if (point != null) return point.position;
        }

        if (playerCount <= 1 || respawnSpacing <= 0f) return transform.position;

        // Spread the players evenly either side of the checkpoint.
        float slot = playerIndex - (playerCount - 1) * 0.5f;
        return transform.position + new Vector3(slot * respawnSpacing, 0f, 0f);
    }

    private void PlayActivationSound()
    {
        if (!activationSound.IsNull)
        {
            RAudio.PlayOneShot(activationSound, transform.position);
            return;
        }

        // Placeholder until there's an event for it - drop one on Activation Sound and this goes away.
        Debug.Log($"[Checkpoint] '{name}' reached by all players - checkpoint sound would play here.", this);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = activated ? Color.green : Color.cyan;

        // Show where players will actually come back, so the spacing can be eyeballed in the editor.
        int count = Mathf.Max(2, respawnPoints != null ? respawnPoints.Length : 0);
        for (int i = 0; i < count; i++)
        {
            Vector3 point = RespawnPointFor(i, count);
            Gizmos.DrawWireSphere(point, 0.3f);
        }
    }
}
