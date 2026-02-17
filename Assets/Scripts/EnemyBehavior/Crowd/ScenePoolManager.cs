using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace EnemyBehavior.Crowd
{
    public sealed class ScenePoolManager : MonoBehaviour
    {
        public static ScenePoolManager Instance { get; private set; }

        [Header("Component Help")]
        [SerializeField, TextArea(4, 8)] private string inspectorHelp =
            "ScenePoolManager: simple scene-local pool used by the boss to spawn adds.\n" +
            "Use only in the boss scene. Add Pools entries for drone and basic crawler prefabs.\n" +
            "Prewarm creates a number of inactive instances at load (8–16 typical) to avoid frame spikes.";

        [System.Serializable]
        public class Pool
        {
            [Tooltip("Prefab to pool (boss adds: drone or basic crawler). Only assign in the boss scene.")]
            public GameObject Prefab;
            [Tooltip("How many instances to pre-instantiate at scene load (8–16 typical). Prewarm reduces runtime hitches by avoiding instantiation during combat.")]
            public int Prewarm = 8;
        }

        [Tooltip("Pools for boss adds (drones and basic crawlers). Use only in the boss scene. Prewarm 8–16 recommended.")]
        public List<Pool> Pools = new List<Pool>();

        private readonly Dictionary<GameObject, Queue<GameObject>> map = new Dictionary<GameObject, Queue<GameObject>>(16);
        private Transform inactiveRoot;

        void Awake()
        {
            // Duplicate guard for additive scenes
            if (Instance != null && Instance != this)
            {
                EnemyBehaviorDebugLogBools.LogWarning(nameof(ScenePoolManager), "ScenePoolManager duplicate detected, destroying this instance.");
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Keep prewarmed instances completely inactive by parenting under an inactive root
            var rootGO = new GameObject("ScenePoolManager_PoolRoot");
            rootGO.SetActive(false);
            rootGO.transform.SetParent(transform, false);
            inactiveRoot = rootGO.transform;

            for (int i = 0; i < Pools.Count; i++)
            {
                var p = Pools[i];
                if (p.Prefab == null || p.Prewarm <= 0) continue;

                var q = new Queue<GameObject>(p.Prewarm);
                for (int n = 0; n < p.Prewarm; n++)
                {
                    var g = Instantiate(p.Prefab, inactiveRoot);
                    g.name = p.Prefab.name + "_Pooled";
                    // Ensure agents do not try to initialize off-mesh while pooled
                    var agent = g.GetComponent<NavMeshAgent>();
                    if (agent != null) agent.enabled = false;
                    q.Enqueue(g);
                }
                map[p.Prefab] = q;
            }
        }

        public GameObject Spawn(GameObject prefab, Vector3 pos, Quaternion rot)
        {
            if (prefab == null) return null;
            if (!map.TryGetValue(prefab, out var q))
            {
                q = new Queue<GameObject>(8);
                map[prefab] = q;
            }

            GameObject g = q.Count > 0 ? q.Dequeue() : Instantiate(prefab, inactiveRoot);

            // Re-parent out of the inactive pool hierarchy before activation
            if (g.transform.parent == inactiveRoot)
                g.transform.SetParent(null, true);

            // Snap to nearest NavMesh to avoid agent placement errors
            if (NavMesh.SamplePosition(pos, out var hit, 2f, NavMesh.AllAreas))
            {
                pos = hit.position;
            }

            g.transform.SetPositionAndRotation(pos, rot);

            // Enable agent after positioning so it binds to the mesh cleanly
            var agent = g.GetComponent<NavMeshAgent>();
            if (agent != null && !agent.enabled)
            {
                agent.enabled = true;
            }

            g.SetActive(true);
            return g;
        }

        public void Despawn(GameObject prefab, GameObject instance)
        {
            if (prefab == null || instance == null) return;
            if (!map.TryGetValue(prefab, out var q))
            {
                q = new Queue<GameObject>(8);
                map[prefab] = q;
            }

            // Disable agent before deactivating to avoid off-mesh warnings
            var agent = instance.GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled)
            {
                agent.ResetPath();
                agent.enabled = false;
            }

            instance.SetActive(false);
            instance.transform.SetParent(inactiveRoot, false);
            q.Enqueue(instance);
        }
    }
}