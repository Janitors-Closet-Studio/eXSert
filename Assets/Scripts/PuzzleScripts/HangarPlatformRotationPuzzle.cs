/*
    Written by Brandon Wahl

    This script manages a hangar platform that rotates around its local Z axis when triggered by a console.
    It behaves like a simple toggle: first interaction rotates the platform 180 degrees, second interaction rotates it back.
*/

using UnityEngine;

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

    private CharacterController playerController;
    private Quaternion originLocalRotation;
    private Quaternion rotatedLocalRotation;
    private Quaternion targetLocalRotation;
    private Quaternion lastPlatformRotation;
    private bool isRotating;

    private void Awake()
    {
        originLocalRotation = transform.localRotation;

        float signedDegrees = Mathf.Abs(rotationDegrees);
        if (rotationDirection == RotationDirection.Clockwise)
        {
            signedDegrees *= -1f;
        }

        rotatedLocalRotation = originLocalRotation * Quaternion.Euler(0f, 0f, signedDegrees);
        playerController = FindFirstObjectByType<CharacterController>();
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

    private void BeginRotation(Quaternion nextTargetRotation, bool completedState)
    {
        if (isRotating)
        {
            return;
        }

        targetLocalRotation = nextTargetRotation;
        lastPlatformRotation = transform.rotation;
        isRotating = true;
        isCompleted = completedState;
    }

    private void Update()
    {
        if (!isRotating)
        {
            return;
        }

        Quaternion nextRotation = Quaternion.RotateTowards(
            transform.localRotation,
            targetLocalRotation,
            rotationSpeedDegreesPerSecond * Time.deltaTime
        );

        transform.localRotation = nextRotation;

        if (movePlayerWithPlatform)
        {
            RotatePlayerWithPlatform();
        }

        if (Quaternion.Angle(transform.localRotation, targetLocalRotation) <= 0.01f)
        {
            transform.localRotation = targetLocalRotation;
            isRotating = false;
        }
    }

    private void RotatePlayerWithPlatform()
    {
        if (playerController == null)
        {
            playerController = FindFirstObjectByType<CharacterController>();
            if (playerController == null)
            {
                return;
            }
        }

        Quaternion currentPlatformRotation = transform.rotation;
        Quaternion rotationDelta = currentPlatformRotation * Quaternion.Inverse(lastPlatformRotation);
        Vector3 playerOffset = playerController.transform.position - transform.position;
        Vector3 rotatedOffset = rotationDelta * playerOffset;
        Vector3 targetPlayerPosition = transform.position + rotatedOffset;
        Vector3 movement = targetPlayerPosition - playerController.transform.position;

        playerController.Move(movement);
        lastPlatformRotation = currentPlatformRotation;
    }
}
