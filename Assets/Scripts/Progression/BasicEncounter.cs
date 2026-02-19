/*
 * Made by Brandon, Implemented by Will
 * 
 * The core framework for implementing the Encounters
 * 
 */

using UnityEngine;

namespace Progression.Encounters
{
    [RequireComponent(typeof(BoxCollider))]
    public abstract class BasicEncounter : ProgressionZone
    {
        public string encounterName => this.gameObject.name;

        #region Inspector Settup
        [Header("Encounter Settings")]

        [SerializeField]
        private bool enableZoneOnComplete = false;

        [SerializeField]
        public ProgressionZone encounterToEnable;

        [SerializeField, Tooltip("Seconds to wait before enabling the next encounter.")]
        protected float enableNextEncounterDelaySeconds = 3f;
        #endregion

        /// <summary>
        /// Indicates whether the encounter has been completed
        /// </summary>
        public abstract bool isCompleted { get; }

        public bool isSetup => !isCleanedUp;

        /// <summary>
        /// Indicates whether the encounter has been cleaned up after completion.
        /// </summary>
        public bool isCleanedUp { get; private set; } = true;

        public abstract string ObjectiveText { get; }

        // Event to update the objective text in the HUD, passing the new objective string as a parameter
        public event System.Action<string> UpdateObjective;
        protected void InvokeUpdateObjective(string newObjective) => UpdateObjective?.Invoke(newObjective);

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

            UpdateObjective?.Invoke(ObjectiveText);
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
            if (!enableZoneOnComplete || encounterToEnable == null) return;

            if (enableNextEncounterDelaySeconds > 0f) StartCoroutine(EnableEncounterAfterDelay());
            else  encounterToEnable.EnableZone();
        }

        private System.Collections.IEnumerator EnableEncounterAfterDelay()
        {
            yield return new WaitForSeconds(enableNextEncounterDelaySeconds);
            encounterToEnable.EnableZone();
        }

        protected void SetEnableNextEncounterDelaySeconds(float seconds) =>
            enableNextEncounterDelaySeconds = Mathf.Max(0f, seconds);
        
    }
}