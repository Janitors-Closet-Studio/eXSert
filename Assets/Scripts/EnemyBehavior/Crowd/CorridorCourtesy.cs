using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#pragma warning disable CS0414
using UnityEngine.AI;

namespace EnemyBehavior.Crowd
{
    // Adds deliberate, human-readable yielding in narrow passages.
    // Agents will back up, wait, and then resume to their previous destination.
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class CorridorCourtesy : MonoBehaviour
    {
        [Header("Component Help")]
        [SerializeField, TextArea(3,6)] private string inspectorHelp =
            "CorridorCourtesy: cooperative yielding in tight corridors.\n" +
            "Detects head-on agents in narrow spaces. One agent backs up to a pullout point, waits (stopped), then resumes.";

        [Header("Detection")]
        [SerializeField, Tooltip("Meters to scan for a head-on agent.")] private float detectRadius = 2.5f;
        [SerializeField, Tooltip("Forward cone angle (deg) to consider an agent 'in front'.")] private float frontConeAngle = 40f;
        [SerializeField, Tooltip("Probe ray length to either side to estimate corridor width.")] private float sideProbeLength = 1.25f;
        [SerializeField, Tooltip("Maximum width (m) to consider 'narrow'. If estimated width is <= this, courtesy can trigger.")] private float narrowWidthThreshold = 2.0f;

        [Header("Yield Behavior")]
        [SerializeField, Tooltip("Meters to back off when yielding.")] private float backoffDistance = 1.8f;
        [SerializeField, Tooltip("Max seconds to wait while yielding before giving up.")] private float maxYieldSeconds = 3.0f;
        [SerializeField, Tooltip("Cooldown after a yield completes before considering another (s).")] private float yieldCooldown = 2.0f;
        [SerializeField, Tooltip("Temporarily set agent avoidancePriority to this while yielding (larger number = yields more). -1 = leave unchanged.")] private int priorityWhileYielding = 90;
        [SerializeField, Tooltip("Optionally scale radius while yielding to encourage separation (1 = no change)."), Range(0.8f, 1.6f)] private float radiusScaleWhileYielding = 1.2f;
        [SerializeField, Tooltip("Prefer retreat targets from recent breadcrumbs (previous positions) instead of simple back-of-forward.")] private bool useBreadcrumbBackoff = true;

        [Header("Breadcrumbs")]
        [SerializeField, Tooltip("Number of previous positions to remember for retreat.")] private int breadcrumbCapacity = 20;
        [SerializeField, Tooltip("Meters the agent must move before adding a new breadcrumb.")] private float breadcrumbMinSpacing = 0.4f;

        [Header("Filters")] 
        [SerializeField, Tooltip("Agents must have this tag to be considered as courtesies (e.g., 'Enemy'). Empty = any.")] private string peerTag = "Enemy";
        [SerializeField, Tooltip("Layers used for side wall probes.")] private LayerMask wallMask = ~0;

        private NavMeshAgent agent;
        private float nextAllowTime;
        private bool yielding;
        private Vector3 savedDestination;
        private bool savedHadPath;
        private int savedPriority;
        private float savedRadius;
        private float savedSpeed;

        private readonly List<Vector3> breadcrumbs = new List<Vector3>(32);
        private Vector3 lastCrumb;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            lastCrumb = transform.position;
            breadcrumbs.Clear();
            breadcrumbs.Add(lastCrumb);
        }
#pragma warning restore CS0414

        void OnEnable()
        {
            StartCoroutine(CourtesyLoop());
        }

        void Update()
        {
            // Record breadcrumbs of recent positions to support rewind-like retreat
            if (!useBreadcrumbBackoff || agent == null || !agent.enabled) return;
            Vector3 p = transform.position;
            if ((p - lastCrumb).sqrMagnitude >= breadcrumbMinSpacing * breadcrumbMinSpacing)
            {
                lastCrumb = p;
                breadcrumbs.Add(p);
                if (breadcrumbs.Count > breadcrumbCapacity)
                {
                    breadcrumbs.RemoveAt(0);
                }
            }
        }

        private IEnumerator CourtesyLoop()
        {
            var wait = WaitForSecondsCache.Get(0.12f);
            while (true)
            {
                if (!yielding && Time.time >= nextAllowTime && agent != null && agent.enabled && agent.isOnNavMesh)
                {
                    TryStartYield();
                }
                yield return wait;
            }
        }

        private void TryStartYield()
        {
            if (!agent.hasPath) return;
            if (!IsNarrowCorridor()) return;

            var peer = FindHeadOnPeer();
            if (peer == null) return;

            var theirAgent = peer.GetComponent<NavMeshAgent>();
            if (theirAgent == null) return;
            int myP = agent.avoidancePriority;
            int theirP = theirAgent.avoidancePriority;
            bool iShouldYield = myP >= theirP;
            if (myP == theirP)
            {
                iShouldYield = GetInstanceID() > peer.GetInstanceID();
            }
            if (!iShouldYield) return;

            StartCoroutine(YieldRoutine(peer.transform));
        }

