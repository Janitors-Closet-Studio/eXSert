using System.Collections;
using Managers.TimeLord;
using UnityEngine;

/// <summary>
/// Lightweight animation driver that issues CrossFade calls directly to the attached Animator.
/// Works like BaseEnemy: you reference states by name and the controller handles playing them.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public class PlayerAnimationController : MonoBehaviour
{
    private static class PlayerAnim
    {
        internal static class SingleTarget
        {
            internal const string Breathing = "ST_Breathing";
            internal const string IdleWorld = "ST_Idle_WC";
            internal const string IdleCombat = "ST_Idle_Combat";
        }

        internal static class AreaOfEffect
        {
            internal const string Breathing = "AOE_Breathing";
            internal const string IdleWorld = "AOE_Idle_WC";
            internal const string IdleCombat = "AOE_Idle_Combat";
        }

        internal static class Locomotion
        {
            internal const string Walk = "Walk";
            internal const string WalkBack = "Walkback";
            internal const string WalkStrafeLeft = "WalkStrafeL";
            internal const string WalkStrafeRight = "WalkStrafeR";
            internal const string Jog = "Jog";
            internal const string Sprint = "Sprint";
            internal const string Dash = "Dash";
        }

        internal static class Air
        {
            internal const string Jump = "Jump";
            internal const string Falling = "Falling";
            internal const string FallingHigh = "Falling_High";
            internal const string Land = "Land";
            internal const string AirJump = "AirJump_Start";
            internal const string AirDash = "AirDash";
        }

        internal static class Guard
        {
            internal const string Raise = "Guard_Up";
            internal const string Idle = "Guard_Idle";
            internal const string Walk = "G_Walk";
            internal const string Attack = "G_Attack";
            internal const string DashLeft = "G_Dash_L";
            internal const string DashRight = "G_Dash_R";
            internal const string Parry = "Parry";
        }

        internal static class SingleTargetAttacks
        {
            internal const string Light1 = "SX1";
            internal const string Light2 = "SX2";
            internal const string Light3 = "SX3";
            internal const string Light4 = "SX4";
            internal const string Light5 = "SX5";
            // Heavy chain now uses AY1-AY3 (legacy SY1-3 retired).
            internal const string Heavy1 = "AY1";
            internal const string Heavy2 = "AY2";
            internal const string Heavy3 = "AY3";
        }

        internal static class AreaOfEffectAttacks
        {
            // Legacy AOE light chain (AX1-AX4) is currently unused.
            internal const string Light1 = "AX1";
            internal const string Light2 = "AX2";
            internal const string Light3 = "AX3";
            internal const string Light4 = "AX4";
            internal const string Heavy1 = "AY1";
            internal const string Heavy2 = "AY2";
            internal const string Heavy3 = "AY3";
        }

        internal static class Reactions
        {
            internal const string Flinch = "Flinch";
            internal const string Knockback = "Knockback";
            internal const string Death = "Death";
        }

        internal static class Specials
        {
            internal const string Launcher = "Launcher";
            internal const string Plunge = "Plunge";
            internal const string Interact = "Interact";
        }

        internal static class Combo
        {
            internal const string Step1 = "AC_X1";
            internal const string Step2 = "AC_X2";
        }
    }

    [Header("Animator Setup")]
    [Tooltip("Animator layer index to drive (0 = Base Layer).")]
    [SerializeField] private int layerIndex = 0;

    [Header("Crossfade Settings")]
    [SerializeField, Range(0f, 0.3f)] private float defaultTransition = 0.16f;
    [SerializeField, Range(0f, 0.6f)] private float fallingTransition = 0.2f;
    [SerializeField, Range(0f, 0.6f)] private float deathTransition = 0.2f;

    [Header("Animation Events")]
    [Tooltip("Attack manager that receives hitbox/cancel callbacks.")]
    [SerializeField] private PlayerAttackManager attackManager;
    [Tooltip("Player movement that receives jump event callbacks.")]
    [SerializeField] private PlayerMovement playerMovement;
    [Tooltip("Player health manager that receives dash i-frame animation window callbacks.")]
    [SerializeField] private PlayerHealthBarManager playerHealth;
    [Tooltip("Optional: log animation event invocations for debugging.")]
    [SerializeField] private bool logAnimationEvents = false;

    private Animator animator;
    private string currentState;

    public string CurrentStateName => currentState;

    private Coroutine hardLockCoroutine;
    private string hardLockedState;

    // Set by the PlungeWaitForLanding animation event; cleared when the player lands.
    private bool waitingForPlungeLand;

    // One-shot transition override consumed by the next locomotion CrossFade (Walk/Jog/Sprint).
    // Set before ForceLocomotionRefresh() on plunge exit so the blend feels smooth.
    private float nextLocomotionBlendOverride = -1f;

    // Separate one-shot override consumed only by PlayDash, so locomotion can't eat it
    // before the dash input unlocks on the frame after CompleteCancelWindow fires.
    private float nextDashBlendOverride = -1f;

    // Separate one-shot override consumed only by AoE (heavy grounded) attack playback.
    // Short by design so a buffered AY1 after a plunge doesn't inherit the long locomotion blend
    // time, which would cause its CancelWindowStart event to fire while the animation is still
    // mostly blended out, making it look cut off.
    private float nextAoeAttackBlendOverride = -1f;

    // Separate one-shot override consumed only by single-target attack playback.
    // Can be set longer (matching the locomotion blend) because ST animations are shorter and
    // their cancel windows don't fire as early relative to the crossfade duration.
    private float nextSingleTargetBlendOverride = -1f;

    /// <summary>
    /// True while the Plunge animation is frozen mid-air waiting for the player to land.
    /// External systems (e.g. EnsureAnimatorRuntimeHealthy) must respect this and not reset animator speed.
    /// </summary>
    public bool IsWaitingForPlungeLand => waitingForPlungeLand;

    // Saved animator state for pause/resume — preserves the exact normalized time so
    // the animation resumes from the same pose rather than restarting from frame 0.
    private string savedStateOnPause;
    private float savedNormalizedTimeOnPause;
    private bool hasSavedStateForResume;

    private void OnEnable()
    {
        PauseCoordinator.OnPaused += OnGamePaused;
        PauseCoordinator.OnResumed += OnGameResumed;
    }

    private void OnDisable()
    {
        PauseCoordinator.OnPaused -= OnGamePaused;
        PauseCoordinator.OnResumed -= OnGameResumed;
    }

    private void OnGamePaused()
    {
        if (animator != null)
        {
            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(layerIndex);
            Debug.Log($"[DIAG-Pause] Animator paused | currentState={currentState} | normTime={info.normalizedTime:F4} | animSpeed={animator.speed:F3} | frame={Time.frameCount}");
        }
    }

    private void OnGameResumed()
    {
        if (animator != null)
        {
            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(layerIndex);
            Debug.Log($"[DIAG-Pause] Animator resumed | currentState={currentState} | normTime={info.normalizedTime:F4} | animSpeed={animator.speed:F3} | frame={Time.frameCount}");
        }
        hasSavedStateForResume = false;
    }

    private void Awake()
    {
        animator = GetComponent<Animator>();
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (attackManager == null)
        {
            attackManager = GetComponent<PlayerAttackManager>();
            if (attackManager == null)
                attackManager = GetComponentInParent<PlayerAttackManager>();
        }

        if (playerMovement == null)
        {
            playerMovement = GetComponent<PlayerMovement>()
                ?? GetComponentInParent<PlayerMovement>()
                ?? GetComponentInChildren<PlayerMovement>();
        }

        if (playerHealth == null)
        {
            playerHealth = GetComponent<PlayerHealthBarManager>()
                ?? GetComponentInParent<PlayerHealthBarManager>()
                ?? GetComponentInChildren<PlayerHealthBarManager>();
        }

        if (animator != null)
            animator.speed = 1f;
    }

    public void SetAnimatorSpeed(float speedMultiplier)
    {
        if (animator == null)
            return;

        animator.speed = Mathf.Max(0.01f, speedMultiplier);
    }

    public void ResetAnimatorSpeed()
    {
        if (animator == null)
            return;

        if (waitingForPlungeLand)
        {
            Debug.Log("[Plunge] ResetAnimatorSpeed suppressed — still waiting for plunge landing");
            return;
        }

        animator.speed = 1f;
    }

    public void FreezeCurrentPose()
    {
        if (animator == null)
            return;

        animator.speed = 0f;
    }

    /// <summary>
    /// Primes a one-shot blend duration that will be consumed by the next Walk/Jog/Sprint
    /// CrossFade, overriding the default transition. Use before ForceLocomotionRefresh() on
    /// plunge exit so movement snaps in smoothly regardless of which locomotion state fires.
    /// </summary>
    public void SetNextLocomotionBlendOverride(float duration)
    {
        nextLocomotionBlendOverride = Mathf.Max(0f, duration);
    }

    /// <summary>
    /// Primes a one-shot blend duration consumed exclusively by the next <see cref="PlayDash"/>
    /// call. Kept separate from the locomotion override so locomotion can't consume it first.
    /// </summary>
    public void SetNextDashBlendOverride(float duration)
    {
        nextDashBlendOverride = Mathf.Max(0f, duration);
    }

    /// <summary>
    /// Primes a one-shot blend duration consumed exclusively by the next AoE (heavy grounded)
    /// attack playback call. Kept short so a buffered AY1 after a plunge doesn't inherit the
    /// long locomotion blend time and appear to cut off at its cancel window.
    /// </summary>
    public void SetNextAoeAttackBlendOverride(float duration)
    {
        nextAoeAttackBlendOverride = Mathf.Max(0f, duration);
    }

    /// <summary>
    /// Primes a one-shot blend duration consumed exclusively by the next single-target attack
    /// playback call. Can be set to a longer value (e.g. matching the locomotion blend) for a
    /// smooth transition out of plunge into single-target attacks.
    /// </summary>
    public void SetNextSingleTargetBlendOverride(float duration)
    {
        nextSingleTargetBlendOverride = Mathf.Max(0f, duration);
    }

    public void PlayIdle() => CrossFade(PlayerAnim.SingleTarget.Breathing);

    public void PlaySingleTargetBreathing(float transition = -1f) => CrossFade(PlayerAnim.SingleTarget.Breathing, transition);
    public void PlaySingleTargetIdleWorld(float transition = -1f) => CrossFade(PlayerAnim.SingleTarget.IdleWorld, transition);
    public void PlaySingleTargetIdleCombat(float transition = -1f) => CrossFade(PlayerAnim.SingleTarget.IdleCombat, transition);

    public void PlayAoeBreathing(float transition = -1f) => CrossFade(PlayerAnim.AreaOfEffect.Breathing, transition);
    public void PlayAoeIdleWorld(float transition = -1f) => CrossFade(PlayerAnim.AreaOfEffect.IdleWorld, transition);
    public void PlayAoeIdleCombat(float transition = -1f) => CrossFade(PlayerAnim.AreaOfEffect.IdleCombat, transition);

    public void PlayWalk(bool forceRestart = false) => CrossFade(PlayerAnim.Locomotion.Walk, ConsumeLocomotionBlendOverride(), forceRestart);
    public void PlayWalkBack(bool forceRestart = false) => CrossFade(PlayerAnim.Locomotion.WalkBack, -1f, forceRestart);
    public void PlayWalkStrafeLeft(bool forceRestart = false) => CrossFade(PlayerAnim.Locomotion.WalkStrafeLeft, -1f, forceRestart);
    public void PlayWalkStrafeRight(bool forceRestart = false) => CrossFade(PlayerAnim.Locomotion.WalkStrafeRight, -1f, forceRestart);
    public void PlayJog(bool forceRestart = false) => CrossFade(PlayerAnim.Locomotion.Jog, ConsumeLocomotionBlendOverride(), forceRestart);
    public void PlaySprint(bool forceRestart = false) => CrossFade(PlayerAnim.Locomotion.Sprint, ConsumeLocomotionBlendOverride(), forceRestart);
    public void PlayDash(float transition = 0.08f)
    {
        StartHardLock(PlayerAnim.Locomotion.Dash);
        CrossFade(PlayerAnim.Locomotion.Dash, Mathf.Max(transition, ConsumeDashBlendOverride()), true);
    }

    public void PlayLocomotion(float moveAmount01)
    {
        string targetState;
        if (moveAmount01 > 0.66f)
            targetState = PlayerAnim.Locomotion.Sprint;
        else if (moveAmount01 > 0.33f)
            targetState = PlayerAnim.Locomotion.Jog;
        else if (moveAmount01 > 0.1f)
            targetState = PlayerAnim.Locomotion.Walk;
        else
            targetState = PlayerAnim.SingleTarget.Breathing;

        CrossFade(targetState);
    }

    public void PlayGuard(float moveAmount01)
    {
        string target = moveAmount01 > 0.1f ? PlayerAnim.Guard.Walk : PlayerAnim.Guard.Idle;
        CrossFade(target);
    }

    public void PlayGuardUp() => CrossFade(PlayerAnim.Guard.Raise, 0.02f, true);
    public void PlayGuardIdle() => CrossFadeOrReplayIfFinished(PlayerAnim.Guard.Idle);
    public void PlayGuardWalk() => CrossFadeOrReplayIfFinished(PlayerAnim.Guard.Walk);
    public void PlayGuardAttack() => CrossFade(PlayerAnim.Guard.Attack, 0.03f, true);
    public void PlayGuardDashLeft() => CrossFade(PlayerAnim.Guard.DashLeft, 0.02f, true);
    public void PlayGuardDashRight() => CrossFade(PlayerAnim.Guard.DashRight, 0.02f, true);
    public void PlayParry() => CrossFade(PlayerAnim.Guard.Parry, 0.01f, true);

    public bool IsHardLocked => !string.IsNullOrEmpty(hardLockedState);

    public bool IsParryHardLocked => hardLockedState == PlayerAnim.Guard.Parry;

    /// <summary>
    /// Plays the Parry animation and prevents other animation requests from overriding it
    /// until the Parry state finishes.
    /// </summary>
    public void PlayParryNonCancelable()
    {
        if (animator == null)
            return;

        StartHardLock(PlayerAnim.Guard.Parry);

        if (!StateExists(PlayerAnim.Guard.Parry))
        {
            Debug.LogWarning($"[PlayerAnimationController] State '{PlayerAnim.Guard.Parry}' not found on Animator layer {layerIndex}.", this);
            ClearHardLock();
            return;
        }

        animator.Play(PlayerAnim.Guard.Parry, layerIndex, 0f);
        currentState = PlayerAnim.Guard.Parry;
    }

    public void PlayJump() => CrossFade(PlayerAnim.Air.Jump);
    public void PlayFalling() => CrossFade(PlayerAnim.Air.Falling, fallingTransition);
    public void PlayFallingHigh() => CrossFade(PlayerAnim.Air.FallingHigh, fallingTransition);
    public void PlayLand() => CrossFade(PlayerAnim.Air.Land, 0.04f, true);
    public void PlayAirJumpStart() => CrossFade(PlayerAnim.Air.AirJump, 0.03f, true);
    public void PlayAirDash(float transition = 0.08f) => CrossFade(PlayerAnim.Air.AirDash, transition, true);

    public void PlayHit() => CrossFade(PlayerAnim.Reactions.Flinch, 0.02f, true);
    public void PlayHeavyHit() => CrossFade(PlayerAnim.Reactions.Knockback, 0.05f, true);

    /// <summary>
    /// Plays the Knockback animation and hard-locks it so no other animation request
    /// can override it until the state finishes playing. Only death breaks through.
    /// </summary>
    public void PlayKnockbackNonCancelable()
    {
        if (animator == null)
            return;

        StartHardLock(PlayerAnim.Reactions.Knockback);

        if (!StateExists(PlayerAnim.Reactions.Knockback))
        {
            Debug.LogWarning($"[PlayerAnimationController] State '{PlayerAnim.Reactions.Knockback}' not found on Animator layer {layerIndex}.", this);
            ClearHardLock();
            return;
        }

        animator.CrossFadeInFixedTime(PlayerAnim.Reactions.Knockback, 0.05f, layerIndex, 0f);
        currentState = PlayerAnim.Reactions.Knockback;
    }

    public void PlayDeath() => CrossFade(PlayerAnim.Reactions.Death, deathTransition, true);

    public bool IsPlayingDeath(out float normalizedTime) => IsPlaying(PlayerAnim.Reactions.Death, out normalizedTime);

    /// <summary>
    /// Plays a single-target attack animation, consuming the single-target blend override if
    /// primed (e.g. the longer plunge-exit blend), otherwise falling back to 0.04s.
    /// </summary>
    public void PlaySingleTargetAttack(string attackStateName)
    {
        CrossFade(attackStateName, Mathf.Max(0.04f, ConsumeSingleTargetBlendOverride()), true);
    }

    /// <summary>
    /// Plays an AoE (heavy grounded) attack animation, consuming the AoE blend override if
    /// primed (e.g. the shorter plunge-exit blend), otherwise falling back to 0.04s.
    /// </summary>
    public void PlayAoeAttack(string attackStateName)
    {
        CrossFade(attackStateName, Mathf.Max(0.04f, ConsumeAoeAttackBlendOverride()), true);
    }

    public void PlaySingleTargetLight(int comboIndex) => CrossFade(GetSingleTargetLight(comboIndex), Mathf.Max(0.04f, ConsumeSingleTargetBlendOverride()), true);

    public void PlaySingleTargetHeavy(int comboIndex) => CrossFade(GetSingleTargetHeavy(comboIndex), Mathf.Max(0.04f, ConsumeSingleTargetBlendOverride()), true);

    // AOE light/heavy helpers disabled with stance removal (kept for reference).
    // public void PlayAoeLight(int comboIndex) => CrossFade(GetAoeLight(comboIndex), 0.04f, true);
    // public void PlayAoeHeavy(int comboIndex) => CrossFade(GetAoeHeavy(comboIndex), 0.04f, true);

    public void PlayLauncher() => CrossFade(PlayerAnim.Specials.Launcher, 0.04f, true);

    public void PlayPlunge()
    {
        waitingForPlungeLand = false;
        Debug.Log($"[Plunge] PlayPlunge called | currentState={currentState} | animSpeed={animator?.speed:F3} | frame={Time.frameCount}");
        CrossFade(PlayerAnim.Specials.Plunge, 0.04f, true);
    }

    public void PlayInteract()
    {
        if (animator == null)
            return;

        StartHardLock(PlayerAnim.Specials.Interact);

        if (!StateExists(PlayerAnim.Specials.Interact))
        {
            Debug.LogWarning($"[PlayerAnimationController] State '{PlayerAnim.Specials.Interact}' not found on Animator layer {layerIndex}.", this);
            ClearHardLock();
            return;
        }

        animator.Play(PlayerAnim.Specials.Interact, layerIndex, 0f);
        currentState = PlayerAnim.Specials.Interact;
    }

    public void PlayComboChain(int step)
    {
        string state = step <= 1 ? PlayerAnim.Combo.Step1 : PlayerAnim.Combo.Step2;
        CrossFade(state, 0.04f, true);
    }

    public void PlayAirState(string stateName)
    {
        CrossFade(stateName, 0.04f, true);
    }

    public void PlayCustom(string stateName, float transition = -1f, bool restart = false)
    {
        CrossFade(stateName, transition, restart);
    }

    private float ConsumeLocomotionBlendOverride()
    {
        if (nextLocomotionBlendOverride < 0f)
            return -1f;

        float value = nextLocomotionBlendOverride;
        nextLocomotionBlendOverride = -1f;
        return value;
    }

    private float ConsumeDashBlendOverride()
    {
        if (nextDashBlendOverride < 0f)
            return -1f;

        float value = nextDashBlendOverride;
        nextDashBlendOverride = -1f;
        return value;
    }

    private float ConsumeSingleTargetBlendOverride()
    {
        if (nextSingleTargetBlendOverride < 0f)
            return -1f;

        float value = nextSingleTargetBlendOverride;
        nextSingleTargetBlendOverride = -1f;
        return value;
    }

    private float ConsumeAoeAttackBlendOverride()
    {
        if (nextAoeAttackBlendOverride < 0f)
            return -1f;

        float value = nextAoeAttackBlendOverride;
        nextAoeAttackBlendOverride = -1f;
        return value;
    }

    private void CrossFade(string stateName, float transition = -1f, bool forceRestart = false)
    {
        if (string.IsNullOrWhiteSpace(stateName) || animator == null)
            return;

        if (!string.IsNullOrEmpty(hardLockedState))
        {
            if (stateName == PlayerAnim.Reactions.Death)
            {
                ClearHardLock();
            }
            else if (stateName != hardLockedState)
            {
                if (CanOverrideHardLock(stateName))
                    ClearHardLock();
                else
                    return;
            }
        }

        if (!forceRestart && currentState == stateName)
            return;

        if (!StateExists(stateName))
        {
            Debug.LogWarning($"[PlayerAnimationController] State '{stateName}' not found on Animator layer {layerIndex}.", this);
            return;
        }

        float crossFade = transition >= 0f ? transition : defaultTransition;
        animator.CrossFadeInFixedTime(stateName, crossFade, layerIndex, 0f);
        currentState = stateName;
    }

    private bool CanOverrideHardLock(string requestedState)
    {
        if (string.IsNullOrEmpty(hardLockedState))
            return true;

        // Knockback hard lock: nothing overrides it (death is already handled before this call).
        if (hardLockedState == PlayerAnim.Reactions.Knockback)
            return false;

        if (hardLockedState == PlayerAnim.Specials.Interact
            || hardLockedState == PlayerAnim.Locomotion.Dash)
        {
            return requestedState != PlayerAnim.SingleTarget.Breathing
            && requestedState != PlayerAnim.SingleTarget.IdleWorld
            && requestedState != PlayerAnim.SingleTarget.IdleCombat
            && requestedState != PlayerAnim.AreaOfEffect.Breathing
            && requestedState != PlayerAnim.AreaOfEffect.IdleWorld
            && requestedState != PlayerAnim.AreaOfEffect.IdleCombat;
        }

        return false;
    }

    private void CrossFadeOrReplayIfFinished(string stateName, float transition = -1f)
    {
        if (string.IsNullOrWhiteSpace(stateName) || animator == null)
            return;

        if (animator.GetCurrentAnimatorStateInfo(layerIndex).IsName(stateName)
            && !animator.IsInTransition(layerIndex))
        {
            float normalizedTime = animator.GetCurrentAnimatorStateInfo(layerIndex).normalizedTime;
            if (normalizedTime >= 1f)
            {
                animator.Play(stateName, layerIndex, 0f);
                currentState = stateName;
                return;
            }
        }

        CrossFade(stateName, transition);
    }

    public string GetCurrentClipName()
    {
        if (animator == null)
            return "<none>";

        AnimatorClipInfo[] clips = animator.GetCurrentAnimatorClipInfo(layerIndex);
        if (clips != null && clips.Length > 0 && clips[0].clip != null)
            return clips[0].clip.name;

        return "<none>";
    }

    private void StartHardLock(string stateName)
    {
        hardLockedState = stateName;

        if (hardLockCoroutine != null)
            StopCoroutine(hardLockCoroutine);

        hardLockCoroutine = StartCoroutine(HardLockUntilStateCompletes(stateName));
    }

    private void ClearHardLock()
    {
        hardLockedState = null;
        if (hardLockCoroutine != null)
        {
            StopCoroutine(hardLockCoroutine);
            hardLockCoroutine = null;
        }
    }

    private IEnumerator HardLockUntilStateCompletes(string stateName)
    {
        const float maxWaitSeconds = 10f;

        float timer = 0f;
        while (timer < maxWaitSeconds)
        {
            var info = animator.GetCurrentAnimatorStateInfo(layerIndex);
            if (info.IsName(stateName))
                break;

            timer += Time.deltaTime;
            yield return null;
        }

        timer = 0f;
        while (timer < maxWaitSeconds)
        {
            var info = animator.GetCurrentAnimatorStateInfo(layerIndex);

            if (!info.IsName(stateName))
                break;

            if (info.normalizedTime >= 1f && !animator.IsInTransition(layerIndex))
                break;

            timer += Time.deltaTime;
            yield return null;
        }

        if (hardLockedState == stateName)
            hardLockedState = null;

        hardLockCoroutine = null;
    }

    private bool StateExists(string stateName)
    {
        int hash = Animator.StringToHash(stateName);
        return animator.HasState(layerIndex, hash);
    }

    public bool IsPlaying(string stateName, out float normalizedTime)
    {
        normalizedTime = 0f;
        if (animator == null || string.IsNullOrEmpty(stateName))
            return false;

        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(layerIndex);
        bool isPlaying = info.IsName(stateName);
        if (isPlaying)
            normalizedTime = info.normalizedTime;

        return isPlaying;
    }

    public void EnsureAnimatorRuntimeHealthy()
    {
        if (animator == null)
            return;

        if (!animator.enabled)
            animator.enabled = true;

        // Never override the intentional speed=0 freeze set by PlungeWaitForLanding.
        if (waitingForPlungeLand)
        {
            Debug.Log($"[Plunge] EnsureAnimatorRuntimeHealthy skipping speed reset — waitingForPlungeLand | animSpeed={animator.speed:F3} | frame={Time.frameCount}");
            return;
        }

        if (animator.speed < 0.95f)
            animator.speed = 1f;
    }

    #region Animation Event Hooks
    // These methods are invoked by animation events directly on the Animator
    public void GenerateHitbox()
    {
        if (logAnimationEvents)
            Debug.Log("[PlayerAnimationController] GenerateHitbox invoked");

        attackManager?.HandleAnimationHitbox();
    }

    public void GenerateHitbox(float duration)
    {
        if (logAnimationEvents)
            Debug.Log($"[PlayerAnimationController] GenerateHitbox({duration}) invoked");

        attackManager?.HandleAnimationHitbox(duration);
    }

    public void CancelWindowStart()
    {
        if (logAnimationEvents)
            Debug.Log("[PlayerAnimationController] CancelWindowStart invoked");

        attackManager?.HandleAnimationCancelWindow();
    }

    public void MoveForward()
    {
        if (logAnimationEvents)
            Debug.Log("[PlayerAnimationController] MoveForward invoked");

        attackManager?.HandleAnimationMoveForward();
    }

    public void Jump()
    {
        if (logAnimationEvents)
            Debug.Log("[PlayerAnimationController] Jump invoked");

        playerMovement?.HandleAnimationJumpEvent();
    }

    public void StartDashInvincibility()
    {
        if (logAnimationEvents)
            Debug.Log("[PlayerAnimationController] StartDashInvincibility invoked");

        playerHealth?.BeginDashInvincibilityWindow();
    }

    public void EndDashInvincibility()
    {
        if (logAnimationEvents)
            Debug.Log("[PlayerAnimationController] EndDashInvincibility invoked");

        playerHealth?.EndDashInvincibilityWindow();
    }

    /// <summary>
    /// Called by an animation event placed just before the ground-slam portion of the Plunge clip.
    /// Freezes the animator in place until the player actually lands, then ResumePlungeFromLanding unfreezes it.
    /// Add this event to the Plunge animation clip at the frame where the slam should only trigger on contact.
    /// </summary>
    public void PlungeWaitForLanding()
    {
        AnimatorStateInfo info = animator != null ? animator.GetCurrentAnimatorStateInfo(layerIndex) : default;
        Debug.Log($"[Plunge] PlungeWaitForLanding invoked | currentState={currentState} | normTime={info.normalizedTime:F4} | animSpeed={animator?.speed:F3} | frame={Time.frameCount}");

        if (logAnimationEvents)
            Debug.Log("[PlayerAnimationController] PlungeWaitForLanding invoked — freezing animator until grounded");

        waitingForPlungeLand = true;
        FreezeCurrentPose();

        Debug.Log($"[Plunge] Animator frozen | speed={animator?.speed:F3} | waitingForPlungeLand={waitingForPlungeLand} | frame={Time.frameCount}");
    }

    /// <summary>
    /// Called by PlayerMovement the moment the player lands from a plunge.
    /// Resumes the frozen Plunge animation so the ground-slam plays from the correct frame.
    /// </summary>
    public void ResumePlungeFromLanding()
    {
        AnimatorStateInfo info = animator != null ? animator.GetCurrentAnimatorStateInfo(layerIndex) : default;
        Debug.Log($"[Plunge] ResumePlungeFromLanding called | waitingForPlungeLand={waitingForPlungeLand} | currentState={currentState} | normTime={info.normalizedTime:F4} | animSpeed={animator?.speed:F3} | frame={Time.frameCount}");

        if (!waitingForPlungeLand)
        {
            Debug.LogWarning($"[Plunge] ResumePlungeFromLanding ignored — waitingForPlungeLand was already false | animSpeed={animator?.speed:F3} | frame={Time.frameCount}");
            return;
        }

        if (logAnimationEvents)
            Debug.Log("[PlayerAnimationController] ResumePlungeFromLanding invoked — resuming animator");

        waitingForPlungeLand = false;
        animator.speed = 1f; // Bypass ResetAnimatorSpeed guard — we are the authority clearing the flag
        Debug.Log($"[Plunge] Animator unfrozen | speed={animator?.speed:F3} | frame={Time.frameCount}");
    }

    // Legacy event names kept to avoid missing-method errors on existing clips
    public void SetComboStage(int stage) { if (logAnimationEvents) Debug.Log($"[PlayerAnimationController] SetComboStage({stage}) ignored"); }
    public void MarkInCombat() { if (logAnimationEvents) Debug.Log("[PlayerAnimationController] MarkInCombat ignored"); }
    public void OpenChainWindow() { if (logAnimationEvents) Debug.Log("[PlayerAnimationController] OpenChainWindow ignored"); }
    public void CloseChainWindow() { if (logAnimationEvents) Debug.Log("[PlayerAnimationController] CloseChainWindow ignored"); }
    public void ReturnToIdle() { if (logAnimationEvents) Debug.Log("[PlayerAnimationController] ReturnToIdle ignored"); }
    public void EnableCancel() { if (logAnimationEvents) Debug.Log("[PlayerAnimationController] EnableCancel ignored"); }
    public void DisableCancel() { if (logAnimationEvents) Debug.Log("[PlayerAnimationController] DisableCancel ignored"); }
    #endregion

    private static string GetSingleTargetLight(int comboIndex) => comboIndex switch
    {
        <= 1 => PlayerAnim.SingleTargetAttacks.Light1,
        2 => PlayerAnim.SingleTargetAttacks.Light2,
        3 => PlayerAnim.SingleTargetAttacks.Light3,
        4 => PlayerAnim.SingleTargetAttacks.Light4,
        _ => PlayerAnim.SingleTargetAttacks.Light5,
    };

    private static string GetSingleTargetHeavy(int comboIndex) => comboIndex switch
    {
        <= 1 => PlayerAnim.SingleTargetAttacks.Heavy1,
        2 => PlayerAnim.SingleTargetAttacks.Heavy2,
        _ => PlayerAnim.SingleTargetAttacks.Heavy3,
    };

    // private static string GetAoeLight(int comboIndex) => comboIndex switch
    // {
    //     <= 1 => PlayerAnim.AreaOfEffectAttacks.Light1,
    //     2 => PlayerAnim.AreaOfEffectAttacks.Light2,
    //     3 => PlayerAnim.AreaOfEffectAttacks.Light3,
    //     _ => PlayerAnim.AreaOfEffectAttacks.Light4,
    // };

    // private static string GetAoeHeavy(int comboIndex) => comboIndex switch
    // {
    //     <= 1 => PlayerAnim.AreaOfEffectAttacks.Heavy1,
    //     2 => PlayerAnim.AreaOfEffectAttacks.Heavy2,
    //     _ => PlayerAnim.AreaOfEffectAttacks.Heavy3,
    // };
}
