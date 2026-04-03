/*
    Written by Brandon Wahl

    This script manages the platform extender puzzle in the hangar level. Here once the designated interact point is click,
    the platform will extend outward or inward respectively.
*/

using UnityEngine;

public class HangarPlatformExtendPuzzle : PuzzlePart
{
    [SerializeField] private float lerpSpeed = 10f;
    [SerializeField] private Vector3 moveDirection = Vector3.right;
    [SerializeField] private float moveDistance = 1.5f;

    private bool isExtending = false;
    private Vector3 startPos;
    private Vector3 targetPos;

    protected Vector3 origin;

    private void Awake()
    {
        origin = transform.localPosition;
    }

    public override void ConsoleInteracted()
    {
        Interact();
    }

    public void Extend()
    {
        StartPuzzle();
    }

    public void Retract()
    {
        EndPuzzle();
    }

    // Extends platform out to desired point
    public override void StartPuzzle()
    {
        if (!isCompleted && !isExtending)
        {
            startPos = origin;
            targetPos = startPos + GetMoveOffset();
            isExtending = true;
            isCompleted = true;
        }
    }

    // If the platform is already extended, if it is clicked again it will revert
    public override void EndPuzzle()
    {
        if (isCompleted && !isExtending)
        {
            startPos = transform.localPosition;
            targetPos = origin;
            isExtending = true;
            isCompleted = false;
        }
    }

    public void Interact()
    {
        Debug.Log($"Interact called | isCompleted={isCompleted} isExtending={isExtending}");

        if (isExtending)
            return;

        if (isCompleted)
            EndPuzzle();
        else
            StartPuzzle();
    }

    private Vector3 GetMoveOffset()
    {
        Vector3 direction = moveDirection.sqrMagnitude > 0.0001f ? moveDirection.normalized : Vector3.right;
        return direction * moveDistance;
    }

    // to reduce performance impact, change this to be a coroutine
    private void Update()
    {
        if (isExtending)
        {
            transform.localPosition = Vector3.MoveTowards(
                transform.localPosition,
                targetPos,
                lerpSpeed * Time.deltaTime
            );

            if (Vector3.Distance(transform.localPosition, targetPos) < 0.0001f)
            {
                transform.localPosition = targetPos;
                isExtending = false;
            }
        }
    }   
}
