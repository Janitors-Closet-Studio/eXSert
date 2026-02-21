using System;
using UnityEngine;

namespace Progression.Encounters
{
    public class EnemySpawnMarker : MonoBehaviour
    {
        #region Inspector Setup
        [Header("Spawn Marker")]
        [SerializeField] private EnemyType enemyType;
        [Tooltip("Prefab used by the factory to spawn this marker's enemy.")]
        [SerializeField] private GameObject enemyPrefab;

        [Header("Transform")]
        [SerializeField, Tooltip("Use the marker's rotation when spawning. Otherwise prefab's default rotation is used.")]
        private bool useMarkerRotation = true;
        [SerializeField, Tooltip("Optional parent to attach spawned enemies to.")]
        private Transform parentOverride;
        #endregion

        public GameObject EnemyPrefab => enemyPrefab;
        public Vector3 SpawnPosition => transform.position;
        public Quaternion SpawnRotation => useMarkerRotation ? transform.rotation : Quaternion.identity;
        public Transform ParentOverride => parentOverride;

        private void Awake()
        {
            if (!Validate())
            {
                Debug.LogWarning($"[EnemySpawnMarker] Validation failed for marker '{name}'. This marker will not spawn an enemy.");
                return;
            }
        }

        // Disable the visual marker in-game, but keep it active in the editor for design
        private void Start() => transform.GetChild(0).gameObject.SetActive(false);

        private void OnValidate() => Validate();

        private bool Validate()
        {
            if (enemyPrefab != null && !enemyPrefab.TryGetComponent<BaseEnemyCore>(out _))
            {
                Debug.LogWarning($"[EnemySpawnMarker] Assigned prefab '{enemyPrefab.name}' on marker '{name}' does not contain a BaseEnemyCore component. This marker will not spawn an enemy.");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Ask the factory for an enemy instance using this marker's settings.
        /// Returns the spawned/pooled BaseEnemyCore or null on failure.
        /// </summary>
        public BaseEnemyCore SpawnEnemy()
        {
            if (enemyPrefab == null)
            {
                Debug.LogError($"[EnemySpawnMarker] No enemy prefab assigned on marker '{name}'.");
                return null;
            }

            var rotation = useMarkerRotation ? transform.rotation : Quaternion.identity;
            return EnemyFactory.RequestEnemy(enemyPrefab, transform.position, rotation, parentOverride);
        }
    }

    internal enum EnemyType
    {
        Alarm, Bomb, Boxer, Crawler, Drone, ETurret, PTurret
    }
}
