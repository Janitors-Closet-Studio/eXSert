using UnityEngine;
#pragma warning disable CS0414

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

        [Header("Debug")]
        [SerializeField, Tooltip("Enable debug logs for this zone")]
        private bool enableDebugLogs = false;
        [SerializeField, Tooltip("Enable mounting diagnostics logs ([BossMountDiag]).")]
        private bool enableMountingDiagnostics = true;
        [SerializeField, Tooltip("Minimum interval between non-critical mounting diagnostic logs.")]
        private float mountingDiagnosticsInterval = 0.2f;
        [SerializeField, Tooltip("Keeps mounted movement state active briefly after input flickers to avoid velocity on/off jitter.")]
        private float mountedMovementInputGraceSeconds = 0.2f;
        [SerializeField, Tooltip("How long movement must be idle before hard idle anchoring engages. Helps prevent stutter while walking.")]
        private float idleAnchorActivationDelay = 0.35f;
        [SerializeField, Tooltip("Scales carrier velocity injected while player is actively moving on top (1 = full carrier speed).")]
        [Range(0f, 1f)] private float movingCarryVelocityMultiplier = 0.65f;
        [SerializeField, Tooltip("Apply additional external carry velocity while mounted and moving. Disable to rely on parenting-only carry (recommended for roomba).")]
        private bool applyExternalCarryWhileMounted = false;

        private Collider zone;
        private Transform playerTransform;
        private Collider playerCollider;
        private int overlapCount;
        private Transform originalParent;
        private Coroutine monitorRoutine;
        private float lastInsideTime;
        private bool isParented;
        private float suppressParentingUntilTime;
        private bool appliedParentExternalVelocity;
        private Vector3 mountedLocalPosition;
        private Quaternion mountedLocalRotation;
        private Vector3 mountedLastWorldPosition;
        private bool hasMountedMotionSample;
        private float nextMountingDiagnosticTime;
        private float lastMountedMovementInputTime;

        private void Reset()
        {
            var col = GetComponent<Collider>();
            col.isTrigger = true;
        }
