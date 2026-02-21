using System;
using System.Collections.Generic;
using UnityEngine;
using Singletons;

namespace Progression.Encounters
{
    public class EnemyFactory : Singleton<EnemyFactory>
    {
        /// <summary>
        /// Represents a collection of object pools for enemy instances, organized by their associated <see
        /// cref="GameObject"/> prefab.
        /// </summary>
        /// <remarks>Each entry in the dictionary maps a <see cref="GameObject"/> to a queue of <see
        /// cref="BaseEnemyCore"/> objects, enabling efficient reuse of enemy instances for that game object.</remarks>
        private readonly Dictionary<GameObject, Queue<BaseEnemyCore>> pools = new();

        /// <summary>
        /// Maintains a mapping between enemy core instances and their associated prefab <see cref="GameObject"/>s.
        /// </summary>
        /// <remarks>This dictionary is used to track which prefab corresponds to each <see cref="BaseEnemyCore"/>
        /// instance. It is intended for internal use within the class to facilitate operations such as instantiation or
        /// lookup of enemy prefabs.</remarks>
        private readonly Dictionary<BaseEnemyCore, GameObject> instanceToPrefab = new();

        public override string ToString() => $"EnemyFactory with {pools.Count} pools and {instanceToPrefab.Count} instances";

        /// <summary>
        /// Instantiates and stores a specified number of inactive enemy instances for the given prefab, preparing them for
        /// future use.
        /// </summary>
        /// <remarks>Use this method to reduce runtime instantiation overhead by preloading enemy instances into
        /// the internal pool.  If the specified prefab does not contain a <see cref="BaseEnemyCore"/> component, no
        /// instances will be created and an error will be logged.</remarks>
        /// <param name="prefab">The enemy prefab to pre-instantiate. Must contain a <see cref="BaseEnemyCore"/> component. Cannot be <see
        /// langword="null"/>.</param>
        /// <param name="count">The number of instances to create and store. Must be greater than or equal to 0.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="prefab"/> is <see langword="null"/>.</exception>
        public static void Prewarm(GameObject prefab, int count)
        {
            // Validate input parameters
            if (count < 1) throw new ArgumentOutOfRangeException(nameof(count), $"Count must be at least 1. Received: {count}");
            if (prefab == null) throw new ArgumentNullException(nameof(prefab));
            if (!prefab.TryGetComponent<BaseEnemyCore>(out _))
            {
                Debug.LogError($"[EnemyFactory] Prefab {prefab.name} does not contain a BaseEnemyCore. Cannot prewarm.");
                return;
            }

            if (!Instance.pools.TryGetValue(prefab, out var queue))
            {
                // Create a new pool for this prefab if it doesn't exist
                queue = new Queue<BaseEnemyCore>();
                Instance.pools[prefab] = queue;
            }

            for (int i = 0; i < count; i++)
            {
                var go = Instantiate(prefab, Instance.transform);
                go.SetActive(false);
                if (!go.TryGetComponent<BaseEnemyCore>(out var core))
                {
                    Destroy(go);
                    continue;
                }

                Instance.instanceToPrefab[core] = prefab;
                queue.Enqueue(core);
            }

            UpdateName();
        }

        /// <summary>
        /// Retrieves an instance of <see cref="BaseEnemyCore"/> from the pool or instantiates a new one using the specified
        /// prefab, position, rotation, and optional parent.
        /// </summary>
        /// <remarks>This method uses object pooling to reuse enemy instances when available, improving
        /// performance by minimizing instantiation overhead. If the pool for the specified prefab is empty, a new instance
        /// is created. The returned enemy is initialized and ready for use. The caller is responsible for managing the
        /// enemy's lifecycle as appropriate for the game logic.</remarks>
        /// <param name="prefab">The <see cref="GameObject"/> prefab containing a <see cref="BaseEnemyCore"/> component to use as the template
        /// for the enemy instance. Cannot be <see langword="null"/> and must include a <see cref="BaseEnemyCore"/>
        /// component.</param>
        /// <param name="position">The world position at which to spawn the enemy instance.</param>
        /// <param name="rotation">The world rotation to apply to the spawned enemy instance.</param>
        /// <param name="parent">The optional <see cref="Transform"/> to set as the parent of the spawned enemy instance. If <see
        /// langword="null"/>, the instance is created at the root level.</param>
        /// <returns>An active <see cref="BaseEnemyCore"/> instance positioned and rotated as specified. Returns <see
        /// langword="null"/> if the prefab does not contain a <see cref="BaseEnemyCore"/> component or if instantiation
        /// fails.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="prefab"/> is <see langword="null"/>.</exception>
        public static BaseEnemyCore RequestEnemy(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            // Validate input parameters
            if (prefab == null) throw new ArgumentNullException(nameof(prefab));
            if (!prefab.TryGetComponent<BaseEnemyCore>(out _))
            {
                Debug.LogError($"[EnemyFactory] Prefab {prefab.name} does not contain a BaseEnemyCore.");
                return null;
            }

            if (!Instance.pools.TryGetValue(prefab, out var queue) || queue.Count == 0)
            {
                // Instantiate a new instance when pool is empty
                var go = Instantiate(prefab, position, rotation, parent);
                if (!go.TryGetComponent<BaseEnemyCore>(out var core))
                {
                    Debug.LogError($"[EnemyFactory] Instance of {prefab.name} missing BaseEnemyCore.");
                    Destroy(go);
                    return null;
                }

                Instance.instanceToPrefab[core] = prefab;
                core.OnDeath += Instance.HandleEnemyDeath;
                core.Spawn(); // factory returns an already-spawned instance

                UpdateName();

                return core;
            }

            // Reuse pooled instance
            var enemy = queue.Dequeue();
            if (enemy == null)
                return RequestEnemy(prefab, position, rotation, parent); // defensive

            enemy.transform.SetParent(parent);
            enemy.transform.SetPositionAndRotation(position, rotation);
            enemy.gameObject.SetActive(true);
            enemy.ResetEnemy();
            enemy.OnDeath += Instance.HandleEnemyDeath;
            enemy.Spawn(); // ensure it's active and initialized

            UpdateName();

            return enemy;
        }

        private void HandleEnemyDeath(BaseEnemyCore enemy) => ReturnToPool(enemy);

        public void ReturnToPool(BaseEnemyCore enemy)
        {
            if (enemy == null) return;

            Debug.Log("[EnemyFactory] Returning enemy to pool: " + enemy.name);

            // Unsubscribe factory handler
            enemy.OnDeath -= HandleEnemyDeath;

            if (!instanceToPrefab.TryGetValue(enemy, out var prefab))
            {
                // Unknown origin; destroy to avoid leaks
                Destroy(enemy.gameObject);
                return;
            }

            // Reset and deactivate
            enemy.ResetEnemy();
            //enemy.gameObject.SetActive(false);
            enemy.transform.SetParent(transform);

            if (!pools.TryGetValue(prefab, out var queue))
            {
                // This should not happen if all enemies are properly tracked,
                // but create a new pool if needed to avoid losing returned enemies
                queue = new Queue<BaseEnemyCore>();
                Instance.pools[prefab] = queue;
            }

            queue.Enqueue(enemy);

            UpdateName();
        }

        public static void ClearPool()
        {
            foreach (var kv in Instance.pools)
            {
                while (kv.Value.Count > 0)
                {
                    var e = kv.Value.Dequeue();
                    if (e != null)
                        Destroy(e.gameObject);
                }
            }
            Instance.pools.Clear();
            Instance.instanceToPrefab.Clear();
        }
    }
}
