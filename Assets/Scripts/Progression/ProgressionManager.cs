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

    public class ProgressionManager : SceneSingleton<ProgressionManager>
    {
        #region Inspector Setup
        [Header("Progression Settings")]

        [Header("Stats")]
        [SerializeField, Tooltip("The total number of encounters in the scene. " +
            "This is used for tracking progression and should be set to the total number of encounters in the scene.")]
        private int totalEncountersInScene = 0;
        #endregion

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
    }
}

