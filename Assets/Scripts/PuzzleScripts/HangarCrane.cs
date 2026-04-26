using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

[System.Serializable]
public class HangarCranePart
{
    public Transform partTransform;
    public float swayAmount;
    public float swaySpeed;
}


public class HangarCrane : CranePuzzle, IConsoleSelectable
{
    private static readonly FieldInfo UniversalRendererIndexField = typeof(UniversalAdditionalCameraData)
        .GetField("m_RendererIndex", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo UniversalDefaultRendererIndexField = typeof(UniversalRenderPipelineAsset)
        .GetField("m_DefaultRendererIndex", BindingFlags.Instance | BindingFlags.NonPublic);

    public List<HangarCranePart> hangarCraneParts = new List<HangarCranePart>();

    [SerializeField]
    private float cancelReturnSpeed = 2f;

    [Header("Puzzle Camera Follow")]
    [SerializeField, Tooltip("When enabled, keeps the puzzle camera following the crane beam target while clamping how far the camera can travel in local space.")]
    private bool useBoundedPuzzleCameraFollow = true;
    [SerializeField, Tooltip("Target the puzzle camera tries to stay centered on, usually the moving crane beam transform.")]
    private Transform puzzleCameraFollowTarget;
    [SerializeField, Tooltip("Optional transform whose local space defines the camera bounds. If left empty, the puzzle camera parent is used.")]
    private Transform puzzleCameraFollowSpace;
    [SerializeField, Tooltip("Local offset applied after matching the follow target. Use this to keep the framing slightly ahead or behind the beam.")]
    private Vector3 puzzleCameraFollowOffset = Vector3.zero;
    [SerializeField, Tooltip("Minimum and maximum local X positions the puzzle camera can move to while following.")]
    private Vector2 puzzleCameraLocalXLimits = new Vector2(-17.86f, 11.6f);
    [SerializeField, Tooltip("Minimum and maximum local Z positions the puzzle camera can move to while following.")]
    private Vector2 puzzleCameraLocalZLimits = new Vector2(-86.3f, -64f);
    [SerializeField, Min(0f), Tooltip("How quickly the puzzle camera settles toward the bounded follow target. Set to 0 for an immediate snap.")]
    private float puzzleCameraFollowSmoothTime = 0.12f;
    [SerializeField, Tooltip("Preserves the camera's starting local Y while only following the crane beam on X and Z.")]
    private bool preservePuzzleCameraLocalY = true;

    [Header("Puzzle Transition")]
    [SerializeField, Min(0f)] private float puzzleTransitionFadeDurationSeconds = 0.35f;
    [SerializeField, Min(0f)] private float puzzleTransitionRevealDurationSeconds = 0.5f;
    [SerializeField, Min(0f)] private float puzzleTransitionBlackHoldSeconds = 0.08f;
    [SerializeField, Min(0f)] private float puzzleTransitionReturnBlackHoldSeconds = 0.2f;
    [SerializeField, Tooltip("Temporarily enables a fullscreen renderer feature on the gameplay output camera while the crane puzzle is active.")]
    private bool usePuzzleTransitionRenderer = false;
    [SerializeField, Tooltip("Name of the renderer feature to toggle on the output renderer, for example 'FullScreenPassRendererFeature'.")]
    private string puzzleTransitionRendererFeatureName = "FullScreenPassRendererFeature";
    [SerializeField, Tooltip("Optional output camera to swap renderers on. If left empty, the script uses Camera.main, then the first CinemachineBrain camera.")]
    private Camera puzzleTransitionOutputCamera;
    [SerializeField, Tooltip("Loaded scene that contains the player prefab camera used for normal gameplay.")]
    private string puzzleTransitionOutputSceneName = "PlayerScene";
    [SerializeField, Tooltip("Camera object name to prefer inside the player scene when swapping the renderer.")]
    private string puzzleTransitionOutputCameraName = "MainCamera";
    [SerializeField, Tooltip("Camera tag to prefer inside the player scene when swapping the renderer.")]
    private string puzzleTransitionOutputCameraTag = "PlayerCamera";

    // Store original local positions for HangarCranePart
    private new Dictionary<HangarCranePart, Vector3> cranePartStartLocalPositions =
        new Dictionary<HangarCranePart, Vector3>();

    // Store current sway state for each part
    private class SwayState
    {
        public Vector3 velocity = Vector3.zero;
        public Vector3 offset = Vector3.zero;
    }

    private Dictionary<HangarCranePart, SwayState> swayStates =
        new Dictionary<HangarCranePart, SwayState>();

    private PuzzleInteraction activeConsoleInteraction;
    private bool isReturningToStart;
    private Coroutine puzzleTransitionRoutine;
    private bool isPuzzleTransitionActive;
    private bool pendingPuzzleExitAfterEntry;
    private bool isPuzzleTransitionRendererApplied;
    private Camera cachedPuzzleTransitionOutputCamera;
    private UniversalAdditionalCameraData cachedPuzzleTransitionCameraData;
    private ScriptableRendererFeature cachedPuzzleTransitionRendererFeature;
    private bool cachedPuzzleTransitionRendererFeatureActive;
    private Vector3 cachedPuzzleCameraInitialLocalPosition;
    private bool hasCachedPuzzleCameraInitialLocalPosition;
    private Vector3 puzzleCameraFollowVelocity;

    private void Awake()
    {
        // Cache original local positions and initialize sway states
        foreach (var part in hangarCraneParts)
        {
            if (part != null && part.partTransform != null)
            {
                cranePartStartLocalPositions[part] = part.partTransform.localPosition;
                swayStates[part] = new SwayState();
            }
        }

        CachePuzzleCameraInitialLocalPosition();
    }

    private void Update()
    {
        SyncPuzzleTransitionRendererState();
    }

    private void LateUpdate()
    {
        UpdatePuzzleCameraFollow();

        float deltaTime = Time.deltaTime;
        CraneMovementDirection dir = GetCurrentMovementDirection();
        foreach (var part in hangarCraneParts)
        {
            if (part == null || part.partTransform == null)
                continue;

            if (!cranePartStartLocalPositions.ContainsKey(part))
                continue;

            // Determine sway direction based on movement
            Vector3 swayDir = Vector3.zero;
            if (isMoving)
            {
                if (dir == CraneMovementDirection.Left || dir == CraneMovementDirection.Right)
                    swayDir = Vector3.forward;
                else if (dir == CraneMovementDirection.Up || dir == CraneMovementDirection.Down)
                    swayDir = Vector3.right;
                else if (
                    dir == CraneMovementDirection.Forward
                    || dir == CraneMovementDirection.Backward
                )
                    swayDir = Vector3.right;
            }

            // Target offset is a sinusoidal oscillation while moving, zero when stopped
            float swayTarget = isMoving
                ? Mathf.Sin(Time.time * part.swaySpeed) * part.swayAmount
                : 0f;
            Vector3 targetOffset = swayDir * swayTarget;

            // Spring-damped interpolation for smooth, natural sway
            SwayState state = swayStates[part];
            float smoothTime = 0.18f; // Lower = snappier, higher = more damped
            state.offset = Vector3.SmoothDamp(
                state.offset,
                targetOffset,
                ref state.velocity,
                smoothTime,
                Mathf.Infinity,
                deltaTime
            );

            // Apply visual sway (localPosition)
            part.partTransform.localPosition = cranePartStartLocalPositions[part] + state.offset;
        }
    }

    private void OnDisable()
    {
        RestorePuzzleTransitionRendererIfNeeded();
        puzzleCameraFollowVelocity = Vector3.zero;
    }

    public override void ConsoleInteracted()
    {
        if (isReturningToStart)
        {
            return;
        }

        activeConsoleInteraction?.SetInteractionEnabled(false);
        BeginPuzzleEntryTransition();
    }

    public void ConsoleInteracted(PuzzleInteraction interaction)
    {
        if (isReturningToStart)
        {
            return;
        }

        activeConsoleInteraction = interaction;
        activeConsoleInteraction?.SetInteractionEnabled(false);
        BeginPuzzleEntryTransition();
    }

    public override void StartPuzzle()
    {
        if (isReturningToStart)
        {
            return;
        }

        base.StartPuzzle();
    }

    protected override void CheckForConfirm()
    {
        if (IsConfirmTriggered())
            EndPuzzle();
    }

    public override void EndPuzzle()
    {
        if (isPuzzleTransitionActive)
        {
            pendingPuzzleExitAfterEntry = true;
            return;
        }

        BeginPuzzleExitTransition();
    }

    protected override bool HandleEscapeTriggered()
    {
        if (isAutomatedMovement || isReturningToStart)
        {
            return true;
        }

        isReturningToStart = true;
        isAutomatedMovement = true;
        isMoving = false;
        activeConsoleInteraction?.SetInteractionEnabled(false);
        StartCoroutine(ReturnCraneToStartAndExit());
        return true;
    }

    private void BeginPuzzleEntryTransition()
    {
        if (isPuzzleTransitionActive)
            return;

        if (puzzleTransitionRoutine != null)
            StopCoroutine(puzzleTransitionRoutine);

        puzzleTransitionRoutine = StartCoroutine(PuzzleEntryTransitionRoutine());
    }

    private void BeginPuzzleExitTransition()
    {
        if (isPuzzleTransitionActive)
            return;

        if (puzzleTransitionRoutine != null)
            StopCoroutine(puzzleTransitionRoutine);

        puzzleTransitionRoutine = StartCoroutine(PuzzleExitTransitionRoutine());
    }

    private IEnumerator PuzzleEntryTransitionRoutine()
    {
        isPuzzleTransitionActive = true;
        pendingPuzzleExitAfterEntry = false;

        ScreenFadeOverlay.Instance.SetImmediate(0f);
        yield return null;

        yield return FadeTo(1f, puzzleTransitionFadeDurationSeconds);

        float blackHoldDuration = Mathf.Max(0f, puzzleTransitionBlackHoldSeconds);
        if (blackHoldDuration > 0f)
            yield return new WaitForSecondsRealtime(blackHoldDuration);

        base.ConsoleInteracted();
        SyncPuzzleTransitionRendererState();

        yield return FadeTo(0f, puzzleTransitionRevealDurationSeconds);

        isPuzzleTransitionActive = false;
        puzzleTransitionRoutine = null;

        if (pendingPuzzleExitAfterEntry)
            BeginPuzzleExitTransition();
    }

    private IEnumerator PuzzleExitTransitionRoutine()
    {
        isPuzzleTransitionActive = true;
        pendingPuzzleExitAfterEntry = false;

        yield return FadeTo(1f, puzzleTransitionFadeDurationSeconds);

        CompletePuzzleExitWithoutStoppingCoroutines();
        RestorePuzzleTransitionRendererIfNeeded();

        float blackHoldDuration = Mathf.Max(0f, puzzleTransitionReturnBlackHoldSeconds);
        if (blackHoldDuration > 0f)
            yield return new WaitForSecondsRealtime(blackHoldDuration);

        yield return FadeTo(0f, puzzleTransitionRevealDurationSeconds);

        isPuzzleTransitionActive = false;
        puzzleTransitionRoutine = null;
        activeConsoleInteraction?.SetInteractionEnabled(true);
    }

    private static IEnumerator FadeTo(float targetAlpha, float durationSeconds)
    {
        if (ScreenFadeOverlay.Instance == null)
            yield break;

        yield return ScreenFadeOverlay.Instance.FadeTo(targetAlpha, durationSeconds);
    }

    private void CachePuzzleCameraInitialLocalPosition()
    {
        if (puzzleCamera == null)
            return;

        Transform followSpace = GetPuzzleCameraFollowSpace();
        if (followSpace == null)
        {
            cachedPuzzleCameraInitialLocalPosition = puzzleCamera.transform.localPosition;
        }
        else
        {
            cachedPuzzleCameraInitialLocalPosition = followSpace.InverseTransformPoint(puzzleCamera.transform.position);
        }

        hasCachedPuzzleCameraInitialLocalPosition = true;
    }

    private void UpdatePuzzleCameraFollow()
    {
        if (!useBoundedPuzzleCameraFollow || !IsCranePuzzleActive)
            return;

        if (puzzleCamera == null || puzzleCameraFollowTarget == null)
            return;

        Transform followSpace = GetPuzzleCameraFollowSpace();
        if (followSpace == null)
            return;

        if (!hasCachedPuzzleCameraInitialLocalPosition)
            CachePuzzleCameraInitialLocalPosition();

        Vector3 targetLocalPosition = followSpace.InverseTransformPoint(puzzleCameraFollowTarget.position) + puzzleCameraFollowOffset;
        Vector3 currentLocalPosition = followSpace.InverseTransformPoint(puzzleCamera.transform.position);

        float minX = Mathf.Min(puzzleCameraLocalXLimits.x, puzzleCameraLocalXLimits.y);
        float maxX = Mathf.Max(puzzleCameraLocalXLimits.x, puzzleCameraLocalXLimits.y);
        float minZ = Mathf.Min(puzzleCameraLocalZLimits.x, puzzleCameraLocalZLimits.y);
        float maxZ = Mathf.Max(puzzleCameraLocalZLimits.x, puzzleCameraLocalZLimits.y);

        Vector3 desiredLocalPosition = currentLocalPosition;
        desiredLocalPosition.x = Mathf.Clamp(targetLocalPosition.x, minX, maxX);
        desiredLocalPosition.z = Mathf.Clamp(targetLocalPosition.z, minZ, maxZ);
        desiredLocalPosition.y = preservePuzzleCameraLocalY
            ? cachedPuzzleCameraInitialLocalPosition.y
            : targetLocalPosition.y;

        Vector3 nextLocalPosition = puzzleCameraFollowSmoothTime <= 0f
            ? desiredLocalPosition
            : Vector3.SmoothDamp(
                currentLocalPosition,
                desiredLocalPosition,
                ref puzzleCameraFollowVelocity,
                puzzleCameraFollowSmoothTime,
                Mathf.Infinity,
                Time.unscaledDeltaTime);

        puzzleCamera.transform.position = followSpace.TransformPoint(nextLocalPosition);
    }

    private Transform GetPuzzleCameraFollowSpace()
    {
        if (puzzleCameraFollowSpace != null)
            return puzzleCameraFollowSpace;

        return puzzleCamera != null ? puzzleCamera.transform.parent : null;
    }

    private void SyncPuzzleTransitionRendererState()
    {
        if (!usePuzzleTransitionRenderer)
        {
            RestorePuzzleTransitionRendererIfNeeded();
            return;
        }

        if (!IsCranePuzzleActive)
        {
            RestorePuzzleTransitionRendererIfNeeded();
            return;
        }

        if (!isPuzzleTransitionRendererApplied)
            ApplyPuzzleTransitionRendererIfNeeded();
    }

    private void ApplyPuzzleTransitionRendererIfNeeded()
    {
        if (!usePuzzleTransitionRenderer || isPuzzleTransitionRendererApplied)
            return;

        if (!TryGetPuzzleTransitionOutputCamera(out Camera outputCamera))
        {
            Debug.LogWarning("[HangarCrane] Could not find an output camera for the puzzle transition renderer swap.");
            return;
        }

        UniversalAdditionalCameraData additionalCameraData = outputCamera.GetUniversalAdditionalCameraData();
        if (additionalCameraData == null)
        {
            Debug.LogWarning($"[HangarCrane] Output camera '{outputCamera.name}' does not have UniversalAdditionalCameraData.");
            return;
        }

        if (!TryGetRendererFeature(additionalCameraData, out ScriptableRendererFeature rendererFeature))
        {
            Debug.LogWarning($"[HangarCrane] Could not find renderer feature '{puzzleTransitionRendererFeatureName}' on the output renderer.");
            return;
        }

        cachedPuzzleTransitionOutputCamera = outputCamera;
        cachedPuzzleTransitionCameraData = additionalCameraData;
        cachedPuzzleTransitionRendererFeature = rendererFeature;
        cachedPuzzleTransitionRendererFeatureActive = rendererFeature.isActive;
        rendererFeature.SetActive(true);
        isPuzzleTransitionRendererApplied = true;
    }

    private void RestorePuzzleTransitionRendererIfNeeded()
    {
        if (!isPuzzleTransitionRendererApplied && cachedPuzzleTransitionRendererFeature == null)
            return;

        if (cachedPuzzleTransitionRendererFeature != null)
            cachedPuzzleTransitionRendererFeature.SetActive(false);

        isPuzzleTransitionRendererApplied = false;
        cachedPuzzleTransitionOutputCamera = null;
        cachedPuzzleTransitionCameraData = null;
        cachedPuzzleTransitionRendererFeature = null;
        cachedPuzzleTransitionRendererFeatureActive = false;
    }

    private bool TryGetPuzzleTransitionOutputCamera(out Camera outputCamera)
    {
        if (puzzleTransitionOutputCamera != null && puzzleTransitionOutputCamera.isActiveAndEnabled)
        {
            outputCamera = puzzleTransitionOutputCamera;
            return true;
        }

        if (TryFindPuzzleTransitionOutputCameraInScene(out outputCamera))
            return true;

        outputCamera = Camera.main;
        if (outputCamera != null && outputCamera.isActiveAndEnabled)
            return true;

        CinemachineBrain brain = FindFirstObjectByType<CinemachineBrain>();
        if (brain != null)
        {
            outputCamera = brain.GetComponent<Camera>();
            if (outputCamera != null && outputCamera.isActiveAndEnabled)
                return true;
        }

        Camera[] cameras = Camera.allCameras;
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera candidate = cameras[i];
            if (candidate == null || !candidate.isActiveAndEnabled)
                continue;

            outputCamera = candidate;
            return true;
        }

        outputCamera = null;
        return false;
    }

