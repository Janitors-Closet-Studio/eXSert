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
    [SerializeField, Tooltip("Optional Cinemachine camera to use for the puzzle interaction.")]
    private CinemachineCamera puzzleCinemachineCamera;
    [SerializeField, Tooltip("Optional regular Camera to use for the puzzle interaction.")]
    private Camera puzzleStandardCamera;
    [SerializeField, Min(0f)] private float puzzleCameraDurationSeconds = 2f;

    private Coroutine puzzleCameraRoutine;
    private int cachedPuzzleCameraPriority;
    private bool cachedStandardCameraEnabled;
    private bool cachedStandardCameraGameObjectActive;

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
        if (puzzleCinemachineCamera == null && puzzleStandardCamera == null)
        {
            Debug.LogWarning("[DoorInteractions] 'Use puzzle camera on interaction' is enabled but no puzzle camera is assigned.");
            return;
        }

        if (puzzleCameraRoutine != null)
            StopCoroutine(puzzleCameraRoutine);

        puzzleCameraRoutine = StartCoroutine(PuzzleCameraRoutine());
    }

    private IEnumerator PuzzleCameraRoutine()
    {
        if (puzzleCinemachineCamera != null)
        {
            cachedPuzzleCameraPriority = puzzleCinemachineCamera.Priority;
            puzzleCinemachineCamera.Priority = 21;
        }
        else if (puzzleStandardCamera != null)
        {
            cachedStandardCameraGameObjectActive = puzzleStandardCamera.gameObject.activeSelf;
            cachedStandardCameraEnabled = puzzleStandardCamera.enabled;

            if (!puzzleStandardCamera.gameObject.activeSelf)
                puzzleStandardCamera.gameObject.SetActive(true);

            puzzleStandardCamera.enabled = true;
        }

        float duration = Mathf.Max(0f, puzzleCameraDurationSeconds);
        if (duration > 0f)
            yield return new WaitForSeconds(duration);

        if (puzzleCinemachineCamera != null)
        {
            puzzleCinemachineCamera.Priority = cachedPuzzleCameraPriority;
        }
        else if (puzzleStandardCamera != null)
        {
            puzzleStandardCamera.enabled = cachedStandardCameraEnabled;

            if (puzzleStandardCamera.gameObject.activeSelf != cachedStandardCameraGameObjectActive)
                puzzleStandardCamera.gameObject.SetActive(cachedStandardCameraGameObjectActive);
        }

        puzzleCameraRoutine = null;
    }
}
