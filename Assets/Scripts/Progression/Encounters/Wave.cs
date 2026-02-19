using System;
using System.Collections.Generic;
using UnityEngine;

namespace Progression.Encounters
{
    public class Wave : MonoBehaviour
    {
        #region Inspector Setup
        [Header("Wave Settings")]
        public string WaveObjectiveText = "Defeat all enemies in this wave!";

        #endregion
        private List<GameObject> enemyGameObjects = new();
        private readonly List<BaseEnemyCore> enemies = new();

        private bool waveCompleted;
        private bool initialized;

        public event Action<Wave> OnWaveComplete;
        public event Action<Vector3> UpdateLastEnemyPosition;

        // Call to set up this Wave when created from code
        public void Initialize(List<GameObject> _enemies)
        {
            enemyGameObjects = _enemies ?? new List<GameObject>();
            waveCompleted = false;
            InitializeEnemies();
            initialized = true;
        }

        private void Awake()
        {
            // If Initialize wasn't called from code, auto-discover children as enemies
            if (initialized) return;

            if (enemyGameObjects == null || enemyGameObjects.Count == 0)
            {
                enemyGameObjects = new List<GameObject>();
                foreach (Transform child in transform)
                    enemyGameObjects.Add(child.gameObject);
            }

            InitializeEnemies();
            initialized = true;
        }

        private void InitializeEnemies()
        {
            enemies.Clear();
            foreach (var enemy in enemyGameObjects)
            {
                if (enemy == null) continue;

                if (enemy.TryGetComponent<BaseEnemyCore>(out var enemyCore))
                {
                    enemies.Add(enemyCore);
                    enemyCore.OnDeath -= OnEnemyDefeated; // Prevent double-subscription
                    enemyCore.OnDeath += OnEnemyDefeated;
                }
            }
        }

        public override string ToString()
        {
            return $"{gameObject.name} with {enemies.Count} enemies";
        }

        public void Cleanup() => UnsubscribeAllEnemies();

        private void UnsubscribeAllEnemies()
        {
            foreach (var enemy in enemies)
                if (enemy != null)
                    enemy.OnDeath -= OnEnemyDefeated;
        }

        public void SpawnEnemies()
        {
            Debug.Log($"[CombatEncounter] Spawning wave: {this}");

            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            foreach (BaseEnemyCore enemy in enemies)
            {
                enemy.Spawn();
            }
        }

        public void ResetEnemies()
        {
            foreach (BaseEnemyCore enemy in enemies)
            {
                enemy.ResetEnemy();
            }

            waveCompleted = false;
        }

        private void OnEnemyDefeated(BaseEnemyCore enemy)
        {
            Debug.Log($"[CombatEncounter] Enemy defeated: {enemy.name}");

#if UNITY_EDITOR
            Debug.Log($"[CombatEncounter] OnEnemyDefeated invoked from:\n{Environment.StackTrace}");
#endif

            if (!enemies.Contains(enemy))
                return;

            UpdateLastEnemyPosition?.Invoke(enemy.transform.position);

            enemy.OnDeath -= OnEnemyDefeated; // Unsubscribe to prevent memory leaks
            enemies.Remove(enemy);

            if (!RemainingEnemiesCheck() && !waveCompleted)
                OnWaveComplete?.Invoke(this); // trigger next wave or end encounter
        }

        private bool RemainingEnemiesCheck()
        {
            if (enemies == null || enemies.Count == 0)
                return false;

            foreach (var enemy in enemies)
                if (enemy != null && enemy.isAlive)
                    return true;

            return false;
        }

        private void OnDestroy()
        {
            Cleanup();
        }

        private void OnValidate()
        {
            // Ensure that enemyGameObjects list is always in sync with child GameObjects in the editor

        }
    }
}