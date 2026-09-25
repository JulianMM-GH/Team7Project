using System.Collections.Generic;
using SupanthaPaul;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerRespawn : MonoBehaviour
{
    /// <summary>Everyone currently in the level. Lets a checkpoint know how many players it's waiting on.</summary>
    public static readonly List<PlayerRespawn> All = new List<PlayerRespawn>();

    [HideInInspector] public Transform currentCheckpoint;

    private PlayerController controller;
    private PlayerInput playerInput;

    private Vector3 startPosition;
    private Vector3 respawnPosition;
    private bool hasRespawnPosition;

    /// <summary>0 for Player 1, 1 for Player 2. Falls back to join order if there's no PlayerInput.</summary>
    public int PlayerIndex
    {
        get
        {
            if (playerInput != null) return Mathf.Max(0, playerInput.playerIndex);
            return Mathf.Max(0, All.IndexOf(this));
        }
    }

    /// <summary>
    /// Where this player comes back. A checkpoint sets it per player so both don't land on
    /// the same spot; without one they go back to wherever they spawned in.
    /// </summary>
    public Vector3 RespawnPosition
    {
        get
        {
            if (hasRespawnPosition) return respawnPosition;
            // Anything that still assigns currentCheckpoint directly keeps working.
            if (currentCheckpoint != null) return currentCheckpoint.position;
            return startPosition;
        }
    }

    void Awake()
    {
        controller = GetComponent<PlayerController>();
        playerInput = GetComponent<PlayerInput>();
        startPosition = transform.position;
    }

    void OnEnable()
    {
        All.RemoveAll(p => p == null);
        if (!All.Contains(this)) All.Add(this);
    }

    void OnDisable()
    {
        All.Remove(this);
    }

    void Start()
    {
        // The spawner positions players after instantiating them, so the real
        // starting point isn't known until now.
        startPosition = transform.position;

        // Anyone joining after a checkpoint has already fired respawns there too.
        if (Checkpoint.Active != null)
            Checkpoint.Active.AssignTo(this);
    }

    /// <summary>Called by a checkpoint once every player has passed through it.</summary>
    public void SetCheckpoint(Transform checkpoint, Vector3 position)
    {
        currentCheckpoint = checkpoint;
        respawnPosition = position;
        hasRespawnPosition = true;
    }

    public void Respawn()
    {
        transform.position = RespawnPosition;

        if (controller != null)
            controller.ResetPhysicsState();

        RAudio.PlayOneShot("Respawn");
    }
}
