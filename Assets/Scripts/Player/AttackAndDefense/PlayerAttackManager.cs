using System;
using System.Collections;
using UnityEngine;

using Utilities.Combat;
using Utilities.Combat.Attacks;

public class PlayerAttackManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerAnimationController animationController;
    [SerializeField] private TierComboManager tierComboManager;
    [SerializeField] private AerialComboManager aerialComboManager;
    [SerializeField] private AttackDatabase attackDatabase;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerHealthBarManager playerHealth;
    [SerializeField] private AttackLockSystem attackLockSystem;
    [SerializeField, Tooltip("Registry of VFX anchor points on the player rig.")]
    private PlayerVfxAnchorRegistry vfxAnchorRegistry;

    [Header("Special Attacks")]
    [SerializeField] private PlayerAttack guardAttackOverride;
    [SerializeField] private string guardAttackId = "G_Attack";
    [SerializeField] private PlayerAttack airDashAttackOverride;
    [SerializeField] private string airDashAttackId = "AirDash";
    [SerializeField] private PlayerAttack launcherAttackOverride;
    [SerializeField] private string launcherAttackId = "Launcher";

    [Header("Special Attack Timing")]
    [SerializeField, Range(0f, 0.3f)] private float guardAttackHitboxDelay = 0.08f;
    [SerializeField, Range(0.2f, 1.5f)] private float guardAttackAutoCancelDuration = 0.65f;
    [SerializeField, Range(0.2f, 1.5f)] private float launcherAutoCancelDuration = 0.9f;
    [SerializeField, Tooltip("Auto-generate guard attack hitbox (use only if the animation lacks events).")]
    private bool guardAttackAutoGenerateHitbox = false;

    [Header("Stance Switching")]
    // Stance swapping removed (kept for reference).
    // [SerializeField, Range(0.1f, 5f)] private float stanceCooldownTime = 1f;
    // [SerializeField] private AudioClip changeStanceAudio;

    [Header("Aerial Attack Recovery")]
    [SerializeField, Tooltip("Additional lockout applied after landing a plunge attack.")]
    [Range(0f, 2f)] private float plungeRecoveryDelay = 0.75f;
    [SerializeField, Tooltip("Cross-fade duration when exiting a plunge into combat idle.")]
    [Range(0f, 1f)] private float plungeIdleBlendTime = 0.25f;

    [Header("Plunge Drone Effects")]
    [SerializeField, Tooltip("Enable plunge-specific drone effects (shared splash + optional physics collapse).")]
    private bool enablePlungeDroneEffects = true;
    [SerializeField, Tooltip("Enable simple rigidbody physics collapse for drones killed by plunge.")]
    private bool enablePlungeDronePhysicsCollapse = true;
    [SerializeField, Range(0f, 10f), Tooltip("Shared splash radius around a drone killed by plunge to damage nearby drones.")]
    private float plungeDroneSharedSplashRadius = 2.5f;
    [SerializeField, Tooltip("Layer mask used for nearby drone splash checks.")]
    private LayerMask plungeDroneSplashMask = ~0;
    [SerializeField, Range(0f, 60f), Tooltip("Downward velocity applied when drone collapse starts.")]
    private float plungeDroneCollapseDownwardVelocity = 16f;
    [SerializeField, Range(0f, 30f), Tooltip("Horizontal push applied away from impact center when drone collapse starts.")]
    private float plungeDroneCollapseRadialForce = 6f;

    [Header("Aerial Drone Reposition Assist")]
    [SerializeField, Tooltip("When enabled, light aerial hits on alive drones nudge them away for cleaner follow-up aerial assist.")]
    private bool enableLightAerialDroneReposition = true;
    [SerializeField, Range(0f, 8f), Tooltip("Horizontal displacement applied to drones hit by light aerial attacks.")]
    private float lightAerialDroneRepositionDistance = 1.8f;
    [SerializeField, Range(-1f, 3f), Tooltip("Vertical offset applied when nudging drones on light aerial hit.")]
    private float lightAerialDroneRepositionVerticalOffset = 0.35f;
    [SerializeField, Range(0f, 2f), Tooltip("Only apply light-aerial drone reposition when player is this much below the drone.")]
    private float lightAerialDroneRepositionMinPlayerBelow = 0.1f;
    [SerializeField, Range(0f, 1f), Tooltip("Blend between away-from-player direction (0) and attacker-forward direction (1).")]
    private float lightAerialDroneRepositionForwardBias = 0.45f;

    [Header("Enemy Hit Stagger")]
    [SerializeField, Tooltip("Master toggle: all player attacks stagger enemies on confirmed hit.")]
    private bool staggerEnemiesOnAllAttacks;
    [SerializeField, Tooltip("If master is off, single-target attacks can still stagger.")]
    private bool staggerOnSingleTargetAttacks = true;
    [SerializeField, Tooltip("If master is off, AoE attacks can still stagger.")]
    private bool staggerOnAoeAttacks;
    [SerializeField, Tooltip("If master is off, aerial attacks can still stagger.")]
    private bool staggerOnAerialAttacks = true;
    [SerializeField, Tooltip("Single-target combo part toggle: SX1")]
    private bool staggerOnSx1 = true;
    [SerializeField, Tooltip("Single-target combo part toggle: SX2")]
    private bool staggerOnSx2 = true;
    [SerializeField, Tooltip("Single-target combo part toggle: SX3")]
    private bool staggerOnSx3 = true;
    [SerializeField, Tooltip("Single-target combo part toggle: SX4")]
    private bool staggerOnSx4 = true;
    [SerializeField, Tooltip("Single-target combo part toggle: SX5")]
    private bool staggerOnSx5 = true;
    [SerializeField, Tooltip("Heavy combo part toggle: AY1")]
    private bool staggerOnAy1 = true;
    [SerializeField, Tooltip("Heavy combo part toggle: AY2")]
    private bool staggerOnAy2 = true;
    [SerializeField, Tooltip("Heavy combo part toggle: AY3")]
    private bool staggerOnAy3 = true;
    [SerializeField, Tooltip("Aerial combo part toggle: AC_X1 / ACX1")]
    private bool staggerOnAcX1 = true;
    [SerializeField, Tooltip("Aerial combo part toggle: AC_X2 / ACX2")]
    private bool staggerOnAcX2 = true;
    [SerializeField, Tooltip("Aerial combo part toggle: Plunge / Heavy Aerial finisher")]
    private bool staggerOnPlunge = true;
    [SerializeField, Range(0.05f, 2f), Tooltip("Enemy stagger duration from player hitboxes.")]
    private float enemyStaggerDuration = 0.35f;

    [Header("Attack Forward Move Blocking")]
    [SerializeField, Tooltip("If enabled, attack forward movement is skipped when an alive enemy is too close in front of the player.")]
    private bool blockAttackForwardMoveWhenEnemyNear = true;
    [SerializeField, Tooltip("If enabled, attack forward movement is skipped while soft-lock positional nudge motion is still active to prevent bounce-back.")]
    private bool blockAttackForwardMoveDuringSoftLockMotion = true;
    [SerializeField, Range(0f, 5f), Tooltip("Distance threshold to block attack forward movement when an enemy is in front.")]
    private float attackForwardMoveBlockDistance = 1.25f;
    [SerializeField, Range(0f, 180f), Tooltip("Front cone angle used for attack forward-move blocking.")]
    private float attackForwardMoveBlockAngle = 80f;

    [Header("Debug")]
    [SerializeField, Tooltip("When enabled, logs plunge hitbox damage (base x multiplier) when the hitbox spawns, even if no enemy is hit.")]
    private bool debugPlungeDamage = true;
    [SerializeField, HideInInspector] private bool debugPlungeDamageInitialized;

    [Header("Audio")]
    [SerializeField, Tooltip("Primary AudioSource for attack and stance SFX. Defaults to SoundManager's SFX source when unset.")]
    private AudioSource attackAudioSource;
    private AudioSource fallbackSfxSource;
    private bool hasLoggedMissingAttackAudioSource;
    // private Coroutine stanceCooldownRoutine;
    private GameObject activeHitbox;
    private PlayerAttack currentAttack;
    private Coroutine hitboxLifetimeRoutine;
    private Coroutine plungeRecoveryRoutine;
    private Coroutine guardAttackFlowRoutine;
    private Coroutine specialAttackAutoCancelRoutine;
    private Coroutine heavyMoveRoutine;
    private bool lastAttackWasAoe;
    private float currentAttackDamageMultiplier = 1f;
    private readonly Collider[] forwardMoveBlockHits = new Collider[24];

    public bool IsAttackInProgress => currentAttack != null;

    [Header("Input Buffering")]
    [SerializeField, Range(0.05f, 0.6f)] private float inputBufferWindow = 0.25f;

    [Header("Attack Speed")]
    [SerializeField, Range(0.1f, 3f), Tooltip("Global speed multiplier applied to all player attacks.")]
    private float globalAttackSpeedMultiplier = 1f;

    [Header("Single Target Light (SX)")]
    [SerializeField, Range(0.1f, 3f)] private float sx1SpeedMultiplier = 1f;
    [SerializeField, Range(0.1f, 3f)] private float sx2SpeedMultiplier = 1f;
    [SerializeField, Range(0.1f, 3f)] private float sx3SpeedMultiplier = 1f;
    [SerializeField, Range(0.1f, 3f)] private float sx4SpeedMultiplier = 1f;
    [SerializeField, Range(0.1f, 3f)] private float sx5SpeedMultiplier = 1f;

    [Header("Heavy (AY)")]
    [SerializeField, Range(0.1f, 3f)] private float ay1SpeedMultiplier = 1f;
    [SerializeField, Range(0.1f, 3f)] private float ay2SpeedMultiplier = 1f;
    [SerializeField, Range(0.1f, 3f)] private float ay3SpeedMultiplier = 1f;

    [Header("Aerial Combo (AC)")]
    [SerializeField, Range(0.1f, 3f)] private float acX1SpeedMultiplier = 1f;
    [SerializeField, Range(0.1f, 3f)] private float acX2SpeedMultiplier = 1f;
    [SerializeField, Range(0.1f, 3f)] private float plungeSpeedMultiplier = 1f;
    [SerializeField, Range(0.1f, 3f)] private float airDashAttackSpeedMultiplier = 1f;

    [Header("Special / Guard")]
    [SerializeField, Range(0.1f, 3f)] private float launcherSpeedMultiplier = 1f;
    [SerializeField, Range(0.1f, 3f)] private float guardAttackSpeedMultiplier = 1f;

    [Header("Legacy / Optional (AX)")]
    [SerializeField, Range(0.1f, 3f)] private float ax1SpeedMultiplier = 1f;
    [SerializeField, Range(0.1f, 3f)] private float ax2SpeedMultiplier = 1f;
    [SerializeField, Range(0.1f, 3f)] private float ax3SpeedMultiplier = 1f;
    [SerializeField, Range(0.1f, 3f)] private float ax4SpeedMultiplier = 1f;

    private enum AttackButton { None, Light, Heavy }
    private AttackButton bufferedAttackButton = AttackButton.None;
    private float bufferedAttackExpiresAt = -1f;
    private float currentAttackSpeedMultiplier = 1f;

    public static event Action<PlayerAttack> OnAttack;

    // New, more explicit events for tutorial / listeners that need to know which attack-type was used:
    public static event Action<PlayerAttack> OnSingleAttack;
    public static event Action<PlayerAttack> OnAoeAttack;

    private void Awake()
    {
        EnsureDebugDefaultsApplied();

        animationController ??= GetComponent<PlayerAnimationController>() ?? GetComponentInChildren<PlayerAnimationController>();
        tierComboManager ??= GetComponent<TierComboManager>() ?? GetComponentInChildren<TierComboManager>() ?? GetComponentInParent<TierComboManager>();
        aerialComboManager ??= GetComponent<AerialComboManager>() ?? GetComponentInChildren<AerialComboManager>() ?? GetComponentInParent<AerialComboManager>();
        characterController ??= GetComponent<CharacterController>();
        playerMovement ??= GetComponent<PlayerMovement>() ?? GetComponentInChildren<PlayerMovement>() ?? GetComponentInParent<PlayerMovement>();
        playerHealth ??= GetComponent<PlayerHealthBarManager>() ?? GetComponentInChildren<PlayerHealthBarManager>() ?? GetComponentInParent<PlayerHealthBarManager>();
        attackLockSystem ??= GetComponent<AttackLockSystem>() ?? GetComponentInChildren<AttackLockSystem>() ?? GetComponentInParent<AttackLockSystem>();
        vfxAnchorRegistry ??= GetComponent<PlayerVfxAnchorRegistry>() ?? GetComponentInChildren<PlayerVfxAnchorRegistry>() ?? GetComponentInParent<PlayerVfxAnchorRegistry>();

        if (attackDatabase == null)
        {
            attackDatabase = Resources.Load<AttackDatabase>("AttackDatabase");
            if (attackDatabase == null)
                Debug.LogWarning("[PlayerAttackManager] AttackDatabase reference is missing.");
        }
    }

    private void OnValidate()
    {
        EnsureDebugDefaultsApplied();
    }

    private void EnsureDebugDefaultsApplied()
    {
        if (debugPlungeDamageInitialized)
            return;

        debugPlungeDamage = true;
        debugPlungeDamageInitialized = true;
    }

    private void Start()
    {
        fallbackSfxSource = SoundManager.Instance != null
            ? (SoundManager.Instance.sfxSource != null ? SoundManager.Instance.sfxSource : SoundManager.Instance.voiceSource)
            : null;

        if (attackAudioSource == null)
        {
            attackAudioSource = fallbackSfxSource;
        }

    }

    private void OnDisable()
    {
        // Stance switching removed.
        // if (stanceCooldownRoutine != null)
        // {
        //     StopCoroutine(stanceCooldownRoutine);
        //     stanceCooldownRoutine = null;
        // }
        if (hitboxLifetimeRoutine != null)
        {
            StopCoroutine(hitboxLifetimeRoutine);
            hitboxLifetimeRoutine = null;
        }
        if (plungeRecoveryRoutine != null)
        {
            StopCoroutine(plungeRecoveryRoutine);
            plungeRecoveryRoutine = null;
        }
        if (heavyMoveRoutine != null)
        {
            StopCoroutine(heavyMoveRoutine);
            heavyMoveRoutine = null;
        }
        StopGuardAttackFlowRoutine();
        StopSpecialAttackAutoCancelRoutine();

        ClearHitbox();
        currentAttackSpeedMultiplier = 1f;
        animationController?.ResetAnimatorSpeed();
        InputReader.inputBusy = false;
    }

    private void Update()
    {
        EnsureAttackStateConsistency();

        if (ShouldIgnoreAttackInput())
        {
            ClearBufferedAttack();
            return;
        }

        if (InputReader.LightAttackTriggered)
            ProcessAttackInput(true);

        if (InputReader.HeavyAttackTriggered)
        {
            if (TryExecuteDashLauncherAttack())
                return;

            ProcessAttackInput(false);
        }

        // Stance swapping removed.
        // if (InputReader.ChangeStanceTriggered)
        //     TryChangeStance();
    }

    private void EnsureAttackStateConsistency()
    {
        if (currentAttack == null)
        {
            if (InputReader.inputBusy && !CombatManager.isGuarding && !CombatManager.isParrying)
            {
                Debug.LogWarning("[PlayerAttackManager] Recovered orphaned inputBusy state with no active attack.");
                InputReader.inputBusy = false;
                animationController?.ResetAnimatorSpeed();
                playerMovement?.SuppressLocomotionAnimations(false);
                playerMovement?.ForceLocomotionRefresh();
            }

            return;
        }

        if (InputReader.inputBusy)
            return;

        if (plungeRecoveryRoutine != null)
            return;

        Debug.LogWarning("[PlayerAttackManager] Recovered stale attack state while input was unlocked.");
        currentAttack = null;
        currentAttackDamageMultiplier = 1f;
        currentAttackSpeedMultiplier = 1f;
        animationController?.ResetAnimatorSpeed();
        playerMovement?.SuppressLocomotionAnimations(false);
        playerMovement?.ForceLocomotionRefresh();
    }

    private void HandleGuardStateAttacks()
    {
        // Guard attacks are disabled. Kept for backwards compatibility if referenced elsewhere.
        ClearBufferedAttack();
    }

    private void ProcessAttackInput(bool lightAttack)
    {
        if (ShouldIgnoreAttackInput())
        {
            ClearBufferedAttack();
            return;
        }

        if (!InputReader.inputBusy)
        {
            if (lightAttack)
                OnLightAttack();
            else
                OnHeavyAttack();
        }
        else
        {
            BufferAttack(lightAttack);
        }
    }

    private void TryExecuteGuardAttack()
    {
        PlayerAttack guardAttack = ResolveGuardAttack();
        if (guardAttack == null)
        {
            Debug.LogWarning("[PlayerAttackManager] Guard attack data missing. Assign GuardAttackOverride or ensure ID exists in AttackDatabase.");
            return;
        }

        ExecuteAttack(
            guardAttack,
            guardAttack.attackId,
            controller => controller?.PlayGuardAttack()
        );

        StartGuardAttackFlow(guardAttack);
        float guardAutoCancel = GetAnimationDurationOrFallback(guardAttack, guardAttackAutoCancelDuration);
        ScheduleSpecialAttackAutoCancel(ScaleDurationForAttack(guardAutoCancel, guardAttack));
    }

    private bool TryExecuteDashLauncherAttack()
    {
        if (playerMovement == null)
            return false;

        if (!playerMovement.CanTriggerLauncherFromDash)
            return false;

        PlayerAttack launcherAttack = ResolveLauncherAttack();
        if (launcherAttack == null)
        {
            Debug.LogWarning("[PlayerAttackManager] Launcher attack data missing. Assign LauncherAttackOverride or ensure ID exists in AttackDatabase.");
            return false;
        }

        if (!playerMovement.TryTriggerLauncherJump())
            return false;

        ExecuteAttack(
            launcherAttack,
            launcherAttack.attackId,
            controller => controller?.PlayLauncher()
        );

        float launcherAutoCancel = GetAnimationDurationOrFallback(launcherAttack, launcherAutoCancelDuration);
        ScheduleSpecialAttackAutoCancel(ScaleDurationForAttack(launcherAutoCancel, launcherAttack));

        return true;
    }

    // private void TryChangeStance()
    // {
    //     if (stanceCooldownRoutine != null)
    //         return;

    //     CombatManager.ChangeStance();

    //     PlaySfx(changeStanceAudio);

    //     stanceCooldownRoutine = StartCoroutine(StanceChangeCooldown());
    // }

    // private IEnumerator StanceChangeCooldown()
    // {
    //     yield return new WaitForSeconds(stanceCooldownTime);
    //     stanceCooldownRoutine = null;
    // }

    public void OnLightAttack()
    {
        if (CombatManager.isGuarding)
            return;

        if (InputReader.inputBusy)
            return;

        AttemptAttack(true);
    }

    public void OnHeavyAttack()
    {
        if (CombatManager.isGuarding)
            return;

        if (InputReader.inputBusy)
            return;

        AttemptAttack(false);
    }

    public void TriggerAirDashAttack()
    {
        PlayerAttack airDashAttack = ResolveAirDashAttack();
        if (airDashAttack == null)
        {
            Debug.LogWarning("[PlayerAttackManager] Air dash attack data missing. Assign AirDashAttackOverride or ensure ID exists in AttackDatabase.");
            return;
        }

        OnAttack?.Invoke(airDashAttack);
        TriggerHitboxWindow(airDashAttack, airDashAttack.hitboxDuration);
    }

    private void BufferAttack(bool lightAttack)
    {
        bufferedAttackButton = lightAttack ? AttackButton.Light : AttackButton.Heavy;
        bufferedAttackExpiresAt = Time.time + inputBufferWindow;
    }

    private void AttemptAttack(bool lightAttack)
    {
        tierComboManager?.CancelComboResetCountdown();

        PlayerAttack attackData;
        string attackId;

        if (IsGrounded())
        {
            attackId = ResolveGroundAttackId(lightAttack);
            if (string.IsNullOrEmpty(attackId))
                return;

            attackData = attackDatabase != null ? attackDatabase.GetAttack(attackId) : null;
            if (attackData == null)
            {
                Debug.LogWarning($"[PlayerAttackManager] Attack '{attackId}' not found in database.");
                return;
            }
        }
        else
        {
            if (playerMovement != null && !playerMovement.CanStartAerialCombat())
            {
                // Prevent "barely off the ground" aerial attacks. Attacks are only allowed
                // on ground, or when sufficiently high to commit to aerial combat.
                return;
            }

            attackData = ResolveAerialAttack(lightAttack);
            attackId = attackData != null ? attackData.attackId : null;

            if (attackData == null || string.IsNullOrEmpty(attackId))
            {
                Debug.LogWarning("[PlayerAttackManager] Failed to resolve aerial attack data.");
                return;
            }
        }

        ExecuteAttack(attackData, attackId);
    }

    private string ResolveGroundAttackId(bool lightAttack)
    {
        if (tierComboManager == null)
        {
            Debug.LogWarning("[PlayerAttackManager] TierComboManager missing; cannot resolve grounded attack.");
            return null;
        }

        TierComboManager.AttackStance stance = lightAttack
            ? TierComboManager.AttackStance.Single
            : TierComboManager.AttackStance.AOE;

        return lightAttack
            ? tierComboManager.RequestFastAttack(stance)
            : tierComboManager.RequestHeavyAttack(stance);
    }

    private PlayerAttack ResolveAerialAttack(bool lightAttack)
    {
        if (aerialComboManager == null)
        {
            Debug.LogWarning("[PlayerAttackManager] AerialComboManager missing; cannot resolve aerial attack.");
            return null;
        }

        return lightAttack
            ? aerialComboManager.RequestAerialLightAttack()
            : aerialComboManager.RequestAerialHeavyAttack();
    }

    private PlayerAttack ResolveGuardAttack()
    {
        if (guardAttackOverride != null)
            return guardAttackOverride;

        if (attackDatabase == null || string.IsNullOrWhiteSpace(guardAttackId))
            return null;

        return attackDatabase.GetAttack(guardAttackId);
    }

    private PlayerAttack ResolveAirDashAttack()
    {
        if (airDashAttackOverride != null)
            return airDashAttackOverride;

        if (attackDatabase == null || string.IsNullOrWhiteSpace(airDashAttackId))
            return null;

        return attackDatabase.GetAttack(airDashAttackId);
    }

    private PlayerAttack ResolveLauncherAttack()
    {
        if (launcherAttackOverride != null)
            return launcherAttackOverride;

        if (attackDatabase == null || string.IsNullOrWhiteSpace(launcherAttackId))
            return null;

        return attackDatabase.GetAttack(launcherAttackId);
    }

    private void ExecuteAttack(
        PlayerAttack attackData,
        string attackId,
        Action<PlayerAnimationController> animationOverride = null,
        bool playDefaultAnimation = true)
    {
        currentAttack = attackData;
        currentAttackDamageMultiplier = 1f;
        currentAttackSpeedMultiplier = ResolveAttackSpeedMultiplier(attackData, attackId);

        if (currentAttack != null
            && currentAttack.attackType == AttackType.HeavyAerial
            && aerialComboManager != null)
        {
            currentAttackDamageMultiplier = Mathf.Max(1f, aerialComboManager.ConsumePendingPlungeDamageMultiplier());

            if (debugPlungeDamage)
            {
                float scaledDamage = currentAttack.damage * currentAttackDamageMultiplier;
                Debug.Log(
                    $"[PlayerAttackManager] Plunge execute: '{currentAttack.attackId}' base={currentAttack.damage} x{currentAttackDamageMultiplier:0.##} => {scaledDamage:0.##}"
                );
            }
        }
        InputReader.inputBusy = true;
        animationController?.SetAnimatorSpeed(currentAttackSpeedMultiplier);

        UpdateLastAttackType(attackData);

        PlaySfx(attackData.attackSFX);

        if (animationOverride != null)
            animationOverride(animationController);
        else if (playDefaultAnimation)
            animationController?.PlayAttack(attackId);

        OnAttack?.Invoke(attackData);

        // Fire more specific events to make it trivial for listeners (like TutorialHandler)
        // to react to single-target vs aoe attacks without extra filtering.
        if (attackData != null)
        {
            switch (attackData.attackType)
            {
                case AttackType.LightSingle:
                    OnSingleAttack?.Invoke(attackData);
                    break;
                case AttackType.HeavyAOE:
                    OnAoeAttack?.Invoke(attackData);
                    break;
                default:
                    // No specific event for other attack types yet, but log them for visibility.
                    Debug.Log($"[PlayerAttackManager] No specific event for attack type {attackData.attackType} on '{attackData.attackName}' ({attackId})");
                    break;

            }
        }

        Debug.Log($"[PlayerAttackManager] Executing attack {attackData.attackName} ({attackId})");
    }

    private void ApplyAttackForwardMove(PlayerAttack attackData)
    {
        if (attackData == null)
            return;

        float distance = attackData.forwardMoveDistance;
        if (distance <= 0f)
            return;

        if (ShouldBlockAttackForwardMove())
            return;

        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.0001f)
            return;
        forward.Normalize();

        float duration = attackData.forwardMoveDuration;
        duration = ScaleDurationForAttack(duration, attackData);
        if (playerMovement != null)
        {
            playerMovement.StartAttackForwardMove(forward, distance, duration);
            return;
        }

        if (duration <= 0f)
        {
            MoveXZ(forward * distance);
            return;
        }

        if (heavyMoveRoutine != null)
            StopCoroutine(heavyMoveRoutine);

        heavyMoveRoutine = StartCoroutine(HeavyAttackForwardMoveRoutine(forward, distance, duration));
    }

    private bool ShouldBlockAttackForwardMove()
    {
        if (blockAttackForwardMoveDuringSoftLockMotion && attackLockSystem != null && attackLockSystem.IsSoftLockMotionInProgress)
            return true;

        if (!blockAttackForwardMoveWhenEnemyNear)
            return false;

        float threshold = Mathf.Max(0f, attackForwardMoveBlockDistance);
        if (threshold <= 0f)
            return false;

        Vector3 origin = transform.position + Vector3.up * 0.5f;
        int hitCount = Physics.OverlapSphereNonAlloc(
            origin,
            threshold,
            forwardMoveBlockHits,
            Physics.AllLayers,
            QueryTriggerInteraction.Ignore);

        Vector3 playerForward = transform.forward;
        playerForward.y = 0f;
        if (playerForward.sqrMagnitude > 0.0001f)
            playerForward.Normalize();
        else
            playerForward = Vector3.forward;

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = forwardMoveBlockHits[i];
            if (col == null)
                continue;

            BaseEnemyCore enemy = col.GetComponentInParent<BaseEnemyCore>();
            if (enemy == null || !enemy.isAlive)
                continue;

            Vector3 toEnemy = enemy.transform.position - transform.position;
            toEnemy.y = 0f;
            if (toEnemy.sqrMagnitude <= 0.0001f)
                return true;

            float angle = Vector3.Angle(playerForward, toEnemy.normalized);
            if (angle <= attackForwardMoveBlockAngle)
                return true;
        }

        return false;
    }

    private IEnumerator HeavyAttackForwardMoveRoutine(Vector3 forward, float distance, float duration)
    {
        float elapsed = 0f;
        Vector3 totalMove = forward * distance;
        Vector3 moved = Vector3.zero;

        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            Vector3 target = totalMove * t;
            Vector3 delta = target - moved;
            if (delta.sqrMagnitude > 0f)
            {
                MoveXZ(delta);
                moved += delta;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        Vector3 finalDelta = totalMove - moved;
        if (finalDelta.sqrMagnitude > 0f)
            MoveXZ(finalDelta);

        heavyMoveRoutine = null;
    }

    private void MoveXZ(Vector3 delta)
    {
        Vector3 planarDelta = new Vector3(delta.x, 0f, delta.z);
        if (planarDelta.sqrMagnitude <= 0f)
            return;

        if (characterController != null)
            characterController.Move(planarDelta);
        else
            transform.position += planarDelta;
    }

    private bool IsGrounded()
    {
        if (characterController != null)
            return characterController.isGrounded;

        return PlayerMovement.isGrounded;
    }

    private void PlaySfx(AudioClip clip)
    {
        if (clip == null)
        {
            Debug.LogWarning("[PlayerAttackManager] PlaySfx called with null AudioClip.");
            return;
        }

        var source = attackAudioSource != null ? attackAudioSource : fallbackSfxSource;

        if (source == null)
        {
            SoundManager soundManager = SoundManager.Instance;
            if (soundManager != null)
            {
                source = soundManager.sfxSource != null ? soundManager.sfxSource : soundManager.voiceSource;
                if (source != null)
                {
                    attackAudioSource = source;
                    fallbackSfxSource = source;
                }
            }
        }

        if (source == null)
        {
            source = GetComponent<AudioSource>();
            if (source == null)
                source = GetComponentInChildren<AudioSource>();
        }

        if (source == null)
        {
            if (!hasLoggedMissingAttackAudioSource)
            {
                hasLoggedMissingAttackAudioSource = true;
                if (!PlayerMovement.IsTestingOrDebugMode)
                    Debug.LogWarning("[PlayerAttackManager] No AudioSource available for attack SFX. Assign attackAudioSource or configure SoundManager.sfxSource.");
            }
            return;
        }

        hasLoggedMissingAttackAudioSource = false;

        source.PlayOneShot(clip);
        Debug.Log($"[PlayerAttackManager] Playing SFX: {clip.name} on {source.gameObject.name}");
    }

    private void ClearHitbox()
    {
        if (hitboxLifetimeRoutine != null)
        {
            StopCoroutine(hitboxLifetimeRoutine);
            hitboxLifetimeRoutine = null;
        }

        if (activeHitbox != null)
        {
            Destroy(activeHitbox);
            activeHitbox = null;
        }
    }

    private void SpawnHitbox(PlayerAttack attack)
    {
        ClearHitbox();

        attack.GetHitboxPose(transform.position, transform.forward, out var spawnPosition, out var spawnRotation);
        activeHitbox = attack.CreateHitBoxAt(spawnPosition, spawnRotation);
        if (activeHitbox != null)
            activeHitbox.transform.SetParent(transform, worldPositionStays: true);

        if (activeHitbox != null
            && currentAttack != null
            && ReferenceEquals(attack, currentAttack)
            && activeHitbox.TryGetComponent<HitboxDamageManager>(out var damageManager))
        {
            bool isHeavyAerial = currentAttack.attackType == AttackType.HeavyAerial;
            bool isLightAerial = currentAttack.attackType == AttackType.LightAerial;

            if (isHeavyAerial)
            {
                float multiplier = Mathf.Max(1f, currentAttackDamageMultiplier);
                float scaledDamage = currentAttack.damage * multiplier;
                damageManager.Configure(currentAttack.attackName, scaledDamage, currentAttack.maxTargetsPerActivation);
                damageManager.ConfigurePlungeDroneEffects(
                    enablePlungeDroneEffects,
                    enablePlungeDronePhysicsCollapse,
                    plungeDroneSharedSplashRadius,
                    plungeDroneSplashMask,
                    plungeDroneCollapseDownwardVelocity,
                    plungeDroneCollapseRadialForce);

                if (debugPlungeDamage)
                {
                    Debug.Log(
                        $"[PlayerAttackManager] Plunge hitbox spawned: '{currentAttack.attackId}' base={currentAttack.damage} x{multiplier:0.##} => {scaledDamage:0.##}"
                    );
                }
            }

            damageManager.ConfigureAerialDroneReposition(
                isLightAerial,
                transform.position,
                transform.forward);
            damageManager.ConfigureAerialDroneRepositionSettings(
                enableLightAerialDroneReposition,
                lightAerialDroneRepositionDistance,
                lightAerialDroneRepositionVerticalOffset,
                lightAerialDroneRepositionMinPlayerBelow,
                lightAerialDroneRepositionForwardBias);
            damageManager.ConfigureAttackType(currentAttack.attackType);
            damageManager.ConfigureAttackId(currentAttack.attackId);
            damageManager.ConfigureAttackerContext(transform.position);
            damageManager.ConfigureEnemyHitStaggerSettings(
                staggerEnemiesOnAllAttacks,
                staggerOnSingleTargetAttacks,
                staggerOnAoeAttacks,
                staggerOnAerialAttacks,
                staggerOnSx1,
                staggerOnSx2,
                staggerOnSx3,
                staggerOnSx4,
                staggerOnSx5,
                staggerOnAy1,
                staggerOnAy2,
                staggerOnAy3,
                staggerOnAcX1,
                staggerOnAcX2,
                staggerOnPlunge,
                enemyStaggerDuration);
        }

        PlayAttackVfx(attack, spawnPosition, spawnRotation);
    }

    public void SpawnOneShotHitbox(string attackId, float activeDuration)
    {
        var attackData = attackDatabase?.GetAttack(attackId);
        if (attackData == null)
        {
            Debug.LogWarning($"[PlayerAttackManager] Cannot spawn hitbox; attack '{attackId}' missing.");
            return;
        }

        TriggerHitboxWindow(attackData, Mathf.Max(0f, activeDuration));
    }

    private void PlayAttackVfx(PlayerAttack attack, Vector3 fallbackPosition, Quaternion fallbackRotation)
    {
        if (attack == null)
            return;

        bool spawnedAny = false;
        foreach (var entry in attack.EnumerateAllVfx())
        {
            if (entry.Prefab == null)
                continue;

            spawnedAny = true;
            SpawnVfxInstance(entry, fallbackPosition, fallbackRotation);
        }

        if (!spawnedAny && attack.hitVfxPrefab != null)
        {
            // Defensive fallback for legacy data if enumeration filtered everything out
            SpawnVfxInstance(new PlayerAttack.VfxEntry(attack.hitVfxPrefab, attack.vfxAnchorId, attack.vfxLifetime), fallbackPosition, fallbackRotation);
        }
    }

    private void SpawnVfxInstance(PlayerAttack.VfxEntry entry, Vector3 fallbackPosition, Quaternion fallbackRotation)
    {
        if (entry.Prefab == null)
            return;

        Transform anchor = vfxAnchorRegistry != null && !string.IsNullOrEmpty(entry.AnchorId)
            ? vfxAnchorRegistry.ResolveAnchor(entry.AnchorId)
            : null;

        Vector3 spawnPosition = anchor != null ? anchor.position : fallbackPosition;
        Quaternion spawnRotation = anchor != null ? anchor.rotation : fallbackRotation;

        GameObject vfxInstance = Instantiate(entry.Prefab, spawnPosition, spawnRotation);
        if (anchor != null)
            vfxInstance.transform.SetParent(anchor, worldPositionStays: true);
        else
            vfxInstance.transform.SetParent(transform, worldPositionStays: true);

        float lifetime = entry.Lifetime;
        if (lifetime >= 0f)
        {
            if (lifetime == 0f)
                Destroy(vfxInstance);
            else
                Destroy(vfxInstance, lifetime);
        }
    }

    private void TriggerHitboxWindow(PlayerAttack attack, float overrideDuration)
    {
        if (attack == null)
            return;

        SpawnHitbox(attack);

        float lifetime = overrideDuration >= 0f
            ? overrideDuration
            : attack.hitboxDuration;

        lifetime = ScaleDurationForAttack(lifetime, attack);

        BeginHitboxLifetime(lifetime);
    }

    private void StartGuardAttackFlow(PlayerAttack guardAttack)
    {
        if (!guardAttackAutoGenerateHitbox || guardAttack == null)
            return;

        StopGuardAttackFlowRoutine();
        guardAttackFlowRoutine = StartCoroutine(GuardAttackFlowRoutine(guardAttack));
    }

    private IEnumerator GuardAttackFlowRoutine(PlayerAttack guardAttack)
    {
        float delay = Mathf.Max(0f, guardAttackHitboxDelay);
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        TriggerHitboxWindow(guardAttack, guardAttack.hitboxDuration);
        guardAttackFlowRoutine = null;
    }

    private void StopGuardAttackFlowRoutine()
    {
        if (guardAttackFlowRoutine == null)
            return;

        StopCoroutine(guardAttackFlowRoutine);
        guardAttackFlowRoutine = null;
    }

    private void ScheduleSpecialAttackAutoCancel(float duration)
    {
        StopSpecialAttackAutoCancelRoutine();

        if (duration <= 0f)
            return;

        specialAttackAutoCancelRoutine = StartCoroutine(SpecialAttackAutoCancelRoutine(duration));
    }

    private IEnumerator SpecialAttackAutoCancelRoutine(float duration)
    {
        yield return new WaitForSeconds(duration);
        specialAttackAutoCancelRoutine = null;

        if (currentAttack != null)
            CompleteCancelWindow();
    }

    private void StopSpecialAttackAutoCancelRoutine()
    {
        if (specialAttackAutoCancelRoutine == null)
            return;

        StopCoroutine(specialAttackAutoCancelRoutine);
        specialAttackAutoCancelRoutine = null;
    }

    private static float GetAnimationDurationOrFallback(PlayerAttack attack, float fallback)
    {
        if (attack != null && attack.animationClip != null)
            return Mathf.Max(0.05f, attack.animationClip.length);

        return Mathf.Max(0f, fallback);
    }

    private void BeginHitboxLifetime(float duration)
    {
        if (duration <= 0f)
        {
            ClearHitbox();
            return;
        }

        if (hitboxLifetimeRoutine != null)
            StopCoroutine(hitboxLifetimeRoutine);

        hitboxLifetimeRoutine = StartCoroutine(HitboxLifetimeRoutine(duration));
    }

    private IEnumerator HitboxLifetimeRoutine(float duration)
    {
        yield return new WaitForSeconds(Mathf.Max(0f, duration));
        hitboxLifetimeRoutine = null;
        ClearHitbox();
    }

    #region Animation Event Hooks
    public void HandleAnimationHitbox()
    {
        HandleAnimationHitbox(-1f);
    }

    public void HandleAnimationHitbox(float overrideDuration)
    {
        if (currentAttack == null)
        {
            Debug.LogWarning("[PlayerAttackManager] Animation requested a hitbox but no attack is active.");
            return;
        }

        TriggerHitboxWindow(currentAttack, overrideDuration);
    }

    public void HandleAnimationCancelWindow()
    {
        StopSpecialAttackAutoCancelRoutine();
        bool needsPlungeRecovery = currentAttack != null
            && currentAttack.attackType == AttackType.HeavyAerial
            && plungeRecoveryDelay > 0f;

        if (needsPlungeRecovery)
        {
            if (plungeRecoveryRoutine != null)
                StopCoroutine(plungeRecoveryRoutine);

            float delay = ScaleDurationByCurrentAttackSpeed(plungeRecoveryDelay);
            plungeRecoveryRoutine = StartCoroutine(PlungeRecoveryRoutine(delay));
        }
        else
        {
            CompleteCancelWindow();
        }
    }

    private IEnumerator PlungeRecoveryRoutine(float delay)
    {
        yield return new WaitForSeconds(Mathf.Max(0f, delay));
        plungeRecoveryRoutine = null;
        CompleteCancelWindow();
    }

    private void CompleteCancelWindow()
    {
        InputReader.inputBusy = false;
        playerMovement?.SuppressLocomotionAnimations(false);
        playerMovement?.ForceLocomotionRefresh();

        var finishedAttack = currentAttack;

        if (finishedAttack != null)
        {
            aerialComboManager?.HandleAttackAnimationComplete(finishedAttack);

            bool shouldReturnToCombatIdle = finishedAttack.attackType == AttackType.HeavyAerial
                && characterController != null
                && characterController.isGrounded;

            if (shouldReturnToCombatIdle)
                PlayCombatIdle(plungeIdleBlendTime);
        }

        currentAttack = null;
        currentAttackDamageMultiplier = 1f;
        currentAttackSpeedMultiplier = 1f;
        animationController?.ResetAnimatorSpeed();

        if (TryConsumeBufferedAttack())
            return;

        tierComboManager?.StartComboResetCountdown();
    }

    public void HandleAnimationMoveForward()
    {
        if (currentAttack == null)
        {
            Debug.LogWarning("[PlayerAttackManager] Animation requested a forward move but no attack is active.");
            return;
        }

        ApplyAttackForwardMove(currentAttack);
    }

    private void PlayCombatIdle(float transition)
    {
        if (animationController == null)
            return;

        if (lastAttackWasAoe)
            animationController.PlayAoeIdleCombat(transition);
        else
            animationController.PlaySingleTargetIdleCombat(transition);
    }

    private void UpdateLastAttackType(PlayerAttack attackData)
    {
        if (attackData == null)
            return;

        switch (attackData.attackType)
        {
            case AttackType.LightSingle:
            case AttackType.HeavySingle:
                lastAttackWasAoe = false;
                break;
            case AttackType.HeavyAOE:
                lastAttackWasAoe = true;
                break;
        }
    }

    private bool TryConsumeBufferedAttack()
    {
        if (ShouldIgnoreAttackInput())
        {
            ClearBufferedAttack();
            return false;
        }

        if (bufferedAttackButton == AttackButton.None)
            return false;

        if (Time.time > bufferedAttackExpiresAt)
        {
            bufferedAttackButton = AttackButton.None;
            bufferedAttackExpiresAt = -1f;
            return false;
        }

        bool lightAttack = bufferedAttackButton == AttackButton.Light;
        bufferedAttackButton = AttackButton.None;
        bufferedAttackExpiresAt = -1f;

        AttemptAttack(lightAttack);
        return true;
    }

    private void ClearBufferedAttack()
    {
        bufferedAttackButton = AttackButton.None;
        bufferedAttackExpiresAt = -1f;
    }

    private bool ShouldIgnoreAttackInput()
    {
        if (playerHealth != null && playerHealth.IsDead)
            return true;

        if (InputReader.IsGameplayInputBlocked)
            return true;

        if (CombatManager.isGuarding || CombatManager.isParrying)
            return true;

        return animationController != null && animationController.IsParryHardLocked;
    }

    public void ForceCancelCurrentAttack(bool resetCombo = true)
    {
        if (plungeRecoveryRoutine != null)
        {
            StopCoroutine(plungeRecoveryRoutine);
            plungeRecoveryRoutine = null;
        }

        ClearBufferedAttack();
        StopGuardAttackFlowRoutine();
        StopSpecialAttackAutoCancelRoutine();
        ClearHitbox();
        playerMovement?.CancelPlungeState();
        currentAttack = null;
        currentAttackDamageMultiplier = 1f;
        currentAttackSpeedMultiplier = 1f;
        animationController?.ResetAnimatorSpeed();
        InputReader.inputBusy = false;
        playerMovement?.SuppressLocomotionAnimations(false);
        playerMovement?.ForceLocomotionRefresh();

        if (resetCombo)
        {
            tierComboManager?.ResetCombo();
        }
        else
        {
            tierComboManager?.CancelComboResetCountdown();
        }
    }

    private float ResolveAttackSpeedMultiplier(PlayerAttack attackData, string attackId)
    {
        float resolved = Mathf.Max(0.1f, globalAttackSpeedMultiplier);
        string id = !string.IsNullOrWhiteSpace(attackId)
            ? attackId
            : attackData != null ? attackData.attackId : null;

        if (string.IsNullOrWhiteSpace(id))
            return resolved;

        string normalized = id.Trim().ToUpperInvariant();
        float perAttack = normalized switch
        {
            "SX1" => sx1SpeedMultiplier,
            "SX2" => sx2SpeedMultiplier,
            "SX3" => sx3SpeedMultiplier,
            "SX4" => sx4SpeedMultiplier,
            "SX5" => sx5SpeedMultiplier,

            "AY1" => ay1SpeedMultiplier,
            "AY2" => ay2SpeedMultiplier,
            "AY3" => ay3SpeedMultiplier,

            "AC_X1" => acX1SpeedMultiplier,
            "AC_X2" => acX2SpeedMultiplier,
            "PLUNGE" => plungeSpeedMultiplier,
            "AIRDASH" => airDashAttackSpeedMultiplier,

            "LAUNCHER" => launcherSpeedMultiplier,
            "G_ATTACK" => guardAttackSpeedMultiplier,

            "AX1" => ax1SpeedMultiplier,
            "AX2" => ax2SpeedMultiplier,
            "AX3" => ax3SpeedMultiplier,
            "AX4" => ax4SpeedMultiplier,

            _ => 1f
        };

        resolved *= Mathf.Max(0.1f, perAttack);

        return Mathf.Max(0.1f, resolved);
    }

    private float ScaleDurationByCurrentAttackSpeed(float duration)
    {
        float speed = Mathf.Max(0.1f, currentAttackSpeedMultiplier);
        return Mathf.Max(0f, duration) / speed;
    }

    private float ScaleDurationForAttack(float duration, PlayerAttack attackData)
    {
        float speed = currentAttack != null && ReferenceEquals(currentAttack, attackData)
            ? Mathf.Max(0.1f, currentAttackSpeedMultiplier)
            : ResolveAttackSpeedMultiplier(attackData, attackData != null ? attackData.attackId : null);

        return Mathf.Max(0f, duration) / speed;
    }
    #endregion
}
