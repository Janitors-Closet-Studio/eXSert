using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UIandUXSystems.HUD;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Progression.Encounters
{
    public class CombatEncounter : BasicEncounter
    {
        #region Inspector Setup
        [Header("Combat Encounter Settings")]
        [SerializeField]
        private bool dropObjectOnClear = false;

        [SerializeField]
        private GameObject objectToDrop;

        [SerializeField]
        private bool loadSceneOnClear = false;

        [SerializeField]
        private SceneAsset sceneToLoadOnClear;

        [SerializeField]
        private bool dropAtLastEnemyPosition = true;

        [SerializeField]
        private float dropYOffset = 0.5f;
        #endregion

        private Vector3 lastEnemyPosition;
        private bool hasLastEnemyPosition;

        protected override Color DebugColor => Color.red;

        private readonly List<Wave> allWaves = new();
        private readonly Queue<Wave> wavesQueue = new();

        private bool encounterStarted = false;
        private Coroutine waveAdvanceRoutine;

        #region Basic Encounter Overrides
        protected override void SetupEncounter()
        {
            base.SetupEncounter();

            OnEncounterCompleted += DropItem;
            OnEncounterCompleted += LoadSceneOnClear;

            allWaves.Clear();

            foreach (Transform child in transform)
            {
                if (!child.name.ToLower().Contains("wave") || !child.gameObject.activeSelf)
                    continue;

                Wave newWave = SetupWave(child);
                newWave.OnWaveComplete += WaveComplete;
                newWave.UpdateLastEnemyPosition += OnUpdateLastEnemyPosition;
                allWaves.Add(newWave);
            }

            ResetWaves();

            Wave SetupWave(Transform parentObject)
            {
                if (debugMessagesEnabled)
                {
                    Debug.Log(
                        $"[CombatEncounter] Setting up wave: {parentObject.name} for encounter: {name}"
                    );
                }

                Wave waveComponent = parentObject.TryGetComponent<Wave>(out var existingWave)
                    ? existingWave
                    : parentObject.gameObject.AddComponent<Wave>();

                List<GameObject> enemiesToAdd = new();
                foreach (Transform waveChild in parentObject)
                    enemiesToAdd.Add(waveChild.gameObject);

                return waveComponent.Initialize(enemiesToAdd, debugMessagesEnabled);
            }
        }

        protected override void PlayerEnteredZone() => BeginEncounter();

        protected override void CleanupEncounter()
        {
            base.CleanupEncounter();

            foreach (Wave wave in allWaves)
                CleanupWave(wave);

            OnEncounterCompleted -= DropItem;
            OnEncounterCompleted -= LoadSceneOnClear;
        }
        #endregion

        private void OnUpdateLastEnemyPosition(Vector3 position)
        {
            lastEnemyPosition = position;
            hasLastEnemyPosition = true;
        }

        private void BeginEncounter()
        {
            if (encounterStarted)
            {
                Debug.LogWarning(
                    $"[CombatEncounter] Player entered encounter zone for {name}, but the encounter has already started. Ignoring."
                );
                return;
            }

            if (debugMessagesEnabled)
            {
                Debug.Log(
                    $"[CombatEncounter] Encounter started: {name} with {wavesQueue.Count} wave/s"
                );
            }

            encounterStarted = true;
            SpawnNextWave();
        }

        private void DropItem()
        {
            if (!dropObjectOnClear || objectToDrop == null)
                return;

            Vector3 dropPosition = objectToDrop.transform.position;
            if (dropAtLastEnemyPosition && hasLastEnemyPosition)
                dropPosition = lastEnemyPosition + new Vector3(0f, dropYOffset, 0f);

            Debug.Log(
                $"[CombatEncounter] Dropping object {objectToDrop.name} at position {dropPosition}"
            );

            objectToDrop.transform.position = dropPosition;
            objectToDrop.SetActive(true);
        }

        private void LoadSceneOnClear()
        {
            if (!loadSceneOnClear)
                return;

            if (sceneToLoadOnClear == null)
            {
                Debug.LogWarning(
                    $"[CombatEncounter] {name} is set to load a scene on clear, but no SceneAsset is assigned."
                );
                return;
            }

            Debug.Log(
                $"[CombatEncounter] Loading scene {sceneToLoadOnClear.name} after clearing encounter {name}."
            );
            SceneLoader.Load(sceneToLoadOnClear, loadScreen: false);
        }

        #region Wave Manipulation Functions
        private void WaveComplete(Wave completedWave)
        {
            Debug.Log($"[CombatEncounter] Wave completed: {completedWave}");

            if (wavesQueue.Peek() != completedWave) return;

            // Gets the waves delay before cleaning up and dequeuing it
            float delay = completedWave.TimeBeforeNextWaveStarts;

            CleanupWave(completedWave);
            wavesQueue.Dequeue();

            // Spawns the next wave if there are more waves in the queue
            if (wavesQueue.Count != 0) SpawnNextWave(delay);

            // If there are no more waves, then the encounter is completed and should be cleaned up
            else HandleEncounterCompleted();
        }

        private void CleanupWave(Wave wave)
        {
            wave.Cleanup();
            wave.UpdateLastEnemyPosition -= OnUpdateLastEnemyPosition;
            wave.OnWaveComplete -= WaveComplete;
        }

        private async void SpawnNextWave(float delay = 0f)
        {
            if (delay > 0f)
                await Task.Delay(TimeSpan.FromSeconds(delay));

            if (wavesQueue.Count == 0)
            {
                Debug.LogError(
                    $"[CombatEncounter] No more waves to spawn for encounter {name}. This should not happen if the encounter is properly completed. Please check the encounter setup."
                );
                return;
            }

            Wave currentWave = wavesQueue.Peek();

            if (debugMessagesEnabled)
                Debug.Log($"[CombatEncounter] Spawning next wave: {currentWave}");

            // Updates the objective if the wave has objective text
            if (!string.IsNullOrEmpty(currentWave.WaveObjectiveText)) 
                InvokeUpdateObjective(currentWave.WaveObjectiveText);

            currentWave.SpawnEnemies();
        }

        private void ResetWaves()
        {
            if (debugMessagesEnabled)
                Debug.Log($"[CombatEncounter] Resetting waves for encounter: {name}");

            wavesQueue.Clear();
            encounterStarted = false;
            hasLastEnemyPosition = false;
            lastEnemyPosition = Vector3.zero;

            if (waveAdvanceRoutine != null)
            {
                StopCoroutine(waveAdvanceRoutine);
                waveAdvanceRoutine = null;
            }

            foreach (Wave wave in allWaves)
            {
                wavesQueue.Enqueue(wave);
                wave.ResetEnemies();
            }
        }

        [ContextMenu("Generate New Wave")]
        public GameObject GenerateNewWave()
        {
            int count = 1;
            foreach (Transform child in transform)
            {
                if (!child.name.ToLower().Contains("wave"))
                    continue;

                count++;
            }

            GameObject newWaveObject = new($"Wave {count}");
            newWaveObject.transform.SetParent(transform);
            newWaveObject.transform.localPosition = Vector3.zero;
            Wave newWave = newWaveObject.AddComponent<Wave>();

            if (debugMessagesEnabled)
                Debug.Log($"[CombatEncounter] Generated new wave: {newWave} for encounter: {name}");

            return newWaveObject;
        }
        #endregion
    }
}
