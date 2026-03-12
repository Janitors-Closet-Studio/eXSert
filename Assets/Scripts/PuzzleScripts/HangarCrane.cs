using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class HangarCranePart
{
    public Transform partTransform;
    public float swayAmount;
    public float swaySpeed;
}


public class HangarCrane : CranePuzzle, IConsoleSelectable
{
    public List<HangarCranePart> hangarCraneParts = new List<HangarCranePart>();

    [SerializeField]
    private float cancelReturnSpeed = 2f;

    // Store original local positions for HangarCranePart
    private new Dictionary<HangarCranePart, Vector3> cranePartStartLocalPositions =
        new Dictionary<HangarCranePart, Vector3>();

    // Store current sway state for each part
    private class SwayState
    {
        public Vector3 velocity = Vector3.zero;
        public Vector3 offset = Vector3.zero;
    }

    private Dictionary<HangarCranePart, SwayState> swayStates =
        new Dictionary<HangarCranePart, SwayState>();

    private PuzzleInteraction activeConsoleInteraction;
    private bool isReturningToStart;

    private void Awake()
    {
        // Cache original local positions and initialize sway states
        foreach (var part in hangarCraneParts)
        {
            if (part != null && part.partTransform != null)
            {
                cranePartStartLocalPositions[part] = part.partTransform.localPosition;
                swayStates[part] = new SwayState();
            }
        }
    }

    private void LateUpdate()
    {
        float deltaTime = Time.deltaTime;
        CraneMovementDirection dir = GetCurrentMovementDirection();
        foreach (var part in hangarCraneParts)
        {
            if (part == null || part.partTransform == null)
                continue;

            if (!cranePartStartLocalPositions.ContainsKey(part))
                continue;

            // Determine sway direction based on movement
            Vector3 swayDir = Vector3.zero;
            if (isMoving)
            {
                if (dir == CraneMovementDirection.Left || dir == CraneMovementDirection.Right)
                    swayDir = Vector3.forward;
                else if (dir == CraneMovementDirection.Up || dir == CraneMovementDirection.Down)
                    swayDir = Vector3.right;
                else if (
                    dir == CraneMovementDirection.Forward
                    || dir == CraneMovementDirection.Backward
                )
                    swayDir = Vector3.right;
            }

            // Target offset is a sinusoidal oscillation while moving, zero when stopped
            float swayTarget = isMoving
                ? Mathf.Sin(Time.time * part.swaySpeed) * part.swayAmount
                : 0f;
            Vector3 targetOffset = swayDir * swayTarget;

            // Spring-damped interpolation for smooth, natural sway
            SwayState state = swayStates[part];
            float smoothTime = 0.18f; // Lower = snappier, higher = more damped
            state.offset = Vector3.SmoothDamp(
                state.offset,
                targetOffset,
                ref state.velocity,
                smoothTime,
                Mathf.Infinity,
                deltaTime
            );

            // Apply visual sway (localPosition)
            part.partTransform.localPosition = cranePartStartLocalPositions[part] + state.offset;
        }
    }

    public override void ConsoleInteracted()
    {
        if (isReturningToStart)
        {
            return;
        }

        base.ConsoleInteracted();
    }

    public void ConsoleInteracted(PuzzleInteraction interaction)
    {
        if (isReturningToStart)
        {
            return;
        }

        activeConsoleInteraction = interaction;
        base.ConsoleInteracted();
    }

    public override void StartPuzzle()
    {
        if (isReturningToStart)
        {
            return;
        }

        base.StartPuzzle();
    }

    protected override void CheckForConfirm()
    {
        if (IsConfirmTriggered())
        {
            EndPuzzle();
        }
    }

    protected override bool HandleEscapeTriggered()
    {
        if (isAutomatedMovement || isReturningToStart)
        {
            return true;
        }

        isReturningToStart = true;
        isAutomatedMovement = true;
        isMoving = false;
        activeConsoleInteraction?.SetInteractionEnabled(false);
        ReleasePuzzleControl(stopRunningCoroutines: false, clearAutomationState: false);
        StartCoroutine(ReturnCraneToStartAndExit());
        return true;
    }

    private IEnumerator ReturnCraneToStartAndExit()
    {
        float moveSpeed = cancelReturnSpeed > 0f ? cancelReturnSpeed : 2f;
        bool allPartsAtStart = false;

        while (!allPartsAtStart)
        {
            allPartsAtStart = true;

            foreach (CranePart part in craneParts)
            {
                if (part == null || part.partObject == null)
                {
                    continue;
                }

                if (!base.cranePartStartLocalPositions.TryGetValue(part, out Vector3 startPosition))
                {
                    continue;
                }

                Transform partTransform = part.partObject.transform;
                Vector3 currentPosition = part.useWorldPosition
                    ? partTransform.position
                    : partTransform.localPosition;

                if ((currentPosition - startPosition).sqrMagnitude > 0.000001f)
                {
                    allPartsAtStart = false;
                }

                Vector3 nextPosition = Vector3.MoveTowards(
                    currentPosition,
                    startPosition,
                    moveSpeed * Time.deltaTime
                );

                if (part.useWorldPosition)
                {
                    partTransform.position = nextPosition;
                }
                else
                {
                    partTransform.localPosition = nextPosition;
                }
            }

            yield return null;
        }

        isAutomatedMovement = false;
        isReturningToStart = false;
        activeConsoleInteraction?.SetInteractionEnabled(true);
    }
}
