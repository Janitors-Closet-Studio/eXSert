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


public class HangarCrane : CranePuzzle
{

    public List<HangarCranePart> hangarCraneParts = new List<HangarCranePart>();    

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
        foreach (HangarCranePart part in hangarCraneParts)
        {
            Vector3 swayDirection = GetSwayDirection(movementDirection);
            part.partTransform.localPosition += swayDirection * part.swayAmount * Time.deltaTime;
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
