/*
Written by Brandon Wahl

Handles player movement and saves/loads player position
*
* edited by Will T
* 
* Added dash functionality and modified jump to include double jump
* Also added animator integration
*
*
* Edited by Kyle Woo
* Added aerial combat integration, including aerial attack hops and plunge attacks
* Included more complex animation system and intricate movement state management
* 
* Edited by William Hamaric
* Added functionality for external forces/velocities to be used for the vacuum/suction move for the Roomba fight
* (Currently) Lines 210-212, 895-900, 1256-1291
*/

using System;
using System.Collections;
using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.InputSystem;
using Utilities.Combat;
using Utilities.Combat.Attacks;

public class PlayerMovement : MonoBehaviour
{
    private CharacterController characterController;
    // Keep a weak static reference to the most-recent active PlayerMovement instance so
    // other systems that reference PlayerMovement.isGrounded continue to work without
    // holding a direct reference to a component instance (avoids leaking a destroyed
    // CharacterController via a static field).
    private static PlayerMovement s_instance;

    public static bool isGrounded
    {
        get
        {
            return s_instance != null && s_instance.characterController != null && s_instance.characterController.isGrounded;
        }
    }

    public static bool isDashingFlag {get; private set; }

    #region Inspector Setup
    [Header("Player Animator")]
    [SerializeField] private PlayerAnimationController animationController;
    [SerializeField] private PlayerAttackManager attackManager;

    public event Action DoubleJumpPerformed;
    public event Action DashPerformed;
    public event Action AirDashPerformed;

    [Header("Input")]
    [SerializeField] private InputActionReference _jumpAction;
    [SerializeField] private InputActionReference _dashAction;

    [Header("Input Filtering")]
    [SerializeField, Range(0f, 0.5f)]
    private float moveInputDeadZone = 0.08f;

    [Header("Player Movement Settings")]
    [SerializeField]
    private float walkSpeed = 2.25f;

    [SerializeField]
    private float jogSpeed = 3.75f;

    [SerializeField]
    private float sprintSpeed = 5.5f;

    [SerializeField, Range(0.2f, 1f)]
    private float joystickWalkThreshold = 0.8f;

    [SerializeField, Range(0.2f, 10f)]
    private float sprintDelaySeconds = 2f;

    [SerializeField, Range(0f, 20f)]
    private float friction = 6f;

    [SerializeField, Range(0f, 0.3f)]
    private float inputReleaseGrace = 0.08f;

    [Header("Landing Settings")]
    [SerializeField, Range(0f, 1f)]
    private float landingLockDuration = 0.35f;

    [Header("Keyboard Overrides")]
    [SerializeField, Range(0.1f, 0.5f)]
    private float keyboardDoubleTapWindow = 0.25f;

    [SerializeField]
    private Transform cameraTransform;

    [SerializeField]
    private Transform guardCameraTransform;

    [SerializeField]
    private bool shouldFaceMoveDirection = true;

    internal Vector3 currentMovement = Vector3.zero;

    [Header("Player Jump Settings")]
    [SerializeField]
    private float gravity = -9.81f;

    [Tooltip("How high the player will jump")]
    [SerializeField, Range(1f, 10f)]
    private float jumpForce;

    [SerializeField, Range(1f, 10f)]
    private float doubleJumpForce;

    [SerializeField, Range(0, 15)]
    private float airAttackHopForce = 5;

    [SerializeField, Range(1, 50)]
    private float terminalVelocity = 20;

    [SerializeField, Range(0f, 0.3f)]
    private float jumpEventTimeout = 0.12f;

    [SerializeField]
    private bool canDoubleJump;

    [Header("Aerial Combat Integration")]
    [SerializeField] private AerialComboManager aerialComboManager;
    [SerializeField, Tooltip("How long to freeze vertical velocity during aerial light attacks")]
    [Range(0.05f, 0.6f)] private float aerialLightAttackHangTime = 0.22f;
    [SerializeField, Tooltip("Initial hover duration before plunging downward")] 
    [Range(0f, 0.3f)] private float plungeHoverTime = 0.06f;
    [SerializeField, Tooltip("Downward speed applied during plunge phase")] 
    [Range(10f, 60f)] private float plungeDownSpeed = 32f;

    [Header("Aerial Combat Height Gate")]
    [SerializeField, Tooltip("Minimum distance above ground required to start aerial attacks/plunge. If 0, uses (basic jump height + extra).")]
    [Range(0f, 20f)] private float aerialCombatMinHeightAboveGroundOverride = 0f;
    [SerializeField, Tooltip("Extra height added on top of the computed basic jump height when no override is provided. Suggested: 1-2.")]
    [Range(0f, 5f)] private float aerialCombatHeightAboveBasicJump = 1.5f;
    [SerializeField, Tooltip("How far down we probe for ground when evaluating aerial height. If no ground is found, aerial combat is allowed.")]
    [Range(1f, 50f)] private float aerialCombatGroundProbeDistance = 12f;

    [Header("GroundCheck Variables")]
    [SerializeField]
    private Vector3 boxSize = new Vector3(.8f, .1f, .8f);

    [SerializeField]
    private float maxDistance;

    [Tooltip("Which layer the ground check detects for")]
    public LayerMask layerMask;

    [Header("Dash Settings")]
    [SerializeField]
    private float dashDistance = 6f;

    [SerializeField, Range(0.05f, 1f)]
    private float dashDuration = 0.25f;

    [SerializeField]
    private float dashCoolDown = 0.6f;

    [Header("External Reactions")]
    [SerializeField, Range(0.05f, 1.5f)]
    private float defaultExternalStunDuration = 0.35f;
    
    [Header("Knockback / Wall Collision")]
    [SerializeField, Tooltip("Enable wall collision detection and resolution during knockback")]
    private bool enableKnockbackWallCollision = true;
    [SerializeField, Tooltip("Layer mask for wall collision detection (set to Environment/Wall layers, NOT enemy layers)")]
    private LayerMask knockbackWallMask = 1; // Default to layer 0 only, user should configure
    [SerializeField, Tooltip("Radius for wall collision detection sphere")]
    private float knockbackCollisionRadius = 0.5f;
    [SerializeField, Tooltip("How quickly knockback velocity decays (0 = instant stop, 1 = no decay). 0.92 = ~0.5 second knockback, 0.97 = ~1.5 second knockback.")]
    private float knockbackDecayRate = 0.97f;
    [SerializeField, Tooltip("Minimum velocity to continue knockback (below this, knockback stops)")]
    private float knockbackMinVelocity = 0.5f;
    [SerializeField, Tooltip("Delay before wall collision checks begin (lets player clear attacker's hitbox and travel some distance). 0.25 = 250ms delay.")]
    private float knockbackWallCheckDelay = 0.25f;
    
