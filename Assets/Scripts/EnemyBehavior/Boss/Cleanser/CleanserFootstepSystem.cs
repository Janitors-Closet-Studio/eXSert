// CleanserFootstepSystem.cs
// Purpose: Plays randomized footstep SFX for the Cleanser boss, synchronized to the walk animation
//          via animation events and with speed-relative cadence during Anime Dash.
// Works with: CleanserBrain (calls PlayFootstep, BeginAnimeDashFootsteps, EndAnimeDashFootsteps,
//             SuppressFootsteps, ResumeFootsteps)

using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace EnemyBehavior.Boss.Cleanser
{
    /// <summary>
    /// Manages Cleanser footstep audio.
    ///
    /// Walking: driven by animation events on the walk/idle-walk cycle.
    ///   - <see cref="PlayFootstep"/> is called by the walk animation event.
    ///   - The cooldown between steps scales with the Cleanser's current walk speed so
    ///     that SFX timing tracks the animation playback rate (set via
    ///     <see cref="SetWalkSpeedGetter"/>).
    ///   - Footsteps are suppressed when <see cref="SuppressFootsteps"/> is active
    ///     (e.g., SpinDash, attacks, ultimate) so the wrong clips don't fire.
    ///
    /// Anime Dash: driven by a coroutine that plays footsteps at a rate proportional
    ///   to the Cleanser's actual movement speed.
    ///
    /// SpinDash: footsteps are fully suppressed while the SpinDash hitbox phase is
    ///   active (call <see cref="SuppressFootsteps"/> / <see cref="ResumeFootsteps"/>
    ///   from CleanserBrain around the spin phase).
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class CleanserFootstepSystem : MonoBehaviour
    {
        [Header("Footstep Clips")]
        [Tooltip("Pool of footstep audio clips. One is chosen at random for each step.")]
        [SerializeField] private AudioClip[] footstepClips = new AudioClip[0];

        [Tooltip("Volume for footstep playback (0-1).")]
        [SerializeField, Range(0f, 1f)] private float footstepVolume = 0.7f;

        [Header("Animation-Aligned Walking")]
        [Tooltip("Minimum seconds between two animation-event-driven footsteps. " +
                 "Acts only as a spam-guard against duplicate events firing on the same frame. " +
                 "Timing sync is handled by the animator speed multiplier, not this value — keep it short.")]
        [SerializeField, Min(0.02f)] private float walkFootstepMinGap = 0.08f;

        [Header("Anime Dash Footsteps")]
        [Tooltip("Movement speed (units/sec) considered 'normal' Anime Dash speed. " +
                 "Step rate scales linearly with actual speed relative to this value.")]
        [SerializeField, Min(0.1f)] private float animeDashReferenceSpeed = 12f;

        [Tooltip("Target gap between footsteps (seconds) at the reference Anime Dash speed. " +
                 "Steps fire faster when accelerating and slower when decelerating.")]
        [SerializeField, Min(0.02f)] private float animeDashBaseInterval = 0.18f;

        // Resolved automatically via RequireComponent — no Inspector slot needed.
        private AudioSource audioSource;

        // ── Internal state ──────────────────────────────────────────────────────
        private float lastWalkFootstepTime = -999f;
        private int suppressionDepth;          // Reference-counted: 0 = footsteps active
        private bool animeDashActive;
        private Coroutine animeDashCoroutine;

        // ── Public surface ──────────────────────────────────────────────────────

        /// <summary>True while footsteps are suppressed (e.g., SpinDash or attack phases).</summary>
        public bool IsSuppressed => suppressionDepth > 0;

        // ── Unity ───────────────────────────────────────────────────────────────

        private void Awake()
        {
            // Grab the AudioSource that RequireComponent guarantees is on this object.
            audioSource = GetComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            // 3D spatial blend so footsteps are positional in the world.
            audioSource.spatialBlend = 1f;
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.minDistance = 2f;
            audioSource.maxDistance = 30f;
        }

        // ── API called by CleanserBrain ─────────────────────────────────────────

        /// <summary>
        /// Called by a walk-animation event on each footfall.
        /// Only plays if footsteps are not suppressed and the cooldown has elapsed.
        /// </summary>
        public void PlayFootstep()
        {
            if (suppressionDepth > 0)
                return;

            if (animeDashActive)
                return;

            float now = Time.time;
            if (now - lastWalkFootstepTime < walkFootstepMinGap)
                return;

            lastWalkFootstepTime = now;
            PlayRandomClip();
        }

        /// <summary>
        /// Increments the suppression counter. Footsteps will not play while this is > 0.
        /// Call <see cref="ResumeFootsteps"/> once per call to this method.
        /// </summary>
        public void SuppressFootsteps()
        {
            suppressionDepth++;
        }

        /// <summary>
        /// Decrements the suppression counter. Footsteps resume when counter reaches 0.
        /// </summary>
        public void ResumeFootsteps()
        {
            suppressionDepth = Mathf.Max(0, suppressionDepth - 1);
        }

        /// <summary>
        /// Starts automatic footstep playback at a cadence driven by <paramref name="speedGetter"/>.
        /// Used during Anime Dash so footsteps track the actual movement velocity.
        /// </summary>
        /// <param name="speedGetter">Delegate returning current movement speed (units/sec). Queried each interval.</param>
        public void BeginAnimeDashFootsteps(System.Func<float> speedGetter)
        {
            if (animeDashActive)
                EndAnimeDashFootsteps();

            animeDashActive = true;
            animeDashCoroutine = StartCoroutine(AnimeDashFootstepCoroutine(speedGetter));
        }

        /// <summary>
        /// Stops the Anime Dash footstep coroutine.
        /// </summary>
        public void EndAnimeDashFootsteps()
        {
            animeDashActive = false;
            if (animeDashCoroutine != null)
            {
                StopCoroutine(animeDashCoroutine);
                animeDashCoroutine = null;
            }
        }

        // ── Internal ────────────────────────────────────────────────────────────

        private IEnumerator AnimeDashFootstepCoroutine(System.Func<float> speedGetter)
        {
            // Phase accumulator approach: advance a counter each frame by
            //   Δt × (currentSpeed / referenceSpeed) / baseInterval
            // and fire a step each time it crosses 1.0.
            // This responds continuously to acceleration and deceleration rather
            // than committing to a fixed wait at the start of each interval.
            float phase = 0f;

            while (animeDashActive)
            {
                yield return null; // advance one frame at a time

                float speed = speedGetter != null ? speedGetter() : animeDashReferenceSpeed;

                if (speed > 0.01f)
                {
                    // How much of one step-interval elapses this frame at current speed.
                    // baseInterval is the target gap at referenceSpeed, so normalising by
                    // it converts real seconds into "step progress" units.
                    float stepRate = (speed / animeDashReferenceSpeed) / animeDashBaseInterval;
                    phase += Time.deltaTime * stepRate;

                    while (phase >= 1f)
                    {
                        phase -= 1f;

                        if (suppressionDepth == 0)
                            PlayRandomClip();
                    }
                }
                // If nearly stopped don't advance phase — no footsteps while standing still.
            }
        }

        private void PlayRandomClip()
        {
            if (footstepClips == null || footstepClips.Length == 0)
                return;

            int attempts = 0;
            AudioClip clip = null;
            while (clip == null && attempts < footstepClips.Length * 2)
            {
                clip = footstepClips[Random.Range(0, footstepClips.Length)];
                attempts++;
            }

            if (clip == null)
                return;

            audioSource.PlayOneShot(clip, footstepVolume);
        }


    }
}
