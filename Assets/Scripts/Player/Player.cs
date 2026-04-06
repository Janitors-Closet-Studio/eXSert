using UnityEngine;
using System;
using Progression.Checkpoints;
using UnityEngine.SceneManagement;

/// <summary>
/// Static class for managing the player.
/// </summary>
public static class Player
{
    public static bool IsActive { get; private set; } = false;
    internal static void SetActive(bool active) => IsActive = active;

    private static GameObject _playerObject;
    public static GameObject PlayerObject => TryGetPlayerObject(out GameObject playerObject) ? playerObject : null;

    private static CheckpointBehavior currentCheckpoint => CheckpointBehavior.currentCheckpoint;

    public static event Action RespawnPlayer;
    public static void TriggerRespawn() => RespawnPlayer?.Invoke();

    public static bool TryGetPlayerObject(out GameObject playerObject)
    {
        if (_playerObject != null)
        {
            playerObject = _playerObject;
            return true;
        }

        if (!SceneAsset.PlayerLoaded)
        {
            playerObject = null;
            return false;
        }

        playerObject = FindPlayerObjectInLoadedScenes();
        if (playerObject == null)
            return false;

        _playerObject = playerObject.transform.root.gameObject;
        playerObject = _playerObject;
        return true;
    }

    internal static void ClearCachedPlayerObject() => _playerObject = null;

    public static void SpawnPlayerAtCheckpoint()
    {
        if (currentCheckpoint == null)
        {
            Debug.LogError("[Player] Cannot spawn at checkpoint because no checkpoint is currently set.");
            return;
        }

        if (!TryGetPlayerObject(out GameObject playerObject))
        {
            Debug.LogError("[Player] Cannot spawn at checkpoint because the player object could not be found.");
            return;
        }

        playerObject.transform.SetParent(null, true);

        PlayerMovement move = playerObject.GetComponent<PlayerMovement>();
        if (move == null)
        {
            Debug.LogError("[Player] Cannot spawn at checkpoint because PlayerMovement is missing from the player root object.");
            return;
        }

        move.enabled = true;
        move.TrySnapToSoftLock(currentCheckpoint.GetSpawnPosition(), currentCheckpoint.GetSpawnRotation());
        playerObject.SetActive(true);
    }

    private static GameObject FindPlayerObjectInLoadedScenes()
    {
        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            Scene scene = SceneManager.GetSceneAt(sceneIndex);
            if (!scene.isLoaded)
                continue;

            GameObject[] rootObjects = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < rootObjects.Length; rootIndex++)
            {
                GameObject playerObject = FindPlayerObjectRecursive(rootObjects[rootIndex].transform);
                if (playerObject != null)
                    return playerObject;
            }
        }

        return null;
    }

    private static GameObject FindPlayerObjectRecursive(Transform currentTransform)
    {
        if (currentTransform.CompareTag("Player"))
            return currentTransform.gameObject;

        for (int childIndex = 0; childIndex < currentTransform.childCount; childIndex++)
        {
            GameObject playerObject = FindPlayerObjectRecursive(currentTransform.GetChild(childIndex));
            if (playerObject != null)
                return playerObject;
        }

        return null;
    }
}
