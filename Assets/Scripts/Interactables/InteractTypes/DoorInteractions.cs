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

    [Header("Camera")]
    [SerializeField] private bool usePuzzleCameraOnInteraction = false;
    [SerializeField] private CinemachineCamera puzzleCamera;
    [SerializeField, Min(0f)] private float puzzleCameraDurationSeconds = 2f;

    private Coroutine puzzleCameraRoutine;
    private int cachedPuzzleCameraPriority;

    protected override void ExecuteInteraction()
    {
        if (usePuzzleCameraOnInteraction)
            BeginTemporaryPuzzleCamera();

        foreach (DoorHandler doorHandler in doorHandlers)
        {
            if (doorHandler != null)
            {
                if (doorHandler.doorLockState == DoorHandler.DoorLockState.Locked)
                {
                    doorHandler.doorLockState = DoorHandler.DoorLockState.Unlocked;
                    doorHandler.DoorHandlerCoroutines();
                }
                

                doorHandler.Interact();
            }
        }
    }

    private void BeginTemporaryPuzzleCamera()
    {
        if (puzzleCamera == null)
        {
            Debug.LogWarning("[DoorInteractions] 'Use puzzle camera on interaction' is enabled but no Puzzle Camera is assigned.");
            return;
        }

        if (puzzleCameraRoutine != null)
            StopCoroutine(puzzleCameraRoutine);

        puzzleCameraRoutine = StartCoroutine(PuzzleCameraRoutine());
    }

    private IEnumerator PuzzleCameraRoutine()
    {
        cachedPuzzleCameraPriority = puzzleCamera.Priority;
        puzzleCamera.Priority = 21;

        float duration = Mathf.Max(0f, puzzleCameraDurationSeconds);
        if (duration > 0f)
            yield return new WaitForSeconds(duration);

        if (puzzleCamera != null)
            puzzleCamera.Priority = cachedPuzzleCameraPriority;

        puzzleCameraRoutine = null;
    }
}