    [Header("Wall Impact Damage (Optional)")]
    [SerializeField, Tooltip("Enable damage when player hits wall at high velocity during knockback")]
    private bool enableWallImpactDamage = false;
    [SerializeField, Tooltip("Minimum impact velocity to trigger wall damage")]
    private float wallDamageVelocityThreshold = 10f;
    [SerializeField, Tooltip("Damage per unit of impact velocity above threshold")]
    private float wallDamagePerVelocity = 0.5f;
    [SerializeField, Tooltip("Maximum wall impact damage")]
    private float wallDamageMax = 5f;

    [Header("Launcher Settings")]
    [SerializeField, Range(1f, 25f)]
    private float launcherJumpVelocity = 12f;

    [SerializeField, Range(0f, 10f)]
    private float launcherForwardVelocity = 2.5f;

    [Header("Camera Settings")]
    [SerializeField] bool invertYAxis = false;

    [Header("High Fall Settings")]
    [SerializeField, Range(0.5f, 30f)]
    private float highFallHeightThreshold = 6f;

    [SerializeField, Range(1f, 100f)]
    private float highFallGroundProbeDistance = 25f;
    #endregion

    private bool canDash = true;
    private bool isDashing;
    private Vector3 dashVelocity = Vector3.zero;
    private bool wasGrounded;
    private float jogToSprintTimer;
    private bool sprintChargeActive;
    private bool wasMoving;
    private float inputReleaseTimer;
    private Vector2 cachedMoveInput;
    private bool doubleJumpAvailable;
    private enum PendingJumpType { None, Ground, Double }
    private PendingJumpType pendingJump = PendingJumpType.None;
    private float pendingJumpTimer;
    private bool airborneAnimationLocked;
    private bool fallingAnimationPlaying;
    private float landingAnimationLockTimer;
    private float airborneStartHeight;
    private bool highFallActive;
    private bool airDashAvailable = true;
    private bool suspendGravityDuringDash;
    private bool airDashInProgress;
    private bool dashForceStop;
    private Coroutine dashRoutine;
    private bool dashInputLockOwned;
    private bool hasCombatIdleController;
    private PlayerCombatIdleController combatIdleController;
    private float aerialAttackLockTimer;
    private bool isPlunging;
    private float plungeTimer;
    private bool plungeLandingPending;
    private bool locomotionAnimationSuppressed;
    private bool movementSpeedOverrideActive;
    private float movementSpeedOverride;
    private Coroutine externalStunRoutine;
    private bool externalStunOwnsInput;
    private bool disabledByDeath;

    // External velocity injection (used by vacuum suction, knockback, etc.)
    private Vector3 externalVelocity = Vector3.zero;
    private bool externalVelocityActive;
    private Vector3 attackMoveVelocity = Vector3.zero;
    private bool attackMoveActive;
    private Coroutine attackMoveRoutine;
    private bool isKnockbackActive;
    private Vector3 knockbackVelocity;
    private float knockbackStartMagnitude;
    private float knockbackStartTime;

    private Transform ResolveCameraTransform()
    {
        Transform fallback = cameraTransform;
        bool guardActive = CombatManager.isGuarding;

        if (guardActive && guardCameraTransform != null)
            fallback = guardCameraTransform;

        if (fallback != null)
            return fallback;

        CinemachineCamera activeCamera = CameraManager.Instance != null
            ? CameraManager.Instance.GetActiveCamera()
            : null;

        if (activeCamera != null)
            return activeCamera.transform;

        return Camera.main != null ? Camera.main.transform : null;
    }

    private Vector3 forward
    {
        get
        {
            Transform cam = ResolveCameraTransform();
            if (cam == null)
                return Vector3.forward;

            Vector3 camForward = cam.forward;
            return new Vector3(camForward.x, 0f, camForward.z);
        }
    }

    private Vector3 right
    {
        get
        {
            Transform cam = ResolveCameraTransform();
            if (cam == null)
                return Vector3.right;

            Vector3 camRight = cam.right;
            return new Vector3(camRight.x, 0f, camRight.z);
        }
    }

    private enum GroundMoveState
    {
        Walk,
        Jog,
        Sprint
    }

    private bool keyboardJogOverride;
    private float tapUpTime = float.NegativeInfinity;
    private float tapDownTime = float.NegativeInfinity;
    private float tapLeftTime = float.NegativeInfinity;
    private float tapRightTime = float.NegativeInfinity;
    private Vector2 previousKeyboardInput = Vector2.zero;

    private GroundMoveState moveState = GroundMoveState.Walk;
    private bool keyboardWalkToggleActive;
    private float CurrentSpeed => moveState switch
    {
        GroundMoveState.Walk => walkSpeed,
        GroundMoveState.Jog => jogSpeed,
        GroundMoveState.Sprint => sprintSpeed,
        _ => walkSpeed
    };

    private void Awake()
    {
        attackManager = attackManager
            ?? GetComponent<PlayerAttackManager>()
            ?? GetComponentInChildren<PlayerAttackManager>()
            ?? GetComponentInParent<PlayerAttackManager>();

        combatIdleController = GetComponent<PlayerCombatIdleController>()
            ?? GetComponentInChildren<PlayerCombatIdleController>()
            ?? GetComponentInParent<PlayerCombatIdleController>()
            ?? FindFirstCombatIdleController();
        hasCombatIdleController = combatIdleController != null;
        // Cache a static reference to the active instance (cleared when destroyed)
        s_instance = this;
    }

    void Start()
    {
        characterController = GetComponent<CharacterController>();
        animationController = animationController
            ?? GetComponent<PlayerAnimationController>()
            ?? GetComponentInChildren<PlayerAnimationController>()
            ?? GetComponentInParent<PlayerAnimationController>();

        if (aerialComboManager == null)
        {
            aerialComboManager = GetComponent<AerialComboManager>()
                ?? GetComponentInParent<AerialComboManager>();

            if (aerialComboManager == null)
                Debug.LogWarning("PlayerMovement: AerialComboManager reference missing - aerial combos won't reset correctly.");
        }

        doubleJumpAvailable = canDoubleJump;
        //airborneStartHeight = transform.position.y;
        airDashAvailable = true;
        suspendGravityDuringDash = false;
    }
    private void OnEnable()
    {
        PlayerAttackManager.OnAttack += AerialAttackHop;
    }

    private void OnDisable()
    {
        PlayerAttackManager.OnAttack -= AerialAttackHop;

        // Stop any running coroutines we own to avoid keeping this MonoBehaviour alive
        if (dashRoutine != null)
        {
            try { StopCoroutine(dashRoutine); } catch { }
            dashRoutine = null;
        }

        if (externalStunRoutine != null)
        {
            try { StopCoroutine(externalStunRoutine); } catch { }
            externalStunRoutine = null;
        }

        // Ensure any input locks we held are released
        if (dashInputLockOwned)
        {
            InputReader.inputBusy = false;
            dashInputLockOwned = false;
        }

        if (externalStunOwnsInput)
        {
            ReleaseExternalStunInputLock();
        }

        // Clear static instance reference if it pointed to this instance
        if (s_instance == this)
            s_instance = null;
    }

