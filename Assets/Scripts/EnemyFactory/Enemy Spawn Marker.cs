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
        [SerializeField] private GameObject[] enemyPrefabs;

        [Header("Transform")]
        [SerializeField, Tooltip("Use the marker's rotation when spawning. Otherwise prefab's default rotation is used.")]
        private bool useMarkerRotation = true;
        [SerializeField, Tooltip("Optional parent to attach spawned enemies to.")]
        private Transform parentOverride;
        #endregion

        public GameObject EnemyPrefab
        {
            // If the chosen prefab is null (initial access), choose a variant to use and cache it
            get
            {
                if (_chosenPrefab == null) _chosenPrefab = ChooseVariant();
                if (_chosenPrefab == null)
                    Debug.LogError($"[EnemySpawnMarker] Failed to choose an enemy prefab for marker '{name}'. Ensure that valid prefabs are assigned. This marker will not spawn an enemy.");
                
                return _chosenPrefab;
            }
        }
        private GameObject _chosenPrefab = null;

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
            // Checks that all assigned prefabs are valid enemy prefabs
            if (enemyPrefabs != null && enemyPrefabs.Length > 0)
            {
                foreach (var prefab in enemyPrefabs)
                {
                    if (prefab != null && prefab.GetComponentInChildren<BaseEnemyCore>(includeInactive: true) == null)
                    {
                        Debug.LogWarning($"[EnemySpawnMarker] Assigned prefab '{prefab.name}' on marker '{name}' does not contain a BaseEnemyCore component. This marker will not spawn an enemy.");
                        return false;
                    }
                }
            }

            // Warns if the array is empty
            else if (enemyPrefabs.Length == 0)
            {
                Debug.LogWarning($"[EnemySpawnMarker] No enemy prefabs assigned on marker '{name}'. This marker will not spawn an enemy.");
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
            if (_chosenPrefab == null)
            {
                Debug.LogError($"[EnemySpawnMarker] Cannot spawn enemy from marker '{name}' because no valid prefab was chosen. Ensure that valid prefabs are assigned. This marker will not spawn an enemy.");
                return null;
            }

            var rotation = useMarkerRotation ? transform.rotation : Quaternion.identity;
            var parent = parentOverride != null ? parentOverride : transform.parent;

            return EnemyFactory.RequestEnemy(EnemyPrefab, transform.position, rotation, parent);
        }

        private GameObject ChooseVariant()
        {
            if (enemyPrefabs == null || enemyPrefabs.Length == 0)
            {
                Debug.LogWarning($"[EnemySpawnMarker] No enemy prefabs assigned on marker '{name}'. Cannot choose a variant.");
                return null;
            }

            GameObject chosenPrefab = (enemyPrefabs.Length == 1) ?
                enemyPrefabs[0] : // Only one prefab
                enemyPrefabs[UnityEngine.Random.Range(0, enemyPrefabs.Length)]; // Multiple prefabs

            return chosenPrefab;
        }
    }

    internal enum EnemyType
    {
        Alarm, Bomb, Boxer, Crawler, Drone, ETurret, PTurret
    }
}
