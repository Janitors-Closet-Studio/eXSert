using UnityEngine;
using Utilities.Combat;

public class Guard : MonoBehaviour
{
    [Header("Guard Movement")]
    [SerializeField, Range(0.5f, 6f)] private float guardMoveSpeed = 2.4f;
    [SerializeField, Range(0f, 1f)] private float movementDeadZone = 0.15f;

    [Header("Guard Dash")]
    [SerializeField, Range(1f, 10f)] private float guardDashDistance = 3.75f;
    [SerializeField, Range(0.05f, 0.6f)] private float guardDashDuration = 0.18f;
    [SerializeField, Range(0f, 2f)] private float guardDashCooldown = 0.45f;

    [Header("Targeting & Camera")]
    [SerializeField] private bool autoHardLockWhileGuarding = false;
    [SerializeField] private bool instantAlignOnEntry = true;
    [SerializeField] private bool switchToGuardCamera = true;
    [SerializeField] private CameraManager cameraOverride;
    [SerializeField, Range(90f, 1440f)] private float freeAimTurnSpeed = 540f;

    [Header("Guard Animation")]
    [SerializeField, Range(0f, 1f)] private float guardWalkEnterThreshold = 0.2f;
    [SerializeField, Range(0f, 1f)] private float guardWalkExitThreshold = 0.12f;
    [SerializeField, Range(0f, 0.5f)] private float guardRaiseBlendLockDuration = 0.12f;
    [SerializeField] private bool debugGuardAnimation;
    [SerializeField, Range(0.05f, 1f)] private float debugGuardLogInterval = 0.15f;

    [Header("Guard Lock Diagnostics")]
    [SerializeField] private bool debugGuardHardLockDiagnostics = true;
    [SerializeField, Range(0.05f, 1f)] private float debugGuardHardLockLogInterval = 0.2f;

    [Header("References")]
    [SerializeField] private PlayerAnimationController animationController;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerAttackManager attackManager;
    [SerializeField] private AttackLockSystem attackLockSystem;

    [Header("Guard SFX")]
    [SerializeField] private AudioClip guardUpSFX;

    private CameraManager cameraManager;
    private bool guardActive;
    private bool guardDashActive;
    private bool forcedHardLock;
    private bool cameraForced;
    private bool originalFaceMoveDirection = true;
    private bool guardWalkAnimationActive;
    private float guardDashCooldownTimer;
    private float lastDashSign = 1f;
    private float guardRaiseLockUntilTime;
    private float nextGuardDebugLogTime;
    private float nextGuardHardLockDebugLogTime;
    private float lastGuardDiagnosticYaw;
    private float lastGuardDiagnosticSampleTime;
    private Vector3 lastGuardDiagnosticPosition;
    private bool hasGuardDiagnosticSample;
    private Animator guardAnimator;
    private Transform GuardRoot => playerMovement != null ? playerMovement.transform : transform;

    private void Awake()
    {
        animationController ??= GetComponent<PlayerAnimationController>();
        playerMovement ??= GetComponent<PlayerMovement>();
        attackManager ??= GetComponent<PlayerAttackManager>();
        attackLockSystem ??= GetComponent<AttackLockSystem>();
        guardAnimator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();
    }

    private void OnEnable()
    {
        cameraManager = cameraOverride != null ? cameraOverride : CameraManager.Instance;
    }

    private void Update()
    {
        bool guardHeld = InputReader.GuardHeld;

        if (!guardActive && guardHeld)
            EnterGuard();
        else if (guardActive && !guardHeld)
            ExitGuard();

        if (!guardActive)
            return;

        guardDashCooldownTimer = Mathf.Max(0f, guardDashCooldownTimer - Time.deltaTime);

        UpdateGuardLocomotion();
        HandleGuardDashInput();

        if (autoHardLockWhileGuarding)
            MaintainHardLock();

        LogGuardHardLockDiagnostics();
    }

    private void FixedUpdate()
    {
        if (!guardActive || guardDashActive)
            return;

        AlignGuardRotation(InputReader.MoveInput);
    }

