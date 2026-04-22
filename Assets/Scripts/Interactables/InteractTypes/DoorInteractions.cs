/*
    Written by Brandon Wahl

    Specialized unlockable interaction for doors.
    Place this script on any GameObject that will allow a certain door to open.
    It could be on a console, a button, or even the door itself.
    Make sure to assign the DoorHandler component of the door you want to interact with in the inspector.
*/
using System.Collections.Generic;
using System.Collections;
using System.Reflection;
using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.Serialization;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UIandUXSystems.HUD;

public class DoorInteractions : UnlockableInteraction
{
    private enum SpecialTransitionYawDirection
    {
        ShortestPath,
        Clockwise,
        CounterClockwise,
    }

    [Tooltip("Place the gameObject with the DoorHandler component here, it may be on a different object or the same object as this script.")]
    [SerializeField] private List<DoorHandler> doorHandlers;

    [Header("Interaction")]
    [SerializeField] private bool onlyInteractableOnce = false;

    [Header("Camera")]
    [FormerlySerializedAs("useSpecialTransition")]
    [FormerlySerializedAs("usePuzzleCameraOnInteraction")]
    [SerializeField] private bool useCameraTransition = false;
    [SerializeField, Tooltip("When enabled together with Use Camera Transition, uses the fade-cut-pan-fade transition instead of the standard camera swap.")]
    private bool useSpecialTransition = false;
    [FormerlySerializedAs("specialTransitionCamera")]
    [FormerlySerializedAs("puzzleCinemachineCamera")]
    [SerializeField, Tooltip("Optional Cinemachine camera to use for the transition.")]
    private CinemachineCamera transitionCinemachineCamera;
    [SerializeField, Min(0f)] private float cameraTransitionDurationSeconds = 3f;
    [FormerlySerializedAs("puzzleCameraFailsafeSeconds")]
    [SerializeField, Min(0f)] private float specialTransitionFailsafeSeconds = 7f;
    [SerializeField, Min(0f)] private float specialTransitionPanDurationSeconds = 3f;
    [SerializeField, Tooltip("Controls how the special transition camera travels around Y. Use Clockwise or CounterClockwise when the shortest path spins the wrong way.")]
    private SpecialTransitionYawDirection specialTransitionYawDirection = SpecialTransitionYawDirection.ShortestPath;
    [SerializeField, Min(0f)] private float specialTransitionFadeDurationSeconds = 0.35f;
    [SerializeField, Min(0f)] private float specialTransitionRevealDurationSeconds = 0.5f;
    [SerializeField, Min(0f)] private float specialTransitionBlackHoldSeconds = 0.08f;
    [SerializeField, Min(0f)] private float specialTransitionReturnBlackHoldSeconds = 0.2f;
    [SerializeField, Tooltip("Temporarily enables a fullscreen renderer feature on the output camera's current URP renderer during the special transition.")]
    private bool useSpecialTransitionRenderer = false;
    [SerializeField, Tooltip("Name of the renderer feature to toggle on the output renderer, for example 'FullScreenPassRendererFeature'.")]
    private string specialTransitionRendererFeatureName = "FullScreenPassRendererFeature";
    [SerializeField, Tooltip("Optional output camera to swap renderers on. If left empty, the script uses Camera.main, then the first CinemachineBrain camera.")]
    private Camera specialTransitionOutputCamera;
    [SerializeField, Tooltip("Loaded scene that contains the player prefab camera used for normal gameplay.")]
    private string specialTransitionOutputSceneName = "PlayerScene";
    [SerializeField, Tooltip("Camera object name to prefer inside the player scene when swapping the renderer.")]
    private string specialTransitionOutputCameraName = "MainCamera";
    [SerializeField, Tooltip("Camera tag to prefer inside the player scene when swapping the renderer.")]
    private string specialTransitionOutputCameraTag = "PlayerCamera";
    [SerializeField, Tooltip("Optional start pose for the special transition camera. If omitted, the camera's current transform is used.")]
    private Transform specialTransitionStartPose;
    [SerializeField, Tooltip("Optional end pose for the special transition camera pan.")]
    private Transform specialTransitionEndPose;

