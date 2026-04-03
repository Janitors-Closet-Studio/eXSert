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
using UnityEngine.AI;
using Unity.Cinemachine;
using UnityEngine.InputSystem;
using Utilities.Combat;
using Utilities.Combat.Attacks;
#pragma warning disable CS0414

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
            return s_instance != null && s_instance.IsGroundedNow();
        }
#pragma warning restore CS0414
    }

    public static bool isDashingFlag {get; private set; }
    public static bool IsTestingOrDebugMode => s_instance != null && s_instance.testingOrDebugMode;

    #region Inspector Setup
    [Header("Testing / Debug")]
    [SerializeField, Tooltip("Enable when using isolated test scenes (no checkpoints/lifebox setup). Prevents systems from enforcing normal gameplay fail conditions that block movement testing.")]
    private bool testingOrDebugMode = false;
    [SerializeField, Tooltip("Verbose movement diagnostics for jump/grounded/animation state transitions. Use for reproducing movement bugs in test scenes.")]
    private bool verboseMovementDebugLogs = false;

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

    [SerializeField, Tooltip("When enabled, movement defaults straight to Jog instead of starting in Walk.")]
    private bool startInJog = true;

    [SerializeField, Tooltip("If Start In Jog is enabled, allows light analog tilt to still use Walk via Joystick Walk Threshold.")]
    private bool allowAnalogWalkWhileStartInJog = false;

    [SerializeField, Range(0.2f, 10f)]
    private float sprintDelaySeconds = 2f;

    [SerializeField, Range(0.1f, 40f), Tooltip("How quickly movement speed ramps up when transitioning into Sprint (units/sec). Lower = smoother/longer blend.")]
    private float sprintSpeedRampRate = 10f;

    [SerializeField, Range(0f, 20f)]
    private float friction = 6f;

    [SerializeField, Range(0f, 0.3f)]
    private float inputReleaseGrace = 0.08f;

    [Header("Attack Movement")]
    [SerializeField, Tooltip("When enabled, movement input can continue while attacks are active.")]
    private bool allowMovementWhileAttacking = true;
    [SerializeField, Tooltip("When enabled, landing configured attack types will disable attack-movement for the rest of that active attack window.")]
    private bool disableAttackMovementAfterLandedHit = true;
    [SerializeField, Tooltip("If enabled, single-target grounded attacks (LightSingle/HeavySingle) disable attack-movement after a confirmed hit.")]
    private bool disableAttackMovementAfterSingleTargetHit = true;
    [SerializeField, Tooltip("If enabled, aerial attacks (LightAerial/HeavyAerial) disable attack-movement after a confirmed hit.")]
    private bool disableAttackMovementAfterAerialHit = false;
    [SerializeField, Tooltip("If enabled, AoE attacks remain movable even after hit-confirm events.")]
    private bool keepAttackMovementForAoeAttacks = true;
    [SerializeField, Range(0.1f, 1.5f), Tooltip("Movement speed multiplier applied while attacking when attack movement is enabled.")]
    private float attackMovementSpeedMultiplier = 1f;
    [SerializeField, Range(0f, 180f), Tooltip("Maximum left/right turn angle allowed while attacking, measured from facing direction at attack start. 180 = no limit.")]
    private float maxAttackTurnAngleWhileAttacking = 50f;
    [SerializeField, Tooltip("When enabled, landing an attack temporarily reduces how much the player can turn while attacking.")]
    private bool reduceAttackTurningAfterLandingHit = true;

    [Header("Animation Safeguards")]
    [SerializeField, Tooltip("If enabled, forcibly recovers to idle when locomotion animation gets stuck while player is grounded and not moving.")]
    private bool enableIdleRecoverySafeguard = true;
    [SerializeField, Range(0.05f, 1f), Tooltip("How long the player must be grounded with no effective movement before idle recovery is forced.")]
    private float idleRecoveryDelay = 0.2f;
    [SerializeField, Range(0.01f, 0.3f), Tooltip("Maximum horizontal velocity magnitude considered as standing still for idle recovery.")]
    private float idleRecoveryHorizontalSpeedThreshold = 0.08f;
    [SerializeField, Range(0f, 180f), Tooltip("Max turn angle allowed while the post-hit turn-reduction window is active.")]
    private float postHitMaxAttackTurnAngle = 10f;
    [SerializeField, Range(0f, 1f), Tooltip("How long post-hit turn reduction remains active.")]
    private float postHitTurnReductionDuration = 0.22f;
    [SerializeField, Tooltip("When enabled, the attack-facing lock anchor freezes during post-hit turn reduction for a tighter feel.")]
    private bool freezeAttackFacingAnchorAfterHit = true;

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
    [SerializeField] private AttackLockSystem attackLockSystem;
    [SerializeField, Tooltip("How long to freeze vertical velocity during aerial light attacks")]
    [Range(0.05f, 0.6f)] private float aerialLightAttackHangTime = 0.22f;
    [SerializeField, Tooltip("Initial hover duration before plunging downward")] 
    [Range(0f, 0.3f)] private float plungeHoverTime = 0.06f;
    [SerializeField, Tooltip("Downward speed applied during plunge phase")] 
    [Range(10f, 60f)] private float plungeDownSpeed = 32f;
    [SerializeField, Tooltip("How long jump input is blocked after landing from a plunge attack.")]
    [Range(0f, 1.5f)] private float plungeJumpLockDuration = 0.2f;

    [Header("Aerial Target Assist")]
    [SerializeField, Tooltip("When enabled, aerial light/plunge attacks pull the player toward nearby lock targets and align vertical level.")]
    private bool enableAerialTargetAssist = true;
    [SerializeField, Tooltip("Enable assist during aerial light attacks.")]
    private bool enableLightAerialTargetAssist = true;
    [SerializeField, Tooltip("Enable assist during plunge startup.")]
    private bool enablePlungeTargetAssist = true;
    [SerializeField, Range(0f, 20f), Tooltip("Maximum distance for aerial assist target acquisition.")]
    private float aerialTargetAssistRange = 10f;
    [SerializeField, Range(0f, 180f), Tooltip("Forward cone angle used for soft target acquisition when no hard lock target exists.")]
    private float aerialTargetAssistAngle = 120f;
    [SerializeField, Tooltip("Layers used for aerial target assist overlap checks.")]
    private LayerMask aerialTargetAssistMask = ~0;
    [SerializeField, Tooltip("When true, hard-lock targets are preferred over nearby targets.")]
    private bool prioritizeHardLockForAerialAssist = true;
    [SerializeField, Tooltip("When enabled, aerial assist prefers drones over other enemy types.")]
    private bool prioritizeDronesForAerialAssist = true;
    [SerializeField, Tooltip("When enabled (and no drone is selected), aerial assist prefers higher Y-level targets.")]
    private bool prioritizeHigherAerialTargets = true;
    [SerializeField, Tooltip("When enabled, aerial assist prefers targets nearest to the camera center and ignores off-screen targets.")]
    private bool prioritizeCenterOfViewForAerialAssist = true;
    [SerializeField, Tooltip("When true, assist can re-acquire nearby targets while active if the current target is lost.")]
    private bool allowAerialAssistRetarget = true;
    [SerializeField, Range(0.01f, 0.5f), Tooltip("How often assist can attempt to re-acquire a target while active.")]
    private float aerialAssistRetargetInterval = 0.08f;
    [SerializeField, Range(0f, 40f), Tooltip("Horizontal pull speed during aerial light attacks.")]
    private float aerialLightAssistHorizontalSpeed = 10f;
    [SerializeField, Range(0f, 40f), Tooltip("Horizontal pull speed during plunge startup.")]
    private float plungeAssistHorizontalSpeed = 13f;
    [SerializeField, Range(0f, 40f), Tooltip("Vertical alignment speed used by aerial target assist.")]
    private float aerialAssistVerticalSpeed = 10f;
    [SerializeField, Range(-3f, 3f), Tooltip("Vertical offset added to light aerial assist target position. Positive values keep the player slightly above the target.")]
    private float lightAerialAssistTargetYOffset = 0f;
    [SerializeField, Range(-3f, 3f), Tooltip("Vertical offset added to plunge assist target position. Positive values place the player above the target.")]
    private float plungeAssistTargetYOffset = 0.75f;
    [SerializeField, Range(1f, 120f), Tooltip("How quickly assist velocity blends toward desired velocity. Higher = snappier, lower = smoother.")]
    private float aerialAssistVelocityBlendRate = 30f;
    [SerializeField, Range(0f, 5f), Tooltip("Distance from target at which aerial assist stops pulling horizontally.")]
    private float aerialTargetAssistStopDistance = 0.8f;
    [SerializeField, Range(0.05f, 2f), Tooltip("Duration multiplier applied to light aerial assist lock time.")]
    private float aerialLightAssistDurationMultiplier = 1f;
    [SerializeField, Range(0f, 1f), Tooltip("Additional seconds added to plunge assist duration.")]
    private float plungeAssistExtraDuration = 0.12f;
    [SerializeField, Range(0.01f, 0.6f), Tooltip("Minimum duration for plunge assist.")]
    private float plungeAssistMinDuration = 0.08f;
    [SerializeField, Tooltip("When false, input-based movement is suppressed during active aerial assist to prevent movement fighting/jitter.")]
    private bool allowInputSteeringDuringAerialAssist = false;
    [SerializeField, Tooltip("When enabled, attack forward-move velocity is ignored while aerial assist is active to prevent competing horizontal motion.")]
    private bool suppressAttackForwardMoveDuringAerialAssist = true;

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

    [SerializeField, Range(0f, 0.3f), Tooltip("Crossfade duration used when entering grounded dash animation.")]
    private float dashAnimationTransition = 0.08f;

    [SerializeField, Range(0f, 0.3f), Tooltip("Crossfade duration used when entering air dash animation.")]
    private float airDashAnimationTransition = 0.08f;

    [SerializeField, Min(1)]
    private int maxDashCharges = 2;

    [SerializeField, Range(0f, 0.35f)]
    private float dashChainDelay = 0.1f;

    [SerializeField, Range(0f, 2f)]
    private float dashCoolDown = 0.5f;

    [Header("Dash Momentum")]
    [SerializeField, Range(0f, 1f), Tooltip("Fraction of dash speed preserved briefly after dash end.")]
    private float dashMomentumCarryPercent = 0.3f;
    [SerializeField, Range(0f, 10f), Tooltip("Maximum bonus speed allowed above sprint from dash carry momentum.")]
    private float dashMomentumMaxBonusSpeed = 1.5f;
    [SerializeField, Range(0.1f, 40f), Tooltip("How quickly carried dash momentum decays after dash end (units/sec).")]
    private float dashMomentumDecayRate = 14f;

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

    [Header("Plunge Landing Separation")]
    [SerializeField, Tooltip("Radius around plunge impact point used to find enemies to push outward.")]
    [Range(0f, 8f)] private float plungeLandingPushRadius = 2.25f;
    [SerializeField, Tooltip("Horizontal distance normal enemies are displaced away from the plunge impact point.")]
    [Range(0f, 4f)] private float plungeEnemyPushDistance = 1.1f;
    [SerializeField, Tooltip("Multiplier applied to Cleanser pushback distance relative to normal plunge enemy push distance.")]
    [Range(0f, 1f)] private float plungeCleanserPushbackMultiplier = 0.45f;
    [SerializeField, Tooltip("How far the player is slid away from nearby enemies on plunge landing to avoid ending up on top of them.")]
    [Range(0f, 3f)] private float plungePlayerSlideDistance = 0.8f;
    [SerializeField, Tooltip("Maximum downward raycast distance when resolving plunge impact point.")]
    [Range(0.5f, 20f)] private float plungeImpactRayDistance = 8f;
    [SerializeField, Tooltip("Layer mask used for finding enemies near plunge impact.")]
    private LayerMask plungeEnemyPushMask = ~0;
    [SerializeField, Tooltip("Continuous outward enemy push speed applied every fixed step while plunge is active.")]
    [Range(0f, 20f)] private float plungeSustainEnemyPushSpeed = 2.5f;
    [SerializeField, Tooltip("Additional horizontal slide speed to force the player off overlapping enemies while plunging.")]
    [Range(0f, 20f)] private float plungeSustainPlayerSlideSpeed = 7f;

    [Header("Plunge Recovery Safeguards")]
    [SerializeField, Tooltip("Failsafe timeout after plunge landing before forcing attack unlock if input remains busy.")]
    [Range(0.1f, 2f)] private float plungeRecoveryFailsafeSeconds = 0.9f;

    [Header("Camera Settings")]
    [SerializeField] bool invertYAxis = false;

    [Header("High Fall Settings")]
    [SerializeField, Range(0.5f, 30f)]
    private float highFallHeightThreshold = 6f;

    [SerializeField, Range(1f, 100f)]
    private float highFallGroundProbeDistance = 25f;

    [Header("Movement SFX")]
    [SerializeField] private AudioClip dashSFX;
    [SerializeField] private AudioClip jumpSFX;
    [SerializeField] private AudioClip doubleJumpSFX;

    #endregion

    private bool isDashing;
    private int dashChargesRemaining;
    private float nextDashAllowedTime;
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
    private Coroutine pendingJumpTimeoutRoutine;
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
    private Coroutine dashRechargeRoutine;
    private bool dashInputLockOwned;
    private bool currentDashUsesCharges;
    private float dashStateExpectedEndTime = -1f;
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
    private bool debugLastGrounded;
    private bool debugLastInputBusy;
    private bool debugLastAirborneAnimationLocked;
    private bool debugLastFallingAnimationPlaying;
    private bool debugLastLocomotionSuppressed;
    private bool debugLastDashing;
    private PendingJumpType debugLastPendingJump = PendingJumpType.None;
    private float noInputGroundedTimer;
    private bool idleRecoveryAppliedThisWindow;
    private readonly Collider[] plungeEnemyPushBuffer = new Collider[32];
    private readonly int[] plungeProcessedEnemyIds = new int[32];
    private bool warnedMissingSoundSource;
    private Vector3 dashCarryVelocity = Vector3.zero;
    private bool attackFacingLockInitialized;
    private bool waitingForPlungeRecoveryUnlock;
    private float plungeRecoveryBusyTimer;
    private Vector3 attackFacingLockForward = Vector3.forward;
    private bool plungeJumpLocked;
    private Coroutine plungeJumpLockRoutine;
    private Vector3 aerialAssistVelocity = Vector3.zero;
    private bool aerialAssistActive;
    private Coroutine aerialAssistRoutine;
    private Transform aerialAssistTarget;
    private float activeAerialAssistHorizontalSpeed;
    private float activeAerialAssistTargetYOffset;
    private float nextAerialAssistRetargetTime;
    private float attackTurnReductionTimer;
    private bool attackFacingAnchorFrozenByHit;
    private bool attackMovementSuppressedByHit;

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

    private bool IsGroundedNow()
    {
        if (characterController == null)
            return false;

        if (characterController.isGrounded)
            return true;

        float probeDistance = Mathf.Max(0.01f, maxDistance);
        if (probeDistance <= 0f)
            return false;

        Bounds bounds = characterController.bounds;
        Vector3 halfExtents = new Vector3(
            Mathf.Max(0.01f, boxSize.x * 0.5f),
            Mathf.Max(0.01f, boxSize.y * 0.5f),
            Mathf.Max(0.01f, boxSize.z * 0.5f));

        Vector3 origin = new Vector3(bounds.center.x, bounds.min.y + halfExtents.y + 0.02f, bounds.center.z);
        LayerMask probeMask = layerMask.value == 0 ? Physics.DefaultRaycastLayers : layerMask;

        return Physics.BoxCast(
            origin,
            halfExtents,
            Vector3.down,
            Quaternion.identity,
            probeDistance,
            probeMask,
            QueryTriggerInteraction.Ignore);
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

        attackLockSystem = attackLockSystem
            ?? GetComponent<AttackLockSystem>()
            ?? GetComponentInChildren<AttackLockSystem>()
            ?? GetComponentInParent<AttackLockSystem>();

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
        ResetDashCharges();
        ResetMoveState();
        wasGrounded = IsGroundedNow();

        debugLastGrounded = wasGrounded;
        debugLastInputBusy = InputReader.inputBusy;
        debugLastAirborneAnimationLocked = airborneAnimationLocked;
        debugLastFallingAnimationPlaying = fallingAnimationPlaying;
        debugLastLocomotionSuppressed = locomotionAnimationSuppressed;
        debugLastDashing = isDashing;
        debugLastPendingJump = pendingJump;
    }
    private void OnEnable()
    {
        PlayerAttackManager.OnAttack += AerialAttackHop;
        HitboxDamageManager.AttackHitConfirmed += HandleAttackHitConfirmed;
        if (dashChargesRemaining <= 0)
            ResetDashCharges();
    }

    private void OnDisable()
    {
        PlayerAttackManager.OnAttack -= AerialAttackHop;
        HitboxDamageManager.AttackHitConfirmed -= HandleAttackHitConfirmed;

        StopPendingJumpTimeout();

        if (plungeJumpLockRoutine != null)
        {
            try { StopCoroutine(plungeJumpLockRoutine); } catch { }
            plungeJumpLockRoutine = null;
        }
        plungeJumpLocked = false;
        attackTurnReductionTimer = 0f;
        attackFacingAnchorFrozenByHit = false;
        attackMovementSuppressedByHit = false;

        StopAerialTargetAssist();

        // Stop any running coroutines we own to avoid keeping this MonoBehaviour alive
        if (dashRoutine != null)
        {
            try { StopCoroutine(dashRoutine); } catch { }
            dashRoutine = null;
        }

        if (dashRechargeRoutine != null)
        {
            try { StopCoroutine(dashRechargeRoutine); } catch { }
            dashRechargeRoutine = null;
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

        isDashingFlag = false;

        // Clear static instance reference if it pointed to this instance
        if (s_instance == this)
            s_instance = null;
    }

    private void OnDestroy()
    {
        // Mirror OnDisable cleanup in case object is destroyed without disabling first
        PlayerAttackManager.OnAttack -= AerialAttackHop;
        HitboxDamageManager.AttackHitConfirmed -= HandleAttackHitConfirmed;

        StopPendingJumpTimeout();

        if (plungeJumpLockRoutine != null)
        {
            try { StopCoroutine(plungeJumpLockRoutine); } catch { }
            plungeJumpLockRoutine = null;
        }
        plungeJumpLocked = false;
        attackTurnReductionTimer = 0f;
        attackFacingAnchorFrozenByHit = false;
        attackMovementSuppressedByHit = false;

        StopAerialTargetAssist();

        if (dashRoutine != null)
        {
            try { StopCoroutine(dashRoutine); } catch { }
            dashRoutine = null;
        }

        if (dashRechargeRoutine != null)
        {
            try { StopCoroutine(dashRechargeRoutine); } catch { }
            dashRechargeRoutine = null;
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

        isDashingFlag = false;

        if (s_instance == this)
            s_instance = null;
    }

    private void Update()
    {
        DebugStateTransitions();

        if (attackTurnReductionTimer > 0f)
        {
            attackTurnReductionTimer = Mathf.Max(0f, attackTurnReductionTimer - Time.deltaTime);
            if (attackTurnReductionTimer <= 0f)
                attackFacingAnchorFrozenByHit = false;
        }

        EnsureDashStateConsistency();

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
        bool attackMovementActive = IsAttackMovementActive();
        bool suppressInputForAssist = aerialAssistActive && !allowInputSteeringDuringAerialAssist;

        if (attackMovementActive)
        {
            bool freezeAnchor = attackFacingAnchorFrozenByHit && freezeAttackFacingAnchorAfterHit;
            if (!freezeAnchor)
            {
                attackFacingLockForward = transform.forward;
                attackFacingLockForward.y = 0f;
                if (attackFacingLockForward.sqrMagnitude < 0.0001f)
                    attackFacingLockForward = Vector3.forward;
                else
                    attackFacingLockForward.Normalize();
            }

            attackFacingLockInitialized = true;
        }
        else
        {
            attackFacingLockInitialized = false;
        }

        if ((InputReader.inputBusy && !attackMovementActive) || isDashing || suppressInputForAssist)
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
            noInputGroundedTimer = 0f;
            idleRecoveryAppliedThisWindow = false;

            bool stateChanged = UpdateMoveState(usingAnalogThresholds, inputMagnitude);

            Vector3 moveDirection = (forward * inputMove.y + right * inputMove.x).normalized;
            float targetSpeed = movementSpeedOverrideActive ? movementSpeedOverride : CurrentSpeed;

            if (attackMovementActive)
                targetSpeed *= attackMovementSpeedMultiplier;

            float appliedSpeed = targetSpeed;

            // Smooth only the Jog -> Sprint speed jump so sprint onset feels less abrupt.
            if (!movementSpeedOverrideActive && moveState == GroundMoveState.Sprint)
            {
                float currentHorizontalSpeed = new Vector2(currentMovement.x, currentMovement.z).magnitude;
                appliedSpeed = Mathf.MoveTowards(currentHorizontalSpeed, targetSpeed, sprintSpeedRampRate * Time.deltaTime);
            }

            Vector3 desiredVelocity = moveDirection * appliedSpeed;
            currentMovement.x = desiredVelocity.x;
            currentMovement.z = desiredVelocity.z;

            if (shouldFaceMoveDirection && moveDirection.sqrMagnitude > 0.001f)
            {
                Vector3 facingDirection;
                if (attackMovementActive && TryGetAttackPriorityFacingDirection(moveDirection, out Vector3 prioritizedFacing))
                    facingDirection = prioritizedFacing;
                else
                    facingDirection = attackMovementActive
                        ? GetAttackConstrainedFacingDirection(moveDirection)
                        : moveDirection;

                Quaternion toRotation = Quaternion.LookRotation(facingDirection, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, toRotation, Time.deltaTime * 10f);
            }

            if (!attackMovementActive && !locomotionAnimationSuppressed && pendingJump == PendingJumpType.None && !previouslyMoving && !stateChanged)
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

            if (!attackMovementActive && !locomotionAnimationSuppressed && pendingJump == PendingJumpType.None && IsGroundedNow() && !airborneAnimationLocked && landingAnimationLockTimer <= 0f &&
                (previouslyMoving || (Mathf.Abs(currentMovement.x) < 0.01f && Mathf.Abs(currentMovement.z) < 0.01f)))
            {
                EnsureCombatIdleControllerReference();
                if (!hasCombatIdleController)
                    animationController?.PlayIdle();
            }

            TryApplyIdleRecoverySafeguard(attackMovementActive);
        }
    }

    private void TryApplyIdleRecoverySafeguard(bool attackMovementActive)
    {
        if (!enableIdleRecoverySafeguard)
            return;

        if (animationController == null)
            return;

        bool grounded = IsGroundedNow();
        if (!grounded || isDashing || isPlunging || plungeLandingPending || pendingJump != PendingJumpType.None || attackMovementActive)
        {
            noInputGroundedTimer = 0f;
            idleRecoveryAppliedThisWindow = false;
            return;
        }

        if (InputReader.inputBusy)
            return;

        float horizontalSpeed = new Vector2(currentMovement.x, currentMovement.z).magnitude;
        if (horizontalSpeed > idleRecoveryHorizontalSpeedThreshold)
        {
            noInputGroundedTimer = 0f;
            idleRecoveryAppliedThisWindow = false;
            return;
        }

        noInputGroundedTimer += Time.deltaTime;
        if (idleRecoveryAppliedThisWindow || noInputGroundedTimer < idleRecoveryDelay)
            return;

        if (locomotionAnimationSuppressed)
            SuppressLocomotionAnimations(false);

        animationController.PlayIdle();
        idleRecoveryAppliedThisWindow = true;

        DebugMovementLog($"Idle recovery safeguard applied. grounded={grounded}, horizontalSpeed={horizontalSpeed:F3}, noInputGroundedTimer={noInputGroundedTimer:F2}");
    }

    private void ApplyPlungeLandingSeparation()
    {
        if (plungeLandingPushRadius <= 0f || plungeEnemyPushDistance <= 0f || characterController == null)
            return;

        Vector3 impactPoint = ResolvePlungeImpactPoint();

        int enemyCount = Physics.OverlapSphereNonAlloc(
            impactPoint,
            plungeLandingPushRadius,
            plungeEnemyPushBuffer,
            plungeEnemyPushMask,
            QueryTriggerInteraction.Ignore);

        if (enemyCount <= 0)
            return;

        int processedCount = 0;
        Vector3 playerSlideDirectionAccum = Vector3.zero;

        for (int i = 0; i < enemyCount; i++)
        {
            Collider col = plungeEnemyPushBuffer[i];
            if (col == null)
                continue;

            BaseEnemyCore enemy = col.GetComponentInParent<BaseEnemyCore>();
            if (enemy == null || !enemy.isAlive)
                continue;

            int enemyId = enemy.GetInstanceID();
            bool alreadyProcessed = false;
            for (int j = 0; j < processedCount; j++)
            {
                if (plungeProcessedEnemyIds[j] == enemyId)
                {
                    alreadyProcessed = true;
                    break;
                }
            }
            if (alreadyProcessed)
                continue;

            if (processedCount < plungeProcessedEnemyIds.Length)
                plungeProcessedEnemyIds[processedCount++] = enemyId;

            Transform enemyTransform = enemy.transform;
            Vector3 away = enemyTransform.position - impactPoint;
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f)
                away = enemyTransform.forward;
            if (away.sqrMagnitude < 0.0001f)
                away = (enemyTransform.position - transform.position);
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f)
                away = Vector3.forward;

            Vector3 pushDir = away.normalized;

            float pushDistance = plungeEnemyPushDistance;
            bool isCleanser = enemy.GetComponentInParent<EnemyBehavior.Boss.Cleanser.CleanserBrain>() != null;
            if (isCleanser)
                pushDistance *= Mathf.Clamp01(plungeCleanserPushbackMultiplier);

            ApplyEnemyPlungePush(enemyTransform, pushDir * pushDistance);
            playerSlideDirectionAccum += -pushDir;
        }

        if (playerSlideDirectionAccum.sqrMagnitude > 0.0001f && plungePlayerSlideDistance > 0f)
        {
            Vector3 slide = playerSlideDirectionAccum.normalized * plungePlayerSlideDistance;
            slide.y = 0f;
            characterController.Move(slide);
        }
    }

    private Vector3 ApplyPlungeSustainRepulsion(float dt)
    {
        if (!isPlunging || dt <= 0f || plungeLandingPushRadius <= 0f || plungeSustainEnemyPushSpeed <= 0f)
            return Vector3.zero;

        Vector3 impactPoint = ResolvePlungeImpactPoint();
        int enemyCount = Physics.OverlapSphereNonAlloc(
            impactPoint,
            plungeLandingPushRadius,
            plungeEnemyPushBuffer,
            plungeEnemyPushMask,
            QueryTriggerInteraction.Ignore);

        if (enemyCount <= 0)
            return Vector3.zero;

        int processedCount = 0;
        Vector3 playerSlideDirAccum = Vector3.zero;

        for (int i = 0; i < enemyCount; i++)
        {
            Collider col = plungeEnemyPushBuffer[i];
            if (col == null)
                continue;

            BaseEnemyCore enemy = col.GetComponentInParent<BaseEnemyCore>();
            if (enemy == null || !enemy.isAlive)
                continue;

            int enemyId = enemy.GetInstanceID();
            bool alreadyProcessed = false;
            for (int j = 0; j < processedCount; j++)
            {
                if (plungeProcessedEnemyIds[j] == enemyId)
                {
                    alreadyProcessed = true;
                    break;
                }
            }
            if (alreadyProcessed)
                continue;

            if (processedCount < plungeProcessedEnemyIds.Length)
                plungeProcessedEnemyIds[processedCount++] = enemyId;

            Transform enemyTransform = enemy.transform;
            Vector3 away = enemyTransform.position - impactPoint;
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f)
                away = enemyTransform.position - transform.position;
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f)
                away = enemyTransform.forward;
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f)
                away = Vector3.forward;

            Vector3 pushDir = away.normalized;

            float pushSpeed = plungeSustainEnemyPushSpeed;
            bool isCleanser = enemy.GetComponentInParent<EnemyBehavior.Boss.Cleanser.CleanserBrain>() != null;
            if (isCleanser)
                pushSpeed *= Mathf.Clamp01(plungeCleanserPushbackMultiplier);

            ApplyEnemyPlungePush(enemyTransform, pushDir * (pushSpeed * dt), allowWarp: false);
            playerSlideDirAccum += -pushDir;
        }

        if (plungeSustainPlayerSlideSpeed <= 0f || playerSlideDirAccum.sqrMagnitude < 0.0001f)
            return Vector3.zero;

        playerSlideDirAccum.y = 0f;
        return playerSlideDirAccum.normalized * plungeSustainPlayerSlideSpeed;
    }

    private Vector3 ResolvePlungeImpactPoint()
    {
        Vector3 impactPoint = transform.position;
        Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;
        if (characterController != null)
            rayOrigin = characterController.bounds.center;

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit groundHit, plungeImpactRayDistance, layerMask, QueryTriggerInteraction.Ignore))
            impactPoint = groundHit.point;
        else
            impactPoint.y = rayOrigin.y;

        return impactPoint;
    }

    private static void ApplyEnemyPlungePush(Transform enemyTransform, Vector3 displacement, bool allowWarp = true)
    {
        if (enemyTransform == null || displacement.sqrMagnitude < 0.000001f)
            return;

        NavMeshAgent enemyAgent = enemyTransform.GetComponentInParent<NavMeshAgent>();
        if (enemyAgent != null && enemyAgent.enabled && enemyAgent.isOnNavMesh)
        {
            Vector3 target = enemyTransform.position + displacement;
            if (allowWarp && NavMesh.SamplePosition(target, out NavMeshHit navHit, 2f, NavMesh.AllAreas))
                enemyAgent.Warp(navHit.position);
            else
                enemyAgent.Move(displacement);

            return;
        }

        Rigidbody rb = enemyTransform.GetComponentInParent<Rigidbody>();
        if (rb != null && !rb.isKinematic)
        {
            rb.AddForce(displacement, ForceMode.VelocityChange);
            return;
        }

        enemyTransform.position += displacement;
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
        DebugMovementLog($"OnJump called | grounded={IsGroundedNow()} ccGrounded={(characterController != null && characterController.isGrounded)} inputBusy={InputReader.inputBusy} pendingJump={pendingJump} canDoubleJump={canDoubleJump} doubleJumpAvailable={doubleJumpAvailable} currentY={currentMovement.y:F2}");

        if (plungeJumpLocked)
            return;

        if (CombatManager.isGuarding)
            return;

        if (InputReader.inputBusy)
            attackManager?.ForceCancelCurrentAttack(resetCombo: false);

        if (characterController == null)
            return;

        if (pendingJump != PendingJumpType.None)
            return;

        // checks to see if the player can jump or double jump
        if (IsGroundedNow())
        {
            airborneAnimationLocked = true;
            fallingAnimationPlaying = false;
            highFallActive = false;
            pendingJump = PendingJumpType.Ground;
            StartPendingJumpTimeout();
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
            StartPendingJumpTimeout();
            animationController?.PlayAirJumpStart();
            if (animationController == null || jumpEventTimeout <= 0f)
                HandleAnimationJumpEvent();
        }
    }

    public void HandleAnimationJumpEvent()
    {
        if (pendingJump == PendingJumpType.None)
            return;

        DebugMovementLog($"HandleAnimationJumpEvent START | pendingJump={pendingJump} grounded={IsGroundedNow()} ccGrounded={(characterController != null && characterController.isGrounded)} currentY={currentMovement.y:F2}");

        StopPendingJumpTimeout();

        if (pendingJump == PendingJumpType.Ground)
        {
            if (!IsGroundedNow())
            {
                pendingJump = PendingJumpType.None;
                return;
            }


            currentMovement.y = jumpForce;
            doubleJumpAvailable = canDoubleJump;
            pendingJump = PendingJumpType.None;
            PlaySFX(jumpSFX);
            DebugMovementLog($"Ground jump applied | currentY={currentMovement.y:F2} doubleJumpAvailable={doubleJumpAvailable}");
            return;
        }

        if (!canDoubleJump || !doubleJumpAvailable)
        {
            pendingJump = PendingJumpType.None;
            return;
        }

        currentMovement.y = MathF.Max(currentMovement.y, doubleJumpForce);
        PlaySFX(doubleJumpSFX);
        doubleJumpAvailable = false;
        pendingJump = PendingJumpType.None;
        DoubleJumpPerformed?.Invoke();
        DebugMovementLog($"Double jump applied | currentY={currentMovement.y:F2} doubleJumpAvailable={doubleJumpAvailable}");
    }

    private void OnDash()
    {
        if (dashChargesRemaining <= 0)
            return;

        if (Time.time < nextDashAllowedTime)
            return;

        if (CombatManager.isGuarding)
            return;

        bool grounded = IsGroundedNow();
        bool dashAllowed = grounded || airDashAvailable;
        if (!dashAllowed)
            return;

        DashPerformed?.Invoke();

        if (InputReader.inputBusy)
            attackManager?.ForceCancelCurrentAttack(resetCombo: false);

        CancelPlungeState();

        dashChargesRemaining = Mathf.Max(0, dashChargesRemaining - 1);
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
                useDashCharges: true,
                onStart: () =>
                {
                    if (IsGroundedNow())
                        animationController?.PlayDash(dashAnimationTransition);
                    else
                        animationController?.PlayAirDash(airDashAnimationTransition);
                },
                onComplete: () =>
                {
                    if (InputReader.MoveInput.sqrMagnitude > 0.1f)
                        TrySetMoveState(GroundMoveState.Sprint, force: true);
                    else
                        ResetMoveState();
                }));
        if(!isAirDash)
            PlaySFX(dashSFX);
    }

    private IEnumerator DashCoroutine(
        Vector3 direction,
        bool isAirDash,
        float distance,
        float duration,
        bool lockInput,
        bool useDashCharges,
        Action onStart,
        Action onComplete)
    {
        duration = Mathf.Max(0.01f, duration);
        direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;

        isDashing = true;
        dashCarryVelocity = Vector3.zero;
        currentDashUsesCharges = useDashCharges;
        dashStateExpectedEndTime = Time.unscaledTime + duration + 0.2f;
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

        Vector3 endedDashVelocity = dashVelocity;
        dashVelocity = Vector3.zero;
        isDashing = false;
        airDashInProgress = false;
        suspendGravityDuringDash = false;
        dashForceStop = false;
        dashRoutine = null;
        dashStateExpectedEndTime = -1f;

        if (lockInput && dashInputLockOwned)
        {
            InputReader.inputBusy = false;
            dashInputLockOwned = false;
        }

        onComplete?.Invoke();

        if (useDashCharges)
            HandleDashRecovery();
        else
            isDashingFlag = false;

        ApplyDashMomentumCarry(endedDashVelocity);

        currentDashUsesCharges = false;
    }

    private void ApplyDashMomentumCarry(Vector3 endedDashVelocity)
    {
        dashCarryVelocity = Vector3.zero;

        if (dashMomentumCarryPercent <= 0f)
            return;

        Vector3 planar = new Vector3(endedDashVelocity.x, 0f, endedDashVelocity.z);
        if (planar.sqrMagnitude <= 0.0001f)
            return;

        float carrySpeed = planar.magnitude * dashMomentumCarryPercent;
        float maxCarrySpeed = Mathf.Max(0f, sprintSpeed + dashMomentumMaxBonusSpeed);
        carrySpeed = Mathf.Min(carrySpeed, maxCarrySpeed);

        dashCarryVelocity = planar.normalized * carrySpeed;
    }

    private void HandleDashRecovery()
    {
        isDashingFlag = false;

        if (dashChargesRemaining > 0)
        {
            nextDashAllowedTime = Time.time + Mathf.Max(0f, dashChainDelay);
            return;
        }

        if (dashRechargeRoutine != null)
        {
            StopCoroutine(dashRechargeRoutine);
            dashRechargeRoutine = null;
        }

        dashRechargeRoutine = StartCoroutine(DashRechargeCoroutine());
    }

    private IEnumerator DashRechargeCoroutine()
    {
        float rechargeDelay = Mathf.Max(0f, dashCoolDown);
        if (rechargeDelay > 0f)
            yield return new WaitForSeconds(rechargeDelay);

        dashChargesRemaining = Mathf.Max(1, maxDashCharges);
        nextDashAllowedTime = 0f;
        dashRechargeRoutine = null;
    }

    private void ResetDashCharges()
    {
        dashChargesRemaining = Mathf.Max(1, maxDashCharges);
        nextDashAllowedTime = 0f;
        isDashingFlag = false;

        if (dashRechargeRoutine != null)
        {
            StopCoroutine(dashRechargeRoutine);
            dashRechargeRoutine = null;
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
                useDashCharges: false,
                onStart,
                onComplete));

        return true;
    }

    private void ForceStopDashImmediate(bool relinquishInputLock = false)
    {
        if (relinquishInputLock)
        {
            if (dashInputLockOwned)
                InputReader.inputBusy = false;

            dashInputLockOwned = false;
        }

        if (!isDashing)
            return;

        dashForceStop = true;
    }

    public bool TryTriggerLauncherJump()
    {
        if (!isDashing || characterController == null || !IsGroundedNow())
            return false;

        AbortDashState(releaseInputLock: true, stopCoroutine: true, applyDashRecovery: true);

        Vector3 forwardDir = transform.forward;
        currentMovement.x = forwardDir.x * launcherForwardVelocity;
        currentMovement.z = forwardDir.z * launcherForwardVelocity;
        currentMovement.y = launcherJumpVelocity;

        suspendGravityDuringDash = false;
        airDashInProgress = false;
        airborneAnimationLocked = true;
        fallingAnimationPlaying = false;

        return true;
    }

    private void EnsureDashStateConsistency()
    {
        if (!isDashing)
            return;

        bool coroutineMissing = dashRoutine == null;
        bool timedOut = dashStateExpectedEndTime > 0f && Time.unscaledTime > dashStateExpectedEndTime;

        if (!coroutineMissing && !timedOut)
            return;

        Debug.LogWarning("[PlayerMovement] Dash state desynced. Forcing dash reset to avoid softlock.");
        AbortDashState(releaseInputLock: true, stopCoroutine: false, applyDashRecovery: true);
    }

    private void AbortDashState(bool releaseInputLock, bool stopCoroutine, bool applyDashRecovery)
    {
        bool shouldRecoverDash = applyDashRecovery && currentDashUsesCharges;

        if (stopCoroutine && dashRoutine != null)
        {
            try { StopCoroutine(dashRoutine); } catch { }
            dashRoutine = null;
        }

        dashVelocity = Vector3.zero;
        isDashing = false;
        isDashingFlag = false;
        dashForceStop = false;
        suspendGravityDuringDash = false;
        airDashInProgress = false;
        dashStateExpectedEndTime = -1f;

        if (releaseInputLock && dashInputLockOwned)
        {
            InputReader.inputBusy = false;
            dashInputLockOwned = false;
        }

        currentDashUsesCharges = false;

        if (shouldRecoverDash)
            HandleDashRecovery();
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
        if (attack == null || IsGroundedNow())
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
            if (enableLightAerialTargetAssist)
                StartAerialTargetAssist(AttackType.LightAerial, lockDuration * Mathf.Max(0.05f, aerialLightAssistDurationMultiplier));
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

        if (enablePlungeTargetAssist)
        {
            float assistDuration = Mathf.Max(plungeAssistMinDuration, plungeHoverTime + plungeAssistExtraDuration);
            StartAerialTargetAssist(AttackType.HeavyAerial, assistDuration);
        }
    }

    public void CancelPlungeState()
    {
        isPlunging = false;
        plungeLandingPending = false;
        plungeTimer = 0f;
        aerialAttackLockTimer = 0f;
        StopAerialTargetAssist();
    }

    private void StartAerialTargetAssist(AttackType attackType, float duration)
    {
        if (!enableAerialTargetAssist || duration <= 0f)
            return;

        if (!TryResolveAerialAssistTarget(out Transform target))
            return;

        float horizontalSpeed = attackType == AttackType.HeavyAerial
            ? plungeAssistHorizontalSpeed
            : aerialLightAssistHorizontalSpeed;

        horizontalSpeed = Mathf.Max(0f, horizontalSpeed);
        if (horizontalSpeed <= 0f && aerialAssistVerticalSpeed <= 0f)
            return;

        if (aerialAssistRoutine != null)
            StopCoroutine(aerialAssistRoutine);

        aerialAssistTarget = target;
        activeAerialAssistHorizontalSpeed = horizontalSpeed;
        activeAerialAssistTargetYOffset = attackType == AttackType.HeavyAerial
            ? plungeAssistTargetYOffset
            : lightAerialAssistTargetYOffset;
        nextAerialAssistRetargetTime = Time.time + Mathf.Max(0.01f, aerialAssistRetargetInterval);
        aerialAssistRoutine = StartCoroutine(AerialTargetAssistRoutine(duration));
    }

    private IEnumerator AerialTargetAssistRoutine(float duration)
    {
        aerialAssistActive = true;
        aerialAssistVelocity = Vector3.zero;

        float timer = Mathf.Max(0.01f, duration);
        while (timer > 0f)
        {
            if (IsGroundedNow() || isDashing)
                break;

            if (!IsAerialAssistTargetValid(aerialAssistTarget))
            {
                if (allowAerialAssistRetarget && Time.time >= nextAerialAssistRetargetTime)
                {
                    nextAerialAssistRetargetTime = Time.time + Mathf.Max(0.01f, aerialAssistRetargetInterval);
                    if (TryResolveAerialAssistTarget(out Transform fallback))
                        aerialAssistTarget = fallback;
                    else
                        break;
                }
                else if (!allowAerialAssistRetarget)
                    break;
            }

            timer -= Time.deltaTime;
            yield return null;
        }

        aerialAssistVelocity = Vector3.zero;
        aerialAssistActive = false;
        aerialAssistRoutine = null;
    }

    private void StopAerialTargetAssist()
    {
        if (aerialAssistRoutine != null)
        {
            try { StopCoroutine(aerialAssistRoutine); } catch { }
            aerialAssistRoutine = null;
        }

        aerialAssistTarget = null;
        activeAerialAssistHorizontalSpeed = 0f;
        activeAerialAssistTargetYOffset = 0f;
        nextAerialAssistRetargetTime = 0f;
        aerialAssistVelocity = Vector3.zero;
        aerialAssistActive = false;
    }

    private bool IsAerialAssistTargetValid(Transform target)
    {
        if (target == null)
            return false;

        BaseEnemyCore enemy = target.GetComponentInParent<BaseEnemyCore>();
        if (enemy != null && !enemy.isAlive)
            return false;

        float range = Mathf.Max(0f, aerialTargetAssistRange);
        if (range > 0f && (target.position - transform.position).sqrMagnitude > range * range)
            return false;

        return true;
    }

    private void UpdateAerialAssistVelocity(float deltaTime)
    {
        if (!aerialAssistActive || aerialAssistTarget == null)
        {
            aerialAssistVelocity = Vector3.MoveTowards(aerialAssistVelocity, Vector3.zero, Mathf.Max(1f, aerialAssistVelocityBlendRate) * deltaTime);
            return;
        }

        Vector3 targetPoint = aerialAssistTarget.position + Vector3.up * activeAerialAssistTargetYOffset;
        Vector3 toTarget = targetPoint - transform.position;
        Vector3 planar = new Vector3(toTarget.x, 0f, toTarget.z);

        Vector3 desiredVelocity = Vector3.zero;
        float stopDistance = Mathf.Max(0f, aerialTargetAssistStopDistance);
        float planarDistance = planar.magnitude;
        if (planarDistance > stopDistance && activeAerialAssistHorizontalSpeed > 0f)
        {
            Vector3 planarDir = planar / Mathf.Max(0.0001f, planarDistance);
            desiredVelocity += planarDir * activeAerialAssistHorizontalSpeed;

            if (shouldFaceMoveDirection)
            {
                Quaternion toRotation = Quaternion.LookRotation(planarDir, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, toRotation, deltaTime * 12f);
            }
        }

        float verticalSpeed = Mathf.Max(0f, aerialAssistVerticalSpeed);
        if (verticalSpeed > 0f)
        {
            float verticalVelocity = Mathf.Clamp(toTarget.y / Mathf.Max(deltaTime, 0.0001f), -verticalSpeed, verticalSpeed);
            desiredVelocity += Vector3.up * verticalVelocity;
        }

        float blendRate = Mathf.Max(1f, aerialAssistVelocityBlendRate);
        aerialAssistVelocity = Vector3.MoveTowards(aerialAssistVelocity, desiredVelocity, blendRate * deltaTime);
    }

    private bool TryResolveAerialAssistTarget(out Transform target)
    {
        target = null;

        float range = Mathf.Max(0f, aerialTargetAssistRange);
        if (range <= 0f)
            return false;

        attackLockSystem = attackLockSystem
            ?? GetComponent<AttackLockSystem>()
            ?? GetComponentInChildren<AttackLockSystem>()
            ?? GetComponentInParent<AttackLockSystem>();

        if (prioritizeHardLockForAerialAssist && attackLockSystem != null && attackLockSystem.IsHardLockActive)
        {
            Transform hardLockTarget = attackLockSystem.CurrentHardLockTarget;
            if (hardLockTarget != null)
            {
                BaseEnemyCore hardLockEnemy = hardLockTarget.GetComponentInParent<BaseEnemyCore>();
                if (hardLockEnemy != null && hardLockEnemy.isAlive)
                {
                    float hardLockDistance = (hardLockTarget.position - transform.position).magnitude;
                    bool hardLockIsDrone = IsDroneTarget(hardLockTarget);
                    bool canUseHardLockTarget = hardLockDistance <= range
                        && (!prioritizeDronesForAerialAssist || hardLockIsDrone);

                    if (canUseHardLockTarget)
                    {
                        target = hardLockTarget;
                        return true;
                    }
                }
            }
        }

        Collider[] nearby = Physics.OverlapSphere(
            transform.position,
            range,
            aerialTargetAssistMask,
            QueryTriggerInteraction.Collide);

        Camera screenCamera = Camera.main;
        bool useViewportPriority = prioritizeCenterOfViewForAerialAssist && screenCamera != null;

        float bestSqrDistance = float.MaxValue;
        float bestHeightDelta = float.NegativeInfinity;
        bool bestIsDrone = false;
        float bestViewportScore = float.MaxValue;
        float maxAngle = Mathf.Clamp(aerialTargetAssistAngle, 0f, 180f);
        Vector3 forwardFlat = transform.forward;
        forwardFlat.y = 0f;
        if (forwardFlat.sqrMagnitude < 0.0001f)
            forwardFlat = Vector3.forward;
        else
            forwardFlat.Normalize();

        for (int i = 0; i < nearby.Length; i++)
        {
            Collider col = nearby[i];
            if (col == null)
                continue;

            BaseEnemyCore enemy = col.GetComponentInParent<BaseEnemyCore>();
            if (enemy == null || !enemy.isAlive)
                continue;

            Transform candidateTarget = col.transform != null ? col.transform : enemy.transform;
            Vector3 toEnemy = candidateTarget.position - transform.position;
            Vector3 toEnemyFlat = new Vector3(toEnemy.x, 0f, toEnemy.z);
            if (toEnemyFlat.sqrMagnitude <= 0.0001f)
                continue;

            float angle = Vector3.Angle(forwardFlat, toEnemyFlat.normalized);
            if (angle > maxAngle)
                continue;

            float sqrDistance = toEnemy.sqrMagnitude;
            bool isDrone = IsDroneTarget(enemy.transform);
            float viewportScore = float.MaxValue;
            if (useViewportPriority)
            {
                Vector3 viewport = screenCamera.WorldToViewportPoint(enemy.transform.position);
                if (viewport.z <= 0f)
                    continue;

                if (viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f)
                    continue;

                Vector2 offset = new Vector2(viewport.x - 0.5f, viewport.y - 0.5f);
                viewportScore = offset.sqrMagnitude;
            }

            bool takeCandidate = false;

            if (target == null)
            {
                takeCandidate = true;
            }
            else if (prioritizeDronesForAerialAssist && isDrone != bestIsDrone)
            {
                takeCandidate = isDrone;
            }
            else if (useViewportPriority)
            {
                if (viewportScore < bestViewportScore)
                    takeCandidate = true;
                else if (Mathf.Approximately(viewportScore, bestViewportScore) && prioritizeHigherAerialTargets)
                {
                    float heightDelta = candidateTarget.position.y - transform.position.y;
                    if (heightDelta > bestHeightDelta + 0.02f)
                        takeCandidate = true;
                    else if (Mathf.Abs(heightDelta - bestHeightDelta) <= 0.02f && sqrDistance < bestSqrDistance)
                        takeCandidate = true;
                }
                else if (Mathf.Approximately(viewportScore, bestViewportScore) && sqrDistance < bestSqrDistance)
                {
                    takeCandidate = true;
                }
            }
            else if (prioritizeHigherAerialTargets)
            {
                float heightDelta = candidateTarget.position.y - transform.position.y;
                if (heightDelta > bestHeightDelta + 0.02f)
                    takeCandidate = true;
                else if (Mathf.Abs(heightDelta - bestHeightDelta) <= 0.02f && sqrDistance < bestSqrDistance)
                    takeCandidate = true;
            }
            else if (sqrDistance < bestSqrDistance)
            {
                takeCandidate = true;
            }

            if (takeCandidate)
            {
                bestSqrDistance = sqrDistance;
                bestHeightDelta = candidateTarget.position.y - transform.position.y;
                bestIsDrone = isDrone;
                bestViewportScore = viewportScore;
                target = candidateTarget;
            }
        }

        return target != null;
    }

    private static bool IsDroneTarget(Transform target)
    {
        if (target == null)
            return false;

        return target.GetComponentInParent<DroneEnemy>() != null;
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

        if (!isDashing && dashCarryVelocity.sqrMagnitude > 0.0001f)
        {
            horizontalMovement += dashCarryVelocity;
            dashCarryVelocity = Vector3.MoveTowards(
                dashCarryVelocity,
                Vector3.zero,
                Mathf.Max(0f, dashMomentumDecayRate) * Time.deltaTime);
        }
        
        // Add external velocity injection (vacuum suction, knockback, etc.)
        if (externalVelocityActive)
        {
            horizontalMovement += new Vector3(externalVelocity.x, 0, externalVelocity.z);
        }

        // Add attack forward-move velocity (smooth lunge)
        if (attackMoveActive && !(aerialAssistActive && suppressAttackForwardMoveDuringAerialAssist))
        {
            horizontalMovement += new Vector3(attackMoveVelocity.x, 0, attackMoveVelocity.z);
        }

        if (aerialAssistActive)
            UpdateAerialAssistVelocity(Time.inFixedTimeStep ? Time.fixedDeltaTime : Time.deltaTime);
        
        // Handle knockback with wall collision
        if (isKnockbackActive)
        {
            horizontalMovement = HandleKnockbackWithWallCollision();
        }
        
        Vector3 finalVelocity = horizontalMovement + Vector3.up * currentMovement.y;

        if (aerialAssistActive || aerialAssistVelocity.sqrMagnitude > 0.0001f)
            finalVelocity += aerialAssistVelocity;

        // Apply movement using the simulation timestep (FixedUpdate-safe) to avoid dash jitter.
        float movementDeltaTime = Time.inFixedTimeStep ? Time.fixedDeltaTime : Time.deltaTime;
        characterController.Move(finalVelocity * movementDeltaTime);

        // reset vertical movement when grounded
        if (characterController.isGrounded && currentMovement.y < 0)
        {
            DebugMovementLog($"ApplyMovement grounded reset | groundedNow={IsGroundedNow()} ccGrounded={characterController.isGrounded} yBeforeReset={currentMovement.y:F2} fallingAnimationPlaying={fallingAnimationPlaying} airborneAnimationLocked={airborneAnimationLocked}");
            currentMovement.y = -1f; // small negative value to keep the player grounded
            if (isPlunging)
                plungeLandingPending = true;
            isPlunging = false;
            plungeTimer = 0f;
            aerialAttackLockTimer = 0f;
            StopAerialTargetAssist();
        }
    }

    private void HandleAirborneAnimations()
    {
        bool grounded = IsGroundedNow();

        if (pendingJump != PendingJumpType.None)
        {
            wasGrounded = grounded;
            return;
        }

        if (!wasGrounded && grounded)
        {
            DebugMovementLog($"Landing transition detected | wasGrounded={wasGrounded} groundedNow={grounded} ccGrounded={(characterController != null && characterController.isGrounded)} currentY={currentMovement.y:F2} pendingJump={pendingJump}");
            bool landedFromPlunge = plungeLandingPending || isPlunging;
            plungeLandingPending = false;

            if (landedFromPlunge)
                StartPlungeJumpLock();

            if (landedFromPlunge)
                ApplyPlungeLandingSeparation();

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
                DebugMovementLog($"Falling animation trigger | currentY={currentMovement.y:F2} airborneLocked={airborneAnimationLocked} aerialLockTimer={aerialAttackLockTimer:F2} isPlunging={isPlunging} inputBusy={InputReader.inputBusy}");
                if (ShouldUseHighFallAnimation())
                    animationController?.PlayFallingHigh();
                else
                    animationController?.PlayFalling();

                fallingAnimationPlaying = true;
            }
        }

        wasGrounded = grounded;
    }

    private void StartPlungeJumpLock()
    {
        if (plungeJumpLockDuration <= 0f)
        {
            plungeJumpLocked = false;
            return;
        }

        if (plungeJumpLockRoutine != null)
            StopCoroutine(plungeJumpLockRoutine);

        plungeJumpLockRoutine = StartCoroutine(PlungeJumpLockRoutine());
    }

    private void EnsurePlungeRecoveryFailsafe()
    {
        if (!waitingForPlungeRecoveryUnlock)
            return;

        if (characterController == null || !characterController.isGrounded || isPlunging)
        {
            plungeRecoveryBusyTimer = 0f;
            return;
        }

        if (!InputReader.inputBusy)
        {
            waitingForPlungeRecoveryUnlock = false;
            plungeRecoveryBusyTimer = 0f;
            return;
        }

        plungeRecoveryBusyTimer += Time.deltaTime;
        if (plungeRecoveryBusyTimer < plungeRecoveryFailsafeSeconds)
            return;

        attackManager?.ForceCancelCurrentAttack(resetCombo: false);
        animationController?.PlayIdle();
        waitingForPlungeRecoveryUnlock = false;
        plungeRecoveryBusyTimer = 0f;
    }

    private void StartPendingJumpTimeout()
    {
        StopPendingJumpTimeout();

        if (jumpEventTimeout <= 0f)
            return;

        pendingJumpTimeoutRoutine = StartCoroutine(PendingJumpTimeoutRoutine());
    }

    private void StopPendingJumpTimeout()
    {
        if (pendingJumpTimeoutRoutine == null)
            return;

        StopCoroutine(pendingJumpTimeoutRoutine);
        pendingJumpTimeoutRoutine = null;
    }

    private IEnumerator PendingJumpTimeoutRoutine()
    {
        yield return new WaitForSeconds(jumpEventTimeout);
        pendingJumpTimeoutRoutine = null;

        if (pendingJump != PendingJumpType.None)
            HandleAnimationJumpEvent();
    }

    private IEnumerator PlungeJumpLockRoutine()
    {
        plungeJumpLocked = true;
        yield return new WaitForSeconds(plungeJumpLockDuration);
        plungeJumpLocked = false;
        plungeJumpLockRoutine = null;
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
            if (startInJog)
            {
                sprintChargeActive = true;
                moveState = GroundMoveState.Jog;
            }
            else
            {
                sprintChargeActive = false;
                moveState = GroundMoveState.Walk;
            }
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

        if (!locomotionAnimationSuppressed && !IsAttackMovementActive())
            PlayMovementAnimation();
        return true;
    }

    private bool IsAttackMovementActive()
    {
        if (!allowMovementWhileAttacking || !InputReader.inputBusy)
            return false;

        if (attackManager == null || !attackManager.IsAttackInProgress)
        {
            attackMovementSuppressedByHit = false;
            return false;
        }

        if (attackMovementSuppressedByHit)
            return false;

        return true;
    }

    private Vector3 GetAttackConstrainedFacingDirection(Vector3 desiredDirection)
    {
        desiredDirection.y = 0f;
        if (desiredDirection.sqrMagnitude < 0.0001f)
            return transform.forward;

        desiredDirection.Normalize();

        float allowedTurnAngle = maxAttackTurnAngleWhileAttacking;
        if (reduceAttackTurningAfterLandingHit && attackTurnReductionTimer > 0f)
            allowedTurnAngle = Mathf.Min(allowedTurnAngle, Mathf.Max(0f, postHitMaxAttackTurnAngle));

        if (!attackFacingLockInitialized || allowedTurnAngle >= 179.9f)
            return desiredDirection;

        float signedAngle = Vector3.SignedAngle(attackFacingLockForward, desiredDirection, Vector3.up);
        float clampedAngle = Mathf.Clamp(signedAngle, -allowedTurnAngle, allowedTurnAngle);

        Vector3 constrained = Quaternion.AngleAxis(clampedAngle, Vector3.up) * attackFacingLockForward;
        return constrained.normalized;
    }

    private bool TryGetAttackPriorityFacingDirection(Vector3 fallbackMoveDirection, out Vector3 facingDirection)
    {
        facingDirection = Vector3.zero;

        attackLockSystem = attackLockSystem
            ?? GetComponent<AttackLockSystem>()
            ?? GetComponentInChildren<AttackLockSystem>()
            ?? GetComponentInParent<AttackLockSystem>();

        if (attackLockSystem != null && attackLockSystem.TryGetSoftLockAttackFacingDirection(out Vector3 lockDirection))
        {
            facingDirection = GetAttackConstrainedFacingDirection(lockDirection);
            return true;
        }

        if (fallbackMoveDirection.sqrMagnitude > 0.001f)
        {
            facingDirection = GetAttackConstrainedFacingDirection(fallbackMoveDirection);
            return true;
        }

        return false;
    }

    private void HandleAttackHitConfirmed(AttackType attackType, bool hitWasDrone)
    {
        bool ignoreDroneHitForMovementSuppression = false;
        if (hitWasDrone)
        {
            attackLockSystem = attackLockSystem
                ?? GetComponent<AttackLockSystem>()
                ?? GetComponentInChildren<AttackLockSystem>()
                ?? GetComponentInParent<AttackLockSystem>();

            ignoreDroneHitForMovementSuppression = attackLockSystem != null
                && attackLockSystem.ShouldIgnoreDroneHitsForGroundedAttackMovement(attackType);
        }

        if (disableAttackMovementAfterLandedHit)
        {
            bool suppressMovement = false;

            if (attackType == AttackType.LightAOE || attackType == AttackType.HeavyAOE)
            {
                suppressMovement = !keepAttackMovementForAoeAttacks;
            }
            else if (attackType == AttackType.LightSingle || attackType == AttackType.HeavySingle)
            {
                suppressMovement = disableAttackMovementAfterSingleTargetHit;
            }
            else if (attackType == AttackType.LightAerial || attackType == AttackType.HeavyAerial)
            {
                suppressMovement = disableAttackMovementAfterAerialHit;
            }

            if (ignoreDroneHitForMovementSuppression)
                suppressMovement = false;

            if (suppressMovement)
                attackMovementSuppressedByHit = true;
        }

        if (!reduceAttackTurningAfterLandingHit)
            return;

        if (postHitTurnReductionDuration <= 0f)
            return;

        attackTurnReductionTimer = Mathf.Max(attackTurnReductionTimer, postHitTurnReductionDuration);
        if (freezeAttackFacingAnchorAfterHit)
            attackFacingAnchorFrozenByHit = true;
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
        if (characterController != null && IsGroundedNow())
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
            if (startInJog && !allowAnalogWalkWhileStartInJog)
            {
                if (moveState == GroundMoveState.Walk)
                    stateChanged |= TrySetMoveState(GroundMoveState.Jog, force: true);
            }
            else if (inputMagnitude < joystickWalkThreshold)
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
        if (locomotionAnimationSuppressed != suppress)
            DebugMovementLog($"SuppressLocomotionAnimations changed: {locomotionAnimationSuppressed} -> {suppress}");

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

    public bool CanTriggerLauncherFromDash => isDashing && characterController != null && IsGroundedNow();

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
        isDashingFlag = false;
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
        dashCarryVelocity = Vector3.zero;

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
        StopPendingJumpTimeout();

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

    private void PlaySFX(AudioClip clip)
    {
        if (clip == null)
            return;

        SoundManager soundManager = SoundManager.Instance;
        AudioSource source = soundManager != null ? soundManager.voiceSource : null;
        if (source == null)
        {
            if (!warnedMissingSoundSource)
            {
                Debug.LogWarning("[PlayerMovement] SoundManager/voiceSource missing. Movement SFX playback skipped.");
                warnedMissingSoundSource = true;
            }
            return;
        }

        source.PlayOneShot(clip);
    }

    private void PlayMovementAnimation()
    {
        if (locomotionAnimationSuppressed)
        {
            DebugMovementLog("PlayMovementAnimation blocked: locomotionAnimationSuppressed=true");
            return;
        }

        if (animationController == null)
        {
            DebugMovementLog("PlayMovementAnimation blocked: animationController is null");
            return;
        }

        if (characterController != null && (!IsGroundedNow() || airborneAnimationLocked))
        {
            DebugMovementLog($"PlayMovementAnimation blocked: grounded={IsGroundedNow()} airborneAnimationLocked={airborneAnimationLocked} ccGrounded={characterController.isGrounded}");
            return;
        }

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

        DebugMovementLog($"PlayMovementAnimation executed: {moveState}");
    }

    private void DebugStateTransitions()
    {
        if (!verboseMovementDebugLogs)
            return;

        bool grounded = IsGroundedNow();
        bool inputBusy = InputReader.inputBusy;

        if (grounded != debugLastGrounded)
        {
            Debug.Log($"[PlayerMovement][Debug] grounded changed: {debugLastGrounded} -> {grounded} | ccGrounded={(characterController != null && characterController.isGrounded)} | currentY={currentMovement.y:F2}");
            debugLastGrounded = grounded;
        }

        if (inputBusy != debugLastInputBusy)
        {
            Debug.Log($"[PlayerMovement][Debug] inputBusy changed: {debugLastInputBusy} -> {inputBusy}");
            debugLastInputBusy = inputBusy;
        }

        if (airborneAnimationLocked != debugLastAirborneAnimationLocked)
        {
            Debug.Log($"[PlayerMovement][Debug] airborneAnimationLocked changed: {debugLastAirborneAnimationLocked} -> {airborneAnimationLocked}");
            debugLastAirborneAnimationLocked = airborneAnimationLocked;
        }

        if (fallingAnimationPlaying != debugLastFallingAnimationPlaying)
        {
            Debug.Log($"[PlayerMovement][Debug] fallingAnimationPlaying changed: {debugLastFallingAnimationPlaying} -> {fallingAnimationPlaying}");
            debugLastFallingAnimationPlaying = fallingAnimationPlaying;
        }

        if (locomotionAnimationSuppressed != debugLastLocomotionSuppressed)
        {
            Debug.Log($"[PlayerMovement][Debug] locomotionAnimationSuppressed changed: {debugLastLocomotionSuppressed} -> {locomotionAnimationSuppressed}");
            debugLastLocomotionSuppressed = locomotionAnimationSuppressed;
        }

        if (isDashing != debugLastDashing)
        {
            Debug.Log($"[PlayerMovement][Debug] isDashing changed: {debugLastDashing} -> {isDashing}");
            debugLastDashing = isDashing;
        }

        if (pendingJump != debugLastPendingJump)
        {
            Debug.Log($"[PlayerMovement][Debug] pendingJump changed: {debugLastPendingJump} -> {pendingJump}");
            debugLastPendingJump = pendingJump;
        }
    }

    private void DebugMovementLog(string message)
    {
        if (!verboseMovementDebugLogs)
            return;

        Debug.Log($"[PlayerMovement][Debug] {message}");
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