    private void OnDisable()
    {
        if (guardActive)
            ExitGuard();
    }

    private void EnterGuard()
    {
        guardActive = true;
        guardWalkAnimationActive = false;
        guardRaiseLockUntilTime = Time.time + Mathf.Max(0f, guardRaiseBlendLockDuration);
        if (guardUpSFX != null)
        {
            AudioSource sfxSource = SoundManager.Instance != null ? SoundManager.Instance.sfxSource : null;
            if (sfxSource != null)
                sfxSource.PlayOneShot(guardUpSFX);
            else if (debugGuardAnimation)
                Debug.LogWarning("[Guard][AnimDebug] Guard up SFX skipped because SoundManager.sfxSource is missing.");
        }

        if (InputReader.inputBusy)
            attackManager?.ForceCancelCurrentAttack(resetCombo: false);

        CombatManager.EnterGuard();

        if (playerMovement != null)
        {
            playerMovement.SetMovementSpeedOverride(guardMoveSpeed);
            originalFaceMoveDirection = playerMovement.ShouldFaceMoveDirection;
            playerMovement.SetShouldFaceMoveDirection(false);
            playerMovement.SuppressLocomotionAnimations(true);
        }

        animationController?.PlayGuardUp();

        if (switchToGuardCamera)
            ActivateGuardCamera();

        if (autoHardLockWhileGuarding && attackLockSystem != null && attackLockSystem.IsHardLockActive)
            attackLockSystem.EnsureHardLock(instantAlignOnEntry);
    }

    private void ExitGuard()
    {
        
        guardActive = false;
        guardDashActive = false;
        guardWalkAnimationActive = false;

        CombatManager.ExitGuard();

        if (playerMovement != null)
        {
            playerMovement.ClearMovementSpeedOverride();
            playerMovement.SetShouldFaceMoveDirection(originalFaceMoveDirection);
            playerMovement.SuppressLocomotionAnimations(false);
        }

        if (cameraForced && cameraManager != null)
        {
            cameraManager.SwitchToGameplay();
            cameraForced = false;
        }

        if (forcedHardLock && attackLockSystem != null)
        {
            attackLockSystem.ReleaseHardLock();
            forcedHardLock = false;
        }

        guardDashCooldownTimer = guardDashCooldown;
        animationController?.PlayIdle();
    }

    private void UpdateGuardLocomotion()
    {
        if (playerMovement != null)
        {
            if (playerMovement.ShouldFaceMoveDirection)
                playerMovement.SetShouldFaceMoveDirection(false);

            if (!playerMovement.IsLocomotionAnimationSuppressed)
                playerMovement.SuppressLocomotionAnimations(true);
        }

        if (playerMovement != null && !playerMovement.HasMovementSpeedOverride)
            playerMovement.SetMovementSpeedOverride(guardMoveSpeed);

        if (guardDashActive)
            return;

        Vector2 moveInput = InputReader.MoveInput;

        float moveAmount = Mathf.Clamp01(moveInput.magnitude);

        if (Time.time < guardRaiseLockUntilTime)
        {
            LogGuardAnimationDebug(moveInput, moveAmount, "GuardRaiseLock", guardWalkAnimationActive);
            return;
        }

        if (guardWalkAnimationActive)
            guardWalkAnimationActive = moveAmount > guardWalkExitThreshold;
        else
            guardWalkAnimationActive = moveAmount > guardWalkEnterThreshold;

        if (guardWalkAnimationActive)
            animationController?.PlayGuardWalk();
        else
            animationController?.PlayGuardIdle();

        LogGuardAnimationDebug(moveInput, moveAmount, guardWalkAnimationActive ? "GuardWalk" : "GuardIdle", guardWalkAnimationActive);
    }

