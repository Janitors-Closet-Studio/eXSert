using System;
using System.Collections.Generic;
using UnityEngine;

namespace Progression.Encounters
{
    [HelpURL("https://docs.google.com/document/d/18pi24ZJ65GG307F6SvKpSoHPs0izxSb6yZ6cfjvYqMQ/edit?pli=1&tab=t.0#bookmark=id.5a4pt81lek3x")]
    internal class Wave : MonoBehaviour
    {
        #region Inspector Setup
        [Header("Wave Settings")]
        public string WaveObjectiveText = "Defeat all enemies in this wave!";

        #endregion

        private readonly List<EnemySpawnMarker> spawnMarkers = new();
        private readonly List<BaseEnemyCore> enemies = new();

        private bool waveCompleted;
        private bool debugMessagesEnabled = false;

        public event Action<Wave> OnWaveComplete;
        public event Action<Vector3> UpdateLastEnemyPosition;

        /// <summary>
        /// Initializes the wave by assigning enemy spawn markers from the specified child GameObjects.
        /// </summary>
        /// <remarks>Only child GameObjects containing an <see cref="EnemySpawnMarker"/> component are
        /// considered for spawning. Any GameObject without an <see cref="EnemySpawnMarker"/> is ignored, and a warning
        /// is logged. The method clears any existing spawn markers before reinitializing.</remarks>
        /// <param name="children">The list of child <see cref="GameObject"/> instances to be evaluated for enemy spawn markers. Must not be
        /// <see langword="null"/> or empty.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="children"/> is <see langword="null"/> or contains no elements.</exception>
        public Wave Initialize(List<GameObject> children, bool enableDebug = false)
        {
            if (children == null || children.Count == 0)
                throw new ArgumentNullException(nameof(children), $"[{name} of combat encounter {transform.parent.name}] No enemy GameObjects provided for wave initialization.");

            // Debug log for wave initialization with the number of potential spawn points found
            if (debugMessagesEnabled = enableDebug) Debug.Log($"[Wave] Initializing wave: {name} with {children.Count} potential enemy spawn markers.");
            waveCompleted = false;

            spawnMarkers.Clear(); // Clear any existing spawn markers before reinitializing
            foreach (var child in children)
            {
                if (child.TryGetComponent<EnemySpawnMarker>(out var validMarker)) 
                    spawnMarkers.Add(validMarker);
                else
                    Debug.LogWarning($"[{name} of combat encounter {transform.parent.name}] Child GameObject {child.name} does not contain an EnemySpawnMarker component and will be ignored.");
            }
            if (spawnMarkers.Count == 0)
                Debug.LogWarning($"[{name} of combat encounter {transform.parent.name}] No valid enemy spawn markers found among the provided child GameObjects.");

            return this;
        }

        public override string ToString() => $"{gameObject.name} with {enemies.Count} enemies";

        public void Cleanup() => UnsubscribeAllEnemies();
        private void UnsubscribeAllEnemies()
        {
            if (debugMessagesEnabled) Debug.Log($"[{name} of combat encounter {transform.parent.name}] Unsubscribing from all enemy death events.");
            foreach (var enemy in enemies) enemy.OnDeath -= OnEnemyDefeated;
        }

        public void SpawnEnemies()
        {
            if (debugMessagesEnabled)
                Debug.Log($"[{name} of combat encounter {transform.parent.name}] Spawning wave.");

            // tells each spawn marker to spawn its enemy and then initilialize it for tracking and setup
            foreach (EnemySpawnMarker marker in spawnMarkers) InitializeEnemy(marker.SpawnEnemy());

            // Local function to initialize each spawned enemy by subscribing to its OnDeath event and adding it to the enemies list for tracking
            void InitializeEnemy(BaseEnemyCore enemy)
            {
                if (enemy == null) throw new ArgumentNullException(nameof(enemy), $"[{name} of combat encounter {transform.parent.name}] Spawned enemy is null. This should not happen if the EnemySpawnMarker and EnemyFactory are properly set up.");
                enemies.Add(enemy);
                enemy.OnDeath -= OnEnemyDefeated; // Prevent double-subscription
                enemy.OnDeath += OnEnemyDefeated;
            }
        }

        public void ResetEnemies()
        {
            foreach (BaseEnemyCore enemy in enemies)
                enemy.ResetEnemy();

            waveCompleted = false;
        }

        private void OnEnemyDefeated(BaseEnemyCore enemy)
        {
            if (debugMessagesEnabled) Debug.Log($"[CombatEncounter] Enemy defeated: {enemy.name}"); // Debug log for enemy defeat with the name of the defeated enemy

            if (!enemies.Contains(enemy)) return;

            UpdateLastEnemyPosition?.Invoke(enemy.transform.position);

            enemy.OnDeath -= OnEnemyDefeated; // Unsubscribe to prevent memory leaks
            enemies.Remove(enemy);

            if (!RemainingEnemiesCheck() && !waveCompleted)
                OnWaveComplete?.Invoke(this); // trigger next wave or end encounter

            bool RemainingEnemiesCheck()
            {
                if (enemies == null || enemies.Count == 0)
                    return false;

                foreach (var enemy in enemies)
                    if (enemy != null && enemy.isAlive)
                        return true;

                return false;
            }
        }

        private void OnDestroy() => Cleanup();
    }
}