#pragma warning restore CS0414

        private void OnValidate()
        {
            zone = GetComponent<Collider>();
            if (zone != null) zone.isTrigger = true;
            if (brain == null) brain = GetComponentInParent<BossRoombaBrain>();
        }

        private bool IsSamePlayerCollider(Transform t)
        {
            return playerTransform != null
                && t != null
                && (t == playerTransform || t.IsChildOf(playerTransform) || t.root == playerTransform);
        }

        private bool TryResolvePlayer(Collider other, out Transform playerRoot, out Collider resolvedCollider)
        {
            playerRoot = null;
            resolvedCollider = null;

            if (other == null)
                return false;

            Transform root = other.transform != null ? other.transform.root : null;
            bool isPlayer = other.CompareTag("Player") || (root != null && root.CompareTag("Player"));
            if (!isPlayer)
                return false;

            // Ignore player-owned trigger hitboxes (attack volumes, helper triggers, etc.)
            // so they cannot mount/unmount the player.
            if (other.isTrigger)
            {
                CharacterController triggerOwner = other.GetComponentInParent<CharacterController>();
                if (triggerOwner == null || other.transform != triggerOwner.transform)
                    return false;
            }

            playerRoot = root != null ? root : other.transform;

            CharacterController cc = playerRoot.GetComponent<CharacterController>()
                ?? playerRoot.GetComponentInChildren<CharacterController>()
                ?? playerRoot.GetComponentInParent<CharacterController>();

            resolvedCollider = cc != null ? cc : other;
            return true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (enableDebugLogs)
                EnemyBehaviorDebugLogBools.Log(nameof(BossTopZone), $"[BossTopZone] OnTriggerEnter: {other.name}, tag: {other.tag}, has Player tag: {other.CompareTag("Player")}");

            if (!TryResolvePlayer(other, out Transform playerRoot, out Collider resolvedCollider)) return;
            if (brain == null) brain = GetComponentInParent<BossRoombaBrain>();

            if (playerTransform == null)
            {
                if (enableDebugLogs)
                    EnemyBehaviorDebugLogBools.Log(nameof(BossTopZone), $"[BossTopZone] Player detected for first time: {other.name}");
                playerTransform = playerRoot;
                playerCollider = resolvedCollider;
                overlapCount = 0;
                lastInsideTime = Time.time;
                if (monitorRoutine == null) monitorRoutine = StartCoroutine(MonitorPresence());
            }

            if (playerTransform == playerRoot)
            {
                playerCollider = resolvedCollider;
                overlapCount++;
                lastInsideTime = Time.time;
                if (enableDebugLogs)
                    EnemyBehaviorDebugLogBools.Log(nameof(BossTopZone), $"[BossTopZone] Player overlap count: {overlapCount}");
                // Do NOT parent here; we only parent in Stay when eligibility is verified.
            }
        }

        private void OnTriggerStay(Collider other)
        {
            if (!TryResolvePlayer(other, out Transform playerRoot, out Collider resolvedCollider)) return;
            if (playerTransform != playerRoot) return;

            playerCollider = resolvedCollider;
            lastInsideTime = Time.time;

            if (IsEligibleForParenting())
            {
                EnsureParented();
            }
            else if (!isParented)
            {
                EnsureUnparented();
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!TryResolvePlayer(other, out Transform playerRoot, out _)) return;
            if (playerTransform != playerRoot) return;

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
            if (zone == null || playerCollider == null)
            {
                MountingDiag("FAIL eligibility: missing zone or player collider");
                return false;
            }

            if (Time.time < suppressParentingUntilTime)
            {
                MountingDiag($"FAIL eligibility: suppression active for {(suppressParentingUntilTime - Time.time):F2}s");
                return false;
            }

            // Must be intersecting the trigger volume
            Vector3 dir; float dist;
            bool penetrating = Physics.ComputePenetration(
                zone, zone.transform.position, zone.transform.rotation,
                playerCollider, playerCollider.transform.position, playerCollider.transform.rotation,
                out dir, out dist);
            bool boundsIntersect = zone.bounds.Intersects(playerCollider.bounds);
            if (!(penetrating || boundsIntersect))
            {
                MountingDiag("FAIL eligibility: player not intersecting top zone trigger");
                return false;
            }

            // If already mounted, keep mount as long as we're still inside the trigger volume.
            // This avoids stutter from tiny per-frame foot/zone top delta jitter.
            if (isParented)
                return true;

            if (!requireTopContactForParenting) return true;

            // Require player's feet to be near the top surface of the zone
            float zoneTop = zone.bounds.max.y;
            float playerFeet = playerCollider.bounds.min.y;
            float verticalDelta = Mathf.Abs(playerFeet - zoneTop);
            if (verticalDelta > topContactMaxVerticalDelta)
            {
                MountingDiag($"FAIL eligibility: top-contact delta too high (delta={verticalDelta:F3}, max={topContactMaxVerticalDelta:F3})");
                return false;
            }

            return true;
        }

        private void FixedUpdate()
        {
            MaintainMountedPlayerAnchor(false);
        }

        private void LateUpdate()
        {
            // Run a late pass so idle anchor correction happens after other movement systems.
            MaintainMountedPlayerAnchor(true);
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
                    // Enforce parenting only when eligible; do not force-unparent here
                    // to avoid mounted jitter from strict top-contact checks.
                    if (IsEligibleForParenting())
                        EnsureParented();
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
            if (enableDebugLogs)
                EnemyBehaviorDebugLogBools.Log(nameof(BossTopZone), $"[BossTopZone] EnsureParented called - parentPlayerWhileOnTop: {parentPlayerWhileOnTop}, playerTransform: {(playerTransform != null ? playerTransform.name : "null")}, isParented: {isParented}");

            if (!parentPlayerWhileOnTop || playerTransform == null) return;

            // During CageBull form, DO NOT parent the player - they should be dodging charges, not riding
            if (brain == null) brain = GetComponentInParent<BossRoombaBrain>();
            if (brain != null && brain.CurrentForm == RoombaForm.CageBull)
            {
                if (enableDebugLogs)
                    EnemyBehaviorDebugLogBools.Log(nameof(BossTopZone), $"[BossTopZone] Skipping parenting during CageBull form");
                return;
            }

            if (!isParented)
            {
                originalParent = playerTransform.parent;
                playerTransform.SetParent(brain != null ? brain.transform : transform.root, true);
                isParented = true;
                mountedLocalPosition = playerTransform.localPosition;
                mountedLocalRotation = playerTransform.localRotation;
                mountedLastWorldPosition = playerTransform.parent != null ? playerTransform.parent.position : Vector3.zero;
                hasMountedMotionSample = playerTransform.parent != null;
                appliedParentExternalVelocity = false;
                lastMountedMovementInputTime = Time.time;
                if (enableDebugLogs)
                    EnemyBehaviorDebugLogBools.Log(nameof(BossTopZone), $"[BossTopZone] Player parented to boss!");
                MountingDiag($"SUCCESS mounted: parent={(playerTransform.parent != null ? playerTransform.parent.name : "null")}", true);
                if (brain != null) brain.SetPlayerOnTop(true);
            }
        }

        private void EnsureUnparented()
        {
            if (enableDebugLogs)
                EnemyBehaviorDebugLogBools.Log(nameof(BossTopZone), $"[BossTopZone] EnsureUnparented called - isParented: {isParented}, playerTransform: {(playerTransform != null ? playerTransform.name : "null")}");

            if (isParented && playerTransform != null)
            {
                TryClearParentExternalVelocity();
                playerTransform.SetParent(originalParent, true);
                isParented = false;
                mountedLocalPosition = Vector3.zero;
                mountedLocalRotation = Quaternion.identity;
                mountedLastWorldPosition = Vector3.zero;
                hasMountedMotionSample = false;
                appliedParentExternalVelocity = false;
                lastMountedMovementInputTime = 0f;
                if (enableDebugLogs)
                    EnemyBehaviorDebugLogBools.Log(nameof(BossTopZone), $"[BossTopZone] Player unparented from boss!");
                MountingDiag("UNMOUNTED player detached from boss", true);
                if (brain == null) brain = GetComponentInParent<BossRoombaBrain>();
                if (brain != null) brain.SetPlayerOnTop(false);
            }
        }

        public void ForceDetachPlayer(float suppressSeconds = 0.35f)
        {
            suppressParentingUntilTime = Mathf.Max(suppressParentingUntilTime, Time.time + Mathf.Max(0f, suppressSeconds));
            EnsureUnparented();
        }

        private void MaintainMountedPlayerAnchor(bool isLatePass)
        {
            if (!isParented || playerTransform == null)
                return;

            Transform carrier = playerTransform.parent;
            if (carrier == null)
                return;

            float dt = Mathf.Max(0.0001f, Time.fixedDeltaTime);
            Vector3 carrierVelocity = Vector3.zero;
            if (hasMountedMotionSample)
                carrierVelocity = (carrier.position - mountedLastWorldPosition) / dt;

            mountedLastWorldPosition = carrier.position;
            hasMountedMotionSample = true;

            CharacterController controller = playerTransform.GetComponent<CharacterController>();
            bool isGrounded = controller == null || controller.isGrounded;
            if (!isGrounded)
                MountingDiag("STATE airborne while mounted - clearing external velocity");

            PlayerMovement movement = playerTransform.GetComponent<PlayerMovement>()
                ?? playerTransform.GetComponentInChildren<PlayerMovement>()
                ?? playerTransform.GetComponentInParent<PlayerMovement>();

            bool hasRawMovementIntent = movement != null && (movement.HasEffectiveMovementInput || movement.IsDashing);
            if (hasRawMovementIntent)
                lastMountedMovementInputTime = Time.time;

            bool isActivelyMoving = hasRawMovementIntent
                || (mountedMovementInputGraceSeconds > 0f && (Time.time - lastMountedMovementInputTime) <= mountedMovementInputGraceSeconds);

            float timeSinceLastMovementIntent = Time.time - lastMountedMovementInputTime;

            // Optional external velocity injection (parenting already provides base carry).
            if (!isLatePass && movement != null && applyExternalCarryWhileMounted && isActivelyMoving && isGrounded)
            {
                Vector3 carryVelocity = new Vector3(carrierVelocity.x, 0f, carrierVelocity.z) * movingCarryVelocityMultiplier;
                movement.SetExternalVelocity(carryVelocity);
                appliedParentExternalVelocity = true;
            }
            else if (!isLatePass)
            {
                TryClearParentExternalVelocity(movement);
            }

            if (isActivelyMoving)
            {
                mountedLocalPosition = playerTransform.localPosition;
                mountedLocalRotation = playerTransform.localRotation;
                if (!isLatePass)
                    MountingDiag($"STATE moving-mounted carrierVel={carrierVelocity.magnitude:F2} rawInput={(hasRawMovementIntent ? 1 : 0)} extCarry={(applyExternalCarryWhileMounted ? 1 : 0)}");
                return;
            }

            // Prevent rapid move<->idle transitions from repeatedly hard-locking local position.
            if (timeSinceLastMovementIntent < Mathf.Max(0f, idleAnchorActivationDelay))
            {
                if (!isLatePass)
                    MountingDiag($"STATE settling-mounted carrierVel={carrierVelocity.magnitude:F2} tSinceMove={timeSinceLastMovementIntent:F2}");
                return;
            }

            // While idle, keep the player anchored relative to the boss so they behave like on solid ground.
            float localDrift = Vector3.Distance(playerTransform.localPosition, mountedLocalPosition);
            if (!isLatePass)
                MountingDiag($"STATE idle-mounted carrierVel={carrierVelocity.magnitude:F2} localDrift={localDrift:F3}");
            playerTransform.localPosition = mountedLocalPosition;
            playerTransform.localRotation = mountedLocalRotation;
        }

        private void MountingDiag(string message, bool force = false)
        {
            if (!enableMountingDiagnostics)
                return;

            if (!force && Time.time < nextMountingDiagnosticTime)
                return;

            nextMountingDiagnosticTime = Time.time + Mathf.Max(0.05f, mountingDiagnosticsInterval);
            EnemyBehaviorDebugLogBools.Log(nameof(BossTopZone), $"[BossMountDiag] {message}");
        }

        private void TryClearParentExternalVelocity(PlayerMovement cachedMovement = null)
        {
            if (!appliedParentExternalVelocity)
                return;

            PlayerMovement movement = cachedMovement;
            if (movement == null && playerTransform != null)
            {
                movement = playerTransform.GetComponent<PlayerMovement>()
                    ?? playerTransform.GetComponentInChildren<PlayerMovement>()
                    ?? playerTransform.GetComponentInParent<PlayerMovement>();
            }

            if (movement != null)
                movement.ClearExternalVelocity();

            appliedParentExternalVelocity = false;
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