    private void HandleGuardDashInput()
    {
        if (guardDashActive)
            return;

        if (guardDashCooldownTimer > 0f)
            return;

        if (!PlayerMovement.isGrounded)
            return;

        if (!InputReader.DashTriggered)
            return;

        if (playerMovement == null)
            return;

        Vector3 dashDirection = ResolveGuardDashDirection();
        if (dashDirection.sqrMagnitude < 0.001f)
            return;

        bool dashStarted = playerMovement.TryStartGuardDash(
            dashDirection,
            guardDashDistance,
            guardDashDuration,
            () =>
            {
                guardDashActive = true;
                PlayGuardDashAnimation(dashDirection);
            },
            () =>
            {
                guardDashActive = false;
                animationController?.PlayGuardIdle();
            });

        if (dashStarted)
            guardDashCooldownTimer = guardDashCooldown;
    }

    private void MaintainHardLock()
    {
        EnsureAttackLockReference();
        if (attackLockSystem != null && attackLockSystem.IsHardLockActive)
            attackLockSystem.AlignPlayerAndCamera(attackLockSystem.CurrentHardLockTarget, instantCameraAlign: false);
    }

    private void ActivateGuardCamera()
    {
        cameraManager ??= cameraOverride != null ? cameraOverride : CameraManager.Instance;
        if (cameraManager == null)
            return;

        if (cameraManager.CurrentState == CameraManager.CameraState.Guard)
            return;

        cameraManager.SwitchToGuard();
        cameraForced = true;
    }

    private bool EnsureHardLock(bool instant)
    {
        EnsureAttackLockReference();
        if (attackLockSystem == null)
            return false;

        bool hadLock = attackLockSystem.IsHardLockActive;
        attackLockSystem.EnsureHardLock(instant);
        return !hadLock;
    }

    private void AlignGuardRotation(Vector2 moveInput)
    {
        if (attackLockSystem != null && attackLockSystem.IsHardLockActive)
        {
            // While hard-locked, let AttackLockSystem own player facing to avoid
            // rotation fighting/jitter between systems.
            return;
        }

        Transform basis = cameraManager != null
            ? cameraManager.GetActiveCamera()?.transform
            : null;

        if (basis == null && Camera.main != null)
            basis = Camera.main.transform;

        if (basis == null)
            return;

        Vector3 cameraForward = Vector3.ProjectOnPlane(basis.forward, Vector3.up);
        if (cameraForward.sqrMagnitude < 0.0001f)
            return;

        RotateGuardRootTowards(cameraForward);
    }