    private Coroutine puzzleCameraRoutine;
    private Coroutine puzzleCameraFailsafeRoutine;
    private Coroutine interactionPromptRoutine;
    private int cachedPuzzleCameraPriority;
    private int puzzleCameraSessionId;
    private bool isPuzzleCameraActive;
    private bool hasInteracted;
    private Vector3 cachedSpecialTransitionCameraPosition;
    private Quaternion cachedSpecialTransitionCameraRotation;
    private string specialTransitionInputBlockToken;
    private Camera cachedSpecialTransitionOutputCamera;
    private UniversalAdditionalCameraData cachedSpecialTransitionCameraData;
    private ScriptableRendererFeature cachedSpecialTransitionRendererFeature;
    private bool cachedSpecialTransitionRendererFeatureActive;
    private readonly List<HiddenUiState> hiddenSpecialTransitionUiStates = new();

    private struct HiddenUiState
    {
        public GameObject GameObject;
        public bool WasActive;
    }

    private static readonly FieldInfo UniversalRendererIndexField = typeof(UniversalAdditionalCameraData)
        .GetField("m_RendererIndex", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo UniversalDefaultRendererIndexField = typeof(UniversalRenderPipelineAsset)
        .GetField("m_DefaultRendererIndex", BindingFlags.Instance | BindingFlags.NonPublic);

    protected override void OnDisable()
    {
        RestorePuzzleCameraIfActive();
        base.OnDisable();
    }

    public bool ContainsDoorHandler(DoorHandler targetDoorHandler)
    {
        if (targetDoorHandler == null || doorHandlers == null)
            return false;

        for (int i = 0; i < doorHandlers.Count; i++)
        {
            if (doorHandlers[i] == targetDoorHandler)
                return true;
        }

        return false;
    }

    public void CloseAssignedDoors()
    {
        if (doorHandlers == null)
            return;

        for (int i = 0; i < doorHandlers.Count; i++)
        {
            DoorHandler doorHandler = doorHandlers[i];
            if (doorHandler == null)
                continue;

            if (doorHandler.currentDoorState != DoorHandler.DoorState.Closed)
                doorHandler.CloseDoor();
        }
    }

    public void EnableInteraction()
    {
        SetInteractionEnabled(true);
    }

    public void DisableInteraction()
    {
        SetInteractionEnabled(false);
    }

    public override void SetInteractionEnabled(bool isEnabled)
    {
        base.SetInteractionEnabled(isEnabled);
    }

    protected override void Interact()
    {

        base.Interact();  
        // Only allow interaction if player has the required item or can otherwise execute
        if (!canExecuteInteraction)
        {
            // Optionally, play error SFX or show locked prompt here if needed
            return;
        }

        // Block repeat execution at the interaction entrypoint so base class events do not fire again.
        if (onlyInteractableOnce && hasInteracted)
        {
            SetInteractionEnabled(false);
            return;
        }

        

        // Only start cooldown/hide flow when this interaction can actually execute.
        BeginInteractionPromptCooldown();

        

        // Consume one-time interaction after the first successful base execution.
        if (onlyInteractableOnce && canExecuteInteraction)
        {
            hasInteracted = true;
            SetInteractionEnabled(false);
        }
    }

    protected override bool IsUnlockedWithoutRequiredItem()
    {
        if (doorHandlers == null || doorHandlers.Count == 0)
            return false;

        bool hasAssignedDoor = false;

        for (int i = 0; i < doorHandlers.Count; i++)
        {
            DoorHandler doorHandler = doorHandlers[i];
            if (doorHandler == null)
                continue;

            hasAssignedDoor = true;

            if (doorHandler.doorLockState != DoorHandler.DoorLockState.Unlocked)
                return false;
        }

        return hasAssignedDoor;
    }

    protected override void ExecuteInteraction()
    {
        if (onlyInteractableOnce && hasInteracted)
            return;

        if (useCameraTransition && useSpecialTransition)
        {
            StartCoroutine(ExecuteInteractionWithNoticeAfterSpecialTransition());
            return;
        }

        if (useCameraTransition)
        {
            StartCoroutine(ExecuteInteractionWithNoticeAfterTemporaryTransition());
            return;
        }

        ExecuteAssignedDoorInteractions();
        ShowUnlockNoticeIfNeeded();

    }

    // Helper to check if a camera transition is active
    public bool HasActiveCameraTransition()
    {
        return useCameraTransition;
    }

    private void ShowUnlockNoticeIfNeeded()
    {
        if (needsItem && canUnlock && InteractionUI.Instance != null)
            InteractionUI.Instance.OnCollectedItem($"Used BAMMMMM{requiredItemID}", $"Unlocked {this.interactId} with {requiredItemID}.", 0.5f, 6f);
    }

    private IEnumerator ExecuteInteractionWithNoticeAfterSpecialTransition()
    {
        BeginSpecialTransition();
        // Wait for the special transition to finish. The door interaction itself is executed
        // inside SpecialTransitionRoutine after the camera has cut to the transition shot.
        while (puzzleCameraRoutine != null)
            yield return null;
        ShowUnlockNoticeIfNeeded();
    }

    private IEnumerator ExecuteInteractionWithNoticeAfterTemporaryTransition()
    {
        BeginTemporaryCameraTransition();
        // Wait for the temporary camera transition to finish (wait for puzzleCameraRoutine to be null)
        while (puzzleCameraRoutine != null)
            yield return null;
        ExecuteAssignedDoorInteractions();
        ShowUnlockNoticeIfNeeded();
    }

    private void ExecuteAssignedDoorInteractions()
    {
        RefreshExecutionState();

        if (doorHandlers != null)
        {
            foreach (DoorHandler doorHandler in doorHandlers)
            {
                if (doorHandler == null || !doorHandler.isActiveAndEnabled)
                    continue;

                if (doorHandler.doorLockState == DoorHandler.DoorLockState.Locked)
                    doorHandler.UnlockDoor();

                doorHandler.Interact();
            }
        }
    }

    private void BeginTemporaryCameraTransition()
    {
        if (transitionCinemachineCamera == null)
        {
            Debug.LogWarning("[DoorInteractions] 'Use Camera Transition' is enabled but no transition camera is assigned.");
            return;
        }

        if (puzzleCameraRoutine != null)
            StopCoroutine(puzzleCameraRoutine);

        puzzleCameraRoutine = StartCoroutine(TemporaryCameraTransitionRoutine());
    }

    private IEnumerator TemporaryCameraTransitionRoutine()
    {
        cachedPuzzleCameraPriority = transitionCinemachineCamera.Priority;
        transitionCinemachineCamera.Priority = 21;

        float duration = Mathf.Max(0f, cameraTransitionDurationSeconds);
        if (duration > 0f)
            yield return new WaitForSeconds(duration);

        transitionCinemachineCamera.Priority = cachedPuzzleCameraPriority;
        puzzleCameraRoutine = null;
    }

    private void BeginInteractionPromptCooldown()
    {
        if (interactionPromptRoutine != null)
            StopCoroutine(interactionPromptRoutine);

        interactionPromptRoutine = StartCoroutine(InteractionPromptCooldownRoutine());
    }

    private IEnumerator InteractionPromptCooldownRoutine()
    {
        GetInteractionUIIfAvailable()?.HideInteractPrompt();

        yield return new WaitForSeconds(3f);

        // Do not restore prompt if this interaction is one-time and already consumed.
        if (onlyInteractableOnce && hasInteracted)
        {
            interactionPromptRoutine = null;
            yield break;
        }

        if (isPlayerNearby && interactable)
            SwapBasedOnInputMethod();

        interactionPromptRoutine = null;
    }

    private void BeginSpecialTransition()
    {
        if (transitionCinemachineCamera == null)
        {
            Debug.LogWarning("[DoorInteractions] 'Use Special Transition' is enabled but no special transition camera is assigned.");
            ExecuteAssignedDoorInteractions();
            return;
        }

        RestorePuzzleCameraIfActive();

        puzzleCameraSessionId++;

        if (puzzleCameraRoutine != null)
        {
            StopCoroutine(puzzleCameraRoutine);
            puzzleCameraRoutine = null;
        }

        if (puzzleCameraFailsafeRoutine != null)
        {
            StopCoroutine(puzzleCameraFailsafeRoutine);
            puzzleCameraFailsafeRoutine = null;
        }

        puzzleCameraRoutine = StartCoroutine(SpecialTransitionRoutine(puzzleCameraSessionId));

        float failsafeDuration = Mathf.Max(0f, specialTransitionFailsafeSeconds);
        if (failsafeDuration > 0f)
            puzzleCameraFailsafeRoutine = StartCoroutine(PuzzleCameraFailsafeRoutine(puzzleCameraSessionId, failsafeDuration));
    }

    private IEnumerator SpecialTransitionRoutine(int sessionId)
    {
        cachedPuzzleCameraPriority = transitionCinemachineCamera.Priority;
        cachedSpecialTransitionCameraPosition = transitionCinemachineCamera.transform.position;
        cachedSpecialTransitionCameraRotation = transitionCinemachineCamera.transform.rotation;

        ApplySpecialTransitionPose(specialTransitionStartPose);

        specialTransitionInputBlockToken = InputReader.RequestGameplayInputBlock();

        yield return ScreenFadeOverlay.Instance.FadeTo(1f, specialTransitionFadeDurationSeconds);

        isPuzzleCameraActive = true;
        transitionCinemachineCamera.Priority = 21;
        ApplySpecialTransitionRendererIfNeeded();
        HideSpecialTransitionUi();

        ExecuteAssignedDoorInteractions();

        yield return null;

        float blackHoldDuration = Mathf.Max(0f, specialTransitionBlackHoldSeconds);
        if (blackHoldDuration > 0f)
            yield return new WaitForSecondsRealtime(blackHoldDuration);

        yield return ScreenFadeOverlay.Instance.FadeTo(0f, specialTransitionRevealDurationSeconds);

        float panDuration = Mathf.Max(0f, specialTransitionPanDurationSeconds);
        if (panDuration > 0f)
            yield return PanSpecialTransitionCamera(panDuration);

        float transitionDuration = Mathf.Max(0f, cameraTransitionDurationSeconds);
        float endPoseHoldDuration = Mathf.Max(0f, transitionDuration - panDuration);
        if (endPoseHoldDuration > 0f)
            yield return new WaitForSecondsRealtime(endPoseHoldDuration);

        yield return ScreenFadeOverlay.Instance.FadeTo(1f, specialTransitionFadeDurationSeconds);

        puzzleCameraRoutine = null;
        RestorePuzzleCameraState(sessionId, triggeredByFailsafe: false);

        float returnBlackHoldDuration = Mathf.Max(0f, specialTransitionReturnBlackHoldSeconds);
        if (returnBlackHoldDuration > 0f)
            yield return new WaitForSecondsRealtime(returnBlackHoldDuration);

        yield return ScreenFadeOverlay.Instance.FadeTo(0f, specialTransitionRevealDurationSeconds);
    }

    private IEnumerator PuzzleCameraFailsafeRoutine(int sessionId, float failsafeDuration)
    {
        yield return new WaitForSeconds(failsafeDuration);

        puzzleCameraFailsafeRoutine = null;

        if (!isPuzzleCameraActive || sessionId != puzzleCameraSessionId)
            yield break;

        Debug.LogWarning("[DoorInteractions] Special transition failsafe triggered. Restoring camera and allowing interaction retry if needed.");
        RestorePuzzleCameraState(sessionId, triggeredByFailsafe: true);
    }

    private void RestorePuzzleCameraState(int sessionId, bool triggeredByFailsafe)
    {
        if (sessionId != puzzleCameraSessionId)
            return;

        RestorePuzzleCameraIfActive();

        if (triggeredByFailsafe)
            TryRearmOneTimeInteraction();
    }

    private void RestorePuzzleCameraIfActive()
    {
        if (puzzleCameraRoutine != null)
        {
            StopCoroutine(puzzleCameraRoutine);
            puzzleCameraRoutine = null;
        }

        if (puzzleCameraFailsafeRoutine != null)
        {
            StopCoroutine(puzzleCameraFailsafeRoutine);
            puzzleCameraFailsafeRoutine = null;
        }

        if (!isPuzzleCameraActive)
            return;

        if (transitionCinemachineCamera != null)
        {
            transitionCinemachineCamera.Priority = cachedPuzzleCameraPriority;
            transitionCinemachineCamera.transform.SetPositionAndRotation(cachedSpecialTransitionCameraPosition, cachedSpecialTransitionCameraRotation);
        }

        RestoreSpecialTransitionRendererIfNeeded();
        RestoreSpecialTransitionUi();

        if (!string.IsNullOrWhiteSpace(specialTransitionInputBlockToken))
        {
            InputReader.ReleaseGameplayInputBlock(specialTransitionInputBlockToken);
            specialTransitionInputBlockToken = null;
        }

        ScreenFadeOverlay.Instance.SetImmediate(0f);

        isPuzzleCameraActive = false;
    }

    private IEnumerator PanSpecialTransitionCamera(float durationSeconds)
    {
        Vector3 startPosition = transitionCinemachineCamera.transform.position;
        Vector3 startEulerAngles = transitionCinemachineCamera.transform.rotation.eulerAngles;

        Vector3 endPosition = specialTransitionEndPose != null
            ? specialTransitionEndPose.position
            : startPosition;
        Vector3 endEulerAngles = specialTransitionEndPose != null
            ? specialTransitionEndPose.rotation.eulerAngles
            : startEulerAngles;

        float elapsed = 0f;
        while (elapsed < durationSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / durationSeconds);
            transitionCinemachineCamera.transform.SetPositionAndRotation(
                Vector3.Lerp(startPosition, endPosition, t),
                GetSpecialTransitionRotation(startEulerAngles, endEulerAngles, t));
            yield return null;
        }

        transitionCinemachineCamera.transform.SetPositionAndRotation(
            endPosition,
            GetSpecialTransitionRotation(startEulerAngles, endEulerAngles, 1f));
    }

