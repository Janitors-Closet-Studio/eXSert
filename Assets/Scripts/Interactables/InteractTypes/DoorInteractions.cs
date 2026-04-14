/*
    Written by Brandon Wahl

    Specialized unlockable interaction for doors.
    Place this script on any GameObject that will allow a certain door to open.
    It could be on a console, a button, or even the door itself.
    Make sure to assign the DoorHandler component of the door you want to interact with in the inspector.
*/
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.Serialization;

public class DoorInteractions : UnlockableInteraction
{
    [Tooltip("Place the gameObject with the DoorHandler component here, it may be on a different object or the same object as this script.")]
    [SerializeField] private List<DoorHandler> doorHandlers;

    [Header("Interaction")]
    [SerializeField] private bool onlyInteractableOnce = false;

    [Header("Camera")]
    [FormerlySerializedAs("usePuzzleCameraOnInteraction")]
    [SerializeField] private bool useSpecialTransition = false;
    [FormerlySerializedAs("puzzleCinemachineCamera")]
    [SerializeField, Tooltip("Optional Cinemachine camera to use for the special transition.")]
    private CinemachineCamera specialTransitionCamera;
    [FormerlySerializedAs("puzzleCameraDurationSeconds")]
    [SerializeField, Min(0f)] private float specialTransitionPanDurationSeconds = 2f;
    [FormerlySerializedAs("puzzleCameraFailsafeSeconds")]
    [SerializeField, Min(0f)] private float specialTransitionFailsafeSeconds = 7f;
    [SerializeField, Min(0f)] private float specialTransitionFadeDurationSeconds = 0.35f;
    [SerializeField, Min(0f)] private float specialTransitionRevealDurationSeconds = 0.5f;
    [SerializeField, Min(0f)] private float specialTransitionBlackHoldSeconds = 0.08f;
    [SerializeField, Min(0f)] private float specialTransitionReturnBlackHoldSeconds = 0.2f;
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

        if (useSpecialTransition)
        {
            BeginSpecialTransition();
            return;
        }

        ExecuteAssignedDoorInteractions();
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
        if (specialTransitionCamera == null)
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
        cachedPuzzleCameraPriority = specialTransitionCamera.Priority;
        cachedSpecialTransitionCameraPosition = specialTransitionCamera.transform.position;
        cachedSpecialTransitionCameraRotation = specialTransitionCamera.transform.rotation;

        ApplySpecialTransitionPose(specialTransitionStartPose);

        specialTransitionInputBlockToken = InputReader.RequestGameplayInputBlock();

        yield return ScreenFadeOverlay.Instance.FadeTo(1f, specialTransitionFadeDurationSeconds);

        isPuzzleCameraActive = true;
        specialTransitionCamera.Priority = 21;

        ExecuteAssignedDoorInteractions();

        yield return null;

        float blackHoldDuration = Mathf.Max(0f, specialTransitionBlackHoldSeconds);
        if (blackHoldDuration > 0f)
            yield return new WaitForSecondsRealtime(blackHoldDuration);

        yield return ScreenFadeOverlay.Instance.FadeTo(0f, specialTransitionRevealDurationSeconds);

        float duration = Mathf.Max(0f, specialTransitionPanDurationSeconds);
        if (duration > 0f)
            yield return PanSpecialTransitionCamera(duration);

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

        if (specialTransitionCamera != null)
        {
            specialTransitionCamera.Priority = cachedPuzzleCameraPriority;
            specialTransitionCamera.transform.SetPositionAndRotation(cachedSpecialTransitionCameraPosition, cachedSpecialTransitionCameraRotation);
        }

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
        Vector3 startPosition = specialTransitionCamera.transform.position;
        Quaternion startRotation = specialTransitionCamera.transform.rotation;

        Vector3 endPosition = specialTransitionEndPose != null
            ? specialTransitionEndPose.position
            : startPosition;
        Quaternion endRotation = specialTransitionEndPose != null
            ? specialTransitionEndPose.rotation
            : startRotation;

        float elapsed = 0f;
        while (elapsed < durationSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / durationSeconds);
            specialTransitionCamera.transform.SetPositionAndRotation(
                Vector3.Lerp(startPosition, endPosition, t),
                Quaternion.Slerp(startRotation, endRotation, t));
            yield return null;
        }

        specialTransitionCamera.transform.SetPositionAndRotation(endPosition, endRotation);
    }

    private void ApplySpecialTransitionPose(Transform pose)
    {
        if (specialTransitionCamera == null || pose == null)
            return;

        specialTransitionCamera.transform.SetPositionAndRotation(pose.position, pose.rotation);
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