    private void RotateGuardRootTowards(Vector3 worldDirection)
    {
        if (worldDirection.sqrMagnitude < 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(worldDirection.normalized, Vector3.up);
        Transform root = GuardRoot;
        root.rotation = Quaternion.RotateTowards(
            root.rotation,
            targetRotation,
            freeAimTurnSpeed * Time.deltaTime);
    }

    private Vector3 ResolveGuardDashDirection()
    {
        Transform basis = cameraManager != null ? cameraManager.GetActiveCamera()?.transform : null;
        if (basis == null && Camera.main != null)
            basis = Camera.main.transform;

        Transform root = GuardRoot;

        Vector3 right = basis != null
            ? Vector3.ProjectOnPlane(basis.right, Vector3.up).normalized
            : root.right;

        if (right.sqrMagnitude < 0.0001f)
            right = root.right;

        Vector2 moveInput = InputReader.MoveInput;
        float dashAxis = Mathf.Abs(moveInput.x) >= movementDeadZone ? Mathf.Sign(moveInput.x) : 0f;

        if (Mathf.Approximately(dashAxis, 0f))
        {
            dashAxis = Mathf.Approximately(lastDashSign, 0f) ? 1f : lastDashSign;
        }
        else
        {
            lastDashSign = dashAxis;
        }

        return right * dashAxis;
    }

    private void PlayGuardDashAnimation(Vector3 direction)
    {
        float rightDot = Vector3.Dot(GuardRoot.right, direction.normalized);
        if (rightDot < 0f)
            animationController?.PlayGuardDashLeft();
        else
            animationController?.PlayGuardDashRight();
    }

    private void EnsureAttackLockReference()
    {
        if (attackLockSystem != null)
            return;

#if UNITY_2022_3_OR_NEWER
        attackLockSystem = FindFirstObjectByType<AttackLockSystem>(FindObjectsInactive.Include);
#else
        attackLockSystem = FindObjectOfType<AttackLockSystem>();
#endif
    }

    private void LogGuardAnimationDebug(Vector2 moveInput, float moveAmount, string requestedState, bool walkActive)
    {
        if (!debugGuardAnimation || Time.time < nextGuardDebugLogTime)
            return;

        nextGuardDebugLogTime = Time.time + Mathf.Max(0.05f, debugGuardLogInterval);

        string currentClip = animationController != null
            ? animationController.GetCurrentClipName()
            : "<none>";

        string currentControllerState = animationController != null ? animationController.CurrentStateName : "<null>";
        bool isHardLocked = attackLockSystem != null && attackLockSystem.IsHardLockActive;
        string targetName = attackLockSystem != null && attackLockSystem.CurrentHardLockTarget != null
            ? attackLockSystem.CurrentHardLockTarget.name
            : "null";

        Debug.Log($"[Guard][AnimDebug] requested={requestedState} currentController={currentControllerState} currentClip={currentClip} move=({moveInput.x:0.00},{moveInput.y:0.00}) mag={moveAmount:0.00} walkActive={walkActive} enter={guardWalkEnterThreshold:0.00} exit={guardWalkExitThreshold:0.00} inputBusy={InputReader.inputBusy} hardLock={isHardLocked} target={targetName}");
    }

    private void LogGuardHardLockDiagnostics()
    {
        if (!debugGuardHardLockDiagnostics || !guardActive)
        {
            hasGuardDiagnosticSample = false;
            return;
        }

        EnsureAttackLockReference();
        if (attackLockSystem == null || !attackLockSystem.IsHardLockActive || attackLockSystem.CurrentHardLockTarget == null)
        {
            hasGuardDiagnosticSample = false;
            return;
        }

        if (Time.time < nextGuardHardLockDebugLogTime)
            return;

        nextGuardHardLockDebugLogTime = Time.time + Mathf.Max(0.05f, debugGuardHardLockLogInterval);

        Transform root = GuardRoot;
        Transform target = attackLockSystem.CurrentHardLockTarget;
        Vector3 rootPosition = root.position;
        Vector3 toTarget = target.position - rootPosition;
        toTarget.y = 0f;

        float currentYaw = root.eulerAngles.y;
        float desiredYaw = toTarget.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(toTarget.normalized, Vector3.up).eulerAngles.y
            : currentYaw;
        float yawError = Mathf.DeltaAngle(currentYaw, desiredYaw);

        float yawSpeed = 0f;
        float planarSpeed = 0f;
        float now = Time.time;

        if (hasGuardDiagnosticSample)
        {
            float dt = Mathf.Max(0.0001f, now - lastGuardDiagnosticSampleTime);
            yawSpeed = Mathf.DeltaAngle(lastGuardDiagnosticYaw, currentYaw) / dt;

            Vector3 planarDelta = rootPosition - lastGuardDiagnosticPosition;
            planarDelta.y = 0f;
            planarSpeed = planarDelta.magnitude / dt;
        }

        lastGuardDiagnosticYaw = currentYaw;
        lastGuardDiagnosticPosition = rootPosition;
        lastGuardDiagnosticSampleTime = now;
        hasGuardDiagnosticSample = true;

        Vector2 moveInput = InputReader.MoveInput;
        float moveAmount = Mathf.Clamp01(moveInput.magnitude);
        string currentClip = animationController != null ? animationController.GetCurrentClipName() : "<none>";
        string controllerState = animationController != null ? animationController.CurrentStateName : "<null>";

        Debug.Log($"[Guard][LockDiag] target={target.name} move=({moveInput.x:0.00},{moveInput.y:0.00}) mag={moveAmount:0.00} walkActive={guardWalkAnimationActive} inputBusy={InputReader.inputBusy} yaw={currentYaw:0.0} desiredYaw={desiredYaw:0.0} yawError={yawError:0.00} yawSpeed={yawSpeed:0.00} planarSpeed={planarSpeed:0.00} controller={controllerState} clip={currentClip}");
    }
}
