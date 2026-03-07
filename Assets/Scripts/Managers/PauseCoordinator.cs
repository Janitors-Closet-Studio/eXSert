using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Managers.TimeLord
{
    /// <summary>
    /// Central authority for time scale coordination.
    /// Systems ask for a time scale (0 = paused, <1 = slow-mo, 1 = normal) and release it when done.
    /// The effective Time.timeScale becomes the smallest requested scale (so pause wins over slow-mo).
    /// When the first request is made the previous Time.timeScale / fixedDeltaTime are saved and restored
    /// only when the last request is released.
    /// </summary>
    public static class PauseCoordinator
    {
        // ownerId -> requested scale (0..inf). Lower value wins. null/empty ownerId is rejected.
        private static readonly Dictionary<string, float> _activeRequests = new();
        private static float _savedTimeScale = 1f;
        private static float _savedFixedDeltaTime = 0.02f;
        private static readonly object _lock = new();

        // Tracks the last value the coordinator applied (useful for monitors/debug).
        private static float _lastAppliedEffectiveScale = 1f;

        /// <summary>
        /// Request a pause (timescale 0). Returns the owner id (useful if you passed null/empty to generate one).
        /// </summary>
        public static string RequestPause(string ownerId = null)
            => RequestTimeScale(ownerId, 0f);

        /// <summary>
        /// Request a specific time scale (0 = paused, 0.5 = half speed, 1 = normal).
        /// If ownerId is null/empty a GUID will be generated and returned.
        /// </summary>
        public static string RequestTimeScale(string ownerId, float scale)
        {
            if (string.IsNullOrWhiteSpace(ownerId))
                ownerId = Guid.NewGuid().ToString();

            scale = Mathf.Max(0f, scale); // clamp negative values

            lock (_lock)
            {
                var wasEmpty = _activeRequests.Count == 0;
                if (wasEmpty)
                {
                    // Save current state to restore later.
                    _savedTimeScale = Mathf.Approximately(Time.timeScale, 0f) ? 1f : Time.timeScale;
                    _savedFixedDeltaTime = Time.fixedDeltaTime;
                }

                _activeRequests[ownerId] = scale;
                ApplyEffectiveTimeScale();

#if UNITY_EDITOR
                Debug.Log($"[PauseCoordinator] RequestTimeScale: '{ownerId}' -> {scale}. Active owners: {string.Join(", ", _activeRequests.Keys)}");
#endif
            }

            return ownerId;
        }

        /// <summary>
        /// Release a previously requested owner id (from RequestTimeScale / RequestPause).
        /// When the last owner is removed the saved timescale/fixedDeltaTime are restored.
        /// </summary>
        public static void ReleaseTimeScale(string ownerId)
        {
            if (string.IsNullOrWhiteSpace(ownerId))
                return;

            lock (_lock)
            {
                if (!_activeRequests.Remove(ownerId))
                    return;

                if (_activeRequests.Count == 0)
                {
                    // Restore saved values
                    Time.timeScale = _savedTimeScale;
                    Time.fixedDeltaTime = _savedFixedDeltaTime;
                    _lastAppliedEffectiveScale = _savedTimeScale;
#if UNITY_EDITOR
                    Debug.Log($"[PauseCoordinator] Release '{ownerId}'. No owners remaining -> restoring timescale {_savedTimeScale} and fixedDeltaTime {_savedFixedDeltaTime}.");
#endif
                }
                else
                {
                    ApplyEffectiveTimeScale();
#if UNITY_EDITOR
                    Debug.Log($"[PauseCoordinator] Release '{ownerId}'. Remaining owners: {string.Join(", ", _activeRequests.Keys)} -> current timescale {Time.timeScale}.");
#endif
                }
            }
        }

        /// <summary>
        /// Force clear all requests and restore original timescale/fixedDeltaTime.
        /// </summary>
        public static void ForceUnpause()
        {
            lock (_lock)
            {
                _activeRequests.Clear();
                Time.timeScale = _savedTimeScale;
                Time.fixedDeltaTime = _savedFixedDeltaTime;
                _lastAppliedEffectiveScale = _savedTimeScale;
#if UNITY_EDITOR
                Debug.Log($"[PauseCoordinator] ForceUnpause. Restored timescale {_savedTimeScale} and fixedDeltaTime {_savedFixedDeltaTime}.");
#endif
            }
        }

        /// <summary>
        /// Returns true when there is at least one active time-scale request.
        /// </summary>
        public static bool IsPausedOrAltered
        {
            get
            {
                lock (_lock)
                {
                    return _activeRequests.Count > 0;
                }
            }
        }

        /// <summary>
        /// Snapshot of current active owner ids.
        /// </summary>
        public static IReadOnlyCollection<string> ActiveOwners
        {
            get
            {
                lock (_lock)
                {
                    return _activeRequests.Keys.ToArray();
                }
            }
        }

        /// <summary>
        /// The timescale value the coordinator currently believes should be applied.
        /// If there are active requests this is the minimum requested scale; otherwise the saved timescale.
        /// Use this from monitors to detect unauthorized modifications.
        /// </summary>
        public static float CurrentEffectiveTimeScale
        {
            get
            {
                lock (_lock)
                {
                    return _activeRequests.Count == 0 ? _savedTimeScale : _activeRequests.Values.Min();
                }
            }
        }

        /// <summary>
        /// Re-apply the coordinator's effective timescale and fixedDeltaTime immediately.
        /// Useful if some external code illegally modified Time.timeScale and you want to correct it.
        /// </summary>
        public static void ReapplyEffectiveTimeScale()
        {
            lock (_lock)
            {
                if (_activeRequests.Count == 0)
                {
                    Time.timeScale = _savedTimeScale;
                    Time.fixedDeltaTime = _savedFixedDeltaTime;
                    _lastAppliedEffectiveScale = _savedTimeScale;
                }
                else
                {
                    var effective = _activeRequests.Values.Min();
                    Time.timeScale = effective;
                    Time.fixedDeltaTime = Mathf.Max(0.0001f, 0.02f * effective);
                    _lastAppliedEffectiveScale = effective;
                }
            }
        }

        private static void ApplyEffectiveTimeScale()
        {
            // Lower (smaller) timescale wins: pause (0) overrides slow-mo.
            var effective = _activeRequests.Values.Min();

            // Apply timescale and scale fixedDeltaTime accordingly.
            Time.timeScale = effective;
            // Keep fixedDeltaTime consistent with timescale to avoid physics step issues.
            Time.fixedDeltaTime = Mathf.Max(0.0001f, 0.02f * effective);
            _lastAppliedEffectiveScale = effective;
        }
    }
}