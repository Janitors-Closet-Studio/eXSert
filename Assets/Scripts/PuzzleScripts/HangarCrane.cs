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


public class HangarCrane : CranePuzzle{
    public List<HangarCranePart> hangarCraneParts = new List<HangarCranePart>();
    // Store original local positions for HangarCranePart
    private new Dictionary<HangarCranePart, Vector3> cranePartStartLocalPositions = new Dictionary<HangarCranePart, Vector3>();
    // Store current sway state for each part
    private class SwayState {
        public Vector3 velocity = Vector3.zero;
        public Vector3 offset = Vector3.zero;
    }
    private Dictionary<HangarCranePart, SwayState> swayStates = new Dictionary<HangarCranePart, SwayState>();

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
            if (part == null || part.partTransform == null) continue;
            if (!cranePartStartLocalPositions.ContainsKey(part)) continue;

            // Determine sway direction based on movement
            Vector3 swayDir = Vector3.zero;
            if (isMoving)
            {
                if (dir == CraneMovementDirection.Left || dir == CraneMovementDirection.Right)
                    swayDir = Vector3.forward;
                else if (dir == CraneMovementDirection.Up || dir == CraneMovementDirection.Down)
                    swayDir = Vector3.right;
                else if (dir == CraneMovementDirection.Forward || dir == CraneMovementDirection.Backward)
                    swayDir = Vector3.right;
            }

            // Target offset is a sinusoidal oscillation while moving, zero when stopped
            float swayTarget = isMoving ? Mathf.Sin(Time.time * part.swaySpeed) * part.swayAmount : 0f;
            Vector3 targetOffset = swayDir * swayTarget;

            // Spring-damped interpolation for smooth, natural sway
            SwayState state = swayStates[part];
            float smoothTime = 0.18f; // Lower = snappier, higher = more damped
            state.offset = Vector3.SmoothDamp(state.offset, targetOffset, ref state.velocity, smoothTime, Mathf.Infinity, deltaTime);

            // Apply visual sway (localPosition)
            part.partTransform.localPosition = cranePartStartLocalPositions[part] + state.offset;
        }
    }
    
}
