using UnityEngine;
using UnityEngine.SceneManagement;

namespace EnemyBehavior.Boss
{
    /// <summary>
    /// Manages the player's lifecycle during the boss fight.
    /// - Claims the player from DontDestroyOnLoad when the boss scene starts
    /// - Returns the player to DontDestroyOnLoad when the boss is defeated
    /// - Handles checkpoint reloads (returns player to DontDestroyOnLoad)
    /// </summary>
    public class BossScenePlayerManager : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField, Tooltip("Tag used to find the player GameObject")]
        private string playerTag = "Player";
        
        [SerializeField, Tooltip("Should the player be automatically claimed on scene load?")]
        private bool autoClaimOnStart = true;
        
        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = true;

        private Transform player;
        private bool playerClaimed = false;

        void Start()
        {
            if (autoClaimOnStart)
            {
                ClaimPlayer();
            }
        }

        /// <summary>
        /// Moves the player from DontDestroyOnLoad into the current scene.
        /// Call this when the boss fight starts.
        /// </summary>
        public void ClaimPlayer()
        {
            if (playerClaimed)
            {
                Log("Player already claimed for this scene.");
                return;
            }

            // Use PlayerPresenceManager if available, fallback to tag search
            if (PlayerPresenceManager.IsPlayerPresent)
                player = PlayerPresenceManager.PlayerTransform;
            else
                player = GameObject.FindGameObjectWithTag(playerTag)?.transform;
            
            if (player == null)
            {
                EnemyBehaviorDebugLogBools.LogError($"[BossScenePlayerManager] Could not find player with tag '{playerTag}'!");
                return;
            }

            // Move player from DontDestroyOnLoad to this scene
            SceneManager.MoveGameObjectToScene(player.gameObject, SceneManager.GetActiveScene());
            playerClaimed = true;
            
            Log($"Player claimed for boss scene: {SceneManager.GetActiveScene().name}");
        }

        /// <summary>
        /// Returns the player to DontDestroyOnLoad.
        /// Call this when the boss is defeated or when reloading a checkpoint.
        /// </summary>
        public void ReleasePlayer()
        {
            if (!playerClaimed)
            {
                Log("Player was not claimed, nothing to release.");
                return;
            }

            if (player == null)
            {
                EnemyBehaviorDebugLogBools.LogWarning(nameof(BossScenePlayerManager), "[BossScenePlayerManager] Player reference is null, cannot release.");
                return;
            }

            // Return player to DontDestroyOnLoad
            DontDestroyOnLoad(player.gameObject);
            playerClaimed = false;
            
            Log($"Player released back to DontDestroyOnLoad from scene: {SceneManager.GetActiveScene().name}");
        }

        /// <summary>
        /// Call this when the boss is defeated.
        /// </summary>
        public void OnBossDefeated()
        {
            Log("Boss defeated! Releasing player...");
            ReleasePlayer();
            
            // Optional: Add any other boss defeat logic here
            // e.g., save progress, unlock next area, show victory screen
        }

        /// <summary>
        /// Call this when reloading a checkpoint (from death or menu).
        /// </summary>
        public void OnCheckpointReload()
        {
            Log("Checkpoint reload requested. Releasing player...");
            ReleasePlayer();
            
            // The checkpoint system will handle moving the player to the spawn point
        }

        void OnDestroy()
        {
            // Safety: if this manager is destroyed while player is claimed,
            // return player to DontDestroyOnLoad
            if (playerClaimed && player != null)
            {
                Log("BossScenePlayerManager destroyed while player was claimed. Releasing player...");
                ReleasePlayer();
            }
        }

        private void Log(string message)
        {
            if (showDebugLogs)
            {
                EnemyBehaviorDebugLogBools.Log(nameof(BossScenePlayerManager), $"[BossScenePlayerManager] {message}");
            }
        }

        #region Public API for Boss Brain
        
        /// <summary>
        /// Returns true if the player is currently claimed by this scene.
        /// </summary>
        public bool IsPlayerClaimed => playerClaimed;
        
        /// <summary>
        /// Get the player transform reference.
        /// </summary>
        public Transform Player => player;
        
        #endregion
    }
}
