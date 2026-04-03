using System.Collections;
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

    private Coroutine hardLockCoroutine;
    private string hardLockedState;

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

        animator.speed = 1f;
    }

    public void PlayIdle() => CrossFade(PlayerAnim.SingleTarget.Breathing);

    public void PlaySingleTargetBreathing(float transition = -1f) => CrossFade(PlayerAnim.SingleTarget.Breathing, transition);
    public void PlaySingleTargetIdleWorld(float transition = -1f) => CrossFade(PlayerAnim.SingleTarget.IdleWorld, transition);
    public void PlaySingleTargetIdleCombat(float transition = -1f) => CrossFade(PlayerAnim.SingleTarget.IdleCombat, transition);

    public void PlayAoeBreathing(float transition = -1f) => CrossFade(PlayerAnim.AreaOfEffect.Breathing, transition);
    public void PlayAoeIdleWorld(float transition = -1f) => CrossFade(PlayerAnim.AreaOfEffect.IdleWorld, transition);
    public void PlayAoeIdleCombat(float transition = -1f) => CrossFade(PlayerAnim.AreaOfEffect.IdleCombat, transition);

    public void PlayWalk(bool forceRestart = false) => CrossFade(PlayerAnim.Locomotion.Walk, -1f, forceRestart);
    public void PlayJog(bool forceRestart = false) => CrossFade(PlayerAnim.Locomotion.Jog, -1f, forceRestart);
    public void PlaySprint(bool forceRestart = false) => CrossFade(PlayerAnim.Locomotion.Sprint, -1f, forceRestart);
    public void PlayDash(float transition = 0.08f) => CrossFade(PlayerAnim.Locomotion.Dash, transition, true);

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
    public void PlayGuardIdle() => CrossFade(PlayerAnim.Guard.Idle);
    public void PlayGuardWalk() => CrossFade(PlayerAnim.Guard.Walk);
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
    public void PlayDeath() => CrossFade(PlayerAnim.Reactions.Death, 0.02f, true);

    public bool IsPlayingDeath(out float normalizedTime) => IsPlaying(PlayerAnim.Reactions.Death, out normalizedTime);

    /// <summary>
    /// Generic attack playback. Pass the actual animator state name (e.g. "SX1", "AY3", "Launcher").
    /// </summary>
    public void PlayAttack(string attackStateName)
    {
        CrossFade(attackStateName, 0.04f, true);
    }

    public void PlaySingleTargetLight(int comboIndex) => CrossFade(GetSingleTargetLight(comboIndex), 0.04f, true);

    public void PlaySingleTargetHeavy(int comboIndex) => CrossFade(GetSingleTargetHeavy(comboIndex), 0.04f, true);

    // AOE light/heavy helpers disabled with stance removal (kept for reference).
    // public void PlayAoeLight(int comboIndex) => CrossFade(GetAoeLight(comboIndex), 0.04f, true);
    // public void PlayAoeHeavy(int comboIndex) => CrossFade(GetAoeHeavy(comboIndex), 0.04f, true);

    public void PlayLauncher() => CrossFade(PlayerAnim.Specials.Launcher, 0.04f, true);

    public void PlayPlunge() => CrossFade(PlayerAnim.Specials.Plunge, 0.04f, true);

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
