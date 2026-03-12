using System;
using UI.Loading;
using UnityEngine;

namespace Progression.Checkpoints
{
    [HelpURL("https://docs.google.com/document/d/18pi24ZJ65GG307F6SvKpSoHPs0izxSb6yZ6cfjvYqMQ/edit?tab=t.0#bookmark=id.gqgefvoh0b90")]
    public class CheckpointBehavior : ProgressionZone, IDataPersistenceManager
    {
        #region Inspector Setup
        [Header("Checkpoint Settings")]
        [SerializeField]
        private string checkpointName = "Checkpoint";
        [Header("Spawn Settings")]
        [SerializeField, Tooltip("Optional transform that marks the exact spawn position and rotation. If null the checkpoint's transform is used.")]
        private Transform spawnPoint;
        [SerializeField, Tooltip("SceneAsset that owns this checkpoint. Assign explicitly for additive-scene save/load routing.")]
        private SceneAsset checkpointSceneAsset;

        [SerializeField, Tooltip("Whether the spawn gizmo should be drawn.")]
        private bool showSpawnGizmos = true;
        #endregion

        protected override Color DebugColor => Color.darkGreen;
        public override string ToString() => $"{checkpointName} with spawn: {GetSpawnPosition()}";
        public string CheckpointId => string.IsNullOrWhiteSpace(checkpointName) ? gameObject.name : checkpointName;
        public SceneAsset CheckpointSceneAsset => ResolveCheckpointSceneAsset();

        // Capsule dimensions are constants shared by all checkpoints.
        // Adjust these values here until they match the desired in-scene size.
        private const float SPAWN_CAPSULE_RADIUS = 0.5f;
        private const float SPAWN_CAPSULE_HEIGHT = 1.8f;

        private static readonly bool ReloadSceneOnRespawn = true;

        // Static reference to the current checkpoint. This allows any part of the code to query the current spawn position and rotation.
        public static CheckpointBehavior currentCheckpoint { get; private set; }
        public static event Action<CheckpointBehavior> OnCheckpointTriggered;

        private static GameObject PlayerObject => Player.PlayerObject;

        public Vector3 GetSpawnPosition() => spawnPoint != null ? spawnPoint.position : transform.position;
        public Quaternion GetSpawnRotation() => spawnPoint != null ? spawnPoint.rotation : transform.rotation;

        private SceneAsset ResolveCheckpointSceneAsset()
        {
            return checkpointSceneAsset != null ? checkpointSceneAsset : SceneAsset.GetSceneAssetOfObject(gameObject);
        }

        public static void OverrideCurrentCheckpoint(CheckpointBehavior newCheckpoint, bool overrideIfNull = true)
        {
            if (newCheckpoint == null)
            {
                Debug.LogError("Cannot override current checkpoint with a null reference.");
                return;
            }
            if (currentCheckpoint != null && !overrideIfNull) return;
            currentCheckpoint = newCheckpoint;
        }

        public static void SubscribeToPlayerRespawn() => Player.RespawnPlayer += RespawnPlayer;
        public static void UnsubscribeFromPlayerRespawn() => Player.RespawnPlayer -= RespawnPlayer;
        
        // Private method to handle the checkpoint's side of Respawning the player.
        // Simply just moves the player to the current checkpoint's spawn position.
        private static void RespawnPlayer()
        {
            Debug.Log("[Checkpoint] Respawning player at current checkpoint...");

            if (currentCheckpoint == null)
            {
                Debug.LogError("No checkpoint has been triggered yet! Cannot respawn player.");
                return;
            }
            
            if (ReloadSceneOnRespawn)
            {
                LoadingScreenController.OnLoadingScreenContentShown += MovePlayerToCheckpoint;
                // Reload the current scene to reset everything, then move the player once the loading content is visibly covering gameplay.
                SceneLoader.Load(currentCheckpoint.CheckpointSceneAsset, forceReload: true);
            }
            else
            {
                // Just move the player to the checkpoint without reloading the scene
                MovePlayerToCheckpoint();
            }

            static void MovePlayerToCheckpoint()
            {
                if (PlayerObject == null)
                {
                    Debug.LogError("Cannot respawn player because the player object could not be found.");
                    return;
                }

                Debug.Log($"[Checkpoint] Moving {PlayerObject.name} to checkpoint: {currentCheckpoint}");

                Player.SpawnPlayerAtCheckpoint(); // This will internally use the currentCheckpoint reference to get the spawn position and rotation

                LoadingScreenController.OnLoadingScreenContentShown -= MovePlayerToCheckpoint;
            }
        }

