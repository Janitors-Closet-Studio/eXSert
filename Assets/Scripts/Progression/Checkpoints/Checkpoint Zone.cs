using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Progression.Checkpoints
{
    public class CheckpointBehavior : ProgressionZone
    {
        #region Inspector Setup
        [Header("Checkpoint Settings")]
        [SerializeField]
        private string checkpointName = "Checkpoint";
        [Header("Spawn Settings")]
        [SerializeField, Tooltip("Optional transform that marks the exact spawn position and rotation. If null the checkpoint's transform is used.")]
        private Transform spawnPoint;

        [SerializeField, Tooltip("Whether the spawn gizmo should be drawn.")]
        private bool showSpawnGizmos = true;
        #endregion

        protected override Color DebugColor => Color.darkGreen;

        // Capsule dimensions are constants shared by all checkpoints.
        // Adjust these values here until they match the desired in-scene size.
        private const float SPAWN_CAPSULE_RADIUS = 0.5f;
        private const float SPAWN_CAPSULE_HEIGHT = 1.8f;

        private const bool RELOAD_SCENE_ON_DEATH = true;

        // Static reference to the current checkpoint. This allows any part of the code to query the current spawn position and rotation.
        private static CheckpointBehavior currentCheckpoint;

        private static GameObject _playerObject;
        private static GameObject PlayerObject
        {
            get
            {
                if (!SceneAsset.PlayerLoaded) return null; // Player scene not loaded, so player object cannot be found

                if (_playerObject != null) return _playerObject;

                _playerObject = GameObject.FindGameObjectWithTag("Player");
                if (_playerObject == null) throw new ArgumentNullException("Player object not found in the scene. Ensure that the player scene contains a GameObject tagged 'Player'.");
                else _playerObject = _playerObject.transform.root.gameObject; // Get the root GameObject in case the player is a child of another object
                
                return _playerObject;
            }
        }

        public Transform SpawnPoint => spawnPoint;

        public Vector3 GetSpawnPosition() => spawnPoint != null ? spawnPoint.position : transform.position;
        public Quaternion GetSpawnRotation() => spawnPoint != null ? spawnPoint.rotation : transform.rotation;

        public static void RespawnPlayer()
        {
            if (currentCheckpoint == null)
            {
                Debug.LogError("No checkpoint has been triggered yet! Cannot respawn player.");
                return;
            }
            
            if (RELOAD_SCENE_ON_DEATH)
            {
                SceneAsset.OnSceneReloaded += MovePlayerToCheckpoint; // Subscribe to the scene reloaded event to move the player after reload completes
                // Reload the current scene to reset everything, then move the player to the checkpoint after reload
                SceneAsset.Load(SceneAsset.GetSceneAssetOfObject(PlayerObject), forceReload: true);
            }
            else
            {
                // Just move the player to the checkpoint without reloading the scene
                MovePlayerToCheckpoint();
            }

            // Local function to move the player to the checkpoint spawn point.
            // This is called regardless if the scene gets reloaded or not.
            // It is important to call it after the scene is reloaded.
            static void MovePlayerToCheckpoint()
            {
                if (PlayerObject == null)
                {
                    Debug.LogError("Cannot respawn player because the player object could not be found.");
                    return;
                }
                PlayerObject.transform.SetPositionAndRotation(currentCheckpoint.GetSpawnPosition(), currentCheckpoint.GetSpawnRotation());
                SceneAsset.OnSceneReloaded -= MovePlayerToCheckpoint; // Unsubscribe after moving the player
            }
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

        private void TriggerCheckpoint()
        {
            if (currentCheckpoint == this) return; // Already the current checkpoint, no need to update
            currentCheckpoint = this;
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