    private bool TryFindPuzzleTransitionOutputCameraInScene(out Camera outputCamera)
    {
        outputCamera = null;

        if (string.IsNullOrWhiteSpace(puzzleTransitionOutputSceneName))
            return false;

        Scene playerScene = SceneManager.GetSceneByName(puzzleTransitionOutputSceneName);
        if (!playerScene.IsValid() || !playerScene.isLoaded)
            return false;

        GameObject[] rootObjects = playerScene.GetRootGameObjects();
        Camera fallbackCamera = null;

        for (int rootIndex = 0; rootIndex < rootObjects.Length; rootIndex++)
        {
            Camera[] cameras = rootObjects[rootIndex].GetComponentsInChildren<Camera>(true);
            for (int cameraIndex = 0; cameraIndex < cameras.Length; cameraIndex++)
            {
                Camera candidate = cameras[cameraIndex];
                if (candidate == null || !candidate.isActiveAndEnabled)
                    continue;

                if (!string.IsNullOrWhiteSpace(puzzleTransitionOutputCameraTag)
                    && candidate.CompareTag(puzzleTransitionOutputCameraTag))
                {
                    outputCamera = candidate;
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(puzzleTransitionOutputCameraName)
                    && candidate.name == puzzleTransitionOutputCameraName)
                {
                    outputCamera = candidate;
                    return true;
                }

                fallbackCamera ??= candidate;
            }
        }

        outputCamera = fallbackCamera;
        return outputCamera != null;
    }

