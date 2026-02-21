using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Progression.Encounters
{
    public class CombatEncounter : BasicEncounter
    {
        #region Inspector Setup
        [Header("Combat Encounter Settings")]

        [SerializeField] private bool tempIsCompleted;

        [Header("Timing")]
        [SerializeField, Tooltip("Seconds to wait before advancing to the next wave.")]
        private float nextWaveDelaySeconds = 0.15f;

        [Header("Progression")]
        [SerializeField] private bool autoFindByTag = false;
        [SerializeField] private string enemyTag = "Enemy";


        [SerializeField] private bool dropObjectOnClear = false;
        [SerializeField] private GameObject objectToDrop;
        private bool dropAtLastEnemyPosition = true;
        #endregion

        private Vector3 lastEnemyPosition;

        public override bool isCompleted { get => tempIsCompleted; }

        public override string ObjectiveText 
        { 
            get => wavesQueue.Count > 0 ? wavesQueue.Peek().WaveObjectiveText : "Encounter Completed!";
        }

        protected override Color DebugColor { get => Color.red; }

        /// <summary>
        /// The entire list of each wave. All waves persist even once they are compleated
        /// </summary>
        private readonly List<Wave> allWaves = new();

        /// <summary>
        /// The queue of the incoming waves. Waves are removed once they are compleated
        /// </summary>
        private readonly Queue<Wave> wavesQueue = new();

        private bool encounterStarted = false;
        private Coroutine waveAdvanceRoutine;

        private void OnUpdateLastEnemyPosition(Vector3 position) => lastEnemyPosition = position;

        protected override void SetupEncounter()
        {
            base.SetupEncounter();

            allWaves.Clear(); // Clear any existing waves in case of editor changes or scene reload

            // iterates through each child object under this encounter
            foreach (Transform child in transform)
            {
                // if the child object doesn't have "wave" in the name, or if its not active, skip it.
                // This allows for organization of the encounter gameobject without breaking functionality
                if (!child.name.ToLower().Contains("wave") || !child.gameObject.activeSelf) continue;

                // Create or get a Wave component on the child object (wave root)
                Wave newWave = SetupWave(child);

                newWave.OnWaveComplete += WaveComplete;
                newWave.UpdateLastEnemyPosition += OnUpdateLastEnemyPosition;

                allWaves.Add(newWave);
            }

            ResetWaves();

            // local function to create a new wave component and initialize it
            Wave SetupWave(Transform parentObject)
            {
                if(debugMessagesEnabled) Debug.Log($"[CombatEncounter] Setting up wave: {parentObject.name} for encounter: {name}");

                // Try to get an existing Wave component on the parent object. If it doesn't exist, add a new one.
                Wave waveComponent = parentObject.TryGetComponent<Wave>(out var existingWave) ? waveComponent = existingWave :
                     waveComponent = parentObject.gameObject.AddComponent<Wave>();

                List <GameObject> enemiesToAdd = new();
                foreach (Transform waveChild in parentObject) enemiesToAdd.Add(waveChild.gameObject);

                return waveComponent.Initialize(enemiesToAdd, debugMessagesEnabled);
            }
        }

        protected override void PlayerEnteredZone() => BeginEncounter();
        private void BeginEncounter()
        {
            if (encounterStarted)
            {
                Debug.LogWarning($"[CombatEncounter] Player entered encounter zone for {name}, but the encounter has already started. Ignoring.");
                return;
            }

            if(debugMessagesEnabled) Debug.Log($"[CombatEncounter] Encounter started: {name} with {wavesQueue.Count} wave/s");

            encounterStarted = true;
            SpawnNextWave();
        }

        private void CompleteEncounter()
        {
            if (debugMessagesEnabled) Debug.Log($"[CombatEncounter] Encounter completed: {name}");

            DropItem();
        }

        private void DropItem()
        {
            if (!dropObjectOnClear || objectToDrop == null)
                return;

            Vector3 dropPosition = objectToDrop.transform.position;
            if (dropAtLastEnemyPosition && lastEnemyPosition != null)
                dropPosition = lastEnemyPosition + Vector3.forward;

            Debug.Log($"[CombatEncounter] Dropping object {objectToDrop.name} at position {dropPosition}");

            objectToDrop.transform.position = dropPosition;
            objectToDrop.SetActive(true);
        }

        #region Wave Manipulation Functions
        private void WaveComplete(Wave completedWave)
        {
            Debug.Log($"[CombatEncounter] Wave completed: {completedWave}");

            if (wavesQueue.Peek() != completedWave) return;

            CleanupWave(completedWave);

            wavesQueue.Dequeue();

            // Check if there are more waves to spawn
            if (wavesQueue.Count != 0) SpawnNextWave(3f);
            else CompleteEncounter();
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
            {
                await Task.Delay(TimeSpan.FromSeconds(delay));
            }

            // Ejects from the function early if there are no more waves to spawn
            if (wavesQueue.Count == 0) 
            { 
                Debug.LogError($"[CombatEncounter] No more waves to spawn for encounter {name}. This should not happen if the encounter is properly completed. Please check the encounter setup.");
                return; 
            }

            Wave currentWave = wavesQueue.Peek();

            if (debugMessagesEnabled) Debug.Log($"[CombatEncounter] Spawning next wave: {currentWave}");
            InvokeUpdateObjective(currentWave.WaveObjectiveText);
            currentWave.SpawnEnemies();
        }

        private void ResetWaves()
        {
            if (debugMessagesEnabled) Debug.Log($"[CombatEncounter] Resetting waves for encounter: {name}");

            wavesQueue.Clear();
            encounterStarted = false;

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
        /// <summary>
        /// Creates and adds a new wave GameObject as a child of the current transform.
        /// </summary>
        /// <remarks>The new wave is named sequentially based on existing child objects whose names
        /// contain "wave". The wave is positioned at the origin relative to its parent and includes a <see
        /// cref="Wave"/> component. Intended to be called while in the editor.</remarks>
        /// <returns>The newly created <see cref="GameObject"/> representing the wave.</returns>
        public GameObject GenerateNewWave()
        {
            int count = 1;
            foreach (Transform child in transform)
            {
                if (!child.name.ToLower().Contains("wave")) continue;
                count++;
            }
            GameObject newWaveObject = new($"Wave {count}");
            newWaveObject.transform.SetParent(transform);
            newWaveObject.transform.localPosition = Vector3.zero;
            Wave newWave = newWaveObject.AddComponent<Wave>();
            if (debugMessagesEnabled) Debug.Log($"[CombatEncounter] Generated new wave: {newWave} for encounter: {name}");
            return newWaveObject;
        }
        #endregion
    }
}