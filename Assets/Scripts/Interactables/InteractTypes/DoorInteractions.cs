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

public class DoorInteractions : UnlockableInteraction
{
    [Tooltip("Place the gameObject with the DoorHandler component here, it may be on a different object or the same object as this script.")]
    [SerializeField] private List<DoorHandler> doorHandlers;

    [Header("Interaction")]
    [SerializeField] private bool onlyInteractableOnce = false;

    [Header("Camera")]
    [SerializeField] private bool usePuzzleCameraOnInteraction = false;
    [SerializeField, Tooltip("Optional Cinemachine camera to use for the puzzle interaction.")]
    private CinemachineCamera puzzleCinemachineCamera;
    [SerializeField, Min(0f)] private float puzzleCameraDurationSeconds = 2f;
    [SerializeField, Min(0f)] private float puzzleCameraFailsafeSeconds = 7f;

    private Coroutine puzzleCameraRoutine;
    private Coroutine puzzleCameraFailsafeRoutine;
    private Coroutine interactionPromptRoutine;
    private int cachedPuzzleCameraPriority;
    private int puzzleCameraSessionId;
    private bool isPuzzleCameraActive;
    private bool hasInteracted;

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

        if (usePuzzleCameraOnInteraction)
            BeginTemporaryPuzzleCamera();

        if (doorHandlers != null)
        {
            foreach (DoorHandler doorHandler in doorHandlers)
            {
                if (!doorHandler.isActiveAndEnabled)
                    continue;

                if (doorHandler != null)
                {
                    if (doorHandler.doorLockState == DoorHandler.DoorLockState.Locked)
                        doorHandler.UnlockDoor();

                    doorHandler.Interact();
                }
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

    private void BeginTemporaryPuzzleCamera()
    {
        if (puzzleCinemachineCamera == null)
        {
            Debug.LogWarning("[DoorInteractions] 'Use puzzle camera on interaction' is enabled but no puzzle camera is assigned.");
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

        puzzleCameraRoutine = StartCoroutine(PuzzleCameraRoutine(puzzleCameraSessionId));

        float failsafeDuration = Mathf.Max(0f, puzzleCameraFailsafeSeconds);
        if (failsafeDuration > 0f)
            puzzleCameraFailsafeRoutine = StartCoroutine(PuzzleCameraFailsafeRoutine(puzzleCameraSessionId, failsafeDuration));
    }

    private IEnumerator PuzzleCameraRoutine(int sessionId)
    {
        cachedPuzzleCameraPriority = puzzleCinemachineCamera.Priority;
        isPuzzleCameraActive = true;
        puzzleCinemachineCamera.Priority = 21;

        float duration = Mathf.Max(0f, puzzleCameraDurationSeconds);
        if (duration > 0f)
            yield return new WaitForSeconds(duration);

        puzzleCameraRoutine = null;
        RestorePuzzleCameraState(sessionId, triggeredByFailsafe: false);
    }

    private IEnumerator PuzzleCameraFailsafeRoutine(int sessionId, float failsafeDuration)
    {
        yield return new WaitForSeconds(failsafeDuration);

        puzzleCameraFailsafeRoutine = null;

        if (!isPuzzleCameraActive || sessionId != puzzleCameraSessionId)
            yield break;

        Debug.LogWarning("[DoorInteractions] Puzzle camera failsafe triggered. Restoring camera and allowing interaction retry if needed.");
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

        if (puzzleCinemachineCamera != null)
            puzzleCinemachineCamera.Priority = cachedPuzzleCameraPriority;

        isPuzzleCameraActive = false;
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
