using System.Collections;
using UnityEngine;

namespace EnemyBehavior.Boss.Cleanser
{
    /// <summary>
    /// Lightweight animation driver for the Cleanser boss that issues CrossFade calls directly to the attached Animator.
    /// Works like PlayerAnimationController: you reference states by name and the controller handles playing them.
    /// 
    /// IMPORTANT: The string values in CleanserAnim must match EXACTLY what the animation states
    /// are called in the Cleanser's Animator Controller.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public class CleanserAnimController : MonoBehaviour
    {
        /// <summary>
        /// Static class containing all animation state names for the Cleanser.
        /// These strings MUST match the exact names of states in the Animator Controller.
        /// </summary>
        private static class CleanserAnim
        {
            /// <summary>
            /// Idle and general state animations.
            /// </summary>
            internal static class Idle
            {
                internal const string Default = "C_Idle";
                internal const string Intro = "C_IntroIdle";
                // TODO: Add when animation exists
                // internal const string Combat = "C_CombatIdle";
            }

            /// <summary>
            /// Locomotion animations (walking, running, etc.)
            /// </summary>
            internal static class Locomotion
            {
                internal const string Walk = "C_Walk";
                // TODO: Add when animations exist
                // internal const string Run = "C_Run";
                // internal const string Strafe = "C_Strafe";
            }

            /// <summary>
            /// Basic attack animations (halberd and wing attacks).
            /// </summary>
            internal static class BasicAttacks
            {
                // Currently implemented
                internal const string OverheadCleave = "C_OverheadAttack";
                internal const string SpareToss = "C_SpareToss";

                // TODO: Add when animations exist
                // internal const string Lunge = "C_Lunge";
                // internal const string SlashIntoSlap = "C_SlashSlap";
                // internal const string RakeIntoSpinSlash = "C_RakeSpin";
                // internal const string SpinSlashOnly = "C_SpinSlash";
                // internal const string LegSweep = "C_LegSweep";
                // internal const string Knockback = "C_Knockback";
                // internal const string MiniCrescentWave = "C_MiniCrescent";
            }

            /// <summary>
            /// Strong/finisher attack animations.
            /// </summary>
            internal static class StrongAttacks
            {
                // TODO: Add when animations exist
                // internal const string HighDive = "C_HighDive";
                // internal const string AnimeDashSlash = "C_AnimeDash";
                // internal const string Whirlwind = "C_Whirlwind";
                // internal const string WhirlwindSlam = "C_WhirlwindSlam";
            }

            /// <summary>
            /// Movement/dash animations used during combat.
            /// </summary>
            internal static class Dashes
            {
                // TODO: Add when animations exist
                // internal const string SpinDash = "C_SpinDash";
                // internal const string GapCloseDash = "C_GapClose";
            }

            /// <summary>
            /// Ultimate attack animations (Double Maximum Sweep).
            /// </summary>
            internal static class Ultimate
            {
                // TODO: Add when animations exist
                // internal const string WallJump = "C_WallJump";
                // internal const string LowSweep = "C_UltimateLowSweep";
                // internal const string MidSweep = "C_UltimateMidSweep";
                // internal const string Float = "C_Float";
                // internal const string ChargeUp = "C_ChargeUp";
                // internal const string MassiveStrike = "C_MassiveStrike";
            }

            /// <summary>
            /// Dual-wield system animations (spare weapon handling).
            /// </summary>
            internal static class DualWield
            {
                // TODO: Add when animations exist
                // internal const string PickupWeapon = "C_PickupSpare";
                // internal const string ReleaseWeapon = "C_ReleaseSpare";
                // internal const string DropWeapon = "C_DropSpare";
            }

            /// <summary>
            /// Reaction animations (hit reactions, stun, death).
            /// </summary>
            internal static class Reactions
            {
                // TODO: Add when animations exist
                // internal const string Flinch = "C_Flinch";
                // internal const string Stun = "C_Stunned";
                // internal const string Death = "C_Death";
            }

            /// <summary>
            /// Counter/parry response animations (when player counters Cleanser).
            /// </summary>
            internal static class CounterResponses
            {
                // TODO: Add when animations exist
                // internal const string Deflect = "C_Deflect";
                // internal const string CounterAttack = "C_CounterAttack";
            }
        }

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
        // TODO: Uncomment when animation exists
        // public void PlayCombatIdle(float transition = -1f) => CrossFade(CleanserAnim.Idle.Combat, transition);

        #endregion

        #region Locomotion Animations

        public void PlayWalk(float transition = -1f) => CrossFade(CleanserAnim.Locomotion.Walk, transition);
        // TODO: Uncomment when animations exist
        // public void PlayRun(float transition = -1f) => CrossFade(CleanserAnim.Locomotion.Run, transition);
        // public void PlayStrafe(float transition = -1f) => CrossFade(CleanserAnim.Locomotion.Strafe, transition);

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

        #endregion

        #region Basic Attack Animations

        public void PlayOverheadCleave() => CrossFade(CleanserAnim.BasicAttacks.OverheadCleave, attackTransition, true);
        public void PlaySpareToss() => CrossFade(CleanserAnim.BasicAttacks.SpareToss, attackTransition, true);

        // TODO: Uncomment when animations exist
        // public void PlayLunge() => CrossFade(CleanserAnim.BasicAttacks.Lunge, attackTransition, true);
        // public void PlaySlashIntoSlap() => CrossFade(CleanserAnim.BasicAttacks.SlashIntoSlap, attackTransition, true);
        // public void PlayRakeIntoSpinSlash() => CrossFade(CleanserAnim.BasicAttacks.RakeIntoSpinSlash, attackTransition, true);
        // public void PlaySpinSlashOnly() => CrossFade(CleanserAnim.BasicAttacks.SpinSlashOnly, attackTransition, true);
        // public void PlayLegSweep() => CrossFade(CleanserAnim.BasicAttacks.LegSweep, attackTransition, true);
        // public void PlayKnockback() => CrossFade(CleanserAnim.BasicAttacks.Knockback, attackTransition, true);
        // public void PlayMiniCrescentWave() => CrossFade(CleanserAnim.BasicAttacks.MiniCrescentWave, attackTransition, true);

        #endregion

        #region Strong Attack Animations

        // TODO: Uncomment when animations exist
        // public void PlayHighDive() => CrossFade(CleanserAnim.StrongAttacks.HighDive, attackTransition, true);
        // public void PlayAnimeDashSlash() => CrossFade(CleanserAnim.StrongAttacks.AnimeDashSlash, attackTransition, true);
        // public void PlayWhirlwind() => CrossFade(CleanserAnim.StrongAttacks.Whirlwind, attackTransition, true);
        // public void PlayWhirlwindSlam() => CrossFade(CleanserAnim.StrongAttacks.WhirlwindSlam, attackTransition, true);

        #endregion

        #region Dash Animations

        // TODO: Uncomment when animations exist
        // public void PlaySpinDash() => CrossFade(CleanserAnim.Dashes.SpinDash, attackTransition, true);
        // public void PlayGapCloseDash() => CrossFade(CleanserAnim.Dashes.GapCloseDash, attackTransition, true);

        #endregion

        #region Ultimate Animations

        // TODO: Uncomment when animations exist
        // public void PlayWallJump() => CrossFade(CleanserAnim.Ultimate.WallJump, attackTransition, true);
        // public void PlayUltimateLowSweep() => CrossFade(CleanserAnim.Ultimate.LowSweep, attackTransition, true);
        // public void PlayUltimateMidSweep() => CrossFade(CleanserAnim.Ultimate.MidSweep, attackTransition, true);
        // public void PlayFloat() => CrossFade(CleanserAnim.Ultimate.Float, defaultTransition);
        // public void PlayChargeUp() => CrossFade(CleanserAnim.Ultimate.ChargeUp, defaultTransition);
        // public void PlayMassiveStrike() => CrossFade(CleanserAnim.Ultimate.MassiveStrike, attackTransition, true);

        #endregion

        #region Dual-Wield Animations

        // TODO: Uncomment when animations exist
        // public void PlayPickupWeapon() => CrossFade(CleanserAnim.DualWield.PickupWeapon, attackTransition, true);
        // public void PlayReleaseWeapon() => CrossFade(CleanserAnim.DualWield.ReleaseWeapon, defaultTransition);
        // public void PlayDropWeapon() => CrossFade(CleanserAnim.DualWield.DropWeapon, defaultTransition);

        #endregion

        #region Reaction Animations

        // TODO: Uncomment when animations exist
        // public void PlayFlinch() => CrossFade(CleanserAnim.Reactions.Flinch, 0.02f, true);
        // public void PlayStun() => CrossFade(CleanserAnim.Reactions.Stun, 0.05f, true);
        // public void PlayDeath() => CrossFade(CleanserAnim.Reactions.Death, 0.02f, true);

        #endregion

        #region Counter Response Animations

        // TODO: Uncomment when animations exist
        // public void PlayDeflect() => CrossFade(CleanserAnim.CounterResponses.Deflect, attackTransition, true);
        // public void PlayCounterAttack() => CrossFade(CleanserAnim.CounterResponses.CounterAttack, attackTransition, true);

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

            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(layerIndex);
            bool isPlaying = info.IsName(stateName);
            if (isPlaying)
                normalizedTime = info.normalizedTime;

            return isPlaying;
        }

        /// <summary>
        /// Check if the Cleanser is currently playing the death animation.
        /// </summary>
        // TODO: Uncomment when death animation exists
        // public bool IsPlayingDeath(out float normalizedTime) => IsPlaying(CleanserAnim.Reactions.Death, out normalizedTime);

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

        #endregion
    }
}