    private Quaternion GetSpecialTransitionRotation(Vector3 startEulerAngles, Vector3 endEulerAngles, float normalizedTime)
    {
        float pitch = Mathf.LerpAngle(startEulerAngles.x, endEulerAngles.x, normalizedTime);
        float yaw = GetSpecialTransitionYaw(startEulerAngles.y, endEulerAngles.y, normalizedTime);
        float roll = Mathf.LerpAngle(startEulerAngles.z, endEulerAngles.z, normalizedTime);
        return Quaternion.Euler(pitch, yaw, roll);
    }

    private float GetSpecialTransitionYaw(float startYaw, float endYaw, float normalizedTime)
    {
        float wrappedStartYaw = Mathf.Repeat(startYaw, 360f);
        float wrappedEndYaw = Mathf.Repeat(endYaw, 360f);

        switch (specialTransitionYawDirection)
        {
            case SpecialTransitionYawDirection.Clockwise:
                return wrappedStartYaw + GetClockwiseYawDelta(wrappedStartYaw, wrappedEndYaw) * normalizedTime;
            case SpecialTransitionYawDirection.CounterClockwise:
                return wrappedStartYaw + GetCounterClockwiseYawDelta(wrappedStartYaw, wrappedEndYaw) * normalizedTime;
            default:
                return Mathf.LerpAngle(wrappedStartYaw, wrappedEndYaw, normalizedTime);
        }
    }

