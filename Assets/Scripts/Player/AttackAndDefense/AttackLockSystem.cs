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
using System.Collections.Generic;
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
    [Tooltip("Absolute maximum range allowed for lock-on target validation.")]
    private float lockOnMaxRange = 12f;

    [SerializeField]
    [Tooltip("Optional model/chest/head origin for lock-on visibility raycasts. If empty, player root + height offset is used.")]
    private Transform playerModelLockOnOrigin;

    [SerializeField, Range(0f, 3f)]
    [Tooltip("Vertical offset used when playerModelLockOnOrigin is not assigned.")]
    private float playerLockOnRayOriginHeight = 1.2f;

    [SerializeField]
    [Tooltip("Layers considered as lock-on visibility blockers (for example walls/level geometry).")]
    private LayerMask lockOnVisibilityBlockerMask = ~0;

    [Header("Debug")]
    [SerializeField]
    [Tooltip("Enable verbose lock-on diagnostics for input flow, target selection, validation and line-of-sight checks.")]
    private bool debugLockOn = true;

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

    [SerializeField, Range(0.03f, 0.35f)]
    [Tooltip("Duration used when soft lock only needs to rotate (no positional nudge).")]
    private float softLockRotateOnlyDuration = 0.1f;

    [SerializeField]
    [Tooltip("When enabled, soft lock may snap position/rotation instantly via PlayerMovement.TrySnapToSoftLock. Disable for fully smooth soft-lock transitions.")]
    private bool allowInstantSoftLockSnap = false;

    [SerializeField]
    [Tooltip("Only soft lock on single-target melee strikes.")]
    private bool onlySoftLockSingleTarget = true;

    [SerializeField]
    [Tooltip("When enabled, soft-lock selection prioritizes enemies closest to the camera center over purely nearest-distance targets.")]
    private bool prioritizeCenterOfViewForSoftLock = true;

    [SerializeField]
    [Tooltip("When enabled, soft-lock target selection prefers enemies that align with both movement input direction and camera-center preference.")]
    private bool prioritizeMovementAndViewAlignedSoftLockTarget = true;

    [SerializeField, Range(0.05f, 0.95f)]
    [Tooltip("Minimum move-input magnitude required before movement direction influences soft-lock target choice.")]
    private float softLockMovementInputThreshold = 0.2f;

    [SerializeField, Range(-1f, 1f)]
    [Tooltip("Minimum dot alignment between movement direction and candidate direction to count as movement-aligned.")]
    private float softLockMovementAlignmentThreshold = 0.35f;

    [SerializeField, Range(0.02f, 0.5f)]
    [Tooltip("Viewport radius from center used to consider a candidate camera-centered for movement+view alignment.")]
    private float softLockCenteredViewportRadius = 0.22f;

    [SerializeField]
    [Tooltip("When enabled, non-aerial attacks ignore drones for soft-lock target selection.")]
    private bool ignoreDronesForNonAerialSoftLock = true;

    [SerializeField]
    [Tooltip("When an attack already has forward movement, skip positional soft-lock nudge to avoid movement systems fighting and causing rubber-banding.")]
    private bool skipSoftLockPositionNudgeForForwardMoveAttacks = true;

    [SerializeField]
    [Tooltip("When enabled, attacks prioritize facing the current soft-lock target even while movement input is held.")]
    private bool prioritizeSoftLockTargetFacingWhileAttacking = true;

    [SerializeField, Range(0f, 1f)]
    [Tooltip("How long soft-lock target-facing persists after a soft-lock attack event.")]
    private float softLockAttackFacingPersistDuration = 0.35f;

    [SerializeField, Range(0.5f, 12f)]
    [Tooltip("Maximum distance for soft-lock attack-facing target validity.")]
    private float softLockAttackFacingMaxDistance = 4f;

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

    [Header("Reticle Stage Settings")]
    [SerializeField, Min(1)]
    [Tooltip("How many nearest enemies inside max lock-on range show the base gear reticle stage.")]
    private int closestEnemiesWithBaseGear = 3;

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
    private Transform softLockAttackFacingTarget;
    private float softLockAttackFacingExpireTime = -1f;
    private float lastTargetInvalidLogTime = -1f;
    private string lastTargetInvalidReason;
    private readonly List<Transform> reticleCandidates = new();
    private readonly HashSet<ReticleController> drivenReticles = new();
    private readonly HashSet<ReticleController> drivenReticlesThisFrame = new();
    public bool IsHardLockActive => hardLockActive && currentTarget != null;
    public Transform CurrentHardLockTarget => currentTarget;
    public bool IsSoftLockMotionInProgress => moveCoroutine != null;

    public bool ShouldIgnoreDroneHitsForGroundedAttackMovement(AttackType attackType)
    {
        if (!ignoreDronesForNonAerialSoftLock)
            return false;

        return attackType != AttackType.LightAerial
            && attackType != AttackType.HeavyAerial;
    }

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
        ClearReticleStageOverrides();
    }

    private void Update()
    {
        if (hardLockActive && currentTarget != null)
        {
            if (IsCurrentTargetDead())
            {
                currentTargetReticle?.PlayTargetLost();
                ClearHardLock(playReticleExit: false);
            }

            if (hardLockActive
                && currentTarget != null
                && !IsTargetValid(currentTarget, GetEffectiveLockOnRange(), out string invalidReason))
            {
                if (debugLockOn
                    && (invalidReason != lastTargetInvalidReason || Time.time - lastTargetInvalidLogTime > 0.5f))
                {
                    lastTargetInvalidReason = invalidReason;
                    lastTargetInvalidLogTime = Time.time;
                    LogLock($"Current hard-lock target became invalid: {invalidReason}");
                }

                float effectiveRange = GetEffectiveLockOnRange();
                Transform replacementTarget = FindClosestTargetToReference(currentTarget, effectiveRange);
                if (replacementTarget == null)
                {
                    LogLock("No replacement hard-lock target found. Clearing hard lock.");
                    ClearHardLock();
                }
                else
                {
                    LogLock($"Switching hard-lock target to replacement '{replacementTarget.name}'.");
                    SetHardLockTarget(replacementTarget, playEntryAnimation: true, playExitAnimation: true);
                }
            }

            if (hardLockActive && currentTarget != null)
            {
                if (steerCamera)
                    AimCameraAtTarget(currentTarget, instant: false);
                else if (rotatePlayerIfCameraDisabled)
                    FaceTargetImmediately(currentTarget);
            }
        }

        UpdateTargetingReticles();
    }

    private void FixedUpdate()
    {
        if (!hardLockActive || currentTarget == null)
            return;

        if (!rotatePlayerDuringHardLock)
            return;

        RotatePlayerTowardTarget(currentTarget, instant: false, deltaTimeOverride: Time.fixedDeltaTime);
    }

    private void UpdateTargetingReticles()
    {
        CollectReticleCandidates(Mathf.Max(GetEffectiveLockOnRange(), Mathf.Max(0f, lockOnMaxRange)));

        Transform softLockTarget = null;
        if (hardLockActive && currentTarget != null)
            softLockTarget = currentTarget;
        else
            softLockTarget = FindNearestEnemyInPlayerCone(GetEffectiveLockOnRange(), includeDrones: true);

        if (softLockTarget == null && reticleCandidates.Count > 0)
            softLockTarget = reticleCandidates[0];

        int baseCount = Mathf.Max(1, closestEnemiesWithBaseGear);
        drivenReticlesThisFrame.Clear();

        for (int i = 0; i < reticleCandidates.Count; i++)
        {
            Transform candidate = reticleCandidates[i];
            ReticleController reticle = ResolveReticleController(candidate);
            if (reticle == null)
                continue;

            bool isHardLockedTarget = hardLockActive && candidate == currentTarget;
            bool isSoftLockTarget = candidate == softLockTarget || isHardLockedTarget;

            bool showArrows = isHardLockedTarget;
            bool showGlow = isSoftLockTarget;
            bool showBase = !showGlow && i < baseCount;

            reticle.SetExternalTargetingState(true, showBase, showGlow, showArrows);
            drivenReticlesThisFrame.Add(reticle);
        }

        foreach (ReticleController reticle in drivenReticles)
        {
            if (reticle == null || drivenReticlesThisFrame.Contains(reticle))
                continue;

            reticle.SetExternalTargetingState(true, showBaseGear: false, showGlowGear: false, showArrowsState: false);
        }

        drivenReticles.Clear();
        foreach (ReticleController reticle in drivenReticlesThisFrame)
            drivenReticles.Add(reticle);
    }

    private void CollectReticleCandidates(float radius)
    {
        reticleCandidates.Clear();

        if (radius <= 0f)
            return;

        Collider[] hits = GetEnemyHits(radius);
        HashSet<Transform> uniqueCandidates = new();

        foreach (Collider hit in hits)
        {
            if (!ColliderIsEnemy(hit))
                continue;

            Transform candidate = GetEnemyRoot(hit.transform);
            if (candidate == null || !candidate.gameObject.activeInHierarchy)
                continue;

            if (!uniqueCandidates.Add(candidate))
                continue;

            BaseEnemyCore enemy = candidate.GetComponent<BaseEnemyCore>();
            if (enemy != null && !enemy.isAlive)
                continue;

            float sqrDistance = (candidate.position - playerTransform.position).sqrMagnitude;
            if (sqrDistance > radius * radius)
                continue;

            reticleCandidates.Add(candidate);
        }

        reticleCandidates.Sort((a, b) =>
        {
            float aDistance = (a.position - playerTransform.position).sqrMagnitude;
            float bDistance = (b.position - playerTransform.position).sqrMagnitude;
            return aDistance.CompareTo(bDistance);
        });
    }

    private void ClearReticleStageOverrides()
    {
        foreach (ReticleController reticle in drivenReticles)
        {
            if (reticle == null)
                continue;

            reticle.SetExternalTargetingState(false, showBaseGear: false, showGlowGear: false, showArrowsState: false);
        }

        drivenReticles.Clear();
        drivenReticlesThisFrame.Clear();
        reticleCandidates.Clear();
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

        bool hasForwardMove = executedAttack.forwardMoveDistance > 0f;
        bool allowPositionNudge = !(skipSoftLockPositionNudgeForForwardMoveAttacks && hasForwardMove);

        bool isAerialAttack = executedAttack.attackType == AttackType.LightAerial
            || executedAttack.attackType == AttackType.HeavyAerial;
        bool includeDrones = !ignoreDronesForNonAerialSoftLock || isAerialAttack;

        TryApplySoftLockNudge(allowPositionNudge, includeDrones);
    }

    private void HandleLockOnToggle()
    {
        LogLock($"LockOn toggle received. hardLockActive={hardLockActive}, currentTarget={(currentTarget != null ? currentTarget.name : "null")}");

        if (hardLockActive)
        {
            ClearHardLock();
            return;
        }

        bool activated = ActivateHardLock(null, instantCameraAlign: false); // Smooth transition when locking on
        if (!activated)
            LogLock("ActivateHardLock failed: no valid hard-lock candidate found.");
    }

    private void HandleLeftTargetRequested() => CycleHardLock(-1);

    private void HandleRightTargetRequested() => CycleHardLock(1);

    private void CycleHardLock(int direction)
    {
        if (!hardLockActive || direction == 0)
        {
            LogLock($"CycleHardLock ignored. hardLockActive={hardLockActive}, direction={direction}");
            return;
        }

        Transform nextTarget = FindAdjacentTarget(direction);
        if (nextTarget == null || nextTarget == currentTarget)
        {
            LogLock($"CycleHardLock found no adjacent target. direction={direction}, current={(currentTarget != null ? currentTarget.name : "null")}");
            return;
        }

        LogLock($"CycleHardLock switching to '{nextTarget.name}' (direction={direction}).");
        SetHardLockTarget(nextTarget, playEntryAnimation: true, playExitAnimation: true);
        ResetLeanState(); // Reset lean when switching targets for clean transition
        AlignPlayerAndCamera(nextTarget, instantCameraAlign: false); // Smooth transition to new target
    }

    public bool ActivateHardLock(Transform forcedTarget = null, bool instantCameraAlign = false)
    {
        Transform candidate = forcedTarget ?? FindBestHardLockTarget();
        if (candidate == null)
        {
            LogLock($"ActivateHardLock failed. forcedTarget={(forcedTarget != null ? forcedTarget.name : "null")}, effectiveRange={GetEffectiveLockOnRange():0.##}");
            return false;
        }

        LogLock($"ActivateHardLock success. target='{candidate.name}', forcedTarget={(forcedTarget != null ? forcedTarget.name : "null")}, effectiveRange={GetEffectiveLockOnRange():0.##}");

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
            RotatePlayerTowardTarget(target, instant: instantCameraAlign);
        else
            FaceTargetImmediately(target);
    }

    private void TryApplySoftLockNudge(bool allowPositionNudge, bool includeDrones)
    {
        // Only target enemies within the player's forward-facing cone
        Transform target = FindNearestEnemyInPlayerCone(softLockRadius, includeDrones);
        if (target == null)
            return;

        MarkSoftLockAttackFacingTarget(target);

        Vector3 direction = GetFlatDirection(target.position);
        if (direction.sqrMagnitude < 0.001f)
            return;

        Quaternion desiredRotation = Quaternion.LookRotation(direction);

        if (!allowPositionNudge)
        {
            StartSoftLockRotate(desiredRotation);
            return;
        }

        float planarDistance = Vector3.Distance(
            new Vector3(target.position.x, playerTransform.position.y, target.position.z),
            playerTransform.position
        );

        if (planarDistance <= softLockNoMoveRadius)
        {
            StartSoftLockRotate(desiredRotation);
            return;
        }

        float moveDistance = Mathf.Clamp(
            planarDistance - softLockStopBuffer,
            0f,
            softLockMoveDistance
        );

        if (moveDistance <= 0.01f)
        {
            StartSoftLockRotate(desiredRotation);
            return;
        }

        Vector3 desiredPosition = playerTransform.position + direction * moveDistance;
        if (allowInstantSoftLockSnap && TrySnapPlayerToSoftLock(desiredPosition, desiredRotation))
            return;

        StopMoveRoutine();
        moveCoroutine = StartCoroutine(
            MoveAndFaceCoroutine(desiredPosition, desiredRotation, softLockMoveDuration)
        );
    }

    public bool TryGetSoftLockAttackFacingDirection(out Vector3 direction)
    {
        direction = Vector3.zero;

        if (!prioritizeSoftLockTargetFacingWhileAttacking)
            return false;

        if (Time.time > softLockAttackFacingExpireTime)
            return false;

        if (!IsSoftLockAttackFacingTargetValid())
            return false;

        Vector3 flat = GetFlatDirection(softLockAttackFacingTarget.position);
        if (flat.sqrMagnitude < 0.001f)
            return false;

        direction = flat;
        return true;
    }

    private void MarkSoftLockAttackFacingTarget(Transform target)
    {
        if (!prioritizeSoftLockTargetFacingWhileAttacking || target == null)
            return;

        softLockAttackFacingTarget = target;
        softLockAttackFacingExpireTime = Time.time + Mathf.Max(0f, softLockAttackFacingPersistDuration);
    }

    private bool IsSoftLockAttackFacingTargetValid()
    {
        if (softLockAttackFacingTarget == null)
            return false;

        if (!IsTargetValid(softLockAttackFacingTarget, Mathf.Max(0.5f, softLockAttackFacingMaxDistance)))
            return false;

        BaseEnemyCore enemy = softLockAttackFacingTarget.GetComponentInParent<BaseEnemyCore>();
        if (enemy != null && !enemy.isAlive)
            return false;

        return true;
    }

    private void StartSoftLockRotate(Quaternion desiredRotation)
    {
        StopMoveRoutine();
        moveCoroutine = StartCoroutine(
            MoveAndFaceCoroutine(
                playerTransform.position,
                desiredRotation,
                Mathf.Max(0.01f, softLockRotateOnlyDuration)));
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
        Transform target = FindScreenAlignedEnemy(GetEffectiveLockOnRange());
        LogLock($"FindBestHardLockTarget => {(target != null ? target.name : "null")}");
        return target;
    }

    private Transform FindClosestTargetToReference(Transform referenceTarget, float radius)
    {
        if (referenceTarget == null)
            return null;

        Collider[] hits = GetEnemyHits(radius);
        Transform closest = null;
        float smallestDistance = float.MaxValue;

        foreach (Collider hit in hits)
        {
            if (!ColliderIsEnemy(hit))
                continue;

            Transform candidate = GetEnemyRoot(hit.transform);
            if (candidate == null || candidate == referenceTarget)
                continue;

            if (!IsTargetValid(candidate, radius))
                continue;

            BaseEnemyCore enemy = candidate.GetComponent<BaseEnemyCore>();
            if (enemy != null && !enemy.isAlive)
                continue;

            float sqrDistanceToReference = (candidate.position - referenceTarget.position).sqrMagnitude;
            if (sqrDistanceToReference < smallestDistance)
            {
                smallestDistance = sqrDistanceToReference;
                closest = candidate;
            }
        }

        LogLock($"FindClosestTargetToReference => {(closest != null ? closest.name : "null")} (reference={(referenceTarget != null ? referenceTarget.name : "null")}, radius={radius:0.##})");
        return closest;
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

            if (!IsTargetValid(candidate, radius))
                continue;

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
    private Transform FindNearestEnemyInPlayerCone(float radius, bool includeDrones)
    {
        Collider[] hits = GetEnemyHits(radius);
        Transform closest = null;
        float smallestDistance = float.MaxValue;
        float bestViewportScore = float.MaxValue;
        Camera screenCamera = null;
        bool canScoreViewport = prioritizeCenterOfViewForSoftLock && TryGetScreenCamera(out screenCamera);
        bool hasMovementPreference = TryGetSoftLockMovementDirection(out Vector3 movementPreferredDirection);
        float centeredViewportScoreThreshold = softLockCenteredViewportRadius * softLockCenteredViewportRadius;
        bool bestSatisfiedMovementAndView = false;
        float bestMovementAlignmentDot = -1f;

        Vector3 playerForward = playerTransform.forward;
        playerForward.y = 0f;
        playerForward.Normalize();

        foreach (Collider hit in hits)
        {
            if (!ColliderIsEnemy(hit))
                continue;

            Transform candidate = GetEnemyRoot(hit.transform);

            if (!includeDrones && candidate.GetComponentInParent<DroneEnemy>() != null)
                continue;

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
            float viewportScore = float.MaxValue;
            bool isCenteredInView = false;
            if (canScoreViewport)
            {
                if (!TryGetViewportScore(screenCamera, candidate, out viewportScore))
                    continue;

                isCenteredInView = viewportScore <= centeredViewportScoreThreshold;
            }

            float movementDot = -1f;
            bool movementAligned = false;
            if (hasMovementPreference)
            {
                movementDot = Vector3.Dot(directionToCandidate.normalized, movementPreferredDirection);
                movementAligned = movementDot >= softLockMovementAlignmentThreshold;
            }

            bool satisfiesMovementAndView = prioritizeMovementAndViewAlignedSoftLockTarget
                && hasMovementPreference
                && movementAligned
                && (!canScoreViewport || isCenteredInView);

            if (satisfiesMovementAndView)
            {
                if (!bestSatisfiedMovementAndView
                    || (canScoreViewport && viewportScore < bestViewportScore)
                    || (canScoreViewport && Mathf.Approximately(viewportScore, bestViewportScore) && movementDot > bestMovementAlignmentDot)
                    || (canScoreViewport && Mathf.Approximately(viewportScore, bestViewportScore) && Mathf.Approximately(movementDot, bestMovementAlignmentDot) && sqrDistance < smallestDistance)
                    || (!canScoreViewport && movementDot > bestMovementAlignmentDot)
                    || (!canScoreViewport && Mathf.Approximately(movementDot, bestMovementAlignmentDot) && sqrDistance < smallestDistance))
                {
                    bestSatisfiedMovementAndView = true;
                    bestViewportScore = viewportScore;
                    bestMovementAlignmentDot = movementDot;
                    smallestDistance = sqrDistance;
                    closest = candidate;
                }
            }
            else if (bestSatisfiedMovementAndView)
            {
                continue;
            }
            else if (sqrDistance < smallestDistance)
            {
                smallestDistance = sqrDistance;
                closest = candidate;
            }
        }

        return closest;
    }

    private bool TryGetSoftLockMovementDirection(out Vector3 direction)
    {
        direction = Vector3.zero;

        Vector2 moveInput = InputReader.MoveInput;
        if (moveInput.sqrMagnitude < softLockMovementInputThreshold * softLockMovementInputThreshold)
            return false;

        if (!TryGetCameraBasis(out Vector3 camForward, out Vector3 camRight))
            return false;

        Vector3 desired = (camForward * moveInput.y) + (camRight * moveInput.x);
        desired.y = 0f;
        if (desired.sqrMagnitude < 0.0001f)
            return false;

        direction = desired.normalized;
        return true;
    }

    private Transform FindScreenAlignedEnemy(float radius)
    {
        if (!TryGetCameraBasis(out Vector3 camForward, out _))
        {
            LogLock("FindScreenAlignedEnemy aborted: unable to resolve camera basis.");
            return null;
        }

        if (!TryGetScreenCamera(out Camera screenCamera))
        {
            LogLock("FindScreenAlignedEnemy aborted: no screen camera available.");
            return null;
        }

        Collider[] hits = GetEnemyHits(radius);
        if (hits == null || hits.Length == 0)
        {
            LogLock($"FindScreenAlignedEnemy found no colliders in range. radius={radius:0.##}");
            return null;
        }

        Transform best = null;
        float bestViewportScore = float.MaxValue;
        float bestDistanceScore = float.MaxValue;
        float bestAngle = float.MaxValue;

        int rejectedNotEnemy = 0;
        int rejectedDead = 0;
        int rejectedInvalid = 0;
        int rejectedDirection = 0;
        int rejectedAngle = 0;
        int rejectedViewport = 0;
        int considered = 0;

        foreach (Collider hit in hits)
        {
            if (!ColliderIsEnemy(hit))
            {
                rejectedNotEnemy++;
                continue;
            }

            Transform candidate = GetEnemyRoot(hit.transform);
            considered++;

            // Skip enemies that are dying
            BaseEnemyCore enemy = candidate.GetComponent<BaseEnemyCore>();
            if (enemy != null && !enemy.isAlive)
            {
                rejectedDead++;
                continue;
            }

            if (!IsTargetValid(candidate, radius, out string invalidReason))
            {
                rejectedInvalid++;
                LogLock($"FindScreenAlignedEnemy rejected '{candidate.name}' by IsTargetValid: {invalidReason}");
                continue;
            }

            if (!TryGetLockOnTargetPoint(candidate, out Vector3 targetPoint))
                targetPoint = candidate.position;

            Vector3 direction = targetPoint - playerTransform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f)
            {
                rejectedDirection++;
                continue;
            }

            direction.Normalize();

            float angle = Vector3.Angle(camForward, direction);
            if (angle > lockOnAngle * 2f)
            {
                rejectedAngle++;
                continue;
            }

            if (!TryGetViewportScore(screenCamera, targetPoint, out float viewportScore))
            {
                rejectedViewport++;
                continue;
            }

            float distanceScore = (targetPoint - playerTransform.position).sqrMagnitude;
            if (viewportScore < bestViewportScore
                || (Mathf.Approximately(viewportScore, bestViewportScore) && distanceScore < bestDistanceScore))
            {
                bestViewportScore = viewportScore;
                bestDistanceScore = distanceScore;
                bestAngle = angle;
                best = candidate;
            }
        }

        if (best == null)
        {
            LogLock(
                $"FindScreenAlignedEnemy found no candidate. considered={considered}, "
                + $"rejectedNotEnemy={rejectedNotEnemy}, rejectedDead={rejectedDead}, "
                + $"rejectedInvalid={rejectedInvalid}, rejectedDirection={rejectedDirection}, "
                + $"rejectedAngle={rejectedAngle}, rejectedViewport={rejectedViewport}, "
                + $"radius={radius:0.##}, angleLimit={lockOnAngle * 2f:0.##}");
        }
        else
        {
            LogLock(
                $"FindScreenAlignedEnemy selected '{best.name}'. viewportScore={bestViewportScore:0.####}, "
                + $"distance={Mathf.Sqrt(bestDistanceScore):0.##}, angle={bestAngle:0.##}");
        }

        return best;
    }

    private Transform FindAdjacentTarget(int direction)
    {
        if (!TryGetCameraBasis(out Vector3 camForward, out Vector3 camRight) || !TryGetScreenCamera(out Camera screenCamera))
            return null;

        Collider[] hits = GetEnemyHits(GetEffectiveLockOnRange());
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

            if (!IsTargetValid(candidate, GetEffectiveLockOnRange()))
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

        bool hasEnemyTag = HasEnemyTagInHierarchy(hit.transform);
        bool hasEnemyCore = hit.GetComponentInParent<BaseEnemyCore>() != null;

        // Accept either explicit Enemy tag or presence of BaseEnemyCore in hierarchy.
        if (!hasEnemyTag && !hasEnemyCore)
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
        if (t != null)
        {
            BaseEnemyCore enemyCore = t.GetComponentInParent<BaseEnemyCore>();
            if (enemyCore != null)
                return enemyCore.transform;
        }

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
        if (screenCamera != null)
            return true;

        CinemachineBrain brain = FindFirstObjectByType<CinemachineBrain>();
        if (brain != null)
        {
            screenCamera = brain.GetComponent<Camera>();
            if (screenCamera != null && screenCamera.isActiveAndEnabled)
                return true;
        }

        Camera[] cameras = Camera.allCameras;
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera candidate = cameras[i];
            if (candidate == null || !candidate.isActiveAndEnabled)
                continue;

            screenCamera = candidate;
            return true;
        }

        screenCamera = null;
        return false;
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

    private bool TryGetViewportScore(Camera screenCamera, Vector3 worldPosition, out float viewportScore)
    {
        viewportScore = float.MaxValue;

        if (!TryGetViewportPosition(screenCamera, worldPosition, out Vector3 viewportPosition))
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

    private bool TryGetViewportPosition(Camera screenCamera, Vector3 worldPosition, out Vector3 viewportPosition)
    {
        viewportPosition = Vector3.zero;

        if (screenCamera == null)
            return false;

        viewportPosition = screenCamera.WorldToViewportPoint(worldPosition);
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
        return IsTargetValid(target, maxDistance, out _);
    }

    private bool IsTargetValid(Transform target, float maxDistance, out string reason)
    {
        reason = string.Empty;

        if (target == null || !target.gameObject.activeInHierarchy)
        {
            reason = "Target is null or inactive in hierarchy.";
            return false;
        }

        // Check if the enemy is still alive (not dying)
        BaseEnemyCore enemy = target.GetComponent<BaseEnemyCore>();
        if (enemy != null && !enemy.isAlive)
        {
            reason = "Target enemy is dead.";
            return false;
        }

        float effectiveMaxDistance = Mathf.Min(Mathf.Max(0f, maxDistance), GetEffectiveLockOnRange());
        float sqrDistance = (target.position - playerTransform.position).sqrMagnitude;
        if (sqrDistance > effectiveMaxDistance * effectiveMaxDistance)
        {
            reason = $"Target out of range. distance={Mathf.Sqrt(sqrDistance):0.##}, max={effectiveMaxDistance:0.##}";
            return false;
        }

        bool hasLos = HasLockOnLineOfSight(target, out string losReason);
        if (!hasLos)
        {
            reason = losReason;
            return false;
        }

        reason = "Valid";
        return true;
    }

    private float GetEffectiveLockOnRange()
    {
        float detectionRange = Mathf.Max(0f, lockOnDistance);
        float maxRange = Mathf.Max(0f, lockOnMaxRange);

        if (maxRange <= 0f)
            return detectionRange;

        if (detectionRange <= 0f)
            return maxRange;

        return Mathf.Min(detectionRange, maxRange);
    }

    private bool HasLockOnLineOfSight(Transform target, out string reason)
    {
        reason = string.Empty;

        if (target == null)
        {
            reason = "No target transform for line-of-sight.";
            return false;
        }

        if (!TryGetLockOnTargetPoint(target, out Vector3 targetPoint))
        {
            reason = "Failed to resolve lock-on target point.";
            return false;
        }

        bool playerRayClear = IsLockOnRayClear(GetPlayerLockOnRayOrigin(), targetPoint, target, out string playerBlocker);

        if (TryGetScreenCamera(out Camera screenCamera) && screenCamera != null)
        {
            bool cameraRayClear = IsLockOnRayClear(screenCamera.transform.position, targetPoint, target, out string cameraBlocker);
            if (!playerRayClear && !cameraRayClear)
            {
                reason = $"LOS blocked. PlayerRay='{playerBlocker}', CameraRay='{cameraBlocker}'";
                return false;
            }

            reason = playerRayClear
                ? "LOS valid via player ray."
                : "LOS valid via camera ray.";
            return true;
        }

        if (!playerRayClear)
        {
            reason = $"LOS blocked. PlayerRay='{playerBlocker}'";
            return false;
        }

        reason = "LOS valid via player ray (no camera available).";
        return true;
    }

    private Vector3 GetPlayerLockOnRayOrigin()
    {
        if (playerModelLockOnOrigin != null)
            return playerModelLockOnOrigin.position;

        return playerTransform.position + Vector3.up * Mathf.Max(0f, playerLockOnRayOriginHeight);
    }

    private bool TryGetLockOnTargetPoint(Transform target, out Vector3 targetPoint)
    {
        targetPoint = target.position;
        BaseEnemyCore enemyCore = ResolveEnemyCore(target);
        if (enemyCore == null)
            return true;

        Collider targetCollider = enemyCore.GetComponentInChildren<Collider>();
        if (targetCollider == null)
            return true;

        targetPoint = targetCollider.bounds.center;
        return true;
    }

    private bool IsLockOnRayClear(Vector3 origin, Vector3 targetPoint, Transform target, out string blocker)
    {
        blocker = string.Empty;

        Vector3 direction = targetPoint - origin;
        float distance = direction.magnitude;
        if (distance <= 0.001f)
            return true;

        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            direction / distance,
            distance,
            lockOnVisibilityBlockerMask,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.collider == null)
                continue;

            Transform hitTransform = hit.collider.transform;
            if (hitTransform == null)
                continue;

            if (hitTransform == transform || hitTransform.IsChildOf(playerTransform))
                continue;

            if (hitTransform == target || hitTransform.IsChildOf(target))
                continue;

            if (HasEnemyTagInHierarchy(hitTransform))
                continue;

            blocker = hitTransform.name;
            return false;
        }

        return true;
    }

    private void LogLock(string message)
    {
        if (!debugLockOn)
            return;

        Debug.Log($"[AttackLockSystem][Debug] {message}");
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

    private void RotatePlayerTowardTarget(Transform target, bool instant, float deltaTimeOverride = -1f)
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

        float angularDifference = Quaternion.Angle(playerTransform.rotation, desired);
        if (angularDifference < 0.1f)
            return;

        float deltaTime = deltaTimeOverride > 0f ? deltaTimeOverride : Time.deltaTime;
        float maxStep = hardLockRotateSpeed * deltaTime;
        playerTransform.rotation = Quaternion.RotateTowards(playerTransform.rotation, desired, maxStep);
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

