using System.Collections.Generic;
using UnityEngine;

namespace EnemyBehavior.Crowd
{
    public sealed class CrowdController : MonoBehaviour
    {
        public static CrowdController Instance { get; private set; }

        [Header("Component Help")]
        [SerializeField, TextArea(3, 6)] private string inspectorHelp =
            "CrowdController: ticks registered CrowdAgents on a cadence based on their importance.\n" +
            "Each tick, agents may replan, apply steering, and stamp density into the DensityGrid.";

        [SerializeField] private float highUpdateRate = 10f;
        [SerializeField] private float midUpdateRate = 3.3f;
        [SerializeField] private float lowUpdateRate = 0.5f;

        private readonly List<CrowdAgent> _agents = new List<CrowdAgent>(512);

        void Awake()
        {
            // Simple duplicate guard for additive scenes
            if (Instance != null && Instance != this)
            {
                EnemyBehaviorDebugLogBools.LogWarning(nameof(CrowdController), "CrowdController duplicate detected, destroying this instance (additive scene overlap).");
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void Register(CrowdAgent a)
        {
            if (!_agents.Contains(a)) _agents.Add(a);
        }

        public void Unregister(CrowdAgent a)
        {
            _agents.Remove(a);
        }

        void Update()
        {
            float t = Time.time;
            for (int i = 0; i < _agents.Count; i++)
            {
                var a = _agents[i];
                if (a == null) continue;
                if (a.ShouldTick(t))
                {
                    if (a.NeedsReplan) a.RequestPath();
                    a.ApplySteering();
                    a.StampDensity();
                }
            }
        }
    }
}
