/*
    Written by Brandon Wahl

    Place this script where you want an item to be interacted with and collected into the player's inventory.
*/

using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Events;

public class ItemInteractions : CollectableInteraction
{
    [Header("Camera")]
    [SerializeField]
    private bool usePuzzleCameraOnInteraction = false;

    [SerializeField]
    [Tooltip("Optional Cinemachine camera to use for the pickup interaction.")]
    private CinemachineCamera puzzleCinemachineCamera;

    [SerializeField, Min(0f)]
    private float puzzleCameraDurationSeconds = 2f;

    [Header("Events")]
    [SerializeField]
    private UnityEvent onItemCollected;

    private Coroutine puzzleCameraRoutine;
    private int cachedPuzzleCameraPriority;

    protected override void ExecuteInteraction()
    {
        if (usePuzzleCameraOnInteraction)
            BeginTemporaryPuzzleCamera();

        InternalPlayerInventory.Instance.AddCollectible(this.interactId);
    }

    protected override void AfterExecuteInteraction()
    {
        onItemCollected?.Invoke();
    }

    private void BeginTemporaryPuzzleCamera()
    {
        if (puzzleCinemachineCamera == null)
        {
            Debug.LogWarning(
                "[ItemInteractions] 'Use puzzle camera on interaction' is enabled but no puzzle camera is assigned."
            );
            return;
        }

        if (puzzleCameraRoutine != null)
            StopCoroutine(puzzleCameraRoutine);

        puzzleCameraRoutine = StartCoroutine(PuzzleCameraRoutine());
    }

    private IEnumerator PuzzleCameraRoutine()
    {
        cachedPuzzleCameraPriority = puzzleCinemachineCamera.Priority;
        puzzleCinemachineCamera.Priority = 21;

        float duration = Mathf.Max(0f, puzzleCameraDurationSeconds);
        if (duration > 0f)
            yield return new WaitForSeconds(duration);

        puzzleCinemachineCamera.Priority = cachedPuzzleCameraPriority;
        puzzleCameraRoutine = null;
    }
}
