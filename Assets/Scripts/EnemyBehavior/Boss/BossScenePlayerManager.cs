using UnityEngine;
using UnityEngine.SceneManagement;

namespace EnemyBehavior.Boss
{
    /// <summary>
    /// Manages the player's lifecycle during the boss fight.
    /// - Claims the player from DontDestroyOnLoad when the boss scene starts
    /// - Returns the player to PlayerScene when the boss is defeated
    /// - Handles checkpoint reloads by returning the player to PlayerScene
    /// </summary>
    public class BossScenePlayerManager : MonoBehaviour
    {
        public static BossScenePlayerManager Instance { get; private set; }

        private const string BossSceneName = "ChargingStation";
        private const string PlayerSceneName = "PlayerScene";
        private const string PlayerLayerName = "Player";
        private const string DefaultPlayerRootName = "eXSert_Vera";

        [Header("Configuration")]
        [SerializeField, Tooltip("Tag used to find the player GameObject")]
        private string playerTag = "Player";

        [SerializeField, Tooltip("Expected root object name for the player prefab.")]
        private string playerRootName = DefaultPlayerRootName;

        [SerializeField, Tooltip("Should the player be automatically claimed on scene load?")]
        private bool autoClaimOnStart = true;

        [
            SerializeField,
            Tooltip(
                "Optional explicit BossHealth reference. If left empty, one is found at runtime."
            )
        ]
        private BossHealth bossHealth;

        [Header("Debug")]
        [SerializeField]
        private bool showDebugLogs = true;

        private Transform player;
        private bool playerClaimed = false;

        private void Awake()
        {
            if (!IsHostedInBossScene())
            {
                Log(
                    $"Disabling manager in scene '{gameObject.scene.name}' because it only manages player transfer for '{BossSceneName}'."
                );
                enabled = false;
                return;
            }

            if (Instance != null && Instance != this)
            {
                Log(
                    "A second BossScenePlayerManager was found. Replacing previous instance reference."
                );
            }

            Instance = this;
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            EnsureBossHealthReference();

            if (bossHealth != null)
                bossHealth.BossDefeated += HandleBossDefeated;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;

            if (bossHealth != null)
                bossHealth.BossDefeated -= HandleBossDefeated;
        }

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
            if (!IsHostedInBossScene())
            {
                Log(
                    $"Ignoring ClaimPlayer from scene '{gameObject.scene.name}'. Player transfer is only valid for '{BossSceneName}'."
                );
                return;
            }

            if (playerClaimed)
            {
                Log("Player already claimed for this scene.");
                return;
            }

            GameObject playerRoot = ResolvePlayerRoot();
            if (playerRoot == null)
            {
                EnemyBehaviorDebugLogBools.LogError(
                    $"[BossScenePlayerManager] Could not resolve player root using tag '{playerTag}', layer '{PlayerLayerName}', or name '{playerRootName}'."
                );
                return;
            }

            player = playerRoot.transform;

            if (player.parent != null)
                player.SetParent(null, true);

            // Move player from DontDestroyOnLoad to this scene
            SceneManager.MoveGameObjectToScene(playerRoot, SceneManager.GetActiveScene());
            playerClaimed = true;

            if (PlayerPresenceManager.Instance != null)
                PlayerPresenceManager.Instance.RegisterPlayer(player);

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
                EnemyBehaviorDebugLogBools.LogWarning(
                    nameof(BossScenePlayerManager),
                    "[BossScenePlayerManager] Player reference is null, cannot release."
                );
                return;
            }

            BossTopZone topZone = GetComponentInChildren<BossTopZone>(true);
            if (topZone != null)
                topZone.ForceDetachPlayer();

            GameObject playerRoot = ResolvePlayerRoot();
            if (playerRoot == null)
            {
                EnemyBehaviorDebugLogBools.LogWarning(
                    nameof(BossScenePlayerManager),
                    "[BossScenePlayerManager] Could not resolve the player root during release."
                );
                return;
            }

            Scene playerScene = SceneManager.GetSceneByName(PlayerSceneName);
            if (playerScene.IsValid() && playerScene.isLoaded)
            {
                SceneManager.MoveGameObjectToScene(playerRoot, playerScene);
                Log(
                    $"Player released back to {PlayerSceneName} from scene: {SceneManager.GetActiveScene().name}"
                );
            }
            else
            {
                DontDestroyOnLoad(playerRoot);
                Log(
                    $"{PlayerSceneName} was unavailable, so player was released to DontDestroyOnLoad from scene: {SceneManager.GetActiveScene().name}"
                );
            }

            player = playerRoot.transform;
            playerClaimed = false;

