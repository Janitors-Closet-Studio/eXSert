using UnityEngine;

namespace EnemyBehavior.Boss
{
    // Attach to a child GameObject that covers the top surface of the boss.
    // Requires a Collider set as trigger. Detects when the Player is on top and informs the brain.
    [RequireComponent(typeof(Collider))]
    public sealed class BossTopZone : MonoBehaviour
    {
        [Header("Component Help")]
        [SerializeField, TextArea(3, 6)] private string inspectorHelp =
            "BossTopZone: trigger volume over the boss top surface.\n" +
            "When the Player enters, the boss can perform knock-off spin. Resize the collider to account for the boss model.\n" +
            "Optional: parent the player to carry on movement.";

        [SerializeField] private BossRoombaBrain brain;
        [SerializeField, Tooltip("If true, temporarily parent the player under the boss while on top to be carried by movement.")]
        private bool parentPlayerWhileOnTop = true;
        [SerializeField, Tooltip("How often to test for absence when no OnTriggerStay calls arrive.")]
        private float monitorHz = 20f;
        [SerializeField, Tooltip("Grace time before unparent when no OnTriggerStay has been received.")]
        private float exitGraceSeconds = 0.15f;
        [SerializeField, Tooltip("Require player to be near the zone's top surface to be considered on top (prevents parenting while just passing through high in the volume).")]
        private bool requireTopContactForParenting = true;
        [SerializeField, Tooltip("Max vertical difference (meters) between player feet and zone top to allow parenting.")]
        private float topContactMaxVerticalDelta = 0.15f;
        [SerializeField, Tooltip("Extra vertical margin (meters) beyond the trigger where the player is considered off the top zone.")]
        private float verticalClearMargin = 0.1f;

        private Collider zone;
        private Transform playerTransform;
        private Collider playerCollider;
        private int overlapCount;
        private Transform originalParent;
        private Coroutine monitorRoutine;
        private float lastInsideTime;
        private bool isParented;

        private void Reset()
        {
            var col = GetComponent<Collider>();
            col.isTrigger = true;
        }

        private void OnValidate()
        {
            zone = GetComponent<Collider>();
            if (zone != null) zone.isTrigger = true;
            if (brain == null) brain = GetComponentInParent<BossRoombaBrain>();
        }

        private bool IsSamePlayerCollider(Transform t)
        {
            return playerTransform != null && (t == playerTransform || t.IsChildOf(playerTransform));
        }

        private void OnTriggerEnter(Collider other)
        {
            EnemyBehaviorDebugLogBools.Log(nameof(BossTopZone), $"[BossTopZone] OnTriggerEnter: {other.name}, tag: {other.tag}, has Player tag: {other.CompareTag("Player")}");
            
            if (!other.CompareTag("Player")) return;
            if (brain == null) brain = GetComponentInParent<BossRoombaBrain>();

            if (playerTransform == null)
            {
                EnemyBehaviorDebugLogBools.Log(nameof(BossTopZone), $"[BossTopZone] Player detected for first time: {other.name}");
                playerTransform = other.transform;
                playerCollider = other;
                overlapCount = 0;
                lastInsideTime = Time.time;
                if (monitorRoutine == null) monitorRoutine = StartCoroutine(MonitorPresence());
            }

            if (IsSamePlayerCollider(other.transform))
            {
                overlapCount++;
                lastInsideTime = Time.time;
                EnemyBehaviorDebugLogBools.Log(nameof(BossTopZone), $"[BossTopZone] Player overlap count: {overlapCount}");
                // Do NOT parent here; we only parent in Stay when eligibility is verified.
            }
        }

        private void OnTriggerStay(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            if (!IsSamePlayerCollider(other.transform)) return;
            lastInsideTime = Time.time;

            if (IsEligibleForParenting())
            {
                EnsureParented();
            }
            else
            {
                EnsureUnparented();
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsSamePlayerCollider(other.transform)) return;
            overlapCount = Mathf.Max(0, overlapCount - 1);
            if (overlapCount == 0)
            {
                lastInsideTime = Time.time;
                // Favor unparenting on exit events
                EnsureUnparented();
            }
        }

