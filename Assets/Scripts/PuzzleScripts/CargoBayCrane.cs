using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

public class CargoBayCrane : CranePuzzle, IConsoleSelectable
{
    private static readonly FieldInfo UniversalRendererIndexField = typeof(UniversalAdditionalCameraData)
        .GetField("m_RendererIndex", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo UniversalDefaultRendererIndexField = typeof(UniversalRenderPipelineAsset)
        .GetField("m_DefaultRendererIndex", BindingFlags.Instance | BindingFlags.NonPublic);

    protected enum DetectionResult
    {
        None,
        Target,
        Wrong
    }

    [Header("Crane References")]
    [SerializeField] public GameObject magnetExtender;
    [SerializeField, Tooltip("Target local Y for the magnet when extending (absolute local height).")]
    protected float magnetExtendHeight;
    [SerializeField, Tooltip("If enabled, extend by a distance from the start position instead of using absolute height.")]
    private bool useExtendDistance = false;
    [SerializeField, Tooltip("Distance to extend downward when using extend distance.")]
    private float magnetExtendDistance = 2f;

    [Header("Grab References")]
    [SerializeField] protected CraneGrabObject craneGrabObjectScript;

    [Header("Grab Settings")]
    [Tooltip("Object crane needs to grab")]
    [SerializeField] private GameObject firstTargetObject;
    [SerializeField] private GameObject secondTargetObject;
    [SerializeField] protected LayerMask grabLayerMask;
    [SerializeField] protected float magnetDetectLength;
    [SerializeField] private GameObject firstTargetDropZone;
    [SerializeField] private GameObject secondTargetDropZone;
    [SerializeField, Tooltip("Max distance the magnet can drop before giving up.")]
    private float maxDropDistance = 20f;
    [SerializeField, Tooltip("Speed at which the magnet drops.")]
    private float dropSpeed = 5f;
    [SerializeField, Tooltip("Layers treated as valid drop surfaces. Defaults to DropLocation when not set.")]
    private LayerMask dropSurfaceMask;
    [SerializeField, Tooltip("Allowed vertical gap between the cargo bottom and the active drop zone before release.")]
    private float dropSurfaceThreshold = 0.05f;

    [Header("Magnet Indicator")]
    [SerializeField] private bool showMagnetIndicator = true;
    [SerializeField] private bool showIndicatorOnlyWhenActive = true;
    [SerializeField] private float indicatorMaxDistance = 50f;
    [SerializeField] private float indicatorWidth = 0.05f;
    [SerializeField] private Color indicatorColor = Color.red;
    [SerializeField] private Color indicatorHighlightColor = Color.white;
    [SerializeField, Tooltip("World-space distance to consider the indicator centered on the target.")]
    private float indicatorHighlightDistance = 0.15f;
    [SerializeField, Range(0f, 2f)] private float indicatorPulseSpeed = 0.5f;
    [SerializeField, Range(0f, 1f)] private float indicatorPulseMinAlpha = 0.35f;
    [SerializeField, Range(0f, 1f)] private float indicatorPulseMaxAlpha = 0.85f;
    [SerializeField] private LayerMask indicatorMask = ~0;
    [SerializeField] private Vector3 indicatorOffset = Vector3.zero;

    [Header("Puzzle Cameras")]
    [SerializeField] private CinemachineCamera firstPuzzleCamera;
    [SerializeField] private CinemachineCamera secondPuzzleCamera;

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

    [Header("Console Move Limits")]
    [SerializeField] private int zLimitPartIndex = 1;
    [SerializeField] private Vector2 firstConsoleZLimits = new Vector2(-3.69f, 19.42f);
    [SerializeField] private Vector2 secondConsoleZLimits = new Vector2(6f, 55f);

    [Header("Console Completion")]
    [SerializeField] private bool lockCompletedConsoles = true;

    [Space(10)]
    [Header("Crane Ambience/SFX")]
    [SerializeField] private UnityEvent playCraneAmbience;
    
    protected Coroutine retractCoroutine;
    internal bool isGrabbed;
    internal GameObject targetObject;
    private GameObject activeTargetDropZone;
    private LineRenderer magnetIndicator;
    private Material magnetIndicatorMaterial;
    private bool indicatorActive;
    private int activeConsoleIndex;
    private readonly bool[] consoleCompleted = new bool[2];
    private Coroutine puzzleTransitionRoutine;
    private bool isPuzzleTransitionActive;
    private bool pendingPuzzleExitAfterEntry;
    private bool isPuzzleTransitionRendererApplied;
    private Camera cachedPuzzleTransitionOutputCamera;
    private UniversalAdditionalCameraData cachedPuzzleTransitionCameraData;
    private ScriptableRendererFeature cachedPuzzleTransitionRendererFeature;
    private bool cachedPuzzleTransitionRendererFeatureActive;
    
    private PuzzleInteraction activeConsoleInteraction;

    private void Start()
    {
        if(playCraneAmbience != null)
            playCraneAmbience.Invoke();

        SetActiveConsole(0);
        EnsureMagnetIndicator();
        indicatorActive = false;
    }

    private void Update()
    {
        SyncPuzzleTransitionRendererState();
        UpdateMagnetIndicator();
    }

    private void OnDisable()
    {
        RestorePuzzleTransitionRendererIfNeeded();
    }

    public override void ConsoleInteracted()
    {
        if (!CanUseConsole(0))
            return;

        SetActiveConsole(0);
        indicatorActive = true;
        BeginPuzzleEntryTransition();
    }

    public void ConsoleInteracted(PuzzleInteraction interaction)
    {
        int consoleIndex = interaction != null ? interaction.ConsoleIndex : 0;
        if (!CanUseConsole(consoleIndex))
            return;

        activeConsoleInteraction = interaction;
        SetActiveConsole(consoleIndex);
        indicatorActive = true;
        BeginPuzzleEntryTransition();
    }

    public override void EndPuzzle()
    {
        indicatorActive = false;

        if (isPuzzleTransitionActive)
        {
            pendingPuzzleExitAfterEntry = true;
            return;
        }

        BeginPuzzleExitTransition();
    }

    private void SetActiveConsole(int consoleIndex)
    {
        activeConsoleIndex = consoleIndex;
        bool useSecond = consoleIndex == 1;

        targetObject = useSecond ? secondTargetObject : firstTargetObject;
        activeTargetDropZone = useSecond ? secondTargetDropZone : firstTargetDropZone;

        SetPuzzleCamera(useSecond ? secondPuzzleCamera : firstPuzzleCamera);

        ApplyZLimits(useSecond ? secondConsoleZLimits : firstConsoleZLimits);
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


    }

    private static IEnumerator FadeTo(float targetAlpha, float durationSeconds)
    {
        if (ScreenFadeOverlay.Instance == null)
            yield break;

        yield return ScreenFadeOverlay.Instance.FadeTo(targetAlpha, durationSeconds);
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
            Debug.LogWarning("[CargoBayCrane] Could not find an output camera for the puzzle transition renderer swap.");
            return;
        }

        UniversalAdditionalCameraData additionalCameraData = outputCamera.GetUniversalAdditionalCameraData();
        if (additionalCameraData == null)
        {
            Debug.LogWarning($"[CargoBayCrane] Output camera '{outputCamera.name}' does not have UniversalAdditionalCameraData.");
            return;
        }

        if (!TryGetRendererFeature(additionalCameraData, out ScriptableRendererFeature rendererFeature))
        {
            Debug.LogWarning($"[CargoBayCrane] Could not find renderer feature '{puzzleTransitionRendererFeatureName}' on the output renderer.");
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
            cachedPuzzleTransitionRendererFeature.SetActive(cachedPuzzleTransitionRendererFeatureActive);

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

            if (string.Equals(candidate.name, puzzleTransitionRendererFeatureName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(candidate.GetType().Name, puzzleTransitionRendererFeatureName, StringComparison.OrdinalIgnoreCase))
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

    private bool CanUseConsole(int consoleIndex)
    {
        if (!lockCompletedConsoles)
            return true;

        if (consoleIndex < 0 || consoleIndex >= consoleCompleted.Length)
            return true;

        return !consoleCompleted[consoleIndex];
    }

    private void ApplyZLimits(Vector2 limits)
    {
        if (craneParts == null || zLimitPartIndex < 0 || zLimitPartIndex >= craneParts.Count)
            return;

        CranePart part = craneParts[zLimitPartIndex];
        if (part == null)
            return;

        part.minZ = Mathf.Min(limits.x, limits.y);
        part.maxZ = Mathf.Max(limits.x, limits.y);
    }

    protected IEnumerator AnimateMagnet(GameObject magnet, Vector3 targetPosition, float duration, bool magnetRetract = true)
    {
        LockOrUnlockMovement(true);
        Vector3 startPosition = magnet.transform.localPosition;
        Vector3 extendTarget = GetExtendTarget(startPosition);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            magnet.transform.localPosition = Vector3.Lerp(startPosition, extendTarget, elapsed / duration);
            
            // Check continuously during extension for objects below
            DetectionResult detectionResult = DetectDesiredObjectBelow();
            
            // If hit wrong object, bounce back immediately
            if (detectionResult == DetectionResult.Wrong && elapsed > 0.1f) // Small delay to avoid instant bounce
            {
                isExtending = false;
                isRetracting = true;
                
                if (retractCoroutine != null)
                {
                    StopCoroutine(retractCoroutine);
                }
                retractCoroutine = StartCoroutine(RetractMagnet(magnet, startPosition, duration * 0.5f));
                yield break;
            }
            else if (detectionResult == DetectionResult.Target) // Target found
            {
                isExtending = false;
                isRetracting = true;
                if (magnetRetract)
                {
                    if (retractCoroutine != null)
                    {
                        StopCoroutine(retractCoroutine);
                    }
                    retractCoroutine = StartCoroutine(RetractMagnet(magnet, startPosition, duration));
                }
                yield break;
            }
            
            elapsed += Time.deltaTime;
            yield return null;
        }

        magnet.transform.localPosition = extendTarget;

        // Final check at full extension
        DetectionResult finalCheck = DetectDesiredObjectBelow();
        
        if (magnetRetract)
        {
            if (retractCoroutine != null)
            {
                StopCoroutine(retractCoroutine);
            }
            isRetracting = true;
            float retractDuration = finalCheck == DetectionResult.Wrong ? duration * 0.5f : duration;
            retractCoroutine = StartCoroutine(RetractMagnet(magnet, startPosition, retractDuration));
        }
        else
        {
            isExtending = false;
        }
    }

    private Vector3 GetExtendTarget(Vector3 startLocalPosition)
    {
        if (useExtendDistance)
        {
            float targetY = startLocalPosition.y - Mathf.Abs(magnetExtendDistance);
            return new Vector3(startLocalPosition.x, targetY, startLocalPosition.z);
        }

        return new Vector3(startLocalPosition.x, magnetExtendHeight, startLocalPosition.z);
    }

    protected IEnumerator MoveCraneToPosition(GameObject crane, Vector3 targetPosition, float duration)
    {
        // ...existing code...
        // Old horizontal collision logic removed; now handled in CranePuzzle.CraneMovement
        Vector3 startPosition = crane.transform.localPosition;
        CranePart cranePart = craneParts.Find(p => p.partObject == crane);
        Vector3 finalTarget = new Vector3(
            cranePart.moveX ? targetPosition.x : startPosition.x,
            cranePart.moveY ? targetPosition.y : startPosition.y,
            cranePart.moveZ ? targetPosition.z : startPosition.z
        );
        if (cranePart.moveX)
            finalTarget.x = Mathf.Clamp(finalTarget.x, cranePart.minX, cranePart.maxX);
        if (cranePart.moveY)
            finalTarget.y = Mathf.Clamp(finalTarget.y, cranePart.minY, cranePart.maxY);
        if (cranePart.moveZ)
            finalTarget.z = Mathf.Clamp(finalTarget.z, cranePart.minZ, cranePart.maxZ);
        float elapsed = 0f;
        while (elapsed < duration)
        {
             RumbleManager.Instance.RumblePulse(rumbleDuration, rumbleLowFrequency, rumbleHighFrequency); // Subtle rumble while moving crane
            float t = elapsed / duration;
            Vector3 nextPosition = Vector3.Lerp(startPosition, finalTarget, t);
            crane.transform.localPosition = nextPosition;
            elapsed += Time.deltaTime;
            yield return null;
        }
        crane.transform.localPosition = finalTarget;
    }

    protected IEnumerator ReturnCraneToStartPosition(GameObject crane, Vector3 startPosition, float duration)
    {
        Vector3 currentPos = crane.transform.localPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            crane.transform.localPosition = Vector3.Lerp(currentPos, startPosition, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        crane.transform.localPosition = startPosition;
    }

    // Returns the World Position where the magnet should move to align with the target object
    private Vector3 CalculateMagnetTargetWorldPos(Vector3 targetWorldPos)
    {
        if (targetObject == null || magnetExtender == null)
            return targetWorldPos;

        Vector3 targetAnchorWorldPos = GetTargetAlignmentWorldPos();
        Vector3 objectWorldOffset = targetAnchorWorldPos - magnetExtender.transform.position;
        return targetWorldPos - objectWorldOffset;
    }

    // Moves the crane parts to position the magnet above the target world position
    private IEnumerator MoveCraneToMagnetTarget(Vector3 magnetTargetWorldPos)
    {

        // Gets the target position in part 1's parent space
        Vector3 targetInPart1ParentSpace = craneParts[1].partObject.transform.parent != null
            ? craneParts[1].partObject.transform.parent.InverseTransformPoint(magnetTargetWorldPos)
            : magnetTargetWorldPos;

        // Calculate target Z for part 1 based on magnet target position
        float magnetZOffsetFromPart1 = magnetExtender.transform.position.z - craneParts[1].partObject.transform.position.z;

        // Determine where part 1 needs to move to align magnet with target
        Vector3 part1TargetWorldPos = new Vector3(
            craneParts[1].partObject.transform.position.x,
            craneParts[1].partObject.transform.position.y,
            magnetTargetWorldPos.z - magnetZOffsetFromPart1
        );

        // Convert part1 target position to its parent's local space
        Vector3 part1TargetInParentSpace = craneParts[1].partObject.transform.parent.InverseTransformPoint(part1TargetWorldPos);
        float targetZForPart1 = part1TargetInParentSpace.z;

        yield return StartCoroutine(MoveCraneToPosition(craneParts[1].partObject, new Vector3(0, 0, targetZForPart1), 1));

        // Now move part 0 to align magnet horizontally
        Vector3 magnetOffsetInPart0Local = magnetExtender.transform.localPosition;
        Vector3 targetInPart1Space = craneParts[1].partObject.transform.InverseTransformPoint(magnetTargetWorldPos);
        Vector3 part0TargetInPart1Space = targetInPart1Space - magnetOffsetInPart0Local;

        yield return StartCoroutine(MoveCraneToPosition(craneParts[0].partObject, new Vector3(part0TargetInPart1Space.x, 0, 0), 1));

        yield return new WaitForSeconds(0.5f);
    }

    // Lowers the magnet until it collides with an object (excluding the target object and magnet itself) or reaches max drop distance
    private IEnumerator LowerMagnetUntilCollision(float dropSpeed, float maxDropDistance, Action<bool> onComplete)
    {
        Vector3 dropStartPos = magnetExtender.transform.localPosition;
        float droppedDistance = 0f;
        bool reachedDropTarget = false;


        Collider targetCollider = targetObject != null ? targetObject.GetComponentInChildren<Collider>() : null;
        Collider activeDropZoneCollider = activeTargetDropZone != null ? activeTargetDropZone.GetComponentInChildren<Collider>() : null;


        // Offset to adjust for mesh/collider mismatch (tweak as needed)
        float crateBottomOffset = 0.5f; // Set negative if collider is above mesh, positive if below
        int obstacleMask = GetDropSurfaceMask();

        // Lower magnet until collision or max distance reached
        while (droppedDistance < maxDropDistance && !reachedDropTarget)
        {
            RumbleManager.Instance.RumblePulse(rumbleDuration, rumbleLowFrequency, rumbleHighFrequency); // Subtle rumble while dropping magnet
            float step = dropSpeed * Time.deltaTime;
            bool hasExplicitDropZone = targetCollider != null && activeDropZoneCollider != null;
            float effectiveStep = step;
            bool releaseAfterMove = false;

            if (TryGetDropStep(targetCollider, activeDropZoneCollider, crateBottomOffset, step, out float adjustedStep, out bool shouldRelease))
            {
                effectiveStep = adjustedStep;
                releaseAfterMove = shouldRelease;
            }

            // --- Vertical raycast from magnet tip to prevent clipping ---
            Vector3 magnetTip = magnetExtender.transform.position;
            float raycastLength = Mathf.Max(0f, effectiveStep) + 0.05f; // slightly more than the next move step
            RaycastHit verticalHit;
            if (Physics.Raycast(magnetTip, Vector3.down, out verticalHit, raycastLength, obstacleMask, QueryTriggerInteraction.Ignore))
            {
                bool hitActiveDropZone = IsDropZoneMatch(verticalHit.collider, activeDropZoneCollider);
                Debug.Log($"[CraneDrop] Magnet vertical ray hit: {verticalHit.collider.name} at {verticalHit.point.y:F3}, active zone match: {hitActiveDropZone}.");
                reachedDropTarget = hitActiveDropZone || !hasExplicitDropZone;
                break;
            }

            if (effectiveStep > 0f)
            {
                magnetExtender.transform.localPosition += Vector3.down * effectiveStep;
                droppedDistance += effectiveStep;
            }

            if (targetCollider != null)
            {
                Bounds bounds = targetCollider.bounds;
                // Raycast from the center bottom of the crate straight down
                Vector3 rayOrigin = new Vector3(bounds.center.x, bounds.min.y + crateBottomOffset, bounds.center.z);
                float rayLength = 0.2f; // slightly more than threshold
                RaycastHit hit;
                if (Physics.Raycast(rayOrigin, Vector3.down, out hit, rayLength, obstacleMask, QueryTriggerInteraction.Ignore))
                {
                    float distanceToSurface = rayOrigin.y - hit.point.y;
                    bool hitActiveDropZone = IsDropZoneMatch(hit.collider, activeDropZoneCollider);
                    Debug.Log($"[CraneDrop] Raycast hit: {hit.collider.name}, Crate bottom: {rayOrigin.y:F3}, Surface: {hit.point.y:F3}, Distance: {distanceToSurface:F3}, Active zone match: {hitActiveDropZone}");
                    if (distanceToSurface <= 0.05f && hitActiveDropZone)
                    {
                        Debug.Log("[CraneDrop] Stopping drop: crate reached drop surface threshold.");
                        reachedDropTarget = true;
                        break;
                    }
                }
            }

            if (releaseAfterMove)
            {
                Debug.Log("[CraneDrop] Active drop zone reached based on collider bounds.");
                reachedDropTarget = true;
                RumbleManager.Instance.RumblePulse(rumbleDuration, rumbleLowFrequency, rumbleHighFrequency); // Subtle rumble while retracting magnet
                break;
            }

            yield return null;
        }

            // Snap magnet to final drop position
            magnetExtender.transform.localPosition = new Vector3(dropStartPos.x, magnetExtender.transform.localPosition.y, dropStartPos.z);
            onComplete?.Invoke(reachedDropTarget);
    }

    protected IEnumerator RetractMagnet(GameObject magnet, Vector3 originalPosition, float duration)
    {
        isRetracting = true;
        Vector3 startPosition = magnet.transform.localPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            RumbleManager.Instance.RumblePulse(rumbleDuration, rumbleLowFrequency, rumbleHighFrequency); // Subtle rumble while retracting magnet
            magnet.transform.localPosition = Vector3.Lerp(startPosition, originalPosition, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if(!isCompleted)
            magnet.transform.localPosition = originalPosition;
        
        isExtending = false;
        
        // Only unlock movement after retraction is complete and we're not grabbing anything
        if(!isGrabbed)
        {
            LockOrUnlockMovement(false);
            StartCoroutine(MoveCraneCoroutine());
        }
        
        if(isGrabbed)
        {
            isAutomatedMovement = true;
            LockOrUnlockMovement(false);

            // Move crane to target drop zone
            if (activeTargetDropZone == null)
            {
                EndPuzzle();
                yield break;
            }

            Vector3 targetWorldPos = GetActiveDropZoneAlignmentWorldPos();
            Vector3 magnetTargetWorldPos = CalculateMagnetTargetWorldPos(targetWorldPos);

            yield return StartCoroutine(MoveCraneToMagnetTarget(magnetTargetWorldPos));

            bool reachedDropTarget = false;
            yield return StartCoroutine(LowerMagnetUntilCollision(dropSpeed, maxDropDistance, result => reachedDropTarget = result));

            if (reachedDropTarget && craneGrabObjectScript != null && targetObject != null)
            {
                craneGrabObjectScript.ReleaseObject(targetObject);
                MarkConsoleCompleted();
            }

            isGrabbed = false;
            targetObject = null;
            yield return StartCoroutine(RetractMagnet(magnetExtender, originalPosition, 1f));

            isAutomatedMovement = false;
            isCompleted = true;
            EndPuzzle();
        }

        isRetracting = false;
    }

    private void MarkConsoleCompleted()
    {
        if (!lockCompletedConsoles)
            return;

        if (activeConsoleIndex < 0 || activeConsoleIndex >= consoleCompleted.Length)
            return;

        consoleCompleted[activeConsoleIndex] = true;
        NotifyPuzzleCompleted();
        activeConsoleInteraction?.NotifyInteractionCompleted();
        activeConsoleInteraction?.SetInteractionEnabled(false);
    }

    // Checks for confirm input to start magnet extension
    protected override void CheckForConfirm()
    {
        if (IsConfirmTriggered() && targetObject != null && !isExtending && !IsMoving())
        {
            isExtending = true;
            StartCoroutine(AnimateMagnet(magnetExtender, new Vector3(targetObject.transform.position.x, magnetExtender.transform.position.y, targetObject.transform.position.z), 2f, true));
        }
    }

    #region Grab and Detection Logic

    protected DetectionResult DetectDesiredObjectBelow()
    {

        GetRayData(out var originA, out var originB, out var originC, out var originD, out var castDir);

        int allLayersMask = ~0;
        bool foundNonTarget = false;

        if (EvaluateFirstValidHit(originA, castDir, allLayersMask, out bool hitTargetA))
        {
            if (hitTargetA)
                return GrabTargetAndReturn();
            foundNonTarget = true;
        }

        if (EvaluateFirstValidHit(originB, castDir, allLayersMask, out bool hitTargetB))
        {
            if (hitTargetB)
                return GrabTargetAndReturn();
            foundNonTarget = true;
        }

        if (EvaluateFirstValidHit(originC, castDir, allLayersMask, out bool hitTargetC))
        {
            if (hitTargetC)
                return GrabTargetAndReturn();
            foundNonTarget = true;
        }

        if (EvaluateFirstValidHit(originD, castDir, allLayersMask, out bool hitTargetD))
        {
            if (hitTargetD)
                return GrabTargetAndReturn();
            foundNonTarget = true;
        }

        if (foundNonTarget)
            return DetectionResult.Wrong;

        return DetectionResult.None;
    }

    private DetectionResult GrabTargetAndReturn()
    {
        if (craneGrabObjectScript != null)
        {
            craneGrabObjectScript.GrabObject(targetObject);
            isGrabbed = true;
            indicatorActive = false;
        }

        return DetectionResult.Target;
    }

    private bool EvaluateFirstValidHit(Vector3 origin, Vector3 direction, int layerMask, out bool firstValidHitWasTarget)
    {
        firstValidHitWasTarget = false;

        RaycastHit[] hits = Physics.RaycastAll(origin, direction, magnetDetectLength, layerMask, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0)
            return false;

        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i].collider;
            if (ShouldIgnoreHitCollider(col))
                continue;

            firstValidHitWasTarget = IsTargetCollider(col);
            return true;
        }

        return false;
    }

    private bool ShouldIgnoreHitCollider(Collider collider)
    {
        if (collider == null)
            return true;

        if (magnetExtender != null && (collider.transform == magnetExtender.transform || collider.transform.IsChildOf(magnetExtender.transform)))
            return true;

        if (collider.transform == transform || collider.transform.IsChildOf(transform))
            return true;

        return false;
    }

    private bool IsTargetCollider(Collider collider)
    {
        if (collider == null || targetObject == null)
            return false;

        if (collider.gameObject == targetObject)
            return true;

        Transform targetTransform = targetObject.transform;
        return collider.transform.IsChildOf(targetTransform)
            || targetTransform.IsChildOf(collider.transform)
            || collider.transform.root == targetTransform.root;
    }

    private void GetRayData(out Vector3 originA, out Vector3 originB, out Vector3 originC, out Vector3 originD, out Vector3 castDir)
    {
        Vector3 offset = magnetExtender.transform.TransformDirection(Vector3.forward * 2f);
        Vector3 offset2 = magnetExtender.transform.TransformDirection(Vector3.right * 2f);
        originA = magnetExtender.transform.position + offset;
        originB = magnetExtender.transform.position - offset;
        originC = magnetExtender.transform.position + offset2;
        originD = magnetExtender.transform.position - offset2;
        castDir = magnetExtender.transform.TransformDirection(Vector3.down);
    }

    public void BounceOffObject()
    {
        // Called by MagnetCollisionHandler when magnet hits a non-target object
        isExtending = false;
        isRetracting = true;
        
        if (retractCoroutine != null)
        {
            StopCoroutine(retractCoroutine);
        }
        
        if (magnetExtender != null)
        {
            Vector3 startPosition = magnetExtender.transform.localPosition;
            retractCoroutine = StartCoroutine(RetractMagnet(magnetExtender, startPosition, 0.5f));
        }

    }

    protected void AssignRayData()
    {
        if (magnetExtender == null) return;

        GetRayData(out var originA, out var originB, out var originC, out var originD, out var castDir);

        if (Physics.Raycast(originA, castDir, out var dbgHitA, magnetDetectLength, grabLayerMask))
        {
            Debug.DrawRay(originA, castDir * dbgHitA.distance, IsTargetCollider(dbgHitA.collider) ? Color.cyan : Color.red);
        }
        else
        {
            Debug.DrawRay(originA, castDir * magnetDetectLength, Color.yellow);
        }

        if (Physics.Raycast(originB, castDir, out var dbgHitB, magnetDetectLength, grabLayerMask))
        {
            Debug.DrawRay(originB, castDir * dbgHitB.distance, IsTargetCollider(dbgHitB.collider) ? Color.cyan : Color.red);
        }
        else
        {
            Debug.DrawRay(originB, castDir * magnetDetectLength, Color.yellow);
        }

        if (Physics.Raycast(originC, castDir, out var dbgHitC, magnetDetectLength, grabLayerMask))
        {
            Debug.DrawRay(originC, castDir * dbgHitC.distance, IsTargetCollider(dbgHitC.collider) ? Color.cyan : Color.red);
        }
        else
        {
            Debug.DrawRay(originC, castDir * magnetDetectLength, Color.yellow);
        }

        if (Physics.Raycast(originD, castDir, out var dbgHitD, magnetDetectLength, grabLayerMask))
        {
            Debug.DrawRay(originD, castDir * dbgHitD.distance, IsTargetCollider(dbgHitD.collider) ? Color.cyan : Color.red);
        }
        else
        {
            Debug.DrawRay(originD, castDir * magnetDetectLength, Color.yellow);
        }
    }

    protected void OnDrawGizmos()
    {
        if (magnetExtender == null) return;

        GetRayData(out var originA, out var originB, out var originC, out var originD, out var castDir);

        // Draw gizmos for all four raycasts
        if (Physics.Raycast(originA, castDir, out var hitA, magnetDetectLength, grabLayerMask))
        {
            Gizmos.color = IsTargetCollider(hitA.collider) ? Color.cyan : Color.red;
            Gizmos.DrawLine(originA, hitA.point);
            Gizmos.DrawWireSphere(hitA.point, 0.1f);
        }
        else
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(originA, originA + castDir * magnetDetectLength);
        }

        if (Physics.Raycast(originB, castDir, out var hitB, magnetDetectLength, grabLayerMask))
        {
            Gizmos.color = IsTargetCollider(hitB.collider) ? Color.cyan : Color.red;
            Gizmos.DrawLine(originB, hitB.point);
            Gizmos.DrawWireSphere(hitB.point, 0.1f);
        }
        else
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(originB, originB + castDir * magnetDetectLength);
        }

        if (Physics.Raycast(originC, castDir, out var hitC, magnetDetectLength, grabLayerMask))
        {
            Gizmos.color = IsTargetCollider(hitC.collider) ? Color.cyan : Color.red;
            Gizmos.DrawLine(originC, hitC.point);
            Gizmos.DrawWireSphere(hitC.point, 0.1f);
        }
        else
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(originC, originC + castDir * magnetDetectLength);
        }