    private static float GetClockwiseYawDelta(float startYaw, float endYaw)
    {
        if (endYaw <= startYaw)
            return endYaw - startYaw;

        return (endYaw - 360f) - startYaw;
    }

    private static float GetCounterClockwiseYawDelta(float startYaw, float endYaw)
    {
        if (endYaw >= startYaw)
            return endYaw - startYaw;

        return (endYaw + 360f) - startYaw;
    }

    private void ApplySpecialTransitionPose(Transform pose)
    {
        if (transitionCinemachineCamera == null || pose == null)
            return;

        transitionCinemachineCamera.transform.SetPositionAndRotation(pose.position, pose.rotation);
    }

    private void ApplySpecialTransitionRendererIfNeeded()
    {
        if (!useSpecialTransitionRenderer)
            return;

        if (!TryGetSpecialTransitionOutputCamera(out Camera outputCamera))
        {
            Debug.LogWarning("[DoorInteractions] Could not find an output camera for the special transition renderer swap.");
            return;
        }

        UniversalAdditionalCameraData additionalCameraData = outputCamera.GetUniversalAdditionalCameraData();
        if (additionalCameraData == null)
        {
            Debug.LogWarning($"[DoorInteractions] Output camera '{outputCamera.name}' does not have UniversalAdditionalCameraData.");
            return;
        }

        if (!TryGetRendererFeature(additionalCameraData, out ScriptableRendererFeature rendererFeature))
        {
            Debug.LogWarning($"[DoorInteractions] Could not find renderer feature '{specialTransitionRendererFeatureName}' on the output renderer.");
            return;
        }

        cachedSpecialTransitionOutputCamera = outputCamera;
        cachedSpecialTransitionCameraData = additionalCameraData;
        cachedSpecialTransitionRendererFeature = rendererFeature;
        cachedSpecialTransitionRendererFeatureActive = rendererFeature.isActive;
        rendererFeature.SetActive(true);
    }

