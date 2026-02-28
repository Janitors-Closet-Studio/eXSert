/* 
    Written by Brandon Wahl

    This script manages the progression of a zone by tracking the completion status of multiple puzzles and combat encounters.
    It keeps track of all the encounters within the scene and manages communication between the different encounters.
    
    Written later on by Will T
*/

using Singletons;
using System.Collections.Generic;
using UnityEngine;
using UIandUXSystems.HUD;

namespace Progression
{
    using Encounters;
    using SceneManagement;

    [HelpURL("")]
    public class ProgressionManager : SceneSingleton<ProgressionManager>
    {
        #region Inspector Setup
        [Header("Progression Settings")]
        [Header("Prewarming")]
        [SerializeField, Tooltip("If true, the manager will prewarm the specified prefabs at the start of the scene. This can help reduce lag spikes when those prefabs are first instantiated.")]
        private bool usePrewarmer = false;
        [SerializeField, Tooltip("If true, the manager will prewarm the specified prefabs at the start of the scene. This can help reduce lag spikes when those prefabs are first instantiated.")]
        private GameObject[] prefabsToPrewarm;

        #endregion

        private int totalEncountersInScene = 0;

        /// <summary>
        /// Indicates whether all encounters in the scene have been completed
        /// </summary>
        private bool allZonesComplete = false;

        private readonly List<BasicEncounter> encounterCompletionMap = new();

        private readonly List<SceneLoadZone> zonesLoaded = new();

        #region Monobehavior Methods
        protected override void Awake()
        {
            base.Awake(); // Singleton behavior

            this.gameObject.name = $"[{SceneAsset.GetSceneAssetOfObject(this.gameObject).name}] Progression Manager";

            if (usePrewarmer) PrewarmEnemies();
        }

        private void OnDisable()
        {
            foreach (BasicEncounter encounter in encounterCompletionMap)
            {
                if (encounter != null && !encounter.isCleanedUp)
                    encounter.ManualCleanUpCall();
            }

            encounterCompletionMap.Clear();
        }
        #endregion

        private void UpdateObjective(string newObjective)
        {
            PlayerHUD.SetObjective(newObjective);
        }

        #region Progression Management
        /// <summary>
        /// Adds the encounter to the manager's database
        /// </summary>
        /// <param name="encounter"></param>
        internal static void AddProgressable(ProgressionZone zone)
        {
            // Get the manager for this zone's scene and add the zone to the appropriate list
            ProgressionManager manager = GetInstance(SceneAsset.GetSceneAssetOfObject(zone.gameObject));

            switch (zone)
            {
                case BasicEncounter encounter:
                    manager.encounterCompletionMap.Add(encounter);
                    manager.totalEncountersInScene++;

                    // Subscribe the manager's UpdateObjective method to the encounter's UpdateObjective event
                    // When the encounter triggers an objective update, the manager can relay that to the HUD
                    encounter.UpdateObjective += manager.UpdateObjective;
                    break;

                case SceneLoadZone loadZone:
                    manager.zonesLoaded.Add(loadZone);
                    break;

                default:
                    // No action for other ProgressionZone types (preserve original behavior)
                    Debug.LogWarning($"[ProgressionManager] Added ProgressionZone {zone.gameObject.name} of type {zone.GetType()} that is not explicitly handled by AddProgressable. No action taken.");
                    break;
            }
        }
        #endregion

        private void PrewarmEnemies()
        {
            Dictionary<GameObject, int> shoppingList = new();

            // Create a shopping list of prefabs to prewarm and their quantities
            foreach (GameObject prefab in prefabsToPrewarm)
            {
                if (prefab == null) continue;

                GameObject intendedPrefab;

                if (prefab.TryGetComponent<EnemySpawnMarker>(out EnemySpawnMarker marker))
                {
                    Debug.LogWarning($"[ProgressionManager] Prefab {prefab.name} is a spawn marker for an enemy. " +
                        $"Please use the enemy prefab instead. " +
                        $"Function will get the correct prefab that was intended to prewarm.");

                    intendedPrefab = marker.EnemyPrefab;

                    if (!intendedPrefab.TryGetComponent<BaseEnemyCore>(out _))
                    {
                        Debug.LogError($"[ProgressionManager] Nevermind. The prefab that is in the marker isn't even a proper enemy. " +
                            $"Did you even learn how to use any of this? " +
                            $"Obviously skipping over this prefab.");
                        continue;
                    }
                }

                else if (prefab.GetComponent<BaseEnemyCore>() == null)
                {
                    Debug.LogWarning($"[ProgressionManager] Prefab {prefab.name} does not have a BaseEnemyCore component and will be skipped in prewarming.");
                    continue;
                }
                
                else intendedPrefab = prefab;

                // Forming the shopping list
                if (!shoppingList.ContainsKey(intendedPrefab))
                    shoppingList[intendedPrefab] = 1;
                else
                    shoppingList[intendedPrefab]++;
            }

            // The prewarming part. It's a single command
            foreach (KeyValuePair<GameObject, int> entry in shoppingList)
                EnemyFactory.Prewarm(entry.Key, entry.Value);
        }
    }
}

