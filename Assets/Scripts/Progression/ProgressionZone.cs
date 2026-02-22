using UnityEngine;

namespace Progression
{
    [RequireComponent(typeof(BoxCollider))]
    public abstract class ProgressionZone : MonoBehaviour
    {
        #region Inspector Setup
        [Header("Progression Zone Settings")]
        [SerializeField, Tooltip("Whether the zone is enabled at the start of the scene. If false, the player will not trigger the zone until it is enabled by another encounter.")]
        private bool startEnabled = true;

        [SerializeField]
        private bool SendDebugMessages = false;
        #endregion

        protected BoxCollider progressionCollider;

        protected bool debugMessagesEnabled => SendDebugMessages;

        /// <summary>
        /// Indicates if the encounter can be started when the player is in the zone
        /// </summary>
        protected bool zoneEnabled = true;

        /// <summary>
        /// Indicates whether the player is currently within the encounter zone
        /// </summary>
        protected bool zoneActive = false;

        protected virtual void Awake()
        {
            progressionCollider = GetComponent<BoxCollider>();

            if (progressionCollider == null)
                Debug.LogError("ProgressionZone requires a BoxCollider component.");
            else
                progressionCollider.isTrigger = true;
        }

        protected virtual void Start()
        {
            AddToManager();

            if (!startEnabled) DisableZone();
            else EnableZone();
        }

        private void AddToManager() => ProgressionManager.AddProgressable(this);

        public void EnableZone()
        {
            zoneEnabled = true;
            UpdateCollider();
        }

        public void DisableZone()
        {
            zoneEnabled = false;
            UpdateCollider();
        }

        private void UpdateCollider() => progressionCollider.enabled = zoneEnabled;

        #region Collider Triggers
        protected void OnTriggerEnter(Collider other)
        {
            if (!zoneEnabled || !other.transform.root.CompareTag("Player")) return;
            zoneActive = true;
            PlayerEnteredZone();
        }
        protected void OnTriggerExit(Collider other)
        {
            if (!zoneEnabled || !other.transform.root.CompareTag("Player")) return;
            zoneActive = false;
            PlayerExitedZone();
        }

        protected abstract void PlayerEnteredZone();
        protected abstract void PlayerExitedZone();
        #endregion

        #region Debug Scripts
        protected abstract Color DebugColor { get; }

        private void OnDrawGizmos()
        {
            if (progressionCollider == null)
                progressionCollider = GetComponent<BoxCollider>();

            Gizmos.color = DebugColor;
            Gizmos.DrawWireCube(progressionCollider.bounds.center, progressionCollider.bounds.size);
        }
        #endregion
    }
}