            if (PlayerPresenceManager.Instance != null)
                PlayerPresenceManager.Instance.RegisterPlayer(player);
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
                Log(
                    "BossScenePlayerManager destroyed while player was claimed. Releasing player..."
                );
                ReleasePlayer();
            }

            if (Instance == this)
                Instance = null;
        }

        private void HandleBossDefeated()
        {
            Log("BossHealth event received. Releasing player immediately.");
            OnBossDefeated();
        }

        private void Log(string message)
        {
            if (showDebugLogs)
            {
                EnemyBehaviorDebugLogBools.Log(
                    nameof(BossScenePlayerManager),
                    $"[BossScenePlayerManager] {message}"
                );
            }
        }

        private GameObject ResolvePlayerRoot()
        {
            if (global::Player.TryGetPlayerObject(out GameObject playerObject))
                return playerObject;

            if (
                PlayerPresenceManager.IsPlayerPresent
                && PlayerPresenceManager.PlayerTransform != null
            )
                return PlayerPresenceManager.PlayerTransform.root.gameObject;

            GameObject resolvedPlayer = FindPlayerRootInLoadedScenes();
            if (resolvedPlayer != null)
                return resolvedPlayer;

            return player != null ? player.root.gameObject : null;
        }

        private void EnsureBossHealthReference()
        {
            if (bossHealth != null && bossHealth)
                return;

            bossHealth = FindBossHealthInPreferredScene();

            if (bossHealth == null)
                bossHealth = FindFirstObjectByType<BossHealth>();

            if (bossHealth == null)
            {
                Log(
                    $"BossHealth could not be found in '{BossSceneName}' for direct defeat subscription."
                );
            }
            else
            {
                Log($"Resolved BossHealth from scene '{bossHealth.gameObject.scene.name}'.");
            }
        }

        private BossHealth FindBossHealthInPreferredScene()
        {
            Scene bossScene = SceneManager.GetSceneByName(BossSceneName);
            if (!bossScene.IsValid() || !bossScene.isLoaded)
                return null;

            GameObject[] rootObjects = bossScene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < rootObjects.Length; rootIndex++)
            {
                BossHealth foundBossHealth = rootObjects[rootIndex]
                    .GetComponentInChildren<BossHealth>(true);
                if (foundBossHealth != null)
                    return foundBossHealth;
            }

            return null;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!string.Equals(scene.name, BossSceneName, System.StringComparison.Ordinal))
                return;

            if (bossHealth != null)
                bossHealth.BossDefeated -= HandleBossDefeated;

            bossHealth = null;
            EnsureBossHealthReference();

            if (bossHealth != null)
                bossHealth.BossDefeated += HandleBossDefeated;
        }

        private GameObject FindPlayerRootInLoadedScenes()
        {
            for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.isLoaded)
                    continue;

                GameObject[] rootObjects = scene.GetRootGameObjects();
                for (int rootIndex = 0; rootIndex < rootObjects.Length; rootIndex++)
                {
                    GameObject resolvedPlayer = FindPlayerRootRecursive(
                        rootObjects[rootIndex].transform
                    );
                    if (resolvedPlayer != null)
                        return resolvedPlayer;
                }
            }

            return null;
        }

        private GameObject FindPlayerRootRecursive(Transform currentTransform)
        {
            if (currentTransform == null)
                return null;

            if (MatchesPlayerCandidate(currentTransform.gameObject))
                return currentTransform.root.gameObject;

            for (int childIndex = 0; childIndex < currentTransform.childCount; childIndex++)
            {
                GameObject resolvedPlayer = FindPlayerRootRecursive(
                    currentTransform.GetChild(childIndex)
                );
                if (resolvedPlayer != null)
                    return resolvedPlayer;
            }

            return null;
        }

        private bool MatchesPlayerCandidate(GameObject candidate)
        {
            if (candidate == null)
                return false;

            if (!string.IsNullOrWhiteSpace(playerTag) && candidate.CompareTag(playerTag))
                return true;

            int playerLayer = LayerMask.NameToLayer(PlayerLayerName);
            if (playerLayer >= 0 && candidate.layer == playerLayer)
                return true;

            if (
                !string.IsNullOrWhiteSpace(playerRootName)
                && string.Equals(candidate.name, playerRootName, System.StringComparison.Ordinal)
            )
            {
                return true;
            }

            return false;
        }

        private bool IsHostedInBossScene()
        {
            Scene ownerScene = gameObject.scene;
            return ownerScene.IsValid()
                && string.Equals(ownerScene.name, BossSceneName, System.StringComparison.Ordinal);
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
