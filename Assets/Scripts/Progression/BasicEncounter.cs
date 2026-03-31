/*
 * Made by Brandon, Implemented by Will
 * 
 * The core framework for implementing the Encounters
 * 
 */

using System;
using UIandUXSystems.HUD;
using UnityEngine;

namespace Progression.Encounters
{
    [RequireComponent(typeof(BoxCollider))]
    [HelpURL("https://docs.google.com/document/d/18pi24ZJ65GG307F6SvKpSoHPs0izxSb6yZ6cfjvYqMQ/edit?pli=1&tab=t.0#bookmark=id.z4zoa520n2tr")]
    public abstract class BasicEncounter : ProgressionZone
    {
        public string encounterName => this.gameObject.name;

        #region Inspector Settup
        [Header("Encounter Settings")]

        [SerializeField]
        private bool enableZoneOnComplete = false;

        [SerializeField]
        public ProgressionZone encounterToEnable;
        #endregion

        /// <summary>
        /// Indicates whether the encounter has been completed
        /// </summary>
        public bool isCompleted { get; private set; } = false;
        public event Action OnEncounterCompleted;

        public bool isSetup => !isCleanedUp;

        /// <summary>
        /// Indicates whether the encounter has been cleaned up after completion.
        /// </summary>
        public bool isCleanedUp { get; private set; } = true;

        // Event to update the objective text in the HUD, passing the new objective string as a parameter
        public event System.Action<HUDMessage> UpdateObjective;
        protected void InvokeUpdateObjective(string newObjective) => 
            UpdateObjective?.Invoke(new(HUDMessageType.Objective, newObjective));

        protected override void Start()
        {
            base.Start();

            SetupEncounter();
        }

        private void OnDisable()
        {
            if (!isCleanedUp)
                CleanupEncounter();
        }

        #region Setup Functions
        /// <summary>
        /// The setup function for the encounter, called during Start after being added to the ProgressionManager
        /// </summary>
        protected virtual void SetupEncounter()
        {
            if (isSetup)
            {
                Debug.LogWarning($"Attempted Setup on encounter {encounterName} which is already set up. Aborting");
                return;
            }

            isCleanedUp = false;
        }

        /// <summary>
        /// The function to clean up the encounter after it is completed, called by the ProgressionManager when this encounter is marked as completed.
        /// </summary>
        protected virtual void CleanupEncounter()
        {
            isCleanedUp = true;

            DisableZone();
        }

        public void ManualEncounterStart()
        {
            Debug.Log($"[BasicEncounter] Manual start call for encounter {encounterName} in scene {SceneAsset.GetSceneAssetOfObject(this.gameObject).name}.");

            PlayerEnteredZone();

            EnableZone();
        }

        public void ManualCleanUpCall()
        {
            Debug.Log($"Manual cleanup call for encounter {encounterName} in scene {SceneAsset.GetSceneAssetOfObject(this.gameObject).name}.");
            CleanupEncounter();
        }
        #endregion

        #region Collider Functions
        protected override void PlayerEnteredZone()
        {
            if (debugMessagesEnabled)
                Debug.Log($"Player entered encounter zone: {encounterName}.");
        }

        protected override void PlayerExitedZone()
        {
            if (debugMessagesEnabled)
                Debug.Log($"Player exited encounter zone: {encounterName}.");

            if (isCompleted && !isCleanedUp) 
                CleanupEncounter();
        }
        #endregion

        protected void HandleEncounterCompleted()
        {
            if (debugMessagesEnabled) Debug.Log($"[BasicEncounter] Encounter completed: {name}");

            isCompleted = true;

            OnEncounterCompleted?.Invoke();

            if (enableZoneOnComplete && encounterToEnable != null)
                encounterToEnable.EnableZone();
            else if (enableZoneOnComplete)
            {
                Debug.LogWarning($"[BasicEncounter] Encounter {name} is set to enable another encounter on completion, but that encounter is not assigned. Enabling this encounter's own zone instead.");
            }
        }
    }
}