        if (Physics.Raycast(originD, castDir, out var hitD, magnetDetectLength, grabLayerMask))
        {
            Gizmos.color = IsTargetCollider(hitD.collider) ? Color.cyan : Color.red;
            Gizmos.DrawLine(originD, hitD.point);
            Gizmos.DrawWireSphere(hitD.point, 0.1f);
        }
        else
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(originD, originD + castDir * magnetDetectLength);
        }
    }
    private int GetDropSurfaceMask()
    {
        if (dropSurfaceMask.value != 0)
            return dropSurfaceMask.value;

        int fallbackMask = LayerMask.GetMask("DropLocation");
        if (fallbackMask != 0)
            return fallbackMask;

        return LayerMask.GetMask("Ground");
    }

    private Vector3 GetTargetAlignmentWorldPos()
    {
        if (targetObject == null)
            return Vector3.zero;

        Collider targetCollider = targetObject.GetComponentInChildren<Collider>();
        if (targetCollider != null)
            return targetCollider.bounds.center;

        return targetObject.transform.position;
    }

    private Vector3 GetActiveDropZoneAlignmentWorldPos()
    {
        if (activeTargetDropZone == null)
            return Vector3.zero;

        Collider dropZoneCollider = activeTargetDropZone.GetComponentInChildren<Collider>();
        if (dropZoneCollider != null)
            return dropZoneCollider.bounds.center;

        return activeTargetDropZone.transform.position;
    }

    private bool TryGetDropStep(Collider targetCollider, Collider activeDropZoneCollider, float crateBottomOffset, float requestedStep, out float adjustedStep, out bool shouldRelease)
    {
        adjustedStep = requestedStep;
        shouldRelease = false;

        if (targetCollider == null || activeDropZoneCollider == null)
            return false;

        Bounds targetBounds = targetCollider.bounds;
        Bounds dropZoneBounds = activeDropZoneCollider.bounds;
        if (!DoBoundsOverlapHorizontally(targetBounds, dropZoneBounds))
            return false;

        float targetBottomY = targetBounds.min.y + crateBottomOffset;
        float dropZoneTopY = dropZoneBounds.max.y;
        float gapToSurface = targetBottomY - dropZoneTopY;

        if (gapToSurface <= dropSurfaceThreshold)
        {
            adjustedStep = 0f;
            shouldRelease = true;
            return true;
        }

        float maxSafeStep = Mathf.Max(0f, gapToSurface - dropSurfaceThreshold);
        if (maxSafeStep < requestedStep)
        {
            adjustedStep = maxSafeStep;
            shouldRelease = true;
            return true;
        }

        return false;
    }

    private bool DoBoundsOverlapHorizontally(Bounds a, Bounds b)
    {
        bool overlapX = a.max.x >= b.min.x && a.min.x <= b.max.x;
        bool overlapZ = a.max.z >= b.min.z && a.min.z <= b.max.z;
        return overlapX && overlapZ;
    }

    private bool IsDropZoneMatch(Collider hitCollider, Collider expectedDropZoneCollider)
    {
        if (hitCollider == null || activeTargetDropZone == null)
            return false;

        if (expectedDropZoneCollider != null)
        {
            return hitCollider == expectedDropZoneCollider
                || hitCollider.transform.IsChildOf(expectedDropZoneCollider.transform)
                || expectedDropZoneCollider.transform.IsChildOf(hitCollider.transform)
                || hitCollider.transform.root == expectedDropZoneCollider.transform.root;
        }

        Transform expectedTransform = activeTargetDropZone.transform;
        return hitCollider.transform == expectedTransform
            || hitCollider.transform.IsChildOf(expectedTransform)
            || expectedTransform.IsChildOf(hitCollider.transform)
            || hitCollider.transform.root == expectedTransform.root;
    }

    private void EnsureMagnetIndicator()
    {
        if (!showMagnetIndicator || magnetExtender == null || magnetIndicator != null)
            return;

        var lineObj = new GameObject("MagnetIndicator");
        lineObj.transform.SetParent(magnetExtender.transform);
        lineObj.transform.localPosition = Vector3.zero;
        lineObj.transform.localRotation = Quaternion.identity;
        lineObj.transform.localScale = Vector3.one;
        lineObj.layer = magnetExtender.layer;

        magnetIndicator = lineObj.AddComponent<LineRenderer>();
        magnetIndicator.useWorldSpace = true;
        magnetIndicator.positionCount = 2;
        magnetIndicator.startWidth = Mathf.Max(0.001f, indicatorWidth);
        magnetIndicator.endWidth = Mathf.Max(0.001f, indicatorWidth);

        Shader indicatorShader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
        if (indicatorShader == null)
            indicatorShader = Shader.Find("Particles/Standard Unlit");
        if (indicatorShader == null)
            indicatorShader = Shader.Find("Unlit/Transparent");
        if (indicatorShader == null)
            indicatorShader = Shader.Find("Sprites/Default");

        magnetIndicatorMaterial = new Material(indicatorShader);
        magnetIndicator.material = magnetIndicatorMaterial;
        magnetIndicator.textureMode = LineTextureMode.Tile;
        magnetIndicator.alignment = LineAlignment.View;
        magnetIndicator.numCapVertices = 4;
        magnetIndicator.shadowCastingMode = ShadowCastingMode.Off;
        magnetIndicator.receiveShadows = false;
        if (magnetIndicatorMaterial != null)
        {
            magnetIndicatorMaterial.renderQueue = (int)RenderQueue.Transparent;
            ApplyIndicatorMaterialColor(indicatorColor);
        }

        magnetIndicator.startColor = indicatorColor;
        magnetIndicator.endColor = indicatorColor;
        magnetIndicator.enabled = false;
    }

    private void UpdateMagnetIndicator()
    {
        if (!showMagnetIndicator || magnetExtender == null)
        {
            if (magnetIndicator != null)
                magnetIndicator.enabled = false;
            return;
        }

        if (isGrabbed)
        {
            if (magnetIndicator != null)
                magnetIndicator.enabled = false;
            return;
        }

        if (showIndicatorOnlyWhenActive && !indicatorActive)
        {
            if (magnetIndicator != null)
                magnetIndicator.enabled = false;
            return;
        }

        EnsureMagnetIndicator();
        if (magnetIndicator == null)
            return;

        Vector3 start = magnetExtender.transform.position + magnetExtender.transform.TransformDirection(indicatorOffset);
        Vector3 dir = magnetExtender.transform.TransformDirection(Vector3.down);
        float maxDist = Mathf.Max(0.01f, indicatorMaxDistance);

        Vector3 end = start + dir * maxDist;
        if (TryGetIndicatorHitPoint(start, dir, maxDist, out Vector3 hitPoint))
            end = hitPoint;

        Color baseColor = indicatorColor;
        if (IsIndicatorNearTarget(end))
            baseColor = indicatorHighlightColor;

        float pulseAlpha = GetPulseAlpha();
        Color pulseColor = new Color(baseColor.r, baseColor.g, baseColor.b, pulseAlpha);
        magnetIndicator.startColor = pulseColor;
        magnetIndicator.endColor = pulseColor;
        ApplyIndicatorMaterialColor(pulseColor);

        magnetIndicator.SetPosition(0, start);
        magnetIndicator.SetPosition(1, end);
        magnetIndicator.enabled = true;
    }

    private void ApplyIndicatorMaterialColor(Color color)
    {
        if (magnetIndicatorMaterial == null)
            return;

        if (magnetIndicatorMaterial.HasProperty("_Color"))
            magnetIndicatorMaterial.SetColor("_Color", color);

        if (magnetIndicatorMaterial.HasProperty("_BaseColor"))
            magnetIndicatorMaterial.SetColor("_BaseColor", color);

        if (magnetIndicatorMaterial.HasProperty("_TintColor"))
            magnetIndicatorMaterial.SetColor("_TintColor", color);
    }

    private bool TryGetIndicatorHitPoint(Vector3 start, Vector3 direction, float maxDistance, out Vector3 hitPoint)
    {
        hitPoint = start + direction * maxDistance;

        RaycastHit[] hits = Physics.RaycastAll(start, direction, maxDistance, indicatorMask, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0)
            return false;

        Array.Sort(hits, static (left, right) => left.distance.CompareTo(right.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            Collider collider = hits[i].collider;
            if (ShouldIgnoreHitCollider(collider))
                continue;

            hitPoint = hits[i].point;
            return true;
        }

        return false;
    }

    private bool IsIndicatorNearTarget(Vector3 indicatorEnd)
    {
        if (targetObject == null)
            return false;

        Vector3 targetCenter = targetObject.transform.position;
        Collider targetCollider = targetObject.GetComponentInChildren<Collider>();
        if (targetCollider != null)
            targetCenter = targetCollider.bounds.center;

        return Vector3.Distance(indicatorEnd, targetCenter) <= indicatorHighlightDistance;
    }

    private float GetPulseAlpha()
    {
        float speed = Mathf.Max(0.01f, indicatorPulseSpeed);
        float t = (Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f * speed) + 1f) * 0.5f;
        float minA = Mathf.Clamp01(indicatorPulseMinAlpha);
        float maxA = Mathf.Clamp01(indicatorPulseMaxAlpha);
        if (maxA < minA)
        {
            float swap = minA;
            minA = maxA;
            maxA = swap;
        }
        return Mathf.Lerp(minA, maxA, t);
    }

    #endregion
}