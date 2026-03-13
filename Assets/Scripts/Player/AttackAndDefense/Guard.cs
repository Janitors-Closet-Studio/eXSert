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
    [SerializeField] private bool autoHardLockWhileGuarding = true;
    [SerializeField] private bool instantAlignOnEntry = true;
    [SerializeField] private bool switchToGuardCamera = true;
    [SerializeField] private CameraManager cameraOverride;
    [SerializeField, Range(90f, 1440f)] private float freeAimTurnSpeed = 540f;

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
    private float guardDashCooldownTimer;
    private float lastDashSign = 1f;
    private Transform GuardRoot => playerMovement != null ? playerMovement.transform : transform;

    private void Awake()
    {
        animationController ??= GetComponent<PlayerAnimationController>();
        playerMovement ??= GetComponent<PlayerMovement>();
        attackManager ??= GetComponent<PlayerAttackManager>();
        attackLockSystem ??= GetComponent<AttackLockSystem>();
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
    }

    private void OnDisable()
    {
        if (guardActive)
            ExitGuard();
    }

    private void EnterGuard()
    {
        guardActive = true;
        SoundManager.Instance.sfxSource.PlayOneShot(guardUpSFX);

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

        if (autoHardLockWhileGuarding)
            forcedHardLock = EnsureHardLock(instantAlignOnEntry);
    }

    private void ExitGuard()
    {
        
        guardActive = false;
        guardDashActive = false;

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
        if (playerMovement != null && !playerMovement.HasMovementSpeedOverride)
            playerMovement.SetMovementSpeedOverride(guardMoveSpeed);

        if (guardDashActive)
            return;

        Vector2 moveInput = InputReader.MoveInput;
        AlignGuardRotation(moveInput);

        if (InputReader.inputBusy)
            return;

        float moveAmount = Mathf.Clamp01(moveInput.magnitude);
        animationController?.PlayGuard(moveAmount);
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
        attackLockSystem?.EnsureHardLock(instantCameraAlign: false);
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
            return;

        if (InputReader.inputBusy)
            return;

        if (moveInput.sqrMagnitude < movementDeadZone * movementDeadZone)
            return;

        Transform basis = cameraManager != null
            ? cameraManager.GetActiveCamera()?.transform
            : null;

        if (basis == null && Camera.main != null)
            basis = Camera.main.transform;

        if (basis == null)
            return;

        Vector3 camForward = Vector3.ProjectOnPlane(basis.forward, Vector3.up).normalized;
        Vector3 camRight = Vector3.ProjectOnPlane(basis.right, Vector3.up).normalized;

        if (camForward.sqrMagnitude < 0.0001f)
            camForward = transform.forward;

        if (camRight.sqrMagnitude < 0.0001f)
            camRight = transform.right;

        Vector3 desiredDirection = (camForward * moveInput.y + camRight * moveInput.x).normalized;
        if (desiredDirection.sqrMagnitude < 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(desiredDirection, Vector3.up);
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
}
