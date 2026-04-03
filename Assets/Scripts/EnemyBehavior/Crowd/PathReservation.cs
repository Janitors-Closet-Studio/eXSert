using UnityEngine;
using UnityEngine.AI;

namespace EnemyBehavior.Crowd
{
    // Stamps a short corridor along the agent's current path into the DensityGrid.
    // Planners with density cost will prefer different routes, effectively reserving this path segment.
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class PathReservation : MonoBehaviour
    {
        [Header("Component Help")]
        [SerializeField, TextArea(3,6)] private string inspectorHelp =
            "PathReservation: stamps a cost corridor ahead along the agent's current path.\n" +
            "Other agents' planners (A*/FlowField) avoid these reserved segments when density multipliers > 0.";

        [Header("Reservation Corridor")]
        [SerializeField, Tooltip("Meters ahead to stamp along the current path.")] private float lookaheadDistance = 10f;
        [SerializeField, Tooltip("Meters between stamp samples along the path.")] private float stampSpacing = 0.5f;
        [SerializeField, Tooltip("Extra meters added to agent.radius for stamp radius (corridor half-width). ")] private float extraRadius = 0.25f;
        [SerializeField, Tooltip("Stamp weight per sample. Higher produces stronger avoidance in planners.")] private float stampWeight = 2.0f;

        [Header("Cadence/Filters")] 
        [SerializeField, Tooltip("How often to restamp the corridor (seconds). ")] private float cadence = 0.25f;
        [SerializeField, Tooltip("Only stamp if remainingDistance exceeds this (meters). ")] private float minRemainingDistance = 2.0f;

        private NavMeshAgent agent;
        private float nextTime;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
        }
#pragma warning restore CS0414

        void Update()
        {
            if (Time.time < nextTime) return;
            nextTime = Time.time + Mathf.Max(0.05f, cadence);

            var grid = EnemyBehavior.Density.DensityGrid.Instance;
            if (grid == null || agent == null || !agent.enabled || !agent.isOnNavMesh) return;
            if (!agent.hasPath || agent.remainingDistance < minRemainingDistance) return;

            var path = agent.path; // copy
            var corners = path.corners;
            if (corners == null || corners.Length == 0) return;

            float rem = lookaheadDistance;
            Vector3 prev = transform.position;
            int idx = 0;

            // Ensure first point is current position, then corners along the path
            while (rem > 0f && idx < corners.Length)
            {
                Vector3 segEnd = corners[idx];
                Vector3 seg = segEnd - prev; seg.y = 0f;
                float segLen = seg.magnitude;
                if (segLen <= 0.001f)
                {
                    idx++; prev = segEnd; continue;
                }
                Vector3 dir = seg / segLen;
                float step = Mathf.Max(0.05f, stampSpacing);
                float t = 0f;
                while (t < segLen && rem > 0f)
                {
                    Vector3 p = prev + dir * t;
                    float radius = (agent.radius + extraRadius);
                    grid.Stamp(p, radius, stampWeight);
                    t += step;
                    rem -= step;
                }
                prev = segEnd;
                idx++;
            }
        }
    }
}
