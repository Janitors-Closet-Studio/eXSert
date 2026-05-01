using System;
using System.Collections;
using System.Linq;
using UI.Loading;
using UIandUXSystems.HUD;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Progression.Checkpoints
{
    [HelpURL(
        "https://docs.google.com/document/d/18pi24ZJ65GG307F6SvKpSoHPs0izxSb6yZ6cfjvYqMQ/edit?tab=t.0#bookmark=id.gqgefvoh0b90"
    )]
    public class CheckpointBehavior : ProgressionZone, IDataPersistenceManager
    {
        #region Inspector Setup
        [Header("Checkpoint Settings")]
        [SerializeField]
        private string checkpointName = "Checkpoint";

        [SerializeField]
        [Tooltip(
            "Optional display name used in checkpoint diary notices. Leave empty to reuse Checkpoint Name."
        )]
        private string checkpointDisplayName = "";

        [Header("Spawn Settings")]
        [SerializeField]
        [Tooltip(
            "Optional transform that marks the exact spawn position and rotation. If null the checkpoint's transform is used."
        )]
        private Transform spawnPoint;

        [SerializeField]
        [Tooltip(
            "If assigned, this diary will be unlocked or marked as read when the checkpoint is triggered. This allows you to gate diary entries behind checkpoints."
        )]
        private DiarySO associatedDiary; // Optional reference to a diary that can be unlocked or marked as read when this checkpoint is triggered.

        [Header("Checkpoint Diary Notice")]
        [SerializeField]
        [Tooltip(
            "Optional custom title shown when this checkpoint unlocks its associated diary. Leave empty to use the diary title. Supports {checkpoint}, {diaryTitle}, and {diaryId}."
        )]
        private string associatedDiaryNoticeTitle = "";

        [SerializeField]
        [Tooltip(
            "Optional custom description shown when this checkpoint unlocks its associated diary. Leave empty to use the diary description. Supports {checkpoint}, {diaryTitle}, {diaryDescription}, and {diaryId}."
        )]
        [TextArea(2, 4)]
        private string associatedDiaryNoticeDescription = "";

        [SerializeField]
        [Tooltip("Fade duration for the checkpoint diary notice.")]
        private float associatedDiaryNoticeFadeDuration = 0.5f;

        [SerializeField]
        [Tooltip("Display duration for the checkpoint diary notice.")]
        private float associatedDiaryNoticeDisplayDuration = 3f;

        [SerializeField]
        [Tooltip(
            "SceneAsset that owns this checkpoint. Assign explicitly for additive-scene save/load routing."
        )]
        private SceneAsset checkpointSceneAsset;

        [SerializeField]
        [Tooltip(
            "Optional object to enable when the game loads or reloads into this checkpoint. Useful for restoring checkpoint-specific blockers or progression objects after a respawn."
        )]
        private GameObject enableObjectOnCheckpointLoad;

        [SerializeField]
        [Tooltip("If enabled, this checkpoint will replay its attached MasterObjective notice when the player loads into this scene at this checkpoint.")]
        private bool showAttachedNoticeOnSceneLoad;

        [SerializeField] private bool isActCheckpoint = false;
        private bool updatedActsForCheckpoint = false; 
        private bool pendingLoadNoticeReplay;

        [Header("Player Refresh")]
        [SerializeField]
        [Tooltip(
            "When enabled, entering this checkpoint restores the player's health to full before the checkpoint save runs."
        )]
        private bool restorePlayerHealthOnTrigger = true;

        [SerializeField, Tooltip("Whether the spawn gizmo should be drawn.")]
        private bool showSpawnGizmos = true;
        #endregion

        #region Inherited Implementation
        protected override Color DebugColor => Color.darkGreen;

        public override string ToString() => $"{checkpointName} with spawn: {GetSpawnPosition()}";
        #endregion

        public string CheckpointId =>
            string.IsNullOrWhiteSpace(checkpointName) ? gameObject.name : checkpointName;
        public string CheckpointDisplayName =>
            string.IsNullOrWhiteSpace(checkpointDisplayName) ? CheckpointId : checkpointDisplayName;
        public SceneAsset CheckpointSceneAsset => ResolveCheckpointSceneAsset();

        private static readonly bool ReloadSceneOnRespawn = true;

        // Static reference to the current checkpoint. This allows any part of the code to query the current spawn position and rotation.
        public static CheckpointBehavior currentCheckpoint { get; private set; }
        public static event Action<CheckpointBehavior> OnCheckpointTriggered;

        private static GameObject PlayerObject => Player.PlayerObject;

        public Vector3 GetSpawnPosition() =>
            spawnPoint != null ? spawnPoint.position : transform.position;

        public Quaternion GetSpawnRotation() =>
            spawnPoint != null ? spawnPoint.rotation : transform.rotation;

        protected override void Start()
        {
            base.Start();
            StartCoroutine(EvaluateInitialCheckpointActivationCoroutine());
        }

        private SceneAsset ResolveCheckpointSceneAsset()
        {
            return checkpointSceneAsset != null
                ? checkpointSceneAsset
                : SceneAsset.GetSceneAssetOfObject(gameObject);
        }

        public static void OverrideCurrentCheckpoint(
            CheckpointBehavior newCheckpoint,
            bool overrideIfNull = true
        )
        {
            if (newCheckpoint == null)
            {
                Debug.LogError("Cannot override current checkpoint with a null reference.");
                return;
            }

            if (currentCheckpoint != null && !overrideIfNull)
                return;

            currentCheckpoint = newCheckpoint;
        }

        public static bool EnsureRespawnCheckpointAvailable()
        {
            if (currentCheckpoint != null)
                return true;

            CheckpointBehavior fallbackCheckpoint = ResolveFallbackRespawnCheckpoint();
            if (fallbackCheckpoint == null)
                return false;

            currentCheckpoint = fallbackCheckpoint;
            Debug.Log($"[Checkpoint] Promoted fallback respawn checkpoint '{currentCheckpoint.CheckpointId}'.");
            return true;
        }

        public static void SubscribeToPlayerRespawn() => Player.RespawnPlayer += RespawnPlayer;

        public static void UnsubscribeFromPlayerRespawn() => Player.RespawnPlayer -= RespawnPlayer;

        // Private method to handle the checkpoint's side of Respawning the player.
        // Simply just moves the player to the current checkpoint's spawn position.
        private static void RespawnPlayer()
        {
            Debug.Log("[Checkpoint] Respawning player at current checkpoint...");

            if (InternalPlayerInventory.Instance != null)
                InternalPlayerInventory.Instance.RemoveTransientKeycardItems();

            if (!EnsureRespawnCheckpointAvailable())
            {
                if (PlayerMovement.IsTestingOrDebugMode)
                {
                    Debug.LogWarning(
                        "[Checkpoint] No checkpoint is set, but Testing/Debug mode is enabled on PlayerMovement. Skipping checkpoint respawn requirements for test scene play."
                    );

                    if (Player.TryGetPlayerObject(out GameObject playerObject))
                    {
                        PlayerMovement move = playerObject.GetComponent<PlayerMovement>();
                        if (move != null)
                        {
                            move.enabled = true;
                            playerObject.transform.SetParent(null, true);
                            move.TrySnapToSoftLock(
                                playerObject.transform.position,
                                playerObject.transform.rotation
                            );
                        }

                        playerObject.SetActive(true);
                    }

                    return;
                }

                Debug.LogError("No checkpoint has been triggered yet! Cannot respawn player.");
                return;
            }

            if (ReloadSceneOnRespawn)
            {
                CoroutineRunner.Run(RespawnWithLoadingTransition());
            }
            else
            {
                // Just move the player to the checkpoint without reloading the scene
                MovePlayerToCheckpoint();
            }

            static IEnumerator RespawnWithLoadingTransition()
            {
                // Ensure loading scene/controller exists so loading visuals fully cover scene reload.
                if (!LoadingScreenController.HasInstance)
                {
                    Scene loadingScene = SceneManager.GetSceneByName("LoadingScene");
                    if (!loadingScene.isLoaded)
                    {
                        AsyncOperation loadLoadingSceneOp = SceneManager.LoadSceneAsync(
                            "LoadingScene",
                            LoadSceneMode.Additive
                        );
                        if (loadLoadingSceneOp != null)
                            yield return loadLoadingSceneOp;
                    }

                    float timeoutAt = Time.unscaledTime + 5f;
                    while (!LoadingScreenController.HasInstance && Time.unscaledTime < timeoutAt)
                        yield return null;
                }

                IEnumerator reloadSteps = ReloadCheckpointSceneAndMovePlayer();
                if (LoadingScreenController.HasInstance)
                {
                    LoadingScreenController.BeginLoading(reloadSteps, pauseGame: true);
                    yield break;
                }

                // Fallback path if loading controller is unavailable.
                yield return reloadSteps;
            }

            static IEnumerator ReloadCheckpointSceneAndMovePlayer()
            {
                yield return SceneLoader.ReloadCheckpointSceneStackCoroutine(currentCheckpoint.CheckpointSceneAsset);
              //  yield return SceneLoader.EnsurePlayerObjectAvailableCoroutine(
              //      characterStartInactive: false
               // );
                MovePlayerToCheckpoint();
            }

            static void MovePlayerToCheckpoint()
            {
                if (!Player.TryGetPlayerObject(out GameObject playerObject))
                {
                    Debug.LogError(
                        "Cannot respawn player because the player object could not be found."
                    );
                    return;
                }

                Debug.Log(
                    $"[Checkpoint] Moving {playerObject.name} to checkpoint: {currentCheckpoint}"
                );

                Player.SpawnPlayerAtCheckpoint(); // This will internally use the currentCheckpoint reference to get the spawn position and rotation
            }
        }

        private static CheckpointBehavior ResolveFallbackRespawnCheckpoint()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            CheckpointBehavior fallbackCheckpoint = ResolveFirstCheckpointForScene(activeScene);
            if (fallbackCheckpoint != null)
                return fallbackCheckpoint;

            for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.IsValid() || !scene.isLoaded || scene == activeScene)
                    continue;

                fallbackCheckpoint = ResolveFirstCheckpointForScene(scene);
                if (fallbackCheckpoint != null)
                    return fallbackCheckpoint;
            }

            return null;
        }

        private static CheckpointBehavior ResolveFirstCheckpointForScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return null;

            string sceneName = scene.name;
            if (string.IsNullOrWhiteSpace(sceneName)
                || string.Equals(sceneName, "PlayerScene", StringComparison.Ordinal)
                || string.Equals(sceneName, "PostProcessScene", StringComparison.Ordinal)
                || string.Equals(sceneName, "LoadingScene", StringComparison.Ordinal)
                || string.Equals(sceneName, "MainMenu", StringComparison.Ordinal))
            {
                return null;
            }

            SceneAsset sceneAsset = SceneAsset.GetSceneAsset(scene);
            if (sceneAsset == null)
                return null;

            Progression.ProgressionManager progressionManager = Progression.ProgressionManager.GetInstance(sceneAsset);
            return progressionManager != null ? progressionManager.FirstCheckpoint : null;
        }

        private void UpdateAvailableActs()
        {
            ActsManager manager = ActsManager.Instance;

            if (manager == null)
            {
                Debug.LogError("Cannot update available acts because ActsManager instance is missing.");
                return;
            }

            if (currentCheckpoint && CheckSceneActDictForValidName())
            {
                // Get the current profileId from DataPersistenceManager if available
                string profileId = "default";
                if (DataPersistenceManager.Instance != null)
                {
                    var getIdMethod = DataPersistenceManager.Instance.GetType().GetMethod("GetSelectedProfileId");
                    if (getIdMethod != null)
                        profileId = (string)getIdMethod.Invoke(DataPersistenceManager.Instance, null);
                }
                int actNumber = MatchSceneActToRoadMap();
                manager.MarkActCompleted(profileId, actNumber);
                DataPersistenceManager.RegisterHighestUnlockedAct(actNumber);
                updatedActsForCheckpoint = true;
            }
        }

        private bool CheckSceneActDictForValidName()
        {
            ActsManager manager = ActsManager.Instance;

            if (manager == null)
            {
                Debug.LogError("Cannot check act scene dict because ActsManager instance is missing.");
                return false;
            }

            if (!manager.actSceneMap.ContainsValue(checkpointName))
            {
                Debug.LogError($"Checkpoint scene '{CheckpointSceneAsset.SceneName}' is not registered in ActsManager.actSceneMap. Please ensure all checkpoint scenes are correctly mapped to their respective acts.");
                return false;
            }

            return true;
        }

        private int MatchSceneActToRoadMap()
        {
            if (!CheckSceneActDictForValidName())
                return -1;

            ActsManager manager = ActsManager.Instance;

            int actNumber = manager.actSceneMap.FirstOrDefault(kv => kv.Value == checkpointName).Key;
            Debug.Log($"[Checkpoint] MatchSceneActToRoadMap: checkpointName='{checkpointName}', actNumber={actNumber}");

            if (actNumber < 0 || actNumber > 4)
            {
                Debug.LogError($"Invalid act number {actNumber} for checkpoint scene '{CheckpointSceneAsset.SceneName}'. Act number must be between 0 and 4.");
                return -1;
            }

            return actNumber;
        }

        private void TriggerCheckpoint()
        {
            bool wasAlreadyCurrentCheckpoint = currentCheckpoint == this;
            bool checkpointPreviouslyRecorded = ActsManager.Instance != null
                && ActsManager.Instance.foundCheckpointZones.Contains(this.gameObject);
            bool diaryAlreadyUnlocked = associatedDiary == null || associatedDiary.isFound;

            if (wasAlreadyCurrentCheckpoint && checkpointPreviouslyRecorded && diaryAlreadyUnlocked)
            {
                if (pendingLoadNoticeReplay && showAttachedNoticeOnSceneLoad)
                {
                    pendingLoadNoticeReplay = false;
                    StartCoroutine(ShowAttachedNoticeAfterLoadCoroutine());
                }

                return;
            }

            currentCheckpoint = this;
            if (ActsManager.Instance != null && !ActsManager.Instance.foundCheckpointZones.Contains(this.gameObject))
                ActsManager.Instance.foundCheckpointZones.Add(this.gameObject);

            Debug.Log($"Checkpoint triggered: {this}");

            if (isActCheckpoint && !updatedActsForCheckpoint) UpdateAvailableActs();
            
            RestorePlayerHealthIfConfigured();
            OnCheckpointTriggered?.Invoke(this);
            pendingLoadNoticeReplay = false;

            // Handle associated diary entry if assigned
            if (associatedDiary != null && !associatedDiary.isFound)
            {
                try
                {
                    associatedDiary.isFound = true; // Mark the diary as found/unlocked

                    // Trigger any events related to finding this diary
                    EventsManager.Instance.diaryEvents.FoundDiary(associatedDiary.diaryID);

                    // Add to unread diaries list for HUD display
                    DiaryManager.Instance.unreadDiaries.Add(associatedDiary);

                    ShowAssociatedDiaryNotice();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Checkpoint Zone] Failed to unlock or mark diary as read for checkpoint '{CheckpointId}'. Exception: {ex}");
                }
            }

            // Make sure to save the game after updating all potential info
            if (DataPersistenceManager.HasGameData())
                DataPersistenceManager.SaveGame();
        }

        private void RestorePlayerHealthIfConfigured()
        {
            if (!restorePlayerHealthOnTrigger)
                return;

            PlayerHealthBarManager playerHealth = PlayerHealthBarManager.Instance;
            if (playerHealth == null && Player.TryGetPlayerObject(out GameObject playerObject))
                playerHealth = playerObject.GetComponent<PlayerHealthBarManager>();

            if (playerHealth == null)
                return;

            playerHealth.ForceFullHeal();
        }

        private void EnableConfiguredLoadObject()
        {
            if (enableObjectOnCheckpointLoad == null)
                return;

            enableObjectOnCheckpointLoad.SetActive(true);
        }

        private IEnumerator EvaluateInitialCheckpointActivationCoroutine()
        {
            yield return null;
            yield return null;

            if (!zoneEnabled || progressionCollider == null)
                yield break;

            if (!Player.TryGetPlayerObject(out GameObject playerObject) || playerObject == null)
                yield break;

            Vector3 playerPosition = playerObject.transform.position;
            if (!progressionCollider.bounds.Contains(playerPosition))
                yield break;

            TriggerCheckpoint();
        }

        private IEnumerator ShowAttachedNoticeAfterLoadCoroutine()
        {
            yield return null;
            yield return null;

            if (!pendingLoadNoticeReplay)
                yield break;

            pendingLoadNoticeReplay = false;
            ForceShowAttachedNotice();
        }

        [ContextMenu("Force Show Attached Notice")]
        public void ForceShowAttachedNotice()
        {
            MasterObjectiveClass masterObjective = MasterObjectiveClass.GetInstance(ResolveCheckpointSceneAsset());
            if (masterObjective == null)
            {
                Debug.LogWarning($"[Checkpoint Zone] No MasterObjectiveClass found for checkpoint '{CheckpointId}'. Cannot force notice replay.");
                return;
            }

            masterObjective.ShowAttachedNoticesForTrigger(this);
        }

        private string ReplaceDiaryNoticeTokens(string template)
        {
            if (string.IsNullOrEmpty(template))
                return string.Empty;

            string diaryTitle = associatedDiary != null ? associatedDiary.diaryTitle : string.Empty;
            string diaryDescription = associatedDiary != null ? associatedDiary.diaryDescription : string.Empty;
            string diaryId = associatedDiary != null ? associatedDiary.diaryID : string.Empty;

            return template
                .Replace("{checkpoint}", CheckpointDisplayName)
                .Replace("{diaryTitle}", diaryTitle)
                .Replace("{diaryDescription}", diaryDescription)
                .Replace("{diaryId}", diaryId);
        }

        private string ResolveAssociatedDiaryNoticeTitle()
        {
            if (!string.IsNullOrWhiteSpace(associatedDiaryNoticeTitle))
                return ReplaceDiaryNoticeTokens(associatedDiaryNoticeTitle);

            if (associatedDiary != null && !string.IsNullOrWhiteSpace(associatedDiary.diaryTitle))
                return associatedDiary.diaryTitle;

            return CheckpointDisplayName;
        }

        private string ResolveAssociatedDiaryNoticeDescription()
        {
            if (!string.IsNullOrWhiteSpace(associatedDiaryNoticeDescription))
                return ReplaceDiaryNoticeTokens(associatedDiaryNoticeDescription);

            if (associatedDiary != null && !string.IsNullOrWhiteSpace(associatedDiary.diaryDescription))
                return associatedDiary.diaryDescription;

            return string.Empty;
        }

        private void ShowAssociatedDiaryNotice()
        {
            if (associatedDiary == null)
                return;

            MasterObjectiveClass masterObjective = MasterObjectiveClass.GetInstance(ResolveCheckpointSceneAsset());
            if (masterObjective == null)
                return;

            masterObjective.CreateAndShowNotice(
                null,
                $"{CheckpointId}_{associatedDiary.diaryID}_checkpoint_diary",
                ResolveAssociatedDiaryNoticeTitle(),
                ResolveAssociatedDiaryNoticeDescription(),
                associatedDiaryNoticeFadeDuration,
                associatedDiaryNoticeDisplayDuration,
                priority: 9
            );
        }

        #region Data Persistence
        public void LoadData(GameData data)
        {
            if (data == null)
                return;

            string sceneName = ResolveCheckpointSceneAsset()?.SceneName;
            if (
                string.IsNullOrEmpty(sceneName)
                || string.IsNullOrEmpty(data.currentSceneName)
                || string.IsNullOrEmpty(data.currentSpawnPointID)
            )
                return;

            if (
                !string.Equals(sceneName, data.currentSceneName, StringComparison.OrdinalIgnoreCase)
            )
                return;

            if (!string.Equals(CheckpointId, data.currentSpawnPointID, StringComparison.Ordinal))
                return;

            currentCheckpoint = this;
            EnableConfiguredLoadObject();
            pendingLoadNoticeReplay = showAttachedNoticeOnSceneLoad;

            if (showAttachedNoticeOnSceneLoad)
                StartCoroutine(ShowAttachedNoticeAfterLoadCoroutine());
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
        #endregion

        #region Gizmo Drawing
        // Capsule dimensions are constants shared by all checkpoints.
        // Adjust these values here until they match the desired in-scene size.
        private const float SPAWN_CAPSULE_RADIUS = 0.5f;
        private const float SPAWN_CAPSULE_HEIGHT = 1.8f;

        protected override void OnDrawGizmos()
        {
            base.OnDrawGizmos();

            if (!showSpawnGizmos)
                return;

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
        #endregion

        #region Collider Functionality
        protected override void PlayerEnteredZone() => TriggerCheckpoint();

        protected override void PlayerExitedZone() { }
        #endregion
    }
}
