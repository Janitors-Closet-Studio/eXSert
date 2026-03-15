using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EnemyBehavior.Boss.Cleanser
{
    /// <summary>
    /// Lightweight animation driver for the Cleanser boss that issues CrossFade calls directly to the attached Animator.
    /// Works like PlayerAnimationController: you reference states by name and the controller handles playing them.
    /// 
    /// IMPORTANT: The string values in CleanserAnim must match EXACTLY what the animation states
    /// are called in the Cleanser's Animator Controller. Legacy names are normalized so older
    /// inspector values like Attack_Lunge or C_Idle continue working after animator renames.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public class CleanserAnimController : MonoBehaviour
    {
        /// <summary>
        /// Static class containing all animation state names for the Cleanser.
        /// Active entries reflect the states currently present in the Animator graph.
        /// Future or not-yet-wired clips stay commented until they are actually added.
        /// </summary>
        private static class CleanserAnim
        {
            /// <summary>
            /// Idle and stance animations.
            /// </summary>
            internal static class Idle
            {
                internal const string Default = "Idle";
                internal const string Intro = "IntroIdle";
                internal const string DoubleHandIdle = "DoubleHandIdle";

                // idk if we are going to have this - Kyle
                // internal const string CombatIdle = "CombatIdle";
            }

            /// <summary>
            /// Locomotion and traversal animations.
            /// </summary>
            internal static class Locomotion
            {
                internal const string Walk = "Walk";
                internal const string JumpFull = "JumpFull";
                internal const string JumpTakeoff = "JumpTakeoff";
                internal const string JumpInAir = "JumpInAir";
                internal const string JumpLanding = "JumpLanding";
            }

            /// <summary>
            /// Non-attack utility and reaction states.
            /// </summary>
            internal static class GeneralStates
            {
                internal const string GrabWeapon = "GrabWeapon";
                internal const string Death = "Death";

                // TODO: Add when animation exists in the current Animator.
                // internal const string GrabWeapons = "GrabWeapons";
                // internal const string Flinch = "Flinch";
                // internal const string Stunned = "Stunned";
            }

            /// <summary>
            /// Basic attacks and current combo-state animations.
            /// </summary>
            internal static class BasicAttacks
            {
                internal const string Lunge = "Lunge";
                internal const string BlockAndLunge = "LungeBlock";
                internal const string Cleave = "Cleave";
                internal const string AdvancingCleave = "CleaveAdvance";
                internal const string DiagUpwardSlash = "DiagUpwardSlash";
                internal const string PommelStrike = "PommelStrike";
                internal const string WingBash = "WingBash";
                internal const string OverheadAttack = "OverheadAttack";
                internal const string SpareToss = "SpareToss";
                internal const string LegSweep = "LegSweep";
                internal const string SlashToSlap = "SlashToSlap";
                internal const string RakeIntoSpinSlash = "RakeIntoSpinSlash";
            }

            /// <summary>
            /// Strong attacks and finishers.
            /// </summary>
            internal static class StrongAttacks
            {
                internal const string AnimeDash = "AnimeDash";
                internal const string SpinAttackWindUp = "JumpSpinAttackHPWindup";
                internal const string SpinWindUp = "JumpSpinAttackWindup";
                internal const string SpinHold = "JumpSpinAttackHPHoldPose";
                internal const string SpinAttackWindDown = "JumpSpinAttackWindDown";

                // TODO: Add when animation exists in the current Animator.
                // internal const string Whirlwind = "Whirlwind";
            }

            /// <summary>
            /// Ultimate attack states.
            /// </summary>
            internal static class Ultimate
            {
                internal const string Main = "Ultimate";
                internal const string JumpArcBase = "JumpArcBase";
                internal const string JumpArcResolution = "JumpArcResolution";
                internal const string JumpArcCancel = "JumpArcCancel";
            }
        }

        private static readonly Dictionary<string, string> LegacyStateAliases = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            // Only keep aliases that cannot be derived by stripping the legacy prefix.
            { "Attack_OverheadCleave", CleanserAnim.BasicAttacks.OverheadAttack },
            { "Attack_OverheadAttack", CleanserAnim.BasicAttacks.OverheadAttack },
            { "OverheadCleave", CleanserAnim.BasicAttacks.OverheadAttack },
            { "DiagonalUpwardSlash", CleanserAnim.BasicAttacks.DiagUpwardSlash },
            { "GrabWeapons", CleanserAnim.GeneralStates.GrabWeapon },
            { "PommelSlam", CleanserAnim.BasicAttacks.PommelStrike },
            { "BlockAndLunge", CleanserAnim.BasicAttacks.BlockAndLunge },
            { "AdvancingCleave", CleanserAnim.BasicAttacks.AdvancingCleave },
            { "SlashtoSlap", CleanserAnim.BasicAttacks.SlashToSlap },
            { "JumpSpinAttack_Windup", CleanserAnim.StrongAttacks.SpinWindUp },
            { "JumpSpinAttack_HoldPose", CleanserAnim.StrongAttacks.SpinHold },
            { "JumpSpinAttack_WindDown", CleanserAnim.StrongAttacks.SpinAttackWindDown },
            { "JumpSpinAttackHoldPose", CleanserAnim.StrongAttacks.SpinHold },
            { "JumpArc_Resolution", CleanserAnim.Ultimate.JumpArcResolution },
            { "JumpArc_Cancellation", CleanserAnim.Ultimate.JumpArcCancel },
            { "JumpArcCancellation", CleanserAnim.Ultimate.JumpArcCancel },
        };

        [Header("Animator Setup")]
        [Tooltip("Animator layer index to drive (0 = Base Layer).")]
        [SerializeField] private int layerIndex = 0;

        [Header("Crossfade Settings")]
        [Tooltip("Default transition time between animation states.")]
        [SerializeField, Range(0f, 0.3f)] private float defaultTransition = 0.15f;
        [Tooltip("Faster transition for attack animations.")]
        [SerializeField, Range(0f, 0.2f)] private float attackTransition = 0.05f;

        [Header("Animation Events")]
        [Tooltip("Reference to CleanserBrain for animation event callbacks.")]
        [SerializeField] private CleanserBrain cleanserBrain;
        [Tooltip("Optional: log animation event invocations for debugging.")]
        [SerializeField] private bool logAnimationEvents = false;

        private Animator animator;
        private string currentState;

        private Coroutine hardLockCoroutine;
        private string hardLockedState;

        #region Unity Lifecycle

        private void Awake()
        {
            animator = GetComponent<Animator>();
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            if (cleanserBrain == null)
            {
                cleanserBrain = GetComponent<CleanserBrain>()
                    ?? GetComponentInParent<CleanserBrain>();
            }
        }

            #endregion

    #region Idle Animations

    public void PlayIdle(float transition = -1f) => CrossFade(CleanserAnim.Idle.Default, transition);
    public void PlayIntroIdle(float transition = -1f) => CrossFade(CleanserAnim.Idle.Intro, transition);
    public void PlayDoubleHandIdle(float transition = -1f) => CrossFade(CleanserAnim.Idle.DoubleHandIdle, transition);

    // TODO: Uncomment when animation exists in the current Animator.
    // public void PlayCombatIdle(float transition = -1f) => CrossFade(CleanserAnim.Idle.CombatIdle, transition);

    #endregion

    #region Locomotion Animations

    public void PlayWalk(float transition = -1f) => CrossFade(CleanserAnim.Locomotion.Walk, transition);
    public void PlayJumpFull(float transition = -1f) => CrossFade(CleanserAnim.Locomotion.JumpFull, transition);
    public void PlayJumpTakeoff(float transition = -1f) => CrossFade(CleanserAnim.Locomotion.JumpTakeoff, transition);
    public void PlayJumpInAir(float transition = -1f) => CrossFade(CleanserAnim.Locomotion.JumpInAir, transition);
    public void PlayJumpLanding(float transition = -1f) => CrossFade(CleanserAnim.Locomotion.JumpLanding, transition);

    // TODO: Uncomment when animation exists in the current Animator.
    // public void PlayJump(float transition = -1f) => CrossFade(CleanserAnim.Locomotion.Jump, transition);

    #endregion

    #region General State Animations

        public void PlayGrabWeapon() => CrossFade(CleanserAnim.GeneralStates.GrabWeapon, attackTransition, true);
        public void PlayDeath() => CrossFade(CleanserAnim.GeneralStates.Death, 0.02f, true);

        // TODO: Uncomment when animation exists in the current Animator.
        // public void PlayFlinch() => CrossFade(CleanserAnim.GeneralStates.Flinch, 0.02f, true);

    #endregion

        /// <summary>
        /// Plays appropriate locomotion animation based on movement speed.
        /// </summary>
        /// <param name="moveSpeed01">Normalized movement speed (0-1).</param>
        public void PlayLocomotion(float moveSpeed01)
        {
            if (moveSpeed01 > 0.1f)
            {
                PlayWalk();
                // TODO: When run animation exists, add threshold check:
                // if (moveSpeed01 > 0.7f)
                //     PlayRun();
                // else
                //     PlayWalk();
            }
            else
            {
                PlayIdle();
            }
        }

        #region Basic Attack Animations

        public void PlayOverheadCleave() => CrossFade(CleanserAnim.BasicAttacks.OverheadAttack, attackTransition, true);
        public void PlaySpareToss() => CrossFade(CleanserAnim.BasicAttacks.SpareToss, attackTransition, true);
        public void PlayLunge() => CrossFade(CleanserAnim.BasicAttacks.Lunge, attackTransition, true);
        public void PlayBlockAndLunge() => CrossFade(CleanserAnim.BasicAttacks.BlockAndLunge, attackTransition, true);
        public void PlayCleave() => CrossFade(CleanserAnim.BasicAttacks.Cleave, attackTransition, true);
        public void PlayAdvancingCleave() => CrossFade(CleanserAnim.BasicAttacks.AdvancingCleave, attackTransition, true);
        public void PlayDiagUpwardSlash() => CrossFade(CleanserAnim.BasicAttacks.DiagUpwardSlash, attackTransition, true);
        public void PlayPommelStrike() => CrossFade(CleanserAnim.BasicAttacks.PommelStrike, attackTransition, true);
        public void PlayWingBash() => CrossFade(CleanserAnim.BasicAttacks.WingBash, attackTransition, true);
        public void PlayLegSweep() => CrossFade(CleanserAnim.BasicAttacks.LegSweep, attackTransition, true);
        public void PlaySlashToSlap() => CrossFade(CleanserAnim.BasicAttacks.SlashToSlap, attackTransition, true);
        public void PlayRakeIntoSpinSlash() => CrossFade(CleanserAnim.BasicAttacks.RakeIntoSpinSlash, attackTransition, true);
        public void PlayAnimeDash() => CrossFade(CleanserAnim.StrongAttacks.AnimeDash, attackTransition, true);
        public void PlaySpinAttackWindUp() => CrossFade(CleanserAnim.StrongAttacks.SpinAttackWindUp, attackTransition, true);
        public void PlaySpinWindUp() => CrossFade(CleanserAnim.StrongAttacks.SpinWindUp, attackTransition, true);
        public void PlaySpinHold() => CrossFade(CleanserAnim.StrongAttacks.SpinHold, defaultTransition, true);
        public void PlaySpinAttackWindDown() => CrossFade(CleanserAnim.StrongAttacks.SpinAttackWindDown, attackTransition, true);
        public void PlayUltimate() => CrossFade(CleanserAnim.Ultimate.Main, attackTransition, true);
        public void PlayJumpArcBase() => CrossFade(CleanserAnim.Ultimate.JumpArcBase, attackTransition, true);
        public void PlayJumpArcResolution() => CrossFade(CleanserAnim.Ultimate.JumpArcResolution, attackTransition, true);
        public void PlayJumpArcCancel() => CrossFade(CleanserAnim.Ultimate.JumpArcCancel, attackTransition, true);

        // TODO: Uncomment when animation exists in the current Animator.
        // public void PlayPommelSlam() => CrossFade(CleanserAnim.BasicAttacks.PommelSlam, attackTransition, true);
        // public void PlayOverheadAttack() => CrossFade(CleanserAnim.BasicAttacks.OverheadAttack, attackTransition, true);
        // public void PlayOverheadCleaveExact() => CrossFade(CleanserAnim.BasicAttacks.OverheadCleave, attackTransition, true);
        // public void PlayWhirlwind() => CrossFade(CleanserAnim.StrongAttacks.Whirlwind, attackTransition, true);

        #endregion

        #region Generic Playback

        /// <summary>
        /// Generic attack playback. Pass the actual animator state name directly.
        /// Use this when CleanserBrain triggers attacks by string name.
        /// </summary>
        public void PlayAttack(string attackStateName)
        {
            CrossFade(attackStateName, attackTransition, true);
        }

        /// <summary>
        /// Play any custom animation state by name.
        /// </summary>
        public void PlayCustom(string stateName, float transition = -1f, bool restart = false)
        {
            CrossFade(stateName, transition, restart);
        }

        #endregion

        #region State Queries

        /// <summary>
        /// Check if a specific animation state is currently playing.
        /// </summary>
        public bool IsPlaying(string stateName, out float normalizedTime)
        {
            normalizedTime = 0f;
            if (animator == null || string.IsNullOrEmpty(stateName))
                return false;

            stateName = NormalizeStateName(stateName);

            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(layerIndex);
            bool isPlaying = info.IsName(stateName);
            if (isPlaying)
                normalizedTime = info.normalizedTime;

            return isPlaying;
        }

        /// <summary>
        /// Check if the Cleanser is currently playing the death animation.
        /// </summary>
        public bool IsPlayingDeath(out float normalizedTime) => IsPlaying(CleanserAnim.GeneralStates.Death, out normalizedTime);

        /// <summary>
        /// Returns the current animation state name.
        /// </summary>
        public string GetCurrentState() => currentState;

        #endregion

        #region Animation Event Hooks
        
        // These methods are invoked by animation events directly on the Animator clips.
        // They forward calls to CleanserBrain which handles the game logic.

        /// <summary>
        /// Animation Event: Signals that the attack animation has completed.
        /// </summary>
        public void OnAttackComplete()
        {
            if (logAnimationEvents)
                Debug.Log("[CleanserAnimController] OnAttackComplete invoked");

            cleanserBrain?.OnAttackAnimationComplete();
        }

        /// <summary>
        /// Animation Event: Enables the attack hitbox (start of active frames).
        /// </summary>
        public void HitboxStart()
        {
            if (logAnimationEvents)
                Debug.Log("[CleanserAnimController] HitboxStart invoked");

            cleanserBrain?.OnAttackHitboxStart();
        }

        /// <summary>
        /// Animation Event: Disables the attack hitbox (end of active frames).
        /// </summary>
        public void HitboxEnd()
        {
            if (logAnimationEvents)
                Debug.Log("[CleanserAnimController] HitboxEnd invoked");

            cleanserBrain?.OnAttackHitboxEnd();
        }

        /// <summary>
        /// Animation Event: Switch to wing attack category (for multi-part attacks).
        /// </summary>
        public void SwitchToWing()
        {
            if (logAnimationEvents)
                Debug.Log("[CleanserAnimController] SwitchToWing invoked");

            cleanserBrain?.OnSwitchToWingCategory();
        }

        /// <summary>
        /// Animation Event: Switch to halberd attack category (for multi-part attacks).
        /// </summary>
        public void SwitchToHalberd()
        {
            if (logAnimationEvents)
                Debug.Log("[CleanserAnimController] SwitchToHalberd invoked");

            cleanserBrain?.OnSwitchToHalberdCategory();
        }

        /// <summary>
        /// Animation Event: Triggers movement during an attack (for lunge, etc.).
        /// </summary>
        public void AttackMoveStart()
        {
            if (logAnimationEvents)
                Debug.Log("[CleanserAnimController] AttackMoveStart invoked");

            cleanserBrain?.OnAttackMovementStart();
        }

        /// <summary>
        /// Animation Event: Shows the attack indicator VFX.
        /// </summary>
        public void ShowIndicator()
        {
            if (logAnimationEvents)
                Debug.Log("[CleanserAnimController] ShowIndicator invoked");

            cleanserBrain?.AttackIndicatorStart();
        }

        /// <summary>
        /// Animation Event: Hides the attack indicator VFX.
        /// </summary>
        public void HideIndicator()
        {
            if (logAnimationEvents)
                Debug.Log("[CleanserAnimController] HideIndicator invoked");

            cleanserBrain?.AttackIndicatorEnd();
        }

        /// <summary>
        /// Animation Event: Enables damage reduction (during wind-up).
        /// </summary>
        public void EnableDamageReduction()
        {
            if (logAnimationEvents)
                Debug.Log("[CleanserAnimController] EnableDamageReduction invoked");

            cleanserBrain?.EnableDamageReduction();
        }

        /// <summary>
        /// Animation Event: Disables damage reduction.
        /// </summary>
        public void DisableDamageReduction()
        {
            if (logAnimationEvents)
                Debug.Log("[CleanserAnimController] DisableDamageReduction invoked");

            cleanserBrain?.DisableDamageReduction();
        }

        #endregion

        #region Core Animation System

        private void CrossFade(string stateName, float transition = -1f, bool forceRestart = false)
        {
            if (string.IsNullOrWhiteSpace(stateName) || animator == null)
                return;

            stateName = NormalizeStateName(stateName);

            // Honor hard locks (non-cancelable animations) unless it's death
            if (!string.IsNullOrEmpty(hardLockedState))
            {
                // TODO: Uncomment when death animation exists
                // if (stateName == CleanserAnim.Reactions.Death)
                // {
                //     ClearHardLock();
                // }
                // else 
                if (stateName != hardLockedState)
                {
                    return;
                }
            }

            // Don't restart same animation unless forced
            if (!forceRestart && currentState == stateName)
                return;

            // Verify state exists in animator
            if (!StateExists(stateName))
            {
                Debug.LogWarning($"[CleanserAnimController] State '{stateName}' not found on Animator layer {layerIndex}.", this);
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

            // Wait for state to start playing
            float timer = 0f;
            while (timer < maxWaitSeconds)
            {
                var info = animator.GetCurrentAnimatorStateInfo(layerIndex);
                if (info.IsName(stateName))
                    break;

                timer += Time.deltaTime;
                yield return null;
            }

            // Wait for state to finish
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

        private static string NormalizeStateName(string stateName)
        {
            if (string.IsNullOrWhiteSpace(stateName))
                return stateName;

            if (LegacyStateAliases.TryGetValue(stateName, out string normalizedState))
                return normalizedState;

            if (stateName.StartsWith("C_", System.StringComparison.OrdinalIgnoreCase))
                return stateName.Substring(2);

            if (stateName.StartsWith("Attack_", System.StringComparison.OrdinalIgnoreCase))
                return stateName.Substring("Attack_".Length);

            if (stateName.StartsWith("Dash_", System.StringComparison.OrdinalIgnoreCase))
                return stateName.Substring("Dash_".Length);

            return stateName;
        }

        #endregion
    }
}
