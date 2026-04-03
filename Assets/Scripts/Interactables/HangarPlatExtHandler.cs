/*
    Written by Brandon Wahl

    This script handles the different types of door interactions with realistic door movements.
    Doors can open upwards, outwards, or inwards depending on the door type set in the inspector.
    Lock management is handled by the DoorInteractions component (part of UnlockableInteraction system).

    Iterated by Kyle Woo
    This script was refactored to support hangar platform's arm extensions, but it can be used for other type of doors or extension platforms as well.
*/

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class HangarPlatExtPartMovement
{
    [Tooltip("GameObject to move")]
    public GameObject partObject;

    [Tooltip("Enable movement on X axis")]
    public bool moveX = false;
    [Tooltip("Enable movement on Y axis")]
    public bool moveY = false;
    [Tooltip("Enable movement on Z axis")]
    public bool moveZ = false;

    public float distToOpenParts = 2.0f;
    [HideInInspector] public Vector3 closedLocalPosition;
}

public class HangarPlatExtHandler : MonoBehaviour
{
    public enum DoorState { Open, Closed }
    public enum DoorLockState { Locked, Unlocked }
    public enum DoorType { OpenUnconvential, OpenOut, OpenIn }

    [Header("Door State & Type")]
    [Tooltip("Current state of the platform (Open/Closed)")]
    public DoorState currentDoorState;

    [Tooltip("Current lock state (Locked/Unlocked)")]
    public DoorLockState doorLockState = DoorLockState.Locked;

    [Tooltip("Movement mode. OpenOut/OpenIn are treated as OpenUnconvential in this handler.")]
    public DoorType doorType = DoorType.OpenUnconvential;

    [Header("Platform Movement Settings")]
    [Tooltip("Original position of the platform (auto-set)")]
    internal Vector3 doorPosOrigin;

    [Tooltip("List of platform parts to move and their movement settings.")]
    public List<HangarPlatExtPartMovement> doorParts = new List<HangarPlatExtPartMovement>();

    [Tooltip("Time in seconds for the platform to fully extend/retract")]
    [SerializeField] private float openSpeed = 2f;

    internal bool isOpened = false;

    private void Awake()
    {
        doorPosOrigin = transform.localPosition;
    }

    public void Interact()
    {
        switch (currentDoorState)
        {
            case DoorState.Open:
                CloseDoor();
                break;
            case DoorState.Closed:
                OpenDoor();
                StartCoroutine(NotAllowReentryCoroutine());
                break;
        }
    }

    public virtual IEnumerator NotAllowReentryCoroutine()
    {
        yield break;
    }

    public void UnlockDoor()
    {
        if (doorLockState == DoorLockState.Unlocked)
            return;

        doorLockState = DoorLockState.Unlocked;
    }

    public void LockDoor()
    {
        if (doorLockState == DoorLockState.Locked)
            return;

        StopAllCoroutines();
        doorLockState = DoorLockState.Locked;
    }

    public void OpenDoor()
    {
        currentDoorState = DoorState.Open;

        if (doorLockState == DoorLockState.Locked)
            doorLockState = DoorLockState.Unlocked;

        StopAllCoroutines();
        StartCoroutine(OpenUnconventialCoroutine());
        isOpened = true;
    }

    public void CloseDoor()
    {
        currentDoorState = DoorState.Closed;

        StopAllCoroutines();
        StartCoroutine(CloseUnconventialCoroutine());
        isOpened = false;
    }

    private IEnumerator OpenUnconventialCoroutine()
    {
        if (doorParts == null || doorParts.Count == 0)
            yield break;

        List<GameObject> movingParts = new List<GameObject>();
        List<Vector3> startLocalPositions = new List<Vector3>();
        List<Vector3> targetLocalPositions = new List<Vector3>();

        foreach (var partMove in doorParts)
        {
            if (partMove.partObject == null)
                continue;

            partMove.closedLocalPosition = partMove.partObject.transform.localPosition;
            Vector3 offset = new Vector3(
                partMove.moveX ? partMove.distToOpenParts : 0f,
                partMove.moveY ? partMove.distToOpenParts : 0f,
                partMove.moveZ ? partMove.distToOpenParts : 0f
            );

            movingParts.Add(partMove.partObject);
            startLocalPositions.Add(partMove.partObject.transform.localPosition);
            targetLocalPositions.Add(partMove.closedLocalPosition + offset);
        }

        if (movingParts.Count == 0)
            yield break;

        if (openSpeed <= 0f)
        {
            for (int i = 0; i < movingParts.Count; i++)
            {
                var part = movingParts[i];
                if (part == null)
                    continue;

                part.transform.localPosition = targetLocalPositions[i];
            }

            isOpened = true;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < openSpeed)
        {
            float t = Mathf.Clamp01(elapsed / openSpeed);
            float easedT = Mathf.SmoothStep(0f, 1f, t);

            for (int i = 0; i < movingParts.Count; i++)
            {
                var part = movingParts[i];
                if (part == null)
                    continue;

                part.transform.localPosition = Vector3.LerpUnclamped(startLocalPositions[i], targetLocalPositions[i], easedT);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        for (int i = 0; i < movingParts.Count; i++)
        {
            var part = movingParts[i];
            if (part == null)
                continue;

            part.transform.localPosition = targetLocalPositions[i];
        }

        isOpened = true;
    }

    private IEnumerator CloseUnconventialCoroutine()
    {
        if (doorParts == null || doorParts.Count == 0)
            yield break;

        List<GameObject> movingParts = new List<GameObject>();
        List<Vector3> startLocalPositions = new List<Vector3>();
        List<Vector3> closedLocalPositions = new List<Vector3>();

        foreach (var partMove in doorParts)
        {
            if (partMove.partObject == null)
                continue;

            movingParts.Add(partMove.partObject);
            startLocalPositions.Add(partMove.partObject.transform.localPosition);
            closedLocalPositions.Add(partMove.closedLocalPosition);
        }

        if (movingParts.Count == 0)
            yield break;

        if (openSpeed <= 0f)
        {
            for (int i = 0; i < movingParts.Count; i++)
            {
                var part = movingParts[i];
                if (part == null)
                    continue;

                part.transform.localPosition = closedLocalPositions[i];
            }

            isOpened = false;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < openSpeed)
        {
            float t = Mathf.Clamp01(elapsed / openSpeed);
            float easedT = Mathf.SmoothStep(0f, 1f, t);

            for (int i = 0; i < movingParts.Count; i++)
            {
                var part = movingParts[i];
                if (part == null)
                    continue;

                part.transform.localPosition = Vector3.LerpUnclamped(startLocalPositions[i], closedLocalPositions[i], easedT);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        for (int i = 0; i < movingParts.Count; i++)
        {
            var part = movingParts[i];
            if (part == null)
                continue;

            part.transform.localPosition = closedLocalPositions[i];
        }

        isOpened = false;
    }
}
