using System;
using UnityEngine;

namespace Managers.TimeLord
{
    /// <summary>
    /// Runtime monitor that detects unexpected direct writes to Time.timeScale and auto-corrects (and logs).
    /// This class is created automatically at startup.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class PauseCoordinatorMonitor : MonoBehaviour
    {
        private const float Epsilon = 0.0001f;
        private float _lastObservedScale;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureExists()
        {
            var go = new GameObject("PauseCoordinatorMonitor", typeof(PauseCoordinatorMonitor));
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            _lastObservedScale = Time.timeScale;
        }

        private void Update()
        {
            float observed = Time.timeScale;
            float expected = PauseCoordinator.CurrentEffectiveTimeScale;

            // If coordinator expects a different value, log and correct.
            if (!Mathf.Approximately(observed, expected) && Math.Abs(observed - expected) > Epsilon)
            {
                Debug.LogWarning($"[PauseCoordinatorMonitor] Detected Time.timeScale change. Observed={observed:F4}, Expected={expected:F4}. ActiveOwners=[{string.Join(", ", PauseCoordinator.ActiveOwners)}]. Reapplying coordinator value and capturing stack trace.");

                // Capture a lightweight stack trace so you can inspect where things are happening around the time of the detection.
                string stack = Environment.StackTrace;
                Debug.LogWarning(stack);

                // Re-apply the coordinator's authoritative timescale immediately.
                PauseCoordinator.ReapplyEffectiveTimeScale();

                // Update last observed to avoid spamming same log in same frame.
                _lastObservedScale = PauseCoordinator.CurrentEffectiveTimeScale;
            }
            else
            {
                // Keep last observed in sync when no mismatch.
                _lastObservedScale = observed;
            }
        }
    }
}