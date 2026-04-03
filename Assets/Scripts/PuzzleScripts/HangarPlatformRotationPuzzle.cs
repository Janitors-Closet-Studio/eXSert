/*
    Written by Brandon Wahl

    This script manages a hangar platform that rotates around its local Z axis when triggered by a console.
    It behaves like a simple toggle: first interaction rotates the platform 180 degrees, second interaction rotates it back.
*/

using UnityEngine;
using System.Collections;


public class HangarPlatformRotationPuzzle : PuzzlePart
{
    private enum RotationDirection
    {
        Clockwise,
        CounterClockwise
    }

    [Header("Rotation Settings")]
    [SerializeField] private RotationDirection rotationDirection = RotationDirection.Clockwise;
    [SerializeField, Min(0f)] private float rotationDegrees = 180f;
    [SerializeField, Min(0f)] private float rotationSpeedDegreesPerSecond = 180f;
    [SerializeField] private bool movePlayerWithPlatform = true;

    [Header("References")]
    [SerializeField] private CharacterController playerController;

    private Quaternion originLocalRotation;
    private Quaternion rotatedLocalRotation;
    private Quaternion targetLocalRotation;
    private Quaternion lastPlatformRotation;
    private bool isRotating;
    private bool hasLoggedMissingPlayerWarning;

    private void Awake()
    {
        originLocalRotation = transform.localRotation;

        float signedDegrees = Mathf.Abs(rotationDegrees);
        if (rotationDirection == RotationDirection.Clockwise)
        {
            signedDegrees *= -1f;
        }

        rotatedLocalRotation = originLocalRotation * Quaternion.Euler(0f, 0f, signedDegrees);
        TryResolvePlayerController();
    }

    public override void ConsoleInteracted()
    {
        Interact();
    }

    public void Rotate()
    {
        Interact();
    }

    public void RotateForward()
    {
        StartPuzzle();
    }

    public void RotateBack()
    {
        EndPuzzle();
    }

    public override void StartPuzzle()
    {
        BeginRotation(rotatedLocalRotation, completedState: true);
    }

    public override void EndPuzzle()
    {
        BeginRotation(originLocalRotation, completedState: false);
    }

    public void Interact()
    {
        if (isRotating)
        {
            return;
        }

        if (isCompleted)
        {
            EndPuzzle();
        }
        else
        {
            StartPuzzle();
        }
    }

    private void ParentPlayerToPlatform(bool parent)
    {
        if (!TryResolvePlayerController())
        {
            if (!hasLoggedMissingPlayerWarning)
            {
                Debug.LogWarning("Player CharacterController not found. Player will not be moved with the platform.");
                hasLoggedMissingPlayerWarning = true;
            }
            return;
        }

        if (parent)
        {
            playerController.transform.SetParent(transform, worldPositionStays: true);
            playerController.enabled = false;
            InputReader.inputBusy = true;
        }
        else
        {
            playerController.transform.SetParent(null, worldPositionStays: true);
            playerController.enabled = true;
            InputReader.inputBusy = false;
        }
    }

    private void BeginRotation(Quaternion nextTargetRotation, bool completedState)
    {
        if (isRotating)
        {
            return;
        }

        ParentPlayerToPlatform(true);
        targetLocalRotation = nextTargetRotation;
        lastPlatformRotation = transform.rotation;
        isRotating = true;
        isCompleted = completedState;
        StartCoroutine(RotateOverTime(transform.localRotation, targetLocalRotation, rotationDegrees / rotationSpeedDegreesPerSecond));
    }

    private IEnumerator RotateOverTime(Quaternion startRotation, Quaternion endRotation, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            transform.localRotation = Quaternion.Slerp(startRotation, endRotation, elapsed / duration);
            if (movePlayerWithPlatform)
            {
                RotatePlayerWithPlatform();
            }
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.localRotation = endRotation;
        isRotating = false;
        ParentPlayerToPlatform(false);
    }

    private void RotatePlayerWithPlatform()
    {
        if (!TryResolvePlayerController())
            return;

        Quaternion currentPlatformRotation = transform.rotation;
        Quaternion rotationDelta = currentPlatformRotation * Quaternion.Inverse(lastPlatformRotation);
        Vector3 playerOffset = playerController.transform.position - transform.position;
        Vector3 rotatedOffset = rotationDelta * playerOffset;
        Vector3 targetPlayerPosition = transform.position + rotatedOffset;
        Vector3 movement = targetPlayerPosition - playerController.transform.position;

        playerController.Move(movement);
        lastPlatformRotation = currentPlatformRotation;
    }

    private bool TryResolvePlayerController()
    {
        if (playerController != null)
            return true;

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
            playerController = playerObject.GetComponent<CharacterController>();

        if (playerController == null)
            playerController = FindFirstObjectByType<CharacterController>();

        if (playerController != null)
            hasLoggedMissingPlayerWarning = false;

        return playerController != null;
    }
}