    private bool TryGetRendererFeature(UniversalAdditionalCameraData additionalCameraData, out ScriptableRendererFeature rendererFeature)
    {
        rendererFeature = null;

        ScriptableRendererData rendererData = GetRendererDataForCamera(additionalCameraData);
        if (rendererData == null || rendererData.rendererFeatures == null)
            return false;

        for (int i = 0; i < rendererData.rendererFeatures.Count; i++)
        {
            ScriptableRendererFeature candidate = rendererData.rendererFeatures[i];
            if (candidate == null)
                continue;

            if (string.Equals(candidate.name, puzzleTransitionRendererFeatureName, System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(candidate.GetType().Name, puzzleTransitionRendererFeatureName, System.StringComparison.OrdinalIgnoreCase))
            {
                rendererFeature = candidate;
                return true;
            }
        }

        return false;
    }

    private static ScriptableRendererData GetRendererDataForCamera(UniversalAdditionalCameraData additionalCameraData)
    {
        UniversalRenderPipelineAsset pipelineAsset = UniversalRenderPipeline.asset;
        if (pipelineAsset == null)
            return null;

        int rendererIndex = GetCurrentRendererIndex(additionalCameraData);
        if (rendererIndex < 0)
            rendererIndex = GetDefaultRendererIndex(pipelineAsset);

        var rendererDataList = pipelineAsset.rendererDataList;
        if (rendererIndex < 0 || rendererIndex >= rendererDataList.Length)
            rendererIndex = GetDefaultRendererIndex(pipelineAsset);

        if (rendererIndex < 0 || rendererIndex >= rendererDataList.Length)
            return null;

        return rendererDataList[rendererIndex];
    }

    private static int GetCurrentRendererIndex(UniversalAdditionalCameraData additionalCameraData)
    {
        if (additionalCameraData == null || UniversalRendererIndexField == null)
            return -1;

        object rendererIndexValue = UniversalRendererIndexField.GetValue(additionalCameraData);
        return rendererIndexValue is int rendererIndex ? rendererIndex : -1;
    }

    private static int GetDefaultRendererIndex(UniversalRenderPipelineAsset pipelineAsset)
    {
        if (pipelineAsset == null || UniversalDefaultRendererIndexField == null)
            return 0;

        object defaultIndexValue = UniversalDefaultRendererIndexField.GetValue(pipelineAsset);
        return defaultIndexValue is int defaultRendererIndex ? defaultRendererIndex : 0;
    }

    private IEnumerator ReturnCraneToStartAndExit()
    {
        float moveSpeed = cancelReturnSpeed > 0f ? cancelReturnSpeed : 2f;
        bool allPartsAtStart = false;

        while (!allPartsAtStart)
        {
            allPartsAtStart = true;

            foreach (CranePart part in craneParts)
            {
                if (part == null || part.partObject == null)
                {
                    continue;
                }

                if (!base.cranePartStartLocalPositions.TryGetValue(part, out Vector3 startPosition))
                {
                    continue;
                }

                Transform partTransform = part.partObject.transform;
                Vector3 currentPosition = part.useWorldPosition
                    ? partTransform.position
                    : partTransform.localPosition;

                if ((currentPosition - startPosition).sqrMagnitude > 0.000001f)
                {
                    allPartsAtStart = false;
                }

                Vector3 nextPosition = Vector3.MoveTowards(
                    currentPosition,
                    startPosition,
                    moveSpeed * Time.deltaTime
                );

                if (part.useWorldPosition)
                {
                    partTransform.position = nextPosition;
                }
                else
                {
                    partTransform.localPosition = nextPosition;
                }
            }

            yield return null;
        }

        isAutomatedMovement = false;
        isReturningToStart = false;
        activeConsoleInteraction?.SetInteractionEnabled(true);
        BeginPuzzleExitTransition();
    }
}
