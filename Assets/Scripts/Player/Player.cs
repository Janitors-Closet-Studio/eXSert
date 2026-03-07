using UnityEngine;
using System;
using Progression.Checkpoints;

/// <summary>
/// Static class for managing the player.
/// </summary>
public static class Player
{
    public static bool IsActive { get; private set; } = false;
    internal static void SetActive(bool active) => IsActive = active;

    private static GameObject _playerObject;
    public static GameObject PlayerObject
    {
        get
        {
            if (_playerObject != null) return _playerObject;

            if (!SceneAsset.PlayerLoaded) return null; // Player scene not loaded, so player object cannot be found
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");

            if (playerObj == null) throw new ArgumentNullException("[Player] Player object not found in the scene. Ensure that the player scene contains a GameObject tagged 'Player'.");

            return _playerObject = playerObj.transform.root.gameObject; // Get the root GameObject in case the player is a child of another object
        }
    }

    private static CheckpointBehavior currentCheckpoint => CheckpointBehavior.currentCheckpoint;

    public static event Action RespawnPlayer;
    public static void TriggerRespawn() => RespawnPlayer?.Invoke();

    public static void SpawnPlayerAtCheckpoint()
    {
        PlayerObject.transform.SetPositionAndRotation(currentCheckpoint.GetSpawnPosition(), currentCheckpoint.GetSpawnRotation());
        Player.PlayerObject.GetComponent<PlayerMovement>().enabled = true;
        Player.PlayerObject.SetActive(true);
    }
}
