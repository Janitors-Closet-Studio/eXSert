/*
    Written by Brandon Wahl

    This script manages a hangar platform that rotates around its local Z axis when triggered by a console.
    It behaves like a simple toggle: first interaction rotates the platform 180 degrees, second interaction rotates it back.
*/

using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;


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

    [Header("SFX")]
    [SerializeField] private AudioClip rotationSFX;
    [SerializeField] private AudioClip rotationCompleteSFX;

    [Header("Rumble Settings")]
    [SerializeField] private float rumbleDuration = 0.1f;
    [SerializeField] private float rumbleLowFrequency = 0.1f;
    [SerializeField] private float rumbleHighFrequency = 0.05f;

    private Quaternion originLocalRotation;
    private Quaternion rotatedLocalRotation;
    private Quaternion targetLocalRotation;
    private Quaternion lastPlatformRotation;
    private bool isRotating;
    private bool hasLoggedMissingPlayerWarning;
    private bool inputBusyOwned;
    private bool playerControllerWasEnabled;
    private Coroutine rotationRoutine;

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
        RumbleManager.Instance.StopControllerRumble(); // Stop rumble when returning to original position
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

    private void LockPlayerForRotation()
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

        playerControllerWasEnabled = playerController.enabled;
        if (playerControllerWasEnabled)
            playerController.enabled = false;

        if (!InputReader.inputBusy)
        {
            InputReader.inputBusy = true;
            inputBusyOwned = true;
        }
    }

    private void ReleasePlayerAfterRotation()
    {
        if (playerController != null)
        {
            EnsurePlayerReturnsToPlayerScene();
            playerController.enabled = playerControllerWasEnabled;
        }

        if (inputBusyOwned)
        {
            InputReader.inputBusy = false;
            inputBusyOwned = false;
        }
    }

    private void BeginRotation(Quaternion nextTargetRotation, bool completedState)
    {
        if (isRotating)
        {
            return;
        }

        LockPlayerForRotation();
        targetLocalRotation = nextTargetRotation;
        lastPlatformRotation = transform.rotation;
        isRotating = true;
        isCompleted = completedState;

        if (rotationRoutine != null)
            StopCoroutine(rotationRoutine);

        float rotationDuration = rotationSpeedDegreesPerSecond <= 0f
            ? 0f
            : Mathf.Abs(rotationDegrees) / rotationSpeedDegreesPerSecond;

        rotationRoutine = StartCoroutine(RotateOverTime(transform.localRotation, targetLocalRotation, rotationDuration));
    }

    private IEnumerator RotateOverTime(Quaternion startRotation, Quaternion endRotation, float duration)
    {
        if (duration <= 0f)
        {
            transform.localRotation = endRotation;
            CompleteRotation();
            yield break;
        }

        float elapsed = 0f;
        // Play the rotation SFX once at the start
        if (rotationSFX != null)
        {
            SoundManager.Instance.sfxSource.clip = rotationSFX;
            SoundManager.Instance.sfxSource.Play();
        }
        while (elapsed < duration)
        {
            RumbleManager.Instance.RumblePulse(rumbleDuration, rumbleLowFrequency, rumbleHighFrequency); // Subtle rumble while rotating platform
            transform.localRotation = Quaternion.Slerp(startRotation, endRotation, elapsed / duration);
            if (movePlayerWithPlatform)
            {
                RotatePlayerWithPlatform();
            }
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.localRotation = endRotation;
        CompleteRotation();
    }

    private void CompleteRotation()
    {
        isRotating = false;
        rotationRoutine = null;
        SoundManager.Instance.sfxSource.Stop();

        if (rotationCompleteSFX != null)
            SoundManager.Instance.sfxSource.PlayOneShot(rotationCompleteSFX);

        ReleasePlayerAfterRotation();
    }

    private void RotatePlayerWithPlatform()
    {
        if (!TryResolvePlayerController())
            return;

        Quaternion currentPlatformRotation = transform.rotation;
        Quaternion rotationDelta = currentPlatformRotation * Quaternion.Inverse(lastPlatformRotation);
        Transform playerTransform = playerController.transform;
        Vector3 playerOffset = playerTransform.position - transform.position;
        Vector3 rotatedOffset = rotationDelta * playerOffset;
        Vector3 targetPlayerPosition = transform.position + rotatedOffset;
        playerTransform.position = targetPlayerPosition;
        playerTransform.rotation = rotationDelta * playerTransform.rotation;
        lastPlatformRotation = currentPlatformRotation;
    }

    private void EnsurePlayerReturnsToPlayerScene()
    {
        if (playerController == null)
            return;

        Transform playerRoot = playerController.transform.root;
        if (playerRoot == null)
            return;

        Scene playerScene = SceneManager.GetSceneByName("PlayerScene");
        if (!playerScene.IsValid() || !playerScene.isLoaded)
            return;

        if (playerRoot.gameObject.scene == playerScene)
            return;

        playerRoot.SetParent(null, true);
        SceneManager.MoveGameObjectToScene(playerRoot.gameObject, playerScene);
    }

    private void OnDisable()
    {
        if (rotationRoutine != null)
        {
            StopCoroutine(rotationRoutine);
            rotationRoutine = null;
        }

        isRotating = false;
        ReleasePlayerAfterRotation();
    }

    private void OnDestroy()
    {
        ReleasePlayerAfterRotation();
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
