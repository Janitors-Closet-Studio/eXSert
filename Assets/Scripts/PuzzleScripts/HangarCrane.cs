using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class HangarCranePart : CranePart
{
    public Transform partTransform;
    public float swayAmount;
    public float swaySpeed;
}


public class HangarCrane : CranePuzzle{
    public List<HangarCranePart> hangarCraneParts = new List<HangarCranePart>();

    private void LateUpdate()
    {
        // Only sway if crane is moving
        if (!isMoving) return;

        float swayTime = Time.time;
        CraneMovementDirection dir = GetCurrentMovementDirection(); // Only call here, not in any field initializer
        foreach (var part in hangarCraneParts)
        {
            if (part == null || part.partTransform == null) continue;

            float swayAmount = part.swayAmount;
            float swaySpeed = part.swaySpeed;

            // Sway direction: perpendicular to crane movement
            Vector3 swayDir = Vector3.right;
            if (dir == CraneMovementDirection.Left || dir == CraneMovementDirection.Right)
                swayDir = Vector3.forward;
            else if (dir == CraneMovementDirection.Up || dir == CraneMovementDirection.Down)
                swayDir = Vector3.right;

            // Calculate sway offset
            float swayOffset = Mathf.Sin(swayTime * swaySpeed) * swayAmount;
            Vector3 visualOffset = swayDir * swayOffset;

            // Apply visual sway (localPosition)
            if (cranePartStartLocalPositions.ContainsKey(part))
                part.partTransform.localPosition = cranePartStartLocalPositions[part] + visualOffset;
        }
    }
    
    private IEnumerator SwayPartsIfMoving()
    {
        CraneMovementDirection directionCraneIsMoving = GetCurrentMovementDirection();
        while (isMoving && directionCraneIsMoving != CraneMovementDirection.None)
        {
            SwayParts(directionCraneIsMoving);
            yield return null;
        }
    }

    public override void CraneMovement()
    {
        base.CraneMovement();
        if (isMoving)
        {
            StartCoroutine(SwayPartsIfMoving());
        }
    }

    private void SwayParts(CraneMovementDirection movementDirection)
    {
        float swayTime = Time.time;
        foreach (HangarCranePart part in hangarCraneParts)
        {
            if (part == null || part.partTransform == null) continue;
            Vector3 swayDirection = GetSwayDirection(movementDirection);
            float swayOffset = Mathf.Sin(swayTime * part.swaySpeed) * part.swayAmount;
            Vector3 visualOffset = swayDirection * swayOffset;
            // Reset to original position plus visual sway
            if (cranePartStartLocalPositions.ContainsKey(part))
                part.partTransform.localPosition = cranePartStartLocalPositions[part] + visualOffset;
        }
    }

    private Vector3 GetSwayDirection(CraneMovementDirection movementDirection)
    {
        switch (movementDirection)
        {
            case CraneMovementDirection.Up:
                return Vector3.up;
            case CraneMovementDirection.Down:
                return Vector3.down;
            case CraneMovementDirection.Left:
                return Vector3.left;
            case CraneMovementDirection.Right:
                return Vector3.right;
            case CraneMovementDirection.Forward:
                return Vector3.forward;
            case CraneMovementDirection.Backward:
                return Vector3.back;
            default:
                return Vector3.zero;
        }
    }
}
