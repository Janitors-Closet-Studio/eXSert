using System;
using System.Collections;
using System.Collections.Generic;
using eXsert;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UI.Loading
{
    /// <summary>
    /// Controls the blackout/prop showcase loading screen. Lives inside the LoadingScene and persists via DontDestroyOnLoad.
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
        [SerializeField, Tooltip("Time (0-1 normalized) into the fade-out at which gameplay should resume so the player never sees a frozen scene.")]
        [Range(0.1f, 0.95f)] private float resumeThresholdNormalized = 0.3f;

        private InputAction loadingLookAction;
        private InputAction loadingZoomAction;
        private bool usingFallbackControls;
        private PlayerControls fallbackLoadingControls;
        private static Coroutine activeRoutine;
        private static bool isLoadingSequenceRunning;
        private static float resumeTimeScale = 1f;

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
            if (!Instance.isActiveAndEnabled)
            {
                if (loadSteps != null)
                {
                    Instance.StartCoroutine(loadSteps);
                }
                // return;
            }

            if (isLoadingSequenceRunning)
            {
                Debug.LogWarning($"[LoadingScreen] BeginLoading called while a loading sequence is already running. Ignoring to prevent duplicate loading overlays.\nStack:\n{Environment.StackTrace}");
                return;
            }

            // Defensive: if a previous coroutine reference remained for any reason, clear it now.
            activeRoutine = null;

            float targetMinimumDisplay = minimumDisplayOverride ?? Instance.minimumDisplaySeconds;
            activeRoutine = Instance.StartCoroutine(Instance.RunLoadingSequence(loadSteps, pauseGame, targetMinimumDisplay));
        }

        private IEnumerator RunLoadingSequence(IEnumerator loadSteps, bool pauseGame, float minimumDisplayDuration)
        {
            isLoadingSequenceRunning = true;

            bool didPause = false;

            try
            {
                resumeTimeScale = Mathf.Approximately(Time.timeScale, 0f) ? 1f : Time.timeScale;
                if (pauseGame)
                {
                    Time.timeScale = 0f;
                    didPause = true;
                }

                // Loading input (spin/zoom) disabled – we only show the prefab now.
                // EnableLoadingInput();

                bool enforceMinimum = minimumDisplayDuration > 0f;
                float minDisplayEndTime = 0f;

                yield return FadeBlack(0f, 1f);

                if (loadingCanvasRoot != null)
                    loadingCanvasRoot.SetActive(true);

                CursorBySchemeAndMap.SetForceHidden(true);

                propManager?.ShowRandomProp();

                yield return FadeBlack(1f, 0f);
                if (enforceMinimum)
                    minDisplayEndTime = Time.unscaledTime + minimumDisplayDuration;

                if (loadSteps != null)
                    yield return StartCoroutine(loadSteps);

                if (enforceMinimum)
                {
                    float remaining = minDisplayEndTime - Time.unscaledTime;
                    if (remaining > 0f)
                        yield return new WaitForSecondsRealtime(remaining);
                }

                yield return FadeBlack(0f, 1f);

                propManager?.ClearProp();
                if (loadingCanvasRoot != null)
                    loadingCanvasRoot.SetActive(false);

                yield return FadeOutAndResume(pauseGame);

                CursorBySchemeAndMap.SetForceHidden(false);

                // Loading input (spin/zoom) disabled – we only show the prefab now.
                // DisableLoadingInput();
            }
            finally
            {
                // If anything goes wrong mid-load (exception, interrupted coroutine), avoid leaving the game frozen.
                if (didPause && Mathf.Approximately(Time.timeScale, 0f))
                    Time.timeScale = resumeTimeScale;

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

        private IEnumerator FadeOutAndResume(bool pauseGame)
        {
            if (blackoutCanvasGroup == null)
            {
                if (pauseGame)
                    Time.timeScale = resumeTimeScale;
                yield break;
            }

            float timer = 0f;
            bool resumed = !pauseGame;
            float resumeThreshold = Mathf.Clamp(resumeThresholdNormalized, 0.05f, 0.95f);

            while (timer < fadeDuration)
            {
                timer += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(timer / fadeDuration);
                float curved = fadeCurve.Evaluate(t);
                blackoutCanvasGroup.alpha = Mathf.Lerp(1f, 0f, curved);

                if (!resumed && t >= resumeThreshold)
                {
                    Time.timeScale = resumeTimeScale;
                    resumed = true;
                }

                yield return null;
            }

            blackoutCanvasGroup.alpha = 0f;

            if (!resumed)
                Time.timeScale = resumeTimeScale;
        }

        private void EnableLoadingInput()
        {
            if (TryBindPlayerInputLoadingActions())
                return;

            if (TryBindFallbackLoadingActions())
                return;

            Debug.LogWarning("[LoadingScreen] LoadingLook/LoadingZoom actions not found; prop rotation disabled during loading.");
        }

        private void DisableLoadingInput()
        {
            UnhookPlayerInputAction(loadingLookAction, HandleLookPerformed);
            UnhookPlayerInputAction(loadingZoomAction, HandleZoomPerformed);
            loadingLookAction = null;
            loadingZoomAction = null;

            if (usingFallbackControls && fallbackLoadingControls != null)
            {
                fallbackLoadingControls.UI.Disable();
                usingFallbackControls = false;
            }

            propManager?.SetLookInput(Vector2.zero);
            propManager?.SetZoomInput(0f);
        }

        private void HandleLookPerformed(InputAction.CallbackContext context)
        {
            propManager?.SetLookInput(context.ReadValue<Vector2>());
        }

        private void HandleZoomPerformed(InputAction.CallbackContext context)
        {
            propManager?.SetZoomInput(context.ReadValue<float>());
        }

        private bool TryBindPlayerInputLoadingActions()
        {
            if (InputReader.Instance == null)
                return false;

            loadingLookAction = InputReader.Instance.LoadingLookAction;
            loadingZoomAction = InputReader.Instance.LoadingZoomAction;

            if (loadingLookAction == null && loadingZoomAction == null)
                return false;

            HookPlayerInputAction(loadingLookAction, HandleLookPerformed);
            HookPlayerInputAction(loadingZoomAction, HandleZoomPerformed);
            return true;
        }

        private bool TryBindFallbackLoadingActions()
        {
            if (fallbackLoadingControls == null)
            {
                fallbackLoadingControls = new PlayerControls();
            }

            loadingLookAction = fallbackLoadingControls.UI.LoadingLook;
            loadingZoomAction = fallbackLoadingControls.UI.LoadingZoom;

            if (loadingLookAction == null && loadingZoomAction == null)
                return false;

            fallbackLoadingControls.UI.Enable();
            HookPlayerInputAction(loadingLookAction, HandleLookPerformed);
            HookPlayerInputAction(loadingZoomAction, HandleZoomPerformed);
            usingFallbackControls = true;
            return true;
        }

        private static void HookPlayerInputAction(InputAction action, Action<InputAction.CallbackContext> callback)
        {
            if (action == null || callback == null)
                return;

            action.performed += callback;
            action.canceled += callback;
            if (!action.enabled)
                action.Enable();
        }

        private static void UnhookPlayerInputAction(InputAction action, Action<InputAction.CallbackContext> callback)
        {
            if (action == null || callback == null)
                return;

            action.performed -= callback;
            action.canceled -= callback;
        }
    }
}