    private void RestoreSpecialTransitionRendererIfNeeded()
    {
        if (cachedSpecialTransitionRendererFeature == null)
            return;

        cachedSpecialTransitionRendererFeature.SetActive(cachedSpecialTransitionRendererFeatureActive);
        cachedSpecialTransitionOutputCamera = null;
        cachedSpecialTransitionCameraData = null;
        cachedSpecialTransitionRendererFeature = null;
        cachedSpecialTransitionRendererFeatureActive = false;
    }

    private bool TryGetSpecialTransitionOutputCamera(out Camera outputCamera)
    {
        if (specialTransitionOutputCamera != null && specialTransitionOutputCamera.isActiveAndEnabled)
        {
            outputCamera = specialTransitionOutputCamera;
            return true;
        }

        if (TryFindPlayerSceneOutputCamera(out outputCamera))
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

    private bool TryFindPlayerSceneOutputCamera(out Camera outputCamera)
    {
        outputCamera = null;

        if (string.IsNullOrWhiteSpace(specialTransitionOutputSceneName))
            return false;

        Scene playerScene = SceneManager.GetSceneByName(specialTransitionOutputSceneName);
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

                if (!string.IsNullOrWhiteSpace(specialTransitionOutputCameraTag)
                    && candidate.CompareTag(specialTransitionOutputCameraTag))
                {
                    outputCamera = candidate;
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(specialTransitionOutputCameraName)
                    && candidate.name == specialTransitionOutputCameraName)
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

    private static int GetCurrentRendererIndex(UniversalAdditionalCameraData additionalCameraData)
    {
        if (additionalCameraData == null || UniversalRendererIndexField == null)
            return -1;

        object rendererIndexValue = UniversalRendererIndexField.GetValue(additionalCameraData);
        return rendererIndexValue is int rendererIndex ? rendererIndex : -1;
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

            if (string.Equals(candidate.name, specialTransitionRendererFeatureName, System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(candidate.GetType().Name, specialTransitionRendererFeatureName, System.StringComparison.OrdinalIgnoreCase))
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

    private static int GetDefaultRendererIndex(UniversalRenderPipelineAsset pipelineAsset)
    {
        if (pipelineAsset == null || UniversalDefaultRendererIndexField == null)
            return 0;

        object defaultIndexValue = UniversalDefaultRendererIndexField.GetValue(pipelineAsset);
        return defaultIndexValue is int defaultRendererIndex ? defaultRendererIndex : 0;
    }

    private void HideSpecialTransitionUi()
    {
        hiddenSpecialTransitionUiStates.Clear();

        InteractionUI interactionUI = InteractionUI.TryGetExisting();
        if (interactionUI != null)
        {
            interactionUI.HideInteractPrompt();
            RegisterUiHideTarget(ResolveUiRoot(interactionUI.gameObject));
            RegisterUiHideTarget(interactionUI.collectUI);
            RegisterUiHideTarget(interactionUI.hintUI);
        }

        RegisterHudTargets(FindObjectsByType<HealthBar>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        RegisterHudTargets(FindObjectsByType<ComboProgressionUIController>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        RegisterHudTargets(FindObjectsByType<StanceIconManager>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        RegisterHudTargets(FindObjectsByType<ObjectiveText>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        RegisterHudTargets(FindObjectsByType<NoticeText>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        RegisterHudTargets(FindObjectsByType<SubobjectiveHandler>(FindObjectsInactive.Include, FindObjectsSortMode.None));

        for (int i = 0; i < hiddenSpecialTransitionUiStates.Count; i++)
        {
            HiddenUiState state = hiddenSpecialTransitionUiStates[i];
            if (state.GameObject != null)
                state.GameObject.SetActive(false);
        }
    }

    private void RestoreSpecialTransitionUi()
    {
        for (int i = 0; i < hiddenSpecialTransitionUiStates.Count; i++)
        {
            HiddenUiState state = hiddenSpecialTransitionUiStates[i];
            if (state.GameObject != null)
                state.GameObject.SetActive(state.WasActive);
        }

        hiddenSpecialTransitionUiStates.Clear();
    }

    private void RegisterHudTargets<T>(T[] components) where T : MonoBehaviour
    {
        for (int i = 0; i < components.Length; i++)
        {
            T component = components[i];
            if (component == null)
                continue;

            RegisterUiHideTarget(ResolveUiRoot(component.gameObject));
        }
    }

    private void RegisterUiHideTarget(GameObject target)
    {
        if (target == null)
            return;

        for (int i = 0; i < hiddenSpecialTransitionUiStates.Count; i++)
        {
            if (hiddenSpecialTransitionUiStates[i].GameObject == target)
                return;
        }

        hiddenSpecialTransitionUiStates.Add(new HiddenUiState
        {
            GameObject = target,
            WasActive = target.activeSelf,
        });
    }

    private static GameObject ResolveUiRoot(GameObject source)
    {
        if (source == null)
            return null;

        Canvas canvas = source.GetComponentInParent<Canvas>(true);
        if (canvas != null)
            return canvas.gameObject;

        return source;
    }

    private void TryRearmOneTimeInteraction()
    {
        if (!onlyInteractableOnce)
            return;

        if (AreAllAssignedDoorsOpen())
            return;

        hasInteracted = false;
        SetInteractionEnabled(true);
    }

    private bool AreAllAssignedDoorsOpen()
    {
        if (doorHandlers == null || doorHandlers.Count == 0)
            return false;

        bool hasAssignedDoor = false;

        for (int i = 0; i < doorHandlers.Count; i++)
        {
            DoorHandler doorHandler = doorHandlers[i];
            if (doorHandler == null)
                continue;

            hasAssignedDoor = true;

            if (doorHandler.currentDoorState != DoorHandler.DoorState.Open)
                return false;
        }

        return hasAssignedDoor;
    }
}