    private void OnDestroy()
    {
        // Mirror OnDisable cleanup in case object is destroyed without disabling first
        PlayerAttackManager.OnAttack -= AerialAttackHop;

        if (dashRoutine != null)
        {
            try { StopCoroutine(dashRoutine); } catch { }
            dashRoutine = null;
        }

        if (externalStunRoutine != null)
        {
            try { StopCoroutine(externalStunRoutine); } catch { }
            externalStunRoutine = null;
        }

        if (dashInputLockOwned)
        {
            InputReader.inputBusy = false;
            dashInputLockOwned = false;
        }

        if (externalStunOwnsInput)
            ReleaseExternalStunInputLock();

        if (s_instance == this)
            s_instance = null;
    }

    private void Update()
    {
        if (InputReader.JumpTriggered)
            OnJump();

        if (InputReader.DashTriggered)
            OnDash();

        if (!isDashing && InputReader.ToggleWalkTriggered && IsKeyboardMouseControlSchemeActive())
        {
            // Walk toggle is keyboard-only: default is Jog, toggle enables Walk.
            // Sprinting still cancels walk-toggle when sprint begins, but the player can
            // also press ToggleWalk during sprint to immediately drop to Walk.
            keyboardWalkToggleActive = !keyboardWalkToggleActive;
            keyboardJogOverride = false;

            if (keyboardWalkToggleActive)
            {
                TrySetMoveState(GroundMoveState.Walk, force: true);
            }
            else
            {
                if (InputReader.MoveInput.sqrMagnitude > 0.01f)
                    TrySetMoveState(GroundMoveState.Jog, force: true);
            }
        }

        if (pendingJump != PendingJumpType.None && pendingJumpTimer > 0f)
        {
            pendingJumpTimer -= Time.deltaTime;
            if (pendingJumpTimer <= 0f)
                HandleAnimationJumpEvent();
        }
    }

    // Update is called once per frame
    public void FixedUpdate()
    {
        // Debug checks
        if (ResolveCameraTransform() == null)
        {
            Debug.LogError("Camera Transform is NULL! Assign your Cinemachine camera references in PlayerMovement.");
            return;
        }

        if (characterController == null)
        {
            Debug.LogError("Character Controller is NULL!");
            return;
        }

        landingAnimationLockTimer = Mathf.Max(0f, landingAnimationLockTimer - Time.deltaTime);

        Move();

        HandleAirborneAnimations();

        ApplyMovement();
    }

