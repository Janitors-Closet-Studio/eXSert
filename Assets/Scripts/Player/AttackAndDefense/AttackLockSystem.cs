/*
 * Written by Will Thomsen
 *  
 * this script is designed to check if the player is facing an enemy when attacking
 * if the player is facing an enemy, the player will move towards the enmemy and line up the attack for the player
 *
 * Updated By Kyle Woo
 * Updated to support soft lock nudges (player movement) and a hard lock mode that
 * steers the active Cinemachine camera toward the selected enemy.
 */

using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using Utilities.Combat.Attacks;

public class AttackLockSystem : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField]
    [Tooltip("Angle within which to lock on to an enemy")]
    private float lockOnAngle = 30f;

    [SerializeField]
    [Tooltip("Maximum distance to search for enemies")]
    private float lockOnDistance = 6f;

    [SerializeField]
    [CriticalReference]
    [Tooltip("Reference to the player GameObject (used as the search origin).")]
    private GameObject player;

    [SerializeField]
    [Tooltip("Optional reference to PlayerMovement so we can pause rotation while dashing.")]
    private PlayerMovement playerMovement;

    [SerializeField]
    [Tooltip("Optionally restrict candidates to specific layers.")]
    private LayerMask enemyLayers = ~0;

    [SerializeField]
    [Tooltip("Require enemies to be on the specified layer mask.")]
    private bool enforceLayerMask = false;

    [Header("Soft Lock Settings")]
    [SerializeField]
    [Tooltip("Radius within which soft lock nudges will move the player toward the target.")]
    private float softLockRadius = 2.5f;

    [SerializeField]
    [Tooltip("Maximum nudge distance when soft locking.")]
    private float softLockMoveDistance = 0.75f;

    [SerializeField]
    [Tooltip("Minimum buffer to leave between the player and the target after a soft lock nudge.")]
    private float softLockStopBuffer = 0.5f;

    [SerializeField]
    [Tooltip("Inside this radius the soft lock stops moving the player and only rotates them toward the target.")]
    private float softLockNoMoveRadius = 1.15f;

    [SerializeField, Range(0.05f, 0.4f)]
    [Tooltip("Duration of the soft lock movement blend.")]
    private float softLockMoveDuration = 0.12f;

    [SerializeField]
    [Tooltip("Only soft lock on single-target melee strikes.")]
    private bool onlySoftLockSingleTarget = true;

    [Header("Camera Lock Settings")]
    [SerializeField]
    [Tooltip("Steer the active camera instead of moving the player root.")]
    private bool steerCamera = true;

    [Header("Hard Lock Settings")]
    [SerializeField]
    [Tooltip("Rotate the player toward the locked enemy while hard lock is active.")]
    private bool rotatePlayerDuringHardLock = true;

    [SerializeField, Range(30f, 1440f)]
    [Tooltip("Degrees per second to rotate while tracking a hard-lock target.")]
    private float hardLockRotateSpeed = 540f;

    [SerializeField, Range(0.05f, 1.5f)]
    [Tooltip("Seconds it should take to align the camera towards the enemy.")]
    private float cameraSnapTime = 0.35f;

    [SerializeField, Range(0f, 0.25f)]
    [Tooltip("Viewport padding used when deciding whether an enemy counts as visible for hard lock selection.")]
    private float hardLockViewportPadding = 0.05f;

    [SerializeField]
    [Tooltip("Camera manager reference. Defaults to CameraManager.Instance if left empty.")]
    private CameraManager cameraManager;

    [SerializeField]
    [Tooltip("Fallback: also rotate the player instantly if camera steering is disabled.")]
    private bool rotatePlayerIfCameraDisabled = false;

    [Header("Lock-On Camera Lean Settings")]
    [SerializeField, Range(1f, 30f)]
    [Tooltip("Maximum degrees the player can lean the camera horizontally away from the lock-on target.")]
    private float maxHorizontalLeanAngle = 10f;

    [SerializeField, Range(1f, 20f)]
    [Tooltip("Maximum degrees the player can lean the camera vertically.")]
    private float maxVerticalLeanAngle = 8f;

    [SerializeField]
    [Tooltip("When enabled, pushing up on the camera input will look down and vice versa.")]
    private bool invertVerticalLean = false;

    [SerializeField, Range(0.5f, 5f)]
    [Tooltip("How quickly the lean responds to input. Higher = faster initial response.")]
    private float leanResponseSpeed = 2f;

    [SerializeField, Range(0.05f, 0.5f)]
    [Tooltip("How quickly the lean returns to center when input is released.")]
    private float leanReturnTime = 0.15f;

    /// <summary>
    /// Gets or sets whether vertical lean input is inverted. Can be used by settings menu.
    /// </summary>
    public bool InvertVerticalLean
    {
        get => invertVerticalLean;
        set => invertVerticalLean = value;
    }

    private Transform playerTransform => player != null ? player.transform : transform;
    private Transform currentTarget;
    private bool hardLockActive;
    private Coroutine moveCoroutine;
    private float cameraYawVelocity; // For horizontal SmoothDamp
    private float cameraPitchVelocity; // For vertical SmoothDamp
    private CinemachineInputAxisController cachedInputAxisController;
    private float currentHorizontalLean; // Current horizontal lean offset in degrees
    private float currentVerticalLean; // Current vertical lean offset in degrees
    private float horizontalLeanVelocity; // For smooth horizontal lean return
    private float verticalLeanVelocity; // For smooth vertical lean return
    private float baseVerticalValue; // The vertical axis value when lock-on started
    private bool hasBaseVerticalValue;
    private BaseEnemyCore currentTargetEnemyCore;
    private ReticleController currentTargetReticle;
    public bool IsHardLockActive => hardLockActive && currentTarget != null;
    public Transform CurrentHardLockTarget => currentTarget;

    private void Awake()
    {
        cameraManager ??= CameraManager.Instance;
        ResolvePlayerMovement();
    }

    private void OnEnable()
    {
        PlayerAttackManager.OnAttack += HandleAttackEvent;
        InputReader.LockOnPressed += HandleLockOnToggle;
        InputReader.LeftTargetPressed += HandleLeftTargetRequested;
        InputReader.RightTargetPressed += HandleRightTargetRequested;
    }

    private void OnDisable()
    {
        PlayerAttackManager.OnAttack -= HandleAttackEvent;
        InputReader.LockOnPressed -= HandleLockOnToggle;
        InputReader.LeftTargetPressed -= HandleLeftTargetRequested;
        InputReader.RightTargetPressed -= HandleRightTargetRequested;
        StopMoveRoutine();
        
        // Ensure camera input is re-enabled when this component is disabled
        if (hardLockActive)
        {
            SetCameraInputEnabled(true);
        }
        
        ClearHardLock(playReticleExit: false);
    }

    private void Update()
    {
        if (!hardLockActive || currentTarget == null)
            return;

        if (IsCurrentTargetDead())
        {
            currentTargetReticle?.PlayTargetLost();
            ClearHardLock(playReticleExit: false);
            return;
        }

        if (!IsTargetValid(currentTarget, lockOnDistance))
        {
            Transform replacementTarget = FindBestHardLockTarget();
            if (replacementTarget == null)
            {
                ClearHardLock();
                return;
            }

            SetHardLockTarget(replacementTarget, playEntryAnimation: true, playExitAnimation: true);
        }

        if (steerCamera)
            AimCameraAtTarget(currentTarget, instant: false);
        else if (rotatePlayerIfCameraDisabled)
            FaceTargetImmediately(currentTarget);

        if (rotatePlayerDuringHardLock)
            RotatePlayerTowardTarget(currentTarget, instant: false);
    }

    private void HandleAttackEvent(PlayerAttack executedAttack)
    {
        if (executedAttack == null)
            return;

        if (hardLockActive)
        {
            if (currentTarget == null)
            {
                Transform fallbackTarget = FindBestHardLockTarget();
                if (fallbackTarget != null)
                    SetHardLockTarget(fallbackTarget, playEntryAnimation: true, playExitAnimation: false);
            }

            if (currentTarget != null)
            {
                if (steerCamera)
                    AimCameraAtTarget(currentTarget, instant: true);
                else if (rotatePlayerIfCameraDisabled)
                    FaceTargetImmediately(currentTarget);

                if (rotatePlayerDuringHardLock)
                    RotatePlayerTowardTarget(currentTarget, instant: true);
            }
            else
            {
                ClearHardLock();
            }

            return;
        }

        if (onlySoftLockSingleTarget && !IsSingleTargetAttack(executedAttack))
            return;

        TryApplySoftLockNudge();
    }

    private void HandleLockOnToggle()
    {
        if (hardLockActive)
        {
            ClearHardLock();
            return;
        }

        ActivateHardLock(null, instantCameraAlign: false); // Smooth transition when locking on
    }

    private void HandleLeftTargetRequested() => CycleHardLock(-1);

    private void HandleRightTargetRequested() => CycleHardLock(1);

    private void CycleHardLock(int direction)
    {
        if (!hardLockActive || direction == 0)
            return;

        Transform nextTarget = FindAdjacentTarget(direction);
        if (nextTarget == null || nextTarget == currentTarget)
            return;

        SetHardLockTarget(nextTarget, playEntryAnimation: true, playExitAnimation: true);
        ResetLeanState(); // Reset lean when switching targets for clean transition
        AlignPlayerAndCamera(nextTarget, instantCameraAlign: false); // Smooth transition to new target
    }

    public bool ActivateHardLock(Transform forcedTarget = null, bool instantCameraAlign = false)
    {
        Transform candidate = forcedTarget ?? FindBestHardLockTarget();
        if (candidate == null)
            return false;

        hardLockActive = true;
        SetHardLockTarget(candidate, playEntryAnimation: true, playExitAnimation: false);
        ResetLeanState(); // Start with no lean
        SetCameraInputEnabled(false); // Disable automatic input - we read it ourselves for lean effect
        AlignPlayerAndCamera(candidate, instantCameraAlign);
        return true;
    }

    public bool EnsureHardLock(bool instantCameraAlign = false)
    {
        if (IsHardLockActive)
        {
            AlignPlayerAndCamera(currentTarget, instantCameraAlign);
            return true;
        }

        return ActivateHardLock(null, instantCameraAlign);
    }

    public void ReleaseHardLock()
    {
        ClearHardLock();
    }

    public void AlignPlayerAndCamera(Transform target, bool instantCameraAlign)
    {
        if (target == null)
            return;

        if (steerCamera)
            AimCameraAtTarget(target, instantCameraAlign);
        else if (rotatePlayerIfCameraDisabled)
            FaceTargetImmediately(target);

        if (rotatePlayerDuringHardLock)
            RotatePlayerTowardTarget(target, instant: true);
        else
            FaceTargetImmediately(target);
    }

    private void TryApplySoftLockNudge()
    {
        // Only target enemies within the player's forward-facing cone
        Transform target = FindNearestEnemyInPlayerCone(softLockRadius);
        if (target == null)
            return;

        Vector3 direction = GetFlatDirection(target.position);
        if (direction.sqrMagnitude < 0.001f)
            return;

        Quaternion desiredRotation = Quaternion.LookRotation(direction);

        float planarDistance = Vector3.Distance(
            new Vector3(target.position.x, playerTransform.position.y, target.position.z),
            playerTransform.position
        );

        if (planarDistance <= softLockNoMoveRadius)
        {
            FaceTargetImmediately(target);
            return;
        }

        float moveDistance = Mathf.Clamp(
            planarDistance - softLockStopBuffer,
            0f,
            softLockMoveDistance
        );

        if (moveDistance <= 0.01f)
        {
            FaceTargetImmediately(target);
            return;
        }

        Vector3 desiredPosition = playerTransform.position + direction * moveDistance;
        if (TrySnapPlayerToSoftLock(desiredPosition, desiredRotation))
            return;

        StopMoveRoutine();
        moveCoroutine = StartCoroutine(
            MoveAndFaceCoroutine(desiredPosition, desiredRotation, softLockMoveDuration)
        );
    }

    private bool TrySnapPlayerToSoftLock(Vector3 worldPosition, Quaternion desiredRotation)
    {
        PlayerMovement movement = ResolvePlayerMovement();
        if (movement == null)
            return false;

        return movement.TrySnapToSoftLock(worldPosition, desiredRotation);
    }

    private void ClearHardLock(bool playReticleExit = true)
    {
        if (playReticleExit)
            currentTargetReticle?.PlayUnlocked();

        UnsubscribeFromCurrentTargetDeath();

        hardLockActive = false;
        currentTarget = null;
        currentTargetReticle = null;
        currentTargetEnemyCore = null;
        cameraYawVelocity = 0f;
        cameraPitchVelocity = 0f;
        ResetLeanState();
        SetCameraInputEnabled(true); // Re-enable full camera input
    }

    private void SetHardLockTarget(Transform target, bool playEntryAnimation, bool playExitAnimation)
    {
        if (currentTarget == target)
        {
            currentTargetReticle ??= ResolveReticleController(target);
            if (playEntryAnimation)
                currentTargetReticle?.PlayLockedOn();

            return;
        }

        ReticleController previousReticle = currentTargetReticle;
        UnsubscribeFromCurrentTargetDeath();
        currentTarget = target;
        currentTargetReticle = ResolveReticleController(target);
        currentTargetEnemyCore = ResolveEnemyCore(target);
        SubscribeToCurrentTargetDeath();

        if (playExitAnimation)
            previousReticle?.PlayUnlocked();

        if (playEntryAnimation)
            currentTargetReticle?.PlayLockedOn();
    }

    private static ReticleController ResolveReticleController(Transform target)
    {
        if (target == null)
            return null;

        return target.GetComponentInChildren<ReticleController>(true);
    }

    private static BaseEnemyCore ResolveEnemyCore(Transform target)
    {
        if (target == null)
            return null;

        return target.GetComponentInParent<BaseEnemyCore>();
    }

    private void SubscribeToCurrentTargetDeath()
    {
        if (currentTargetEnemyCore == null)
            return;

        currentTargetEnemyCore.OnDeath -= HandleCurrentTargetDeath;
        currentTargetEnemyCore.OnDeath += HandleCurrentTargetDeath;
    }

    private void UnsubscribeFromCurrentTargetDeath()
    {
        if (currentTargetEnemyCore == null)
            return;

        currentTargetEnemyCore.OnDeath -= HandleCurrentTargetDeath;
    }

    private void HandleCurrentTargetDeath(BaseEnemyCore deadEnemy)
    {
        if (deadEnemy == null || deadEnemy != currentTargetEnemyCore)
            return;

        currentTargetReticle?.PlayTargetLost();
        ClearHardLock(playReticleExit: false);
    }

    private bool IsCurrentTargetDead()
    {
        if (currentTargetEnemyCore != null)
            return !currentTargetEnemyCore.isAlive;

        if (currentTarget == null)
            return false;

        BaseEnemyCore enemy = currentTarget.GetComponentInParent<BaseEnemyCore>();
        return enemy != null && !enemy.isAlive;
    }

    private void ResetLeanState()
    {
        currentHorizontalLean = 0f;
        currentVerticalLean = 0f;
        horizontalLeanVelocity = 0f;
        verticalLeanVelocity = 0f;
        hasBaseVerticalValue = false; // Will be recaptured on next frame
    }

    /// <summary>
    /// Enables or disables the camera input axis controller.
    /// During lock-on, we disable it so we can read input ourselves for the lean effect.
    /// </summary>
    private void SetCameraInputEnabled(bool enabled)
    {
        CinemachineCamera activeCamera = cameraManager != null ? cameraManager.GetActiveCamera() : null;
        if (activeCamera == null)
            return;

        // Cache and control the input axis controller
        if (cachedInputAxisController == null || cachedInputAxisController.gameObject != activeCamera.gameObject)
        {
            cachedInputAxisController = activeCamera.GetComponent<CinemachineInputAxisController>();
        }

        if (cachedInputAxisController != null)
        {
            cachedInputAxisController.enabled = enabled;
        }
    }

    private void StopMoveRoutine()
    {
        if (moveCoroutine == null)
            return;

        StopCoroutine(moveCoroutine);
        moveCoroutine = null;
    }


    private Transform FindBestHardLockTarget()
    {
        // Hard lock only targets enemies that are currently visible in the camera view.
        return FindScreenAlignedEnemy(lockOnDistance);
    }

    private Transform FindNearestEnemy(float radius, Transform ignore = null)
    {
        Collider[] hits = GetEnemyHits(radius);
        Transform closest = null;
        float smallestDistance = float.MaxValue;

        foreach (Collider hit in hits)
        {
            if (!ColliderIsEnemy(hit))
                continue;

            Transform candidate = GetEnemyRoot(hit.transform);
            if (candidate == ignore)
                continue;

            // Skip enemies that are dying
            BaseEnemyCore enemy = candidate.GetComponent<BaseEnemyCore>();
            if (enemy != null && !enemy.isAlive)
                continue;

            float sqrDistance = (candidate.position - playerTransform.position).sqrMagnitude;
            if (sqrDistance < smallestDistance)
            {
                smallestDistance = sqrDistance;
                closest = candidate;
            }
        }

        return closest;
    }

    /// <summary>
    /// Finds the nearest enemy within the specified radius that is within the player's
    /// forward cone (based on lockOnAngle). Used as fallback when no target in tight cone.
    /// </summary>
    private Transform FindNearestEnemyInFront(float radius)
    {
        Collider[] hits = GetEnemyHits(radius);
        Transform closest = null;
        float smallestDistance = float.MaxValue;

        Vector3 playerForward = playerTransform.forward;
        playerForward.y = 0f;
        playerForward.Normalize();

        foreach (Collider hit in hits)
        {
            if (!ColliderIsEnemy(hit))
                continue;

            Transform candidate = GetEnemyRoot(hit.transform);

            // Skip enemies that are dying
            BaseEnemyCore enemy = candidate.GetComponent<BaseEnemyCore>();
            if (enemy != null && !enemy.isAlive)
                continue;

            Vector3 directionToCandidate = GetFlatDirection(candidate.position);
            if (directionToCandidate.sqrMagnitude < 0.001f)
                continue;

            // Only include enemies within the lockOnAngle cone
            float angle = Vector3.Angle(playerForward, directionToCandidate);
            if (angle > lockOnAngle)
                continue;

            float sqrDistance = (candidate.position - playerTransform.position).sqrMagnitude;
            if (sqrDistance < smallestDistance)
            {
                smallestDistance = sqrDistance;
                closest = candidate;
            }
        }

        return closest;
    }

    /// <summary>
    /// Finds the nearest enemy within the specified radius that is also within the player's
    /// forward-facing cone of vision (based on lockOnAngle).
    /// </summary>
    private Transform FindNearestEnemyInPlayerCone(float radius)
    {
        Collider[] hits = GetEnemyHits(radius);
        Transform closest = null;
        float smallestDistance = float.MaxValue;

        Vector3 playerForward = playerTransform.forward;
        playerForward.y = 0f;
        playerForward.Normalize();

        foreach (Collider hit in hits)
        {
            if (!ColliderIsEnemy(hit))
                continue;

            Transform candidate = GetEnemyRoot(hit.transform);

            // Skip enemies that are dying
            BaseEnemyCore enemy = candidate.GetComponent<BaseEnemyCore>();
            if (enemy != null && !enemy.isAlive)
                continue;

            Vector3 directionToCandidate = GetFlatDirection(candidate.position);
            if (directionToCandidate.sqrMagnitude < 0.001f)
                continue;

            // Check if the enemy is within the player's forward cone
            float angle = Vector3.Angle(playerForward, directionToCandidate);
            if (angle > lockOnAngle)
                continue;

            float sqrDistance = (candidate.position - playerTransform.position).sqrMagnitude;
            if (sqrDistance < smallestDistance)
            {
                smallestDistance = sqrDistance;
                closest = candidate;
            }
        }

        return closest;
    }

    private Transform FindScreenAlignedEnemy(float radius)
    {
        if (!TryGetCameraBasis(out Vector3 camForward, out _) || !TryGetScreenCamera(out Camera screenCamera))
            return null;

        Collider[] hits = GetEnemyHits(radius);
        Transform best = null;
        float bestViewportScore = float.MaxValue;
        float bestDistanceScore = float.MaxValue;

        foreach (Collider hit in hits)
        {
            if (!ColliderIsEnemy(hit))
                continue;

            Transform candidate = GetEnemyRoot(hit.transform);

            // Skip enemies that are dying
            BaseEnemyCore enemy = candidate.GetComponent<BaseEnemyCore>();
            if (enemy != null && !enemy.isAlive)
                continue;

            Vector3 direction = GetFlatDirection(candidate.position);
            if (direction.sqrMagnitude < 0.001f)
                continue;

            float angle = Vector3.Angle(camForward, direction);
            if (angle > lockOnAngle * 2f)
                continue;

            if (!TryGetViewportScore(screenCamera, candidate, out float viewportScore))
                continue;

            float distanceScore = (candidate.position - playerTransform.position).sqrMagnitude;
            if (viewportScore < bestViewportScore
                || (Mathf.Approximately(viewportScore, bestViewportScore) && distanceScore < bestDistanceScore))
            {
                bestViewportScore = viewportScore;
                bestDistanceScore = distanceScore;
                best = candidate;
            }
        }

        return best;
    }

    private Transform FindAdjacentTarget(int direction)
    {
        if (!TryGetCameraBasis(out Vector3 camForward, out Vector3 camRight) || !TryGetScreenCamera(out Camera screenCamera))
            return null;

        Collider[] hits = GetEnemyHits(lockOnDistance);
        Transform best = null;
        float bestScore = float.MaxValue;
        float sideThreshold = 0.05f;

        if (!TryGetViewportPosition(screenCamera, currentTarget, out Vector3 currentViewportPosition))
            return null;

        foreach (Collider hit in hits)
        {
            if (!ColliderIsEnemy(hit))
                continue;

            Transform candidate = GetEnemyRoot(hit.transform);
            if (candidate == currentTarget)
                continue;

            // Skip enemies that are dying
            BaseEnemyCore enemy = candidate.GetComponent<BaseEnemyCore>();
            if (enemy != null && !enemy.isAlive)
                continue;

            Vector3 directionToCandidate = GetFlatDirection(candidate.position);
            if (directionToCandidate.sqrMagnitude < 0.001f)
                continue;

            float sideDot = Vector3.Dot(camRight, directionToCandidate);
            if (direction < 0 && sideDot >= -sideThreshold)
                continue;
            if (direction > 0 && sideDot <= sideThreshold)
                continue;

            float angle = Vector3.Angle(camForward, directionToCandidate);
            if (angle > lockOnAngle * 2f)
                continue;

            if (!TryGetViewportPosition(screenCamera, candidate, out Vector3 candidateViewportPosition))
                continue;

            float horizontalDelta = candidateViewportPosition.x - currentViewportPosition.x;
            if (direction < 0 && horizontalDelta >= -0.01f)
                continue;
            if (direction > 0 && horizontalDelta <= 0.01f)
                continue;

            float viewportDelta = Mathf.Abs(horizontalDelta) + Mathf.Abs(candidateViewportPosition.y - currentViewportPosition.y) * 0.25f;
            if (viewportDelta < bestScore)
            {
                bestScore = viewportDelta;
                best = candidate;
            }
        }

        return best;
    }

    private Collider[] GetEnemyHits(float radius)
    {
        int mask = enforceLayerMask ? enemyLayers.value : ~0;

        return Physics.OverlapSphere(
            playerTransform.position,
            radius,
            mask,
            QueryTriggerInteraction.Collide
        );
    }

    private bool ColliderIsEnemy(Collider hit)
    {
        if (hit == null)
            return false;

        // Check the collider's GameObject and its parents for the "Enemy" tag
        if (!HasEnemyTagInHierarchy(hit.transform))
            return false;

        if (!enforceLayerMask)
            return true;

        int bit = 1 << hit.gameObject.layer;
        return (enemyLayers.value & bit) != 0;
    }

    /// <summary>
    /// Checks if any object in the hierarchy (self or parents) has the "Enemy" tag.
    /// </summary>
    private bool HasEnemyTagInHierarchy(Transform t)
    {
        while (t != null)
        {
            if (t.CompareTag("Enemy"))
                return true;
            t = t.parent;
        }
        return false;
    }

    /// <summary>
    /// Returns the closest ancestor (or self) with the "Enemy" tag.
    /// This ensures we get the actual enemy object, not a higher-level container.
    /// Falls back to the provided transform if no tagged object is found.
    /// </summary>
    private Transform GetEnemyRoot(Transform t)
    {
        Transform original = t;
        while (t != null)
        {
            if (t.CompareTag("Enemy"))
                return t;
            t = t.parent;
        }
        return original;
    }

    private bool TryGetCameraBasis(out Vector3 forward, out Vector3 right)
    {
        forward = Vector3.zero;
        right = Vector3.zero;

        CinemachineCamera activeCamera =
            cameraManager != null ? cameraManager.GetActiveCamera() : null;
        
        if (activeCamera == null || activeCamera.transform == null)
            return false;

        forward = activeCamera.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            forward = activeCamera.transform.forward;
        forward.Normalize();

        right = activeCamera.transform.right;
        right.y = 0f;
        if (right.sqrMagnitude < 0.0001f)
            right = Vector3.Cross(Vector3.up, forward);
        right.Normalize();

        return true;
    }

    private bool TryGetScreenCamera(out Camera screenCamera)
    {
        screenCamera = Camera.main;
        return screenCamera != null;
    }

    private bool TryGetViewportScore(Camera screenCamera, Transform candidate, out float viewportScore)
    {
        viewportScore = float.MaxValue;

        if (!TryGetViewportPosition(screenCamera, candidate, out Vector3 viewportPosition))
            return false;

        Vector2 offsetFromCenter = new Vector2(viewportPosition.x - 0.5f, viewportPosition.y - 0.5f);
        viewportScore = offsetFromCenter.sqrMagnitude;
        return true;
    }

    private bool TryGetViewportPosition(Camera screenCamera, Transform candidate, out Vector3 viewportPosition)
    {
        viewportPosition = Vector3.zero;

        if (screenCamera == null || candidate == null)
            return false;

        viewportPosition = screenCamera.WorldToViewportPoint(candidate.position);
        if (viewportPosition.z <= 0f)
            return false;

        float min = 0f + hardLockViewportPadding;
        float max = 1f - hardLockViewportPadding;

        return viewportPosition.x >= min
            && viewportPosition.x <= max
            && viewportPosition.y >= min
            && viewportPosition.y <= max;
    }

    private static bool IsSingleTargetAttack(PlayerAttack attack)
    {
        if (attack == null)
            return false;

        return attack.attackType == AttackType.LightSingle
            || attack.attackType == AttackType.HeavySingle;
    }

    private bool IsTargetValid(Transform target, float maxDistance)
    {
        if (target == null || !target.gameObject.activeInHierarchy)
            return false;

        // Check if the enemy is still alive (not dying)
        BaseEnemyCore enemy = target.GetComponent<BaseEnemyCore>();
        if (enemy != null && !enemy.isAlive)
            return false;

        float sqrDistance = (target.position - playerTransform.position).sqrMagnitude;
        return sqrDistance <= maxDistance * maxDistance;
    }

    private Vector3 GetFlatDirection(Vector3 targetPosition)
    {
        Vector3 direction = targetPosition - playerTransform.position;
        direction.y = 0f;
        return direction.normalized;
    }

    private void AimCameraAtTarget(Transform target, bool instant)
    {
        CinemachineCamera activeCamera =
            cameraManager != null ? cameraManager.GetActiveCamera() : null;
        if (activeCamera == null)
            return;

        CinemachineOrbitalFollow orbital = activeCamera.GetComponent<CinemachineOrbitalFollow>();
        if (orbital == null)
            return;

        // Capture the base vertical value on first call (the camera's vertical position when lock started)
        if (!hasBaseVerticalValue)
        {
            baseVerticalValue = orbital.VerticalAxis.Value;
            hasBaseVerticalValue = true;
        }

        Vector3 toTarget = target.position - playerTransform.position;
        Vector3 flat = new Vector3(toTarget.x, 0f, toTarget.z);
        if (flat.sqrMagnitude < 0.001f)
            return;

        // Calculate base yaw pointing at the enemy
        float baseYaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;

        // Calculate lean offsets from player input
        CalculateLeanOffsets(out float horizontalLean, out float verticalLean);
        
        float desiredYaw = baseYaw + horizontalLean;
        
        // Vertical: base value + lean offset, clamped to orbital bounds
        float desiredVertical = Mathf.Clamp(
            baseVerticalValue + verticalLean,
            orbital.VerticalAxis.Range.x,
            orbital.VerticalAxis.Range.y
        );

        if (instant)
        {
            orbital.HorizontalAxis.Value = desiredYaw;
            orbital.VerticalAxis.Value = desiredVertical;
            cameraYawVelocity = 0f;
            cameraPitchVelocity = 0f;
        }
        else
        {
            // Use SmoothDampAngle for smooth horizontal rotation
            float nextYaw = Mathf.SmoothDampAngle(
                orbital.HorizontalAxis.Value,
                desiredYaw,
                ref cameraYawVelocity,
                cameraSnapTime
            );
            orbital.HorizontalAxis.Value = nextYaw;

            // Use SmoothDamp for smooth vertical movement
            float nextVertical = Mathf.SmoothDamp(
                orbital.VerticalAxis.Value,
                desiredVertical,
                ref cameraPitchVelocity,
                cameraSnapTime * 0.5f // Slightly faster vertical response
            );
            orbital.VerticalAxis.Value = nextVertical;
        }
    }


    /// <summary>
    /// Calculates the camera lean offsets based on player input.
    /// Uses a soft curve (tanh) so initial input is responsive but slows as it approaches the limit.
    /// </summary>
    private void CalculateLeanOffsets(out float horizontalLean, out float verticalLean)
    {
        // Get player look input
        Vector2 lookInput = InputReader.LookInput;
        float horizontalInput = lookInput.x;
        
        // Apply vertical inversion based on player preference
        // Default (non-inverted): up input looks up, down input looks down
        float verticalInput = invertVerticalLean ? lookInput.y : -lookInput.y;

        // Process horizontal lean
        if (Mathf.Abs(horizontalInput) > 0.01f)
        {
            float normalizedLean = currentHorizontalLean / maxHorizontalLeanAngle;
            float availableRoom = 1f - Mathf.Abs((float)System.Math.Tanh(normalizedLean * 2f));
            availableRoom = Mathf.Max(availableRoom, 0.1f);
            
            float inputEffect = horizontalInput * leanResponseSpeed * availableRoom * Time.deltaTime * 60f;
            currentHorizontalLean += inputEffect;
            currentHorizontalLean = Mathf.Clamp(currentHorizontalLean, -maxHorizontalLeanAngle, maxHorizontalLeanAngle);
        }
        else
        {
            currentHorizontalLean = Mathf.SmoothDamp(currentHorizontalLean, 0f, ref horizontalLeanVelocity, leanReturnTime);
            if (Mathf.Abs(currentHorizontalLean) < 0.1f)
            {
                currentHorizontalLean = 0f;
                horizontalLeanVelocity = 0f;
            }
        }

        // Process vertical lean
        if (Mathf.Abs(verticalInput) > 0.01f)
        {
            float normalizedLean = currentVerticalLean / maxVerticalLeanAngle;
            float availableRoom = 1f - Mathf.Abs((float)System.Math.Tanh(normalizedLean * 2f));
            availableRoom = Mathf.Max(availableRoom, 0.1f);
            
            float inputEffect = verticalInput * leanResponseSpeed * availableRoom * Time.deltaTime * 60f;
            currentVerticalLean += inputEffect;
            currentVerticalLean = Mathf.Clamp(currentVerticalLean, -maxVerticalLeanAngle, maxVerticalLeanAngle);
        }
        else
        {
            currentVerticalLean = Mathf.SmoothDamp(currentVerticalLean, 0f, ref verticalLeanVelocity, leanReturnTime);
            if (Mathf.Abs(currentVerticalLean) < 0.1f)
            {
                currentVerticalLean = 0f;
                verticalLeanVelocity = 0f;
            }
        }

        horizontalLean = currentHorizontalLean;
        verticalLean = currentVerticalLean;
    }

    private void FaceTargetImmediately(Transform target)
    {
        Vector3 direction = target.position - playerTransform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
            return;

        playerTransform.rotation = Quaternion.LookRotation(direction);
    }

    private void RotatePlayerTowardTarget(Transform target, bool instant)
    {
        if (target == null || playerTransform == null)
            return;

        if (!instant && IsPlayerCurrentlyDashing())
            return;

        Vector3 direction = target.position - playerTransform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
            return;

        Quaternion desired = Quaternion.LookRotation(direction);

        if (instant || hardLockRotateSpeed <= 0f)
        {
            playerTransform.rotation = desired;
            return;
        }

        // Use Slerp for smoother rotation that doesn't overshoot
        // Convert degrees/second to a lerp factor (higher speed = faster lerp)
        float angularDifference = Quaternion.Angle(playerTransform.rotation, desired);
        
        // Only rotate if there's a meaningful difference (reduces micro-jitter)
        if (angularDifference < 0.5f)
            return;

        // Calculate a smooth lerp factor based on the rotation speed
        // This creates a smoother feel than RotateTowards
        float rotationFactor = Mathf.Clamp01(hardLockRotateSpeed * Time.deltaTime / Mathf.Max(angularDifference, 1f));
        rotationFactor = Mathf.Max(rotationFactor, 0.1f); // Minimum rotation speed
        
        playerTransform.rotation = Quaternion.Slerp(
            playerTransform.rotation,
            desired,
            rotationFactor
        );
    }

    private bool IsPlayerCurrentlyDashing()
    {
        PlayerMovement movement = ResolvePlayerMovement();
        return movement != null && movement.IsDashing;
    }

    private PlayerMovement ResolvePlayerMovement()
    {
        if (playerMovement != null)
            return playerMovement;

        if (player != null)
        {
            playerMovement = player.GetComponent<PlayerMovement>()
                ?? player.GetComponentInChildren<PlayerMovement>()
                ?? player.GetComponentInParent<PlayerMovement>();
            if (playerMovement != null)
                return playerMovement;
        }

        playerMovement = GetComponent<PlayerMovement>()
            ?? GetComponentInChildren<PlayerMovement>()
            ?? GetComponentInParent<PlayerMovement>();

        return playerMovement;
    }

    private IEnumerator MoveAndFaceCoroutine(Vector3 endPos, Quaternion endRot, float duration)
    {
        if (duration <= Mathf.Epsilon)
        {
            playerTransform.SetPositionAndRotation(endPos, endRot);
            moveCoroutine = null;
            yield break;
        }

        Vector3 startPos = playerTransform.position;
        Quaternion startRot = playerTransform.rotation;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            playerTransform.position = Vector3.Lerp(startPos, endPos, t);
            playerTransform.rotation = Quaternion.Slerp(startRot, endRot, t);
            yield return null;
        }

        playerTransform.SetPositionAndRotation(endPos, endRot);
        moveCoroutine = null;
    }

    private void OnDrawGizmosSelected()
    {
        Transform origin = playerTransform;
        if (origin == null)
            return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(origin.position, lockOnDistance);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(origin.position, softLockRadius);

        Vector3 forward = origin.forward;
        Quaternion leftRotation = Quaternion.Euler(0f, -lockOnAngle, 0f);
        Quaternion rightRotation = Quaternion.Euler(0f, lockOnAngle, 0f);

        Gizmos.DrawLine(
            origin.position,
            origin.position + (leftRotation * forward) * lockOnDistance
        );

        Gizmos.DrawLine(
            origin.position,
            origin.position + (rightRotation * forward) * lockOnDistance
        );

        if (currentTarget != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(origin.position, currentTarget.position);
        }
    }
}