        private void TriggerCheckpoint()
        {
            if (currentCheckpoint == this) return; // Already the current checkpoint, no need to update
            currentCheckpoint = this;
            OnCheckpointTriggered?.Invoke(this);

            if (DataPersistenceManager.HasGameData())
                DataPersistenceManager.SaveGame();
        }

        public void LoadData(GameData data)
        {
            if (data == null)
                return;

            string sceneName = ResolveCheckpointSceneAsset()?.SceneName;
            if (string.IsNullOrEmpty(sceneName) || string.IsNullOrEmpty(data.currentSceneName) || string.IsNullOrEmpty(data.currentSpawnPointID))
                return;

            if (!string.Equals(sceneName, data.currentSceneName, StringComparison.OrdinalIgnoreCase))
                return;

            if (!string.Equals(CheckpointId, data.currentSpawnPointID, StringComparison.Ordinal))
                return;

            currentCheckpoint = this;
        }

        public void SaveData(GameData data)
        {
            if (data == null || currentCheckpoint != this)
                return;

            SceneAsset checkpointScene = ResolveCheckpointSceneAsset();
            if (checkpointScene == null)
                return;

            data.currentSceneName = checkpointScene.SceneName;
            data.currentSpawnPointID = CheckpointId;
            data.lastSavedScene = checkpointScene.SceneName;
        }

        protected override void OnDrawGizmos()
        {
            base.OnDrawGizmos();

            if (!showSpawnGizmos) return;

            var pos = GetSpawnPosition();
            var rot = GetSpawnRotation();
            var up = rot * Vector3.up;
            var right = rot * Vector3.right;
            var forward = rot * Vector3.forward;

            var radius = Mathf.Max(0.01f, SPAWN_CAPSULE_RADIUS);
            var height = Mathf.Max(radius * 2f, SPAWN_CAPSULE_HEIGHT); // at least diameter

            // Top and bottom sphere centers for capsule
            var halfBody = (height * 0.5f) - radius;
            var top = pos + up * halfBody;
            var bottom = pos - up * halfBody;

            // Draw capsule (approximation): two wire spheres and 4 connecting lines
            Gizmos.color = DebugColor;
            Gizmos.DrawWireSphere(top, radius);
            Gizmos.DrawWireSphere(bottom, radius);

            var dirs = new[] { right, forward, -right, -forward };
            foreach (var d in dirs)
            {
                Gizmos.DrawLine(top + d * radius, bottom + d * radius);
            }

            // Draw arrow showing facing direction
            var forwardDir = forward.normalized;
            var arrowLength = Mathf.Max(0.5f, radius * 2f + 0.5f);
            var arrowBase = pos; // spawn at capsule center
            var arrowTip = arrowBase + forwardDir * arrowLength;

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(arrowBase, arrowTip);

            // Arrow head
            var headSize = Mathf.Max(0.15f, radius * 0.5f);
            var leftHead = Quaternion.AngleAxis(150f, up) * forwardDir;
            var rightHead = Quaternion.AngleAxis(-150f, up) * forwardDir;
            Gizmos.DrawLine(arrowTip, arrowTip + leftHead * headSize);
            Gizmos.DrawLine(arrowTip, arrowTip + rightHead * headSize);
        }

        #region Collider Functionality
        protected override void PlayerEnteredZone() => TriggerCheckpoint();

        protected override void PlayerExitedZone() { }
        #endregion
    }
}