    private void Move()
    {
        if (InputReader.inputBusy || isDashing)
        {
            currentMovement.x = 0f;
            currentMovement.z = 0f;
            return;
        }

        if (isPlunging || plungeLandingPending)
        {
            currentMovement.x = 0f;
            currentMovement.z = 0f;
            return;
        }

        Vector2 inputMove = ApplyMoveDeadZone(InputReader.MoveInput);
        float inputMagnitude = inputMove.magnitude;
        bool hasMovementInput = inputMagnitude > moveInputDeadZone;

        Vector2 keyboardInput = ReadKeyboardDirection();
        bool keyboardMovementActive = ProcessKeyboardDoubleTap(keyboardInput);
        bool usingAnalogThresholds = !keyboardMovementActive && IsAnalogControlSchemeActive();

        if (!hasMovementInput)
        {
            inputReleaseTimer += Time.deltaTime;
            if (inputReleaseTimer < inputReleaseGrace && cachedMoveInput.sqrMagnitude > 0.0001f)
            {
                inputMove = cachedMoveInput;
                inputMagnitude = inputMove.magnitude;
                hasMovementInput = true;
            }
        }
        else
        {
            cachedMoveInput = inputMove;
            inputReleaseTimer = 0f;
        }
        bool previouslyMoving = wasMoving;

        if (hasMovementInput)
        {
            bool stateChanged = UpdateMoveState(usingAnalogThresholds, inputMagnitude);

            Vector3 moveDirection = (forward * inputMove.y + right * inputMove.x).normalized;
            float targetSpeed = movementSpeedOverrideActive ? movementSpeedOverride : CurrentSpeed;
            Vector3 desiredVelocity = moveDirection * targetSpeed;
            currentMovement.x = desiredVelocity.x;
            currentMovement.z = desiredVelocity.z;

            if (shouldFaceMoveDirection && moveDirection.sqrMagnitude > 0.001f)
            {
                Quaternion toRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, toRotation, Time.deltaTime * 10f);
            }

            if (!locomotionAnimationSuppressed && pendingJump == PendingJumpType.None && !previouslyMoving && !stateChanged)
            {
                PlayMovementAnimation();
            }

            landingAnimationLockTimer = 0f;
            wasMoving = true;
        }
        else
        {
            wasMoving = false;
            inputReleaseTimer = 0f;
            cachedMoveInput = Vector2.zero;
            ResetMoveState();
            currentMovement.x = Mathf.MoveTowards(currentMovement.x, 0f, friction * Time.deltaTime);
            currentMovement.z = Mathf.MoveTowards(currentMovement.z, 0f, friction * Time.deltaTime);

            if (!locomotionAnimationSuppressed && pendingJump == PendingJumpType.None && characterController.isGrounded && !airborneAnimationLocked && landingAnimationLockTimer <= 0f &&
                (previouslyMoving || (Mathf.Abs(currentMovement.x) < 0.01f && Mathf.Abs(currentMovement.z) < 0.01f)))
            {
                EnsureCombatIdleControllerReference();
                if (!hasCombatIdleController)
                    animationController?.PlayIdle();
            }
        }
    }

    private void EnsureCombatIdleControllerReference()
    {
        if (hasCombatIdleController && combatIdleController != null)
            return;

        combatIdleController = combatIdleController
            ?? GetComponent<PlayerCombatIdleController>()
            ?? GetComponentInChildren<PlayerCombatIdleController>()
            ?? GetComponentInParent<PlayerCombatIdleController>()
            ?? FindFirstCombatIdleController();

        hasCombatIdleController = combatIdleController != null;
    }

    private PlayerCombatIdleController FindFirstCombatIdleController()
    {
#if UNITY_2022_3_OR_NEWER
        return FindFirstObjectByType<PlayerCombatIdleController>(FindObjectsInactive.Include);
#else
        return FindObjectOfType<PlayerCombatIdleController>(true);
#endif
    }

    private void OnJump()
    {
        if (CombatManager.isGuarding)
            return;

        if (InputReader.inputBusy)
            attackManager?.ForceCancelCurrentAttack(resetCombo: false);

        if (characterController == null)
            return;

        if (pendingJump != PendingJumpType.None)
            return;

        // checks to see if the player can jump or double jump
        if (characterController.isGrounded)
        {
            airborneAnimationLocked = true;
            fallingAnimationPlaying = false;
            highFallActive = false;
            pendingJump = PendingJumpType.Ground;
            pendingJumpTimer = jumpEventTimeout;
            animationController?.PlayJump();
            if (animationController == null || jumpEventTimeout <= 0f)
                HandleAnimationJumpEvent();
        }
        else if (canDoubleJump && doubleJumpAvailable)
        {
            airborneAnimationLocked = true;
            fallingAnimationPlaying = false;
            highFallActive = true;
            pendingJump = PendingJumpType.Double;
            pendingJumpTimer = jumpEventTimeout;
            animationController?.PlayAirJumpStart();
            if (animationController == null || jumpEventTimeout <= 0f)
                HandleAnimationJumpEvent();
        }
    }

    public void HandleAnimationJumpEvent()
    {
        if (pendingJump == PendingJumpType.None)
            return;

        pendingJumpTimer = 0f;

        if (pendingJump == PendingJumpType.Ground)
        {
            if (!characterController.isGrounded)
            {
                pendingJump = PendingJumpType.None;
                return;
            }


            currentMovement.y = jumpForce;

            
            doubleJumpAvailable = canDoubleJump;
            pendingJump = PendingJumpType.None;
            return;
        }

        if (!canDoubleJump || !doubleJumpAvailable)
        {
            pendingJump = PendingJumpType.None;
            return;
        }

        currentMovement.y = MathF.Max(currentMovement.y, doubleJumpForce);
        doubleJumpAvailable = false;
        pendingJump = PendingJumpType.None;
        DoubleJumpPerformed?.Invoke();
    }

    private void OnDash()
    {
        if (!canDash)
            return;

        if (CombatManager.isGuarding)
            return;

        bool grounded = characterController.isGrounded;
        bool dashAllowed = grounded || airDashAvailable;
        if (!dashAllowed)
            return;

        DashPerformed?.Invoke();

        if (InputReader.inputBusy)
            attackManager?.ForceCancelCurrentAttack(resetCombo: false);

        CancelPlungeState();

        canDash = false;
        isDashingFlag = true;

        Vector3 dashDirection = (forward * InputReader.MoveInput.y) + (right * InputReader.MoveInput.x);
        if (dashDirection.sqrMagnitude < 0.01f)
        {
            dashDirection = transform.forward;
        }
        dashDirection.Normalize();

        if (!grounded)
        {
            airDashAvailable = false;
            aerialComboManager?.TryAirDash();
            attackManager?.TriggerAirDashAttack();
            AirDashPerformed?.Invoke();
        }

        if (dashDirection.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(dashDirection, Vector3.up);
        }

        bool isAirDash = !grounded;

        dashForceStop = false;
        if (dashRoutine != null)
            StopCoroutine(dashRoutine);

        dashRoutine = StartCoroutine(
            DashCoroutine(
                dashDirection,
                isAirDash,
                dashDistance,
                dashDuration,
                lockInput: true,
                respectCooldown: true,
                onStart: () =>
                {
                    if (characterController.isGrounded)
                        animationController?.PlayDash();
                    else
                        animationController?.PlayAirDash();
                },
                onComplete: () =>
                {
                    if (InputReader.MoveInput.sqrMagnitude > 0.1f)
                        TrySetMoveState(GroundMoveState.Sprint, force: true);
                    else
                        ResetMoveState();
                }));
    }

    private IEnumerator DashCoroutine(
        Vector3 direction,
        bool isAirDash,
        float distance,
        float duration,
        bool lockInput,
        bool respectCooldown,
        Action onStart,
        Action onComplete)
    {
        duration = Mathf.Max(0.01f, duration);
        direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;

        isDashing = true;
        airDashInProgress = isAirDash;
        dashVelocity = direction * (distance / duration);
        currentMovement.x = 0f;
        currentMovement.z = 0f;

        if (lockInput)
        {
            dashInputLockOwned = true;
            InputReader.inputBusy = true;
        }

        if (isAirDash)
        {
            suspendGravityDuringDash = true;
            currentMovement.y = 0f;
        }

        onStart?.Invoke();

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (dashForceStop)
                break;

            elapsed += Time.deltaTime;
            yield return null;
        }

        dashVelocity = Vector3.zero;
        isDashing = false;
        airDashInProgress = false;
        suspendGravityDuringDash = false;
        dashForceStop = false;
        dashRoutine = null;

        if (lockInput && dashInputLockOwned)
        {
            InputReader.inputBusy = false;
            dashInputLockOwned = false;
        }

        onComplete?.Invoke();

        if (respectCooldown)
        {
            yield return new WaitForSeconds(dashCoolDown);
            canDash = true;
            isDashingFlag = false;
        }
    }

    public bool TryStartGuardDash(
        Vector3 direction,
        float distance,
        float duration,
        Action onStart,
        Action onComplete)
    {
        if (isDashing || direction.sqrMagnitude < 0.0001f)
            return false;

        DashPerformed?.Invoke();

        StartCoroutine(
            DashCoroutine(
                direction,
                isAirDash: false,
                distance,
                duration,
                lockInput: true,
                respectCooldown: false,
                onStart,
                onComplete));

        return true;
    }

    private void ForceStopDashImmediate(bool relinquishInputLock = false)
    {
        if (!isDashing)
            return;

        dashForceStop = true;

        if (relinquishInputLock)
            dashInputLockOwned = false;
    }

    public bool TryTriggerLauncherJump()
    {
        if (!isDashing || characterController == null || !characterController.isGrounded)
            return false;

        ForceStopDashImmediate(relinquishInputLock: true);

        Vector3 forwardDir = transform.forward;
        currentMovement.x = forwardDir.x * launcherForwardVelocity;
        currentMovement.z = forwardDir.z * launcherForwardVelocity;
        currentMovement.y = launcherJumpVelocity;

        dashVelocity = Vector3.zero;
        isDashing = false;

        suspendGravityDuringDash = false;
        airDashInProgress = false;
        airborneAnimationLocked = true;
        fallingAnimationPlaying = false;

        return true;
    }

    public void ApplyExternalStun(float duration)
    {
        if (duration <= 0f)
            duration = defaultExternalStunDuration;

        if (externalStunRoutine != null)
        {
            StopCoroutine(externalStunRoutine);
            externalStunRoutine = null;
            ReleaseExternalStunInputLock();
        }

        externalStunRoutine = StartCoroutine(ExternalStunRoutine(duration));
    }

    private IEnumerator ExternalStunRoutine(float duration)
    {
        ForceStopDashImmediate(relinquishInputLock: true);
        isDashing = false;
        dashVelocity = Vector3.zero;
        currentMovement.x = 0f;
        currentMovement.z = 0f;

        bool alreadyBusy = InputReader.inputBusy;
        if (!alreadyBusy)
        {
            InputReader.inputBusy = true;
            externalStunOwnsInput = true;
        }
        else
        {
            externalStunOwnsInput = false;
        }

        float timer = duration;
        while (timer > 0f)
        {
            timer -= Time.deltaTime;
            yield return null;
        }

        ReleaseExternalStunInputLock();
        externalStunRoutine = null;
    }

    private void ReleaseExternalStunInputLock()
    {
        if (!externalStunOwnsInput)
            return;

        InputReader.inputBusy = false;
        externalStunOwnsInput = false;
    }

    public void EnterDeathState()
    {
        ForceStopDashImmediate(relinquishInputLock: true);

        if (externalStunRoutine != null)
        {
            StopCoroutine(externalStunRoutine);
            externalStunRoutine = null;
        }

        ReleaseExternalStunInputLock();

        currentMovement = Vector3.zero;
        dashVelocity = Vector3.zero;
        isDashing = false;

        if (enabled)
        {
            disabledByDeath = true;
            enabled = false;
        }
        else
        {
            disabledByDeath = false;
        }
    }

    public void ExitDeathState()
    {
        if (!disabledByDeath)
            return;

        disabledByDeath = false;
        enabled = true;
    }

    private void AerialAttackHop(PlayerAttack attack)
    {
        if (attack == null || characterController.isGrounded)
            return;

        if (attack.attackType == AttackType.LightAerial)
        {
            float lockDuration = aerialLightAttackHangTime > 0f
                ? aerialLightAttackHangTime
                : Mathf.Clamp(airAttackHopForce * 0.02f, 0.05f, 0.6f);
            aerialAttackLockTimer = lockDuration;
            currentMovement.y = 0f;
            airborneAnimationLocked = true;
            fallingAnimationPlaying = false;
        }
        else if (attack.attackType == AttackType.HeavyAerial)
        {
            StartPlunge();
        }
    }

    public void StartPlunge()
    {
        if (characterController == null)
            return;
        if (isPlunging)
            return;

        isPlunging = true;
        plungeTimer = plungeHoverTime;
        aerialAttackLockTimer = 0f;
        suspendGravityDuringDash = false;
        currentMovement.y = Mathf.Min(0f, currentMovement.y);
        currentMovement.x *= 0.5f;
        currentMovement.z *= 0.5f;
        plungeLandingPending = false;
    }

    public void CancelPlungeState()
    {
        isPlunging = false;
        plungeLandingPending = false;
        plungeTimer = 0f;
        aerialAttackLockTimer = 0f;
    }

    private void ApplyMovement()
    {
        if (isPlunging)
        {
            if (plungeTimer > 0f)
            {
                plungeTimer -= Time.deltaTime;
                currentMovement.y = 0f;
            }
            else
            {
                currentMovement.y = -plungeDownSpeed;
            }
        }
        else if (aerialAttackLockTimer > 0f)
        {
            aerialAttackLockTimer -= Time.deltaTime;
            currentMovement.y = 0f;
        }
        else if (!suspendGravityDuringDash)
        {
            currentMovement.y += gravity * Time.deltaTime;
        }
        currentMovement.y = Mathf.Clamp(currentMovement.y, -terminalVelocity, terminalVelocity);

        Vector3 horizontalMovement = isDashing ? dashVelocity : new Vector3(currentMovement.x, 0, currentMovement.z);
        
        // Add external velocity injection (vacuum suction, knockback, etc.)
        if (externalVelocityActive)
        {
            horizontalMovement += new Vector3(externalVelocity.x, 0, externalVelocity.z);
        }

        // Add attack forward-move velocity (smooth lunge)
        if (attackMoveActive)
        {
            horizontalMovement += new Vector3(attackMoveVelocity.x, 0, attackMoveVelocity.z);
        }
        
        // Handle knockback with wall collision
        if (isKnockbackActive)
        {
            horizontalMovement = HandleKnockbackWithWallCollision();
        }
        
        Vector3 finalVelocity = horizontalMovement + Vector3.up * currentMovement.y;

        // move the character controller
        characterController.Move(finalVelocity * Time.deltaTime);

        // reset vertical movement when grounded
        if (characterController.isGrounded && currentMovement.y < 0)
        {
            currentMovement.y = -1f; // small negative value to keep the player grounded
            if (isPlunging)
                plungeLandingPending = true;
            isPlunging = false;
            plungeTimer = 0f;
            aerialAttackLockTimer = 0f;
        }
    }

    private void HandleAirborneAnimations()
    {
        bool grounded = characterController.isGrounded;

        if (pendingJump != PendingJumpType.None)
        {
            wasGrounded = grounded;
            return;
        }

        if (!wasGrounded && grounded)
        {
            bool landedFromPlunge = plungeLandingPending || isPlunging;
            plungeLandingPending = false;

            if (landedFromPlunge)
            {
                // Plunge handles its own animation; keep playing until cancel window fires.
            }
            else
            {
                animationController?.PlayLand();
            }
            ResetMoveState();
            doubleJumpAvailable = canDoubleJump;
            airborneAnimationLocked = false;
            fallingAnimationPlaying = false;
            highFallActive = false;
            airDashAvailable = true;
            suspendGravityDuringDash = false;
            isPlunging = false;
            plungeTimer = 0f;
            aerialAttackLockTimer = 0f;
            aerialComboManager?.OnLanded();

            bool movementBuffered = InputReader.MoveInput.sqrMagnitude > moveInputDeadZone * moveInputDeadZone;
            landingAnimationLockTimer = landedFromPlunge || movementBuffered ? 0f : landingLockDuration;

            if (movementBuffered && !landedFromPlunge && !locomotionAnimationSuppressed && !airborneAnimationLocked)
            {
                // Force locomotion to cancel lingering fall/land clips when input is held.
                wasMoving = false;
                PlayMovementAnimation();
            }
        }
        else if (!grounded)
        {
            if (wasGrounded)
            {
                airborneAnimationLocked = true;
                fallingAnimationPlaying = false;
                airborneStartHeight = transform.position.y;
                highFallActive = false;
                airDashAvailable = true;
            }

            bool aerialAttackAnimationActive = aerialAttackLockTimer > 0f
                || isPlunging
                || (aerialComboManager != null && aerialComboManager.IsInAerialCombo && InputReader.inputBusy);

            if (aerialAttackAnimationActive)
            {
                // Keep aerial attack clips in control so their events (hitboxes, cancel windows) still fire.
                fallingAnimationPlaying = false;
            }
            else if (currentMovement.y <= 0f && !fallingAnimationPlaying && !airDashInProgress)
            {
                if (ShouldUseHighFallAnimation())
                    animationController?.PlayFallingHigh();
                else
                    animationController?.PlayFalling();

                fallingAnimationPlaying = true;
            }
        }

        wasGrounded = grounded;
    }

    private void ResetMoveState()
    {
        jogToSprintTimer = 0f;
        keyboardJogOverride = false;

        if (IsKeyboardMouseControlSchemeActive())
        {
            if (keyboardWalkToggleActive)
            {
                sprintChargeActive = false;
                moveState = GroundMoveState.Walk;
            }
            else
            {
                sprintChargeActive = true;
                moveState = GroundMoveState.Jog;
            }
        }
        else
        {
            sprintChargeActive = false;
            moveState = GroundMoveState.Walk;
        }
    }

    private bool TrySetMoveState(GroundMoveState state, bool force = false)
    {
        if (!force && moveState == state)
            return false;

        moveState = state;

        switch (state)
        {
            case GroundMoveState.Walk:
                jogToSprintTimer = 0f;
                sprintChargeActive = false;
                break;
            case GroundMoveState.Jog:
                jogToSprintTimer = 0f;
                sprintChargeActive = true;
                break;
            case GroundMoveState.Sprint:
                sprintChargeActive = false;
                if (keyboardWalkToggleActive && IsKeyboardMouseControlSchemeActive())
                    keyboardWalkToggleActive = false;
                break;
        }

        if (!locomotionAnimationSuppressed)
            PlayMovementAnimation();
        return true;
    }

    private bool IsAnalogControlSchemeActive()
    {
        if (InputReader.Instance == null)
            return false;

        string scheme = InputReader.activeControlScheme;
        if (string.IsNullOrEmpty(scheme))
            return false;

        scheme = scheme.ToLowerInvariant();
        return scheme.Contains("gamepad") || scheme.Contains("controller");
    }

    public bool CanStartAerialCombat()
    {
        // Must be in the air; grounded attacks are handled separately.
        if (characterController != null && characterController.isGrounded)
            return false;

        float gravityMagnitude = Mathf.Max(0.01f, Mathf.Abs(gravity));
        float basicJumpHeight = (jumpForce * jumpForce) / (2f * gravityMagnitude);
        float minHeight = aerialCombatMinHeightAboveGroundOverride > 0f
            ? aerialCombatMinHeightAboveGroundOverride
            : (basicJumpHeight + aerialCombatHeightAboveBasicJump);

        float probeDistance = Mathf.Max(0.1f, aerialCombatGroundProbeDistance);

        Vector3 origin = transform.position;
        if (characterController != null)
        {
            Bounds b = characterController.bounds;
            origin = new Vector3(b.center.x, b.min.y + 0.05f, b.center.z);
        }

        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, probeDistance, layerMask, QueryTriggerInteraction.Ignore))
            return true;

        return hit.distance >= minHeight;
    }

    private bool IsKeyboardMouseControlSchemeActive()
    {
        if (InputReader.Instance == null)
            return false;

        string scheme = InputReader.activeControlScheme;
        if (string.IsNullOrEmpty(scheme))
            return false;

        scheme = scheme.ToLowerInvariant();
        return scheme.Contains("keyboard");
    }

    private bool UpdateMoveState(bool useAnalogThresholds, float inputMagnitude)
    {
        bool stateChanged = false;

        if (useAnalogThresholds)
        {
            if (inputMagnitude < joystickWalkThreshold)
            {
                stateChanged |= TrySetMoveState(GroundMoveState.Walk);
            }
            else if (moveState == GroundMoveState.Walk)
            {
                stateChanged |= TrySetMoveState(GroundMoveState.Jog);
            }
        }
        else if (IsKeyboardMouseControlSchemeActive())
        {
            if (keyboardWalkToggleActive)
            {
                keyboardJogOverride = false;
                if (moveState != GroundMoveState.Walk)
                    stateChanged |= TrySetMoveState(GroundMoveState.Walk, force: true);
                return stateChanged;
            }

            // Keyboard default: start at Jog (Walk only when toggled)
            if (moveState == GroundMoveState.Walk)
            {
                stateChanged |= TrySetMoveState(GroundMoveState.Jog, force: true);
            }
        }

        if (moveState == GroundMoveState.Jog && sprintChargeActive)
        {
            jogToSprintTimer += Time.deltaTime;
            if (jogToSprintTimer >= sprintDelaySeconds && inputMagnitude > moveInputDeadZone)
            {
                stateChanged |= TrySetMoveState(GroundMoveState.Sprint);
            }
        }

        return stateChanged;
    }

    private Vector2 ApplyMoveDeadZone(Vector2 rawInput)
    {
        if (Mathf.Abs(rawInput.x) < moveInputDeadZone)
            rawInput.x = 0f;

        if (Mathf.Abs(rawInput.y) < moveInputDeadZone)
            rawInput.y = 0f;

        return rawInput;
    }

    private Vector2 ReadKeyboardDirection()
    {
        if (Keyboard.current == null)
            return Vector2.zero;

        float x = 0f;
        float y = 0f;

        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
            x -= 1f;

        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
            x += 1f;

        if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
            y += 1f;

        if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
            y -= 1f;

        return new Vector2(x, y);
    }

    private bool ProcessKeyboardDoubleTap(Vector2 keyboardInput)
    {
        bool keyboardActive = keyboardInput.sqrMagnitude > 0f;

        if (!keyboardActive)
        {
            keyboardJogOverride = false;
            previousKeyboardInput = Vector2.zero;
            return false;
        }

        float now = Time.time;

        bool upPressed = previousKeyboardInput.y <= 0f && keyboardInput.y > 0f;
        bool downPressed = previousKeyboardInput.y >= 0f && keyboardInput.y < 0f;
        bool rightPressed = previousKeyboardInput.x <= 0f && keyboardInput.x > 0f;
        bool leftPressed = previousKeyboardInput.x >= 0f && keyboardInput.x < 0f;

        EvaluateKeyboardTap(ref tapUpTime, upPressed, now);
        EvaluateKeyboardTap(ref tapDownTime, downPressed, now);
        EvaluateKeyboardTap(ref tapRightTime, rightPressed, now);
        EvaluateKeyboardTap(ref tapLeftTime, leftPressed, now);

        previousKeyboardInput = keyboardInput;
        return true;
    }

    private void EvaluateKeyboardTap(ref float lastTapTime, bool pressedThisFrame, float now)
    {
        if (!pressedThisFrame)
            return;

        if (!keyboardWalkToggleActive && now - lastTapTime <= keyboardDoubleTapWindow)
        {
            ActivateKeyboardJogOverride();
        }

        lastTapTime = now;
    }

    private void ActivateKeyboardJogOverride()
    {
        keyboardJogOverride = true;
        TrySetMoveState(GroundMoveState.Jog, force: true);
    }

    public void SuppressLocomotionAnimations(bool suppress)
    {
        locomotionAnimationSuppressed = suppress;
        if (suppress)
            wasMoving = false;
    }

    public bool IsLocomotionAnimationSuppressed => locomotionAnimationSuppressed;

    public bool HasEffectiveMovementInput
    {
        get
        {
            Vector2 inputMove = ApplyMoveDeadZone(InputReader.MoveInput);
            if (inputMove.sqrMagnitude > moveInputDeadZone * moveInputDeadZone)
                return true;

            return inputReleaseTimer < inputReleaseGrace && cachedMoveInput.sqrMagnitude > 0.0001f;
        }
    }

    public void ForceLocomotionRefresh()
    {
        wasMoving = false;
    }

    public bool IsJumpPending => pendingJump != PendingJumpType.None;

    public void SetMovementSpeedOverride(float speed)
    {
        movementSpeedOverride = Mathf.Max(0f, speed);
        movementSpeedOverrideActive = true;
    }

    public void ClearMovementSpeedOverride()
    {
        movementSpeedOverrideActive = false;
    }

    public bool HasMovementSpeedOverride => movementSpeedOverrideActive;

    public bool ShouldFaceMoveDirection => shouldFaceMoveDirection;

    public void SetShouldFaceMoveDirection(bool shouldFace)
    {
        shouldFaceMoveDirection = shouldFace;
    }

    public bool IsDashing => isDashing;

    public bool CanTriggerLauncherFromDash => isDashing && characterController != null && characterController.isGrounded;

    public bool TrySnapToSoftLock(Vector3 worldPosition, Quaternion desiredRotation)
    {
        Transform root = transform;

        if (characterController == null)
        {
            root.SetPositionAndRotation(worldPosition, desiredRotation);
        }
        else
        {
            bool wasEnabled = characterController.enabled;
            characterController.enabled = false;
            root.SetPositionAndRotation(worldPosition, desiredRotation);
            characterController.enabled = wasEnabled;
            currentMovement.y = Mathf.Min(currentMovement.y, 0f);
        }

        // Clear any residual movement so FixedUpdate/ApplyMovement won't move the player back
        currentMovement.x = 0f;
        currentMovement.z = 0f;

        // Stop dash state
        dashVelocity = Vector3.zero;
        isDashing = false;
        dashForceStop = false;
        suspendGravityDuringDash = false;
        airDashInProgress = false;
        if (dashRoutine != null)
        {
            try { StopCoroutine(dashRoutine); } catch { }
            dashRoutine = null;
        }
        if (dashInputLockOwned)
        {
            InputReader.inputBusy = false;
            dashInputLockOwned = false;
        }

        // Clear external velocity injection
        externalVelocity = Vector3.zero;
        externalVelocityActive = false;

        // Stop attack forward-move
        attackMoveVelocity = Vector3.zero;
        attackMoveActive = false;
        if (attackMoveRoutine != null)
        {
            try { StopCoroutine(attackMoveRoutine); } catch { }
            attackMoveRoutine = null;
        }

        // Clear knockback state
        isKnockbackActive = false;
        knockbackVelocity = Vector3.zero;

        // Clear pending jump
        pendingJump = PendingJumpType.None;
        pendingJumpTimer = 0f;

        // Release external stun input lock if we own it
        if (externalStunOwnsInput)
            ReleaseExternalStunInputLock();

        return true;
    }

    private bool ShouldUseHighFallAnimation()
    {
        if (highFallActive)
            return true;

        float fallDistance = airborneStartHeight - transform.position.y;
        if (fallDistance >= highFallHeightThreshold)
        {
            highFallActive = true;
            return true;
        }

        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit,
                highFallGroundProbeDistance, layerMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.distance >= highFallHeightThreshold)
            {
                highFallActive = true;
                return true;
            }
        }

        return false;
    }

    private void PlayMovementAnimation()
    {
        if (locomotionAnimationSuppressed)
            return;

        if (animationController == null)
            return;

        if (characterController != null && (!characterController.isGrounded || airborneAnimationLocked))
            return;

        if (landingAnimationLockTimer > 0f && InputReader.MoveInput.sqrMagnitude < moveInputDeadZone * moveInputDeadZone)
            return;

        switch (moveState)
        {
            case GroundMoveState.Walk:
                animationController.PlayWalk();
                break;
            case GroundMoveState.Jog:
                animationController.PlayJog();
                break;
            case GroundMoveState.Sprint:
                animationController.PlaySprint();
                break;
        }
    }

    #region External Velocity Injection (Vacuum, Knockback, etc.)
    
    /// <summary>
    /// Sets an external velocity that will be applied to the player each frame.
    /// Used by systems like vacuum suction that need to move the player externally.
    /// Call ClearExternalVelocity() when done.
    /// </summary>
    public void SetExternalVelocity(Vector3 velocity)
    {
#if UNITY_EDITOR
        // Only log when first activated (not every frame)
        if (!externalVelocityActive && velocity.sqrMagnitude > 0.1f)
        {
            Debug.Log($"[PlayerMovement] SetExternalVelocity STARTED: {velocity}, magnitude={velocity.magnitude:F2}");
        }
#endif
        
        externalVelocity = velocity;
        externalVelocityActive = true;
    }
    
    /// <summary>
    /// Applies a knockback impulse to the player with wall collision handling.
    /// Unlike SetExternalVelocity, this decays over time and handles wall impacts.
    /// The Y component is applied immediately as vertical velocity for launch effect.
    /// </summary>
    public void ApplyKnockback(Vector3 impulse)
    {
        knockbackVelocity = impulse;
        knockbackStartMagnitude = impulse.magnitude;
        knockbackStartTime = Time.time;
        isKnockbackActive = true;
        
        // Apply Y component as immediate vertical velocity for launch effect
        // This makes the knockback launch the player upward, not just push horizontally
        if (impulse.y > 0f)
        {
            currentMovement.y = impulse.y;
        }
        
        // CRITICAL FIX: Apply an immediate position offset to "punch" through any blocking colliders
        // This prevents the CharacterController from getting stuck on the boss's body
        Vector3 immediateOffset = new Vector3(impulse.x, 0f, impulse.z).normalized * 0.5f;
        if (characterController != null && immediateOffset.sqrMagnitude > 0.001f)
        {
            // Temporarily disable CharacterController to allow direct position manipulation
            bool wasEnabled = characterController.enabled;
            characterController.enabled = false;
            transform.position += immediateOffset;
            characterController.enabled = wasEnabled;
        }
        
#if UNITY_EDITOR
        Debug.Log($"[PlayerMovement] ApplyKnockback: {impulse}, magnitude={knockbackStartMagnitude:F2}, applied Y velocity={impulse.y:F2}, immediate offset={immediateOffset}");
#endif
    }
    
    /// <summary>
    /// Handles knockback movement with wall collision detection and optional damage.
    /// </summary>
    private Vector3 HandleKnockbackWithWallCollision()
    {
        // Check minimum velocity threshold using horizontal components only (Y is for visual arc, not travel)
        float horizontalSqrMag = knockbackVelocity.x * knockbackVelocity.x + knockbackVelocity.z * knockbackVelocity.z;
        
        if (!isKnockbackActive || horizontalSqrMag < knockbackMinVelocity * knockbackMinVelocity)
        {
            // Knockback finished
#if UNITY_EDITOR
            if (isKnockbackActive)
            {
                Debug.Log($"[PlayerMovement] Knockback finished - horizontal velocity {Mathf.Sqrt(horizontalSqrMag):F2} below threshold {knockbackMinVelocity:F2}");
            }
#endif
            isKnockbackActive = false;
            knockbackVelocity = Vector3.zero;
            return Vector3.zero;
        }
        
        // Get horizontal knockback direction for wall checks (ignore Y)
        Vector3 horizontalKnockback = new Vector3(knockbackVelocity.x, 0, knockbackVelocity.z);
        Vector3 movement = horizontalKnockback * Time.deltaTime;
        
        // Check for wall collision if enabled AND after the delay period
        // The delay prevents immediately hitting the attacker's collider
        float timeSinceKnockback = Time.time - knockbackStartTime;
        if (enableKnockbackWallCollision && timeSinceKnockback >= knockbackWallCheckDelay)
        {
            // Check horizontally only (at character center height) to avoid hitting ground
            Vector3 checkPos = transform.position + Vector3.up * (characterController.height * 0.5f);
            Vector3 moveDir = horizontalKnockback.normalized;
            float checkDist = movement.magnitude + knockbackCollisionRadius;
            
            if (moveDir.sqrMagnitude > 0.001f && Physics.SphereCast(checkPos, knockbackCollisionRadius, moveDir, out RaycastHit hit, checkDist, knockbackWallMask, QueryTriggerInteraction.Ignore))
            {
                // Wall collision detected
                float impactVelocity = horizontalKnockback.magnitude;
                
#if UNITY_EDITOR
                Debug.Log($"[PlayerMovement] Knockback wall collision! Impact velocity: {impactVelocity:F2}, hit: {hit.collider.name} (layer: {hit.collider.gameObject.layer})");
#endif
                
                // Apply wall damage if enabled and above threshold
                if (enableWallImpactDamage && impactVelocity >= wallDamageVelocityThreshold)
                {
                    float damageAmount = (impactVelocity - wallDamageVelocityThreshold) * wallDamagePerVelocity;
                    damageAmount = Mathf.Min(damageAmount, wallDamageMax);
                    
                    // Apply damage to player
                    var healthSystem = GetComponent<IHealthSystem>();
                    if (healthSystem != null && damageAmount > 0)
                    {
                        healthSystem.LoseHP(damageAmount);
                        Debug.Log($"[PlayerMovement] Wall impact damage: {damageAmount:F1}");
                    }
                }
                
                // Stop knockback and push player away from wall
                isKnockbackActive = false;
                knockbackVelocity = Vector3.zero;
                
                // Calculate push-out direction (away from wall)
                Vector3 pushOut = hit.normal * 0.1f;
                characterController.Move(pushOut);
                
                return Vector3.zero;
            }
        }
        
        // Store current velocity before decay for this frame's movement
        Vector3 thisFrameMovement = horizontalKnockback;
        
        // Apply knockback decay (TIME-BASED, not frame-based)
        // This makes decay rate frame-rate independent
        float decayThisFrame = Mathf.Pow(knockbackDecayRate, Time.deltaTime * 60f);
        knockbackVelocity *= decayThisFrame;
        
#if UNITY_EDITOR
        // Log every 10 frames to track knockback progress
        if (Time.frameCount % 10 == 0)
        {
            Debug.Log($"[PlayerMovement] Knockback active: horizontal vel={thisFrameMovement.magnitude:F2}, after decay={Mathf.Sqrt(knockbackVelocity.x*knockbackVelocity.x + knockbackVelocity.z*knockbackVelocity.z):F2}");
        }
#endif
        
        return thisFrameMovement;
    }

    /// <summary>
    /// Clears any active external velocity injection.
    /// </summary>
    public void ClearExternalVelocity()
    {
#if UNITY_EDITOR
        if (externalVelocityActive)
        {
            Debug.Log("[PlayerMovement] ClearExternalVelocity called - external velocity stopped");
        }
#endif
        externalVelocity = Vector3.zero;
        externalVelocityActive = false;
    }

    /// <summary>
    /// Smoothly moves the player forward over a duration without vertical displacement.
    /// This uses a velocity blend so movement remains grounded and non-teleporting.
    /// </summary>
    public void StartAttackForwardMove(Vector3 forwardDirection, float distance, float duration)
    {
        if (distance <= 0f)
            return;

        Vector3 planarForward = new Vector3(forwardDirection.x, 0f, forwardDirection.z);
        if (planarForward.sqrMagnitude <= 0.0001f)
            return;

        planarForward.Normalize();

        if (attackMoveRoutine != null)
            StopCoroutine(attackMoveRoutine);

        attackMoveRoutine = StartCoroutine(AttackForwardMoveRoutine(planarForward, distance, duration));
    }

    private IEnumerator AttackForwardMoveRoutine(Vector3 forwardDirection, float distance, float duration)
    {
        duration = Mathf.Max(0.02f, duration);

        attackMoveActive = true;
        float elapsed = 0f;
        float moved = 0f;

        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t); // smoothstep
            float targetDistance = distance * eased;
            float delta = targetDistance - moved;

            if (Time.deltaTime > 0f)
                attackMoveVelocity = forwardDirection * (delta / Time.deltaTime);

            moved = targetDistance;
            elapsed += Time.deltaTime;
            yield return null;
        }

        attackMoveVelocity = Vector3.zero;
        attackMoveActive = false;
        attackMoveRoutine = null;
    }

    /// <summary>
    /// Returns true if an external velocity is currently being applied.
    /// </summary>
    public bool HasExternalVelocity => externalVelocityActive;

    /// <summary>
    /// Gets the CharacterController for direct external manipulation.
    /// Use with caution - prefer SetExternalVelocity for most use cases.
    /// </summary>
    public CharacterController GetCharacterController() => characterController;

    #endregion
}
