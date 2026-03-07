using System;
using System.Collections;
using System.Collections.Generic;
using eXsert;
using UnityEngine;
using Managers.TimeLord;

namespace UI.Loading
{
    /// <summary>
    /// Controls the blackout/prop showcase loading screen. Lives inside the LoadingScene and persists via DontDestroyOnLoad.
    /// Streamlined: pause/unpause is now coordinated by <see cref="Managers.PauseCoordinator"/>.
    /// </summary>
    public sealed class LoadingScreenController : MonoBehaviour
    {
        public static LoadingScreenController Instance { get; private set; }
        public static bool HasInstance => Instance != null;

        [Header("Scene References")]
        [SerializeField] private CanvasGroup blackoutCanvasGroup;
        [SerializeField] private GameObject loadingCanvasRoot;
        [SerializeField] private LoadingPropManager propManager;
        [SerializeField, Tooltip("Optional objects that should also persist when the loading scene becomes DontDestroyOnLoad.")]
        private List<GameObject> additionalPersistentRoots = new();

        [Header("Timings")]
        [SerializeField, Range(0.05f, 2f)] private float fadeDuration = 2f;
        [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        [SerializeField, Range(0f, 60f)] private float minimumDisplaySeconds = 1.5f;

        private static Coroutine activeRoutine;
        private static bool isLoadingSequenceRunning;

        private const string PauseOwnerId = "LoadingScreenController";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (blackoutCanvasGroup != null)
            {
                blackoutCanvasGroup.alpha = 0f;
            }

            if (loadingCanvasRoot != null)
            {
                loadingCanvasRoot.SetActive(false);
            }

            DontDestroyOnLoad(gameObject);
            foreach (var root in additionalPersistentRoots)
            {
                if (root != null)
                    DontDestroyOnLoad(root);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>
        /// Begins the loading workflow. The supplied routine performs the actual scene loading work.
        /// </summary>
        public static void BeginLoading(IEnumerator loadSteps, bool pauseGame = true, float? minimumDisplayOverride = null)
        {
            if (Instance == null)
            {
                Debug.LogWarning("[LoadingScreen] No LoadingScreenController instance available. Loading will proceed without overlay.");
                return;
            }

            if (!Instance.isActiveAndEnabled)
            {
                if (loadSteps != null)
                {
                    Instance.StartCoroutine(loadSteps);
                }
                return;
            }

            if (isLoadingSequenceRunning)
            {
                Debug.LogWarning($"[LoadingScreen] BeginLoading called while a loading sequence is already running. Ignoring to prevent duplicate loading overlays.\nStack:\n{Environment.StackTrace}");
                return;
            }

            activeRoutine = null;

            float targetMinimumDisplay = minimumDisplayOverride ?? Instance.minimumDisplaySeconds;
            activeRoutine = Instance.StartCoroutine(Instance.RunLoadingSequence(loadSteps, pauseGame, targetMinimumDisplay));
        }

        private IEnumerator RunLoadingSequence(IEnumerator loadSteps, bool pauseGame, float minimumDisplayDuration)
        {
            isLoadingSequenceRunning = true;
            string pauseToken = null;

            try
            {
                // Request pause via coordinator so it becomes authoritative.
                if (pauseGame)
                {
                    pauseToken = PauseCoordinator.RequestPause(PauseOwnerId);
                }

                bool enforceMinimum = minimumDisplayDuration > 0f;
                float minDisplayEndTime = 0f;

                // Fade to black (unscaled)
                yield return FadeBlack(0f, 1f);

                if (loadingCanvasRoot != null)
                    loadingCanvasRoot.SetActive(true);

                CursorBySchemeAndMap.SetForceHidden(true);

                propManager?.ShowRandomProp();

                // Fade in loading content
                yield return FadeBlack(1f, 0f);

                if (enforceMinimum)
                    minDisplayEndTime = Time.unscaledTime + minimumDisplayDuration;

                // Run actual loading steps (scene load, asset load, etc.)
                if (loadSteps != null)
                    yield return StartCoroutine(loadSteps);

                // Enforce minimum display time (uses unscaled time so timescale==0 doesn't affect it)
                if (enforceMinimum)
                {
                    float remaining = minDisplayEndTime - Time.unscaledTime;
                    if (remaining > 0f)
                        yield return new WaitForSecondsRealtime(remaining);
                }

                // Fade back to black (unscaled)
                yield return FadeBlack(0f, 1f);

                propManager?.ClearProp();
                if (loadingCanvasRoot != null)
                    loadingCanvasRoot.SetActive(false);

                // Fade out and then release our pause request (if we had one)
                yield return FadeOutAndReleasePause(pauseToken);

                CursorBySchemeAndMap.SetForceHidden(false);
            }
            finally
            {
                // Ensure our pause request is released even if something fails during the sequence.
                if (!string.IsNullOrWhiteSpace(pauseToken))
                    PauseCoordinator.ReleaseTimeScale(pauseToken);

                isLoadingSequenceRunning = false;
                activeRoutine = null;
            }
        }

        private IEnumerator FadeBlack(float from, float to)
        {
            if (blackoutCanvasGroup == null)
                yield break;

            float timer = 0f;
            while (timer < fadeDuration)
            {
                timer += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(timer / fadeDuration);
                float curved = fadeCurve.Evaluate(t);
                blackoutCanvasGroup.alpha = Mathf.Lerp(from, to, curved);
                yield return null;
            }

            blackoutCanvasGroup.alpha = to;
        }

        private IEnumerator FadeOutAndReleasePause(string pauseToken)
        {
            if (blackoutCanvasGroup == null)
            {
                if (!string.IsNullOrWhiteSpace(pauseToken))
                    PauseCoordinator.ReleaseTimeScale(pauseToken);
                yield break;
            }

            float timer = 0f;

            while (timer < fadeDuration)
            {
                timer += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(timer / fadeDuration);
                float curved = fadeCurve.Evaluate(t);
                blackoutCanvasGroup.alpha = Mathf.Lerp(1f, 0f, curved);
                yield return null;
            }

            blackoutCanvasGroup.alpha = 0f;

            if (!string.IsNullOrWhiteSpace(pauseToken))
                PauseCoordinator.ReleaseTimeScale(pauseToken);
        }
    }
}