        private bool IsEligibleForParenting()
        {
            if (zone == null || playerCollider == null) return false;

            // Must be intersecting the trigger volume
            Vector3 dir; float dist;
            bool penetrating = Physics.ComputePenetration(
                zone, zone.transform.position, zone.transform.rotation,
                playerCollider, playerCollider.transform.position, playerCollider.transform.rotation,
                out dir, out dist);
            bool boundsIntersect = zone.bounds.Intersects(playerCollider.bounds);
            if (!(penetrating || boundsIntersect)) return false;

            if (!requireTopContactForParenting) return true;

            // Require player's feet to be near the top surface of the zone
            float zoneTop = zone.bounds.max.y;
            float playerFeet = playerCollider.bounds.min.y;
            float verticalDelta = Mathf.Abs(playerFeet - zoneTop);
            if (verticalDelta > topContactMaxVerticalDelta) return false;

            return true;
        }

        private System.Collections.IEnumerator MonitorPresence()
        {
            float dt = 1f / Mathf.Max(5f, monitorHz);
            var wait = WaitForSecondsCache.Get(dt);
            while (playerTransform != null)
            {
                bool inside = false;
                if (zone != null && playerCollider != null)
                {
                    Vector3 dir; float dist;
                    bool penetrating = Physics.ComputePenetration(
                        zone, zone.transform.position, zone.transform.rotation,
                        playerCollider, playerCollider.transform.position, playerCollider.transform.rotation,
                        out dir, out dist);
                    bool boundsIntersect = zone.bounds.Intersects(playerCollider.bounds);

                    // Vertical fast-clear if clearly above the top surface
                    float zoneTop = zone.bounds.max.y + verticalClearMargin;
                    bool verticallyAbove = playerCollider.bounds.min.y > zoneTop;

                    inside = (penetrating || boundsIntersect) && !verticallyAbove;
                }

                if (inside)
                {
                    lastInsideTime = Time.time;
                    // Enforce parenting eligibility continuously
                    if (IsEligibleForParenting()) EnsureParented(); else EnsureUnparented();
                }
                else if (Time.time - lastInsideTime > exitGraceSeconds)
                {
                    EnsureUnparented();
                    ForceClearRefs();
                    break;
                }
                yield return wait;
            }
            monitorRoutine = null;
        }

        private void EnsureParented()
        {
            EnemyBehaviorDebugLogBools.Log(nameof(BossTopZone), $"[BossTopZone] EnsureParented called - parentPlayerWhileOnTop: {parentPlayerWhileOnTop}, playerTransform: {(playerTransform != null ? playerTransform.name : "null")}, isParented: {isParented}");
            
            if (!parentPlayerWhileOnTop || playerTransform == null) return;
            
            // During CageBull form, DO NOT parent the player - they should be dodging charges, not riding
            if (brain == null) brain = GetComponentInParent<BossRoombaBrain>();
            if (brain != null && brain.CurrentForm == RoombaForm.CageBull)
            {
                EnemyBehaviorDebugLogBools.Log(nameof(BossTopZone), $"[BossTopZone] Skipping parenting during CageBull form");
                return;
            }
            
            if (!isParented)
            {
                originalParent = playerTransform.parent;
                playerTransform.SetParent(brain != null ? brain.transform : transform.root, true);
                isParented = true;
                EnemyBehaviorDebugLogBools.Log(nameof(BossTopZone), $"[BossTopZone] Player parented to boss!");
                if (brain != null) brain.SetPlayerOnTop(true);
            }
        }

        private void EnsureUnparented()
        {
            EnemyBehaviorDebugLogBools.Log(nameof(BossTopZone), $"[BossTopZone] EnsureUnparented called - isParented: {isParented}, playerTransform: {(playerTransform != null ? playerTransform.name : "null")}");
            
            if (isParented && playerTransform != null)
            {
                playerTransform.SetParent(originalParent, true);
                isParented = false;
                EnemyBehaviorDebugLogBools.Log(nameof(BossTopZone), $"[BossTopZone] Player unparented from boss!");
                if (brain == null) brain = GetComponentInParent<BossRoombaBrain>();
                if (brain != null) brain.SetPlayerOnTop(false);
            }
        }

        private void ForceClear()
        {
            EnsureUnparented();
            ForceClearRefs();
        }

        private void ForceClearRefs()
        {
            playerTransform = null;
            playerCollider = null;
            originalParent = null;
            overlapCount = 0;
        }

        private void OnDisable()
        {
            if (monitorRoutine != null) { StopCoroutine(monitorRoutine); monitorRoutine = null; }
            if (playerTransform != null)
            {
                ForceClear();
            }
        }
    }
}