        private IEnumerator YieldRoutine(Transform peer)
        {
            yielding = true;
            savedHadPath = agent.hasPath;
            if (savedHadPath) savedDestination = agent.destination;
            savedPriority = agent.avoidancePriority;
            savedRadius = agent.radius;
            savedSpeed = agent.speed;

            if (priorityWhileYielding >= 0) agent.avoidancePriority = Mathf.Clamp(priorityWhileYielding, 0, 99);
            if (!Mathf.Approximately(radiusScaleWhileYielding, 1f)) agent.radius = savedRadius * radiusScaleWhileYielding;

            // Compute retreat target from breadcrumbs if available
            Vector3 retreatTarget;
            if (!TryGetBreadcrumbRetreat(out retreatTarget))
            {
                // Fallback: simple back of forward
                Vector3 dir = agent.desiredVelocity.sqrMagnitude > 0.01f ? agent.desiredVelocity.normalized : transform.forward;
                retreatTarget = transform.position - dir * backoffDistance;
            }

            if (NavMesh.SamplePosition(retreatTarget, out NavMeshHit hit, Mathf.Max(1.25f, backoffDistance), NavMesh.AllAreas))
            {
                // Move to retreat point at a modest speed
                agent.speed = Mathf.Min(savedSpeed, 2.0f + savedSpeed * 0.25f);
                agent.isStopped = false;
                agent.SetDestination(hit.position);

                // Wait until we are close to retreat point or time out
                float tMove = 0f;
                while (Vector3.Distance(transform.position, hit.position) > 0.15f && tMove < 1.2f)
                {
                    tMove += Time.deltaTime;
                    yield return null;
                }
            }

            // Come to a full stop and wait for peer to clear front cone
            agent.isStopped = true;
            float t = 0f;
            while (t < maxYieldSeconds)
            {
                if (!PeerInFrontCone()) break;
                t += Time.deltaTime;
                yield return null;
            }

            // Restore properties
            agent.isStopped = false;
            if (priorityWhileYielding >= 0) agent.avoidancePriority = savedPriority;
            if (!Mathf.Approximately(radiusScaleWhileYielding, 1f)) agent.radius = savedRadius;
            agent.speed = savedSpeed;

            if (savedHadPath)
            {
                agent.SetDestination(savedDestination);
            }

            nextAllowTime = Time.time + yieldCooldown;
            yielding = false;
        }

        private bool TryGetBreadcrumbRetreat(out Vector3 retreat)
        {
            retreat = transform.position;
            if (!useBreadcrumbBackoff || breadcrumbs.Count == 0)
                return false;

            Vector3 fwd = transform.forward; fwd.y = 0f; if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
            // Iterate oldest to newest so we prefer positions clearly behind where we came from
            for (int i = 0; i < breadcrumbs.Count; i++)
            {
                Vector3 p = breadcrumbs[i];
                Vector3 toP = p - transform.position; toP.y = 0f;
                float dist = toP.magnitude;
                if (dist < backoffDistance * 0.8f || dist > backoffDistance * 2.5f) continue;
                if (Vector3.Dot(fwd, toP.normalized) > -0.2f) continue; // must be roughly behind

                // Ensure straight-line navmesh raycast is clear between current and breadcrumb
                if (!NavMesh.Raycast(transform.position, p, out var hit, NavMesh.AllAreas))
                {
                    retreat = p;
                    return true;
                }
            }
            return false;
        }

        private bool PeerInFrontCone()
        {
            var peer = FindHeadOnPeer();
            return peer != null;
        }

        private GameObject FindHeadOnPeer()
        {
            Collider[] cols = Physics.OverlapSphere(transform.position, detectRadius);
            float coneCos = Mathf.Cos(frontConeAngle * Mathf.Deg2Rad);
            foreach (var c in cols)
            {
                if (c.attachedRigidbody != null && c.attachedRigidbody.gameObject == gameObject) continue;
                var go = c.attachedRigidbody ? c.attachedRigidbody.gameObject : c.gameObject;
                if (!string.IsNullOrEmpty(peerTag) && !go.CompareTag(peerTag)) continue;
                var otherAgent = go.GetComponent<NavMeshAgent>();
                if (otherAgent == null) continue;

                Vector3 toOther = (go.transform.position - transform.position);
                toOther.y = 0f;
                Vector3 myF = transform.forward; myF.y = 0f;
                Vector3 otherF = go.transform.forward; otherF.y = 0f;
                toOther.Normalize(); myF.Normalize(); otherF.Normalize();

                float a = Vector3.Dot(myF, toOther);
                float b = Vector3.Dot(otherF, -toOther);
                if (a >= coneCos && b >= coneCos)
                {
                    return go; // head-on in front cones
                }
            }
            return null;
        }

        private bool IsNarrowCorridor()
        {
            // Probe left/right to estimate free width
            Vector3 fwd = transform.forward; fwd.y = 0f; fwd.Normalize();
            Vector3 perp = Vector3.Cross(Vector3.up, fwd).normalized;
            float left = ProbeSide(-perp);
            float right = ProbeSide(perp);
            float width = left + right;
            return width <= narrowWidthThreshold;
        }

        private float ProbeSide(Vector3 dir)
        {
            Vector3 origin = transform.position + Vector3.up * 0.5f;
            if (Physics.Raycast(origin, dir, out var hit, sideProbeLength, wallMask, QueryTriggerInteraction.Ignore))
            {
                return hit.distance;
            }
            return sideProbeLength;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f,1f,0f,0.2f);
            Gizmos.DrawWireSphere(transform.position, detectRadius);
            // Draw side probes
            Vector3 fwd = transform.forward; fwd.y = 0f; if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            Vector3 perp = Vector3.Cross(Vector3.up, fwd).normalized;
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position + Vector3.up*0.5f, transform.position + Vector3.up*0.5f + perp * sideProbeLength);
            Gizmos.DrawLine(transform.position + Vector3.up*0.5f, transform.position + Vector3.up*0.5f - perp * sideProbeLength);
        }
#endif
    }
}
