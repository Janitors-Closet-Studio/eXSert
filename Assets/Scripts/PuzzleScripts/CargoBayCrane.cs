using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

public class CargoBayCrane : CranePuzzle, IConsoleSelectable
{
    protected enum DetectionResult
    {
        None,
        Target,
        Wrong
    }

    [Header("Crane References")]
    [SerializeField] public GameObject magnetExtender;
    [SerializeField, Tooltip("Target local Y for the magnet when extending (absolute local height).")]
    protected float magnetExtendHeight;
    [SerializeField, Tooltip("If enabled, extend by a distance from the start position instead of using absolute height.")]
    private bool useExtendDistance = false;
    [SerializeField, Tooltip("Distance to extend downward when using extend distance.")]
    private float magnetExtendDistance = 2f;

    [Header("Grab References")]
    [SerializeField] protected CraneGrabObject craneGrabObjectScript;

    [Header("Grab Settings")]
    [Tooltip("Object crane needs to grab")]
    [SerializeField] private GameObject firstTargetObject;
    [SerializeField] private GameObject secondTargetObject;
    [SerializeField] protected LayerMask grabLayerMask;
    [SerializeField] protected float magnetDetectLength;
    [SerializeField] private GameObject firstTargetDropZone;
    [SerializeField] private GameObject secondTargetDropZone;
    [SerializeField, Tooltip("Max distance the magnet can drop before giving up.")]
    private float maxDropDistance = 20f;
    [SerializeField, Tooltip("Speed at which the magnet drops.")]
    private float dropSpeed = 5f;

    [Header("Magnet Indicator")]
    [SerializeField] private bool showMagnetIndicator = true;
    [SerializeField] private bool showIndicatorOnlyWhenActive = true;
    [SerializeField] private float indicatorMaxDistance = 50f;
    [SerializeField] private float indicatorWidth = 0.05f;
    [SerializeField] private Color indicatorColor = Color.red;
    [SerializeField] private Color indicatorHighlightColor = Color.white;
    [SerializeField, Tooltip("World-space distance to consider the indicator centered on the target.")]
    private float indicatorHighlightDistance = 0.15f;
    [SerializeField, Range(0f, 2f)] private float indicatorPulseSpeed = 0.5f;
    [SerializeField, Range(0f, 1f)] private float indicatorPulseMinAlpha = 0.35f;
    [SerializeField, Range(0f, 1f)] private float indicatorPulseMaxAlpha = 0.85f;
    [SerializeField] private LayerMask indicatorMask = ~0;
    [SerializeField] private Vector3 indicatorOffset = Vector3.zero;

    [Header("Puzzle Cameras")]
    [SerializeField] private CinemachineCamera firstPuzzleCamera;
    [SerializeField] private CinemachineCamera secondPuzzleCamera;

    [Header("Console Move Limits")]
    [SerializeField] private int zLimitPartIndex = 1;
    [SerializeField] private Vector2 firstConsoleZLimits = new Vector2(-3.69f, 19.42f);
    [SerializeField] private Vector2 secondConsoleZLimits = new Vector2(6f, 55f);

    [Header("Console Completion")]
    [SerializeField] private bool lockCompletedConsoles = true;

    [Space(10)]
    [Header("Crane Ambience/SFX")]
    [SerializeField] private UnityEvent playCraneAmbience;
    
    protected Coroutine retractCoroutine;
    internal bool isGrabbed;
    internal GameObject targetObject;
    private GameObject activeTargetDropZone;
    private LineRenderer magnetIndicator;
    private bool indicatorActive;
    private int activeConsoleIndex;
    private readonly bool[] consoleCompleted = new bool[2];
    
    private void Start()
    {
        if(playCraneAmbience != null)
            playCraneAmbience.Invoke();

        SetActiveConsole(0);
        EnsureMagnetIndicator();
        indicatorActive = false;
    }

    private void Update()
    {
        UpdateMagnetIndicator();
    }

    public override void ConsoleInteracted()
    {
        if (!CanUseConsole(0))
            return;

        SetActiveConsole(0);
        indicatorActive = true;
        base.ConsoleInteracted();
    }

    public void ConsoleInteracted(PuzzleInteraction interaction)
    {
        int consoleIndex = interaction != null ? interaction.ConsoleIndex : 0;

        if (!CanUseConsole(consoleIndex))
            return;

        SetActiveConsole(consoleIndex);
        indicatorActive = true;
        base.ConsoleInteracted();
    }

    public override void EndPuzzle()
    {
        indicatorActive = false;
        base.EndPuzzle();
    }

    private void SetActiveConsole(int consoleIndex)
    {
        activeConsoleIndex = consoleIndex;
        bool useSecond = consoleIndex == 1;

        targetObject = useSecond ? secondTargetObject : firstTargetObject;
        activeTargetDropZone = useSecond ? secondTargetDropZone : firstTargetDropZone;

        SetPuzzleCamera(useSecond ? secondPuzzleCamera : firstPuzzleCamera);

        ApplyZLimits(useSecond ? secondConsoleZLimits : firstConsoleZLimits);
    }

    private bool CanUseConsole(int consoleIndex)
    {
        if (!lockCompletedConsoles)
            return true;

        if (consoleIndex < 0 || consoleIndex >= consoleCompleted.Length)
            return true;

        return !consoleCompleted[consoleIndex];
    }

    private void ApplyZLimits(Vector2 limits)
    {
        if (craneParts == null || zLimitPartIndex < 0 || zLimitPartIndex >= craneParts.Count)
            return;

        CranePart part = craneParts[zLimitPartIndex];
        if (part == null)
            return;

        part.minZ = Mathf.Min(limits.x, limits.y);
        part.maxZ = Mathf.Max(limits.x, limits.y);
    }

    protected IEnumerator AnimateMagnet(GameObject magnet, Vector3 targetPosition, float duration, bool magnetRetract = true)
    {
        LockOrUnlockMovement(true);
        Vector3 startPosition = magnet.transform.localPosition;
        Vector3 extendTarget = GetExtendTarget(startPosition);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            magnet.transform.localPosition = Vector3.Lerp(startPosition, extendTarget, elapsed / duration);
            
            // Check continuously during extension for objects below
            DetectionResult detectionResult = DetectDesiredObjectBelow();
            
            // If hit wrong object, bounce back immediately
            if (detectionResult == DetectionResult.Wrong && elapsed > 0.1f) // Small delay to avoid instant bounce
            {
                isExtending = false;
                isRetracting = true;
                
                if (retractCoroutine != null)
                {
                    StopCoroutine(retractCoroutine);
                }
                retractCoroutine = StartCoroutine(RetractMagnet(magnet, startPosition, duration * 0.5f));
                yield break;
            }
            else if (detectionResult == DetectionResult.Target) // Target found
            {
                isExtending = false;
                isRetracting = true;
                if (magnetRetract)
                {
                    if (retractCoroutine != null)
                    {
                        StopCoroutine(retractCoroutine);
                    }
                    retractCoroutine = StartCoroutine(RetractMagnet(magnet, startPosition, duration));
                }
                yield break;
            }
            
            elapsed += Time.deltaTime;
            yield return null;
        }

        magnet.transform.localPosition = extendTarget;

        // Final check at full extension
        DetectionResult finalCheck = DetectDesiredObjectBelow();
        
        if (magnetRetract)
        {
            if (retractCoroutine != null)
            {
                StopCoroutine(retractCoroutine);
            }
            isRetracting = true;
            float retractDuration = finalCheck == DetectionResult.Wrong ? duration * 0.5f : duration;
            retractCoroutine = StartCoroutine(RetractMagnet(magnet, startPosition, retractDuration));
        }
        else
        {
            isExtending = false;
        }
    }

    private Vector3 GetExtendTarget(Vector3 startLocalPosition)
    {
        if (useExtendDistance)
        {
            float targetY = startLocalPosition.y - Mathf.Abs(magnetExtendDistance);
            return new Vector3(startLocalPosition.x, targetY, startLocalPosition.z);
        }

        return new Vector3(startLocalPosition.x, magnetExtendHeight, startLocalPosition.z);
    }

    protected IEnumerator MoveCraneToPosition(GameObject crane, Vector3 targetPosition, float duration)
    {
        // ...existing code...
        // Old horizontal collision logic removed; now handled in CranePuzzle.CraneMovement
        Vector3 startPosition = crane.transform.localPosition;
        CranePart cranePart = craneParts.Find(p => p.partObject == crane);
        Vector3 finalTarget = new Vector3(
            cranePart.moveX ? targetPosition.x : startPosition.x,
            cranePart.moveY ? targetPosition.y : startPosition.y,
            cranePart.moveZ ? targetPosition.z : startPosition.z
        );
        if (cranePart.moveX)
            finalTarget.x = Mathf.Clamp(finalTarget.x, cranePart.minX, cranePart.maxX);
        if (cranePart.moveY)
            finalTarget.y = Mathf.Clamp(finalTarget.y, cranePart.minY, cranePart.maxY);
        if (cranePart.moveZ)
            finalTarget.z = Mathf.Clamp(finalTarget.z, cranePart.minZ, cranePart.maxZ);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            Vector3 nextPosition = Vector3.Lerp(startPosition, finalTarget, t);
            crane.transform.localPosition = nextPosition;
            elapsed += Time.deltaTime;
            yield return null;
        }
        crane.transform.localPosition = finalTarget;
    }

    protected IEnumerator ReturnCraneToStartPosition(GameObject crane, Vector3 startPosition, float duration)
    {
        Vector3 currentPos = crane.transform.localPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            crane.transform.localPosition = Vector3.Lerp(currentPos, startPosition, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        crane.transform.localPosition = startPosition;
    }

    // Returns the World Position where the magnet should move to align with the target object
    private Vector3 CalculateMagnetTargetWorldPos(Vector3 targetWorldPos)
    {
        if (targetObject == null)
            return targetWorldPos;

        // Local pos of target object
        Vector3 objectLocalPos = targetObject.transform.localPosition;
        // Offset of object relative to magnet in magnet's local space
        Vector3 objectWorldOffset = magnetExtender.transform.TransformVector(objectLocalPos);
        return targetWorldPos - objectWorldOffset;
    }

    // Moves the crane parts to position the magnet above the target world position
    private IEnumerator MoveCraneToMagnetTarget(Vector3 magnetTargetWorldPos)
    {

        // Gets the target position in part 1's parent space
        Vector3 targetInPart1ParentSpace = craneParts[1].partObject.transform.parent != null
            ? craneParts[1].partObject.transform.parent.InverseTransformPoint(magnetTargetWorldPos)
            : magnetTargetWorldPos;

        // Calculate target Z for part 1 based on magnet target position
        float magnetZOffsetFromPart1 = magnetExtender.transform.position.z - craneParts[1].partObject.transform.position.z;

        // Determine where part 1 needs to move to align magnet with target
        Vector3 part1TargetWorldPos = new Vector3(
            craneParts[1].partObject.transform.position.x,
            craneParts[1].partObject.transform.position.y,
            magnetTargetWorldPos.z - magnetZOffsetFromPart1
        );

        // Convert part1 target position to its parent's local space
        Vector3 part1TargetInParentSpace = craneParts[1].partObject.transform.parent.InverseTransformPoint(part1TargetWorldPos);
        float targetZForPart1 = part1TargetInParentSpace.z;

        yield return StartCoroutine(MoveCraneToPosition(craneParts[1].partObject, new Vector3(0, 0, targetZForPart1), 1));

        // Now move part 0 to align magnet horizontally
        Vector3 magnetOffsetInPart0Local = magnetExtender.transform.localPosition;
        Vector3 targetInPart1Space = craneParts[1].partObject.transform.InverseTransformPoint(magnetTargetWorldPos);
        Vector3 part0TargetInPart1Space = targetInPart1Space - magnetOffsetInPart0Local;

        yield return StartCoroutine(MoveCraneToPosition(craneParts[0].partObject, new Vector3(part0TargetInPart1Space.x, 0, 0), 1));

        yield return new WaitForSeconds(0.5f);
    }

    // Lowers the magnet until it collides with an object (excluding the target object and magnet itself) or reaches max drop distance
    private IEnumerator LowerMagnetUntilCollision(float dropSpeed, float maxDropDistance, Action<bool> onComplete)
    {
        Vector3 dropStartPos = magnetExtender.transform.localPosition;
        float droppedDistance = 0f;
        bool reachedDropTarget = false;


        Collider targetCollider = targetObject != null ? targetObject.GetComponentInChildren<Collider>() : null;
        Collider activeDropZoneCollider = activeTargetDropZone != null ? activeTargetDropZone.GetComponent<Collider>() : null;


        // Offset to adjust for mesh/collider mismatch (tweak as needed)
        float crateBottomOffset = 0.5f; // Set negative if collider is above mesh, positive if below
        int obstacleMask = LayerMask.GetMask("Default", "Ground");

        // Lower magnet until collision or max distance reached
        while (droppedDistance < maxDropDistance && !reachedDropTarget)
        {
            float step = dropSpeed * Time.deltaTime;

            // --- Vertical raycast from magnet tip to prevent clipping ---
            Vector3 magnetTip = magnetExtender.transform.position;
            float raycastLength = step + 0.05f; // slightly more than step
            RaycastHit verticalHit;
            if (Physics.Raycast(magnetTip, Vector3.down, out verticalHit, raycastLength, obstacleMask, QueryTriggerInteraction.Ignore))
            {
                Debug.Log($"[CraneDrop] Magnet vertical ray hit: {verticalHit.collider.name} at {verticalHit.point.y:F3}, stopping extension.");
                reachedDropTarget = true;
                break;
            }

            magnetExtender.transform.localPosition += Vector3.down * step;
            droppedDistance += step;

            if (targetCollider != null)
            {
                Bounds bounds = targetCollider.bounds;
                // Raycast from the center bottom of the crate straight down
                Vector3 rayOrigin = new Vector3(bounds.center.x, bounds.min.y + crateBottomOffset, bounds.center.z);
                float rayLength = 0.2f; // slightly more than threshold
                RaycastHit hit;
                if (Physics.Raycast(rayOrigin, Vector3.down, out hit, rayLength, LayerMask.GetMask("Ground"), QueryTriggerInteraction.Ignore))
                {
                    float distanceToGround = rayOrigin.y - hit.point.y;
                    Debug.Log($"[CraneDrop] Raycast hit: {hit.collider.name}, Crate bottom: {rayOrigin.y:F3}, Ground: {hit.point.y:F3}, Distance: {distanceToGround:F3}");
                    if (distanceToGround <= 0.05f)
                    {
                        Debug.Log("[CraneDrop] Stopping drop: crate reached ground threshold.");
                        reachedDropTarget = true;
                        break;
                    }
                }
            }

            yield return null;
        }

            // Snap magnet to final drop position
            magnetExtender.transform.localPosition = new Vector3(dropStartPos.x, magnetExtender.transform.localPosition.y, dropStartPos.z);
            onComplete?.Invoke(reachedDropTarget);
    }

    protected IEnumerator RetractMagnet(GameObject magnet, Vector3 originalPosition, float duration)
    {
        isRetracting = true;
        Vector3 startPosition = magnet.transform.localPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            magnet.transform.localPosition = Vector3.Lerp(startPosition, originalPosition, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if(!isCompleted)
            magnet.transform.localPosition = originalPosition;
        
        isExtending = false;
        
        // Only unlock movement after retraction is complete and we're not grabbing anything
        if(!isGrabbed)
        {
            LockOrUnlockMovement(false);
            StartCoroutine(MoveCraneCoroutine());
        }
        
        if(isGrabbed)
        {
            isAutomatedMovement = true;
            LockOrUnlockMovement(false);

            // Move crane to target drop zone
            if (activeTargetDropZone == null)
            {
                EndPuzzle();
                yield break;
            }

            Vector3 targetWorldPos = activeTargetDropZone.transform.position;
            Vector3 magnetTargetWorldPos = CalculateMagnetTargetWorldPos(targetWorldPos);

            yield return StartCoroutine(MoveCraneToMagnetTarget(magnetTargetWorldPos));

            bool reachedDropTarget = false;
            yield return StartCoroutine(LowerMagnetUntilCollision(dropSpeed, maxDropDistance, result => reachedDropTarget = result));

            if (reachedDropTarget && craneGrabObjectScript != null && targetObject != null)
            {
                craneGrabObjectScript.ReleaseObject(targetObject);
                MarkConsoleCompleted();
            }

            isGrabbed = false;
            targetObject = null;
            yield return StartCoroutine(RetractMagnet(magnetExtender, originalPosition, 1f));

            isAutomatedMovement = false;
            isCompleted = true;
            EndPuzzle();
        }

        isRetracting = false;
    }

    private void MarkConsoleCompleted()
    {
        if (!lockCompletedConsoles)
            return;

        if (activeConsoleIndex < 0 || activeConsoleIndex >= consoleCompleted.Length)
            return;

        consoleCompleted[activeConsoleIndex] = true;
    }

    // Checks for confirm input to start magnet extension
    protected override void CheckForConfirm()
    {
        if (IsConfirmTriggered() && targetObject != null && !isExtending && !IsMoving())
        {
            isExtending = true;
            StartCoroutine(AnimateMagnet(magnetExtender, new Vector3(targetObject.transform.position.x, magnetExtender.transform.position.y, targetObject.transform.position.z), 2f, true));
        }
    }

    #region Grab and Detection Logic

    protected DetectionResult DetectDesiredObjectBelow()
    {

        GetRayData(out var originA, out var originB, out var originC, out var originD, out var castDir);

        int allLayersMask = ~0;
        bool foundNonTarget = false;

        if (EvaluateFirstValidHit(originA, castDir, allLayersMask, out bool hitTargetA))
        {
            if (hitTargetA)
                return GrabTargetAndReturn();
            foundNonTarget = true;
        }

        if (EvaluateFirstValidHit(originB, castDir, allLayersMask, out bool hitTargetB))
        {
            if (hitTargetB)
                return GrabTargetAndReturn();
            foundNonTarget = true;
        }

        if (EvaluateFirstValidHit(originC, castDir, allLayersMask, out bool hitTargetC))
        {
            if (hitTargetC)
                return GrabTargetAndReturn();
            foundNonTarget = true;
        }

        if (EvaluateFirstValidHit(originD, castDir, allLayersMask, out bool hitTargetD))
        {
            if (hitTargetD)
                return GrabTargetAndReturn();
            foundNonTarget = true;
        }

        if (foundNonTarget)
            return DetectionResult.Wrong;

        return DetectionResult.None;
    }

    private DetectionResult GrabTargetAndReturn()
    {
        if (craneGrabObjectScript != null)
        {
            craneGrabObjectScript.GrabObject(targetObject);
            isGrabbed = true;
        }

        return DetectionResult.Target;
    }

    private bool EvaluateFirstValidHit(Vector3 origin, Vector3 direction, int layerMask, out bool firstValidHitWasTarget)
    {
        firstValidHitWasTarget = false;

        RaycastHit[] hits = Physics.RaycastAll(origin, direction, magnetDetectLength, layerMask, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0)
            return false;

        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i].collider;
            if (ShouldIgnoreHitCollider(col))
                continue;

            firstValidHitWasTarget = IsTargetCollider(col);
            return true;
        }

        return false;
    }

    private bool ShouldIgnoreHitCollider(Collider collider)
    {
        if (collider == null)
            return true;

        if (magnetExtender != null && (collider.transform == magnetExtender.transform || collider.transform.IsChildOf(magnetExtender.transform)))
            return true;

        if (collider.transform == transform || collider.transform.IsChildOf(transform))
            return true;

        return false;
    }

    private bool IsTargetCollider(Collider collider)
    {
        if (collider == null || targetObject == null)
            return false;

        if (collider.gameObject == targetObject)
            return true;

        Transform targetTransform = targetObject.transform;
        return collider.transform.IsChildOf(targetTransform)
            || targetTransform.IsChildOf(collider.transform)
            || collider.transform.root == targetTransform.root;
    }

    private void GetRayData(out Vector3 originA, out Vector3 originB, out Vector3 originC, out Vector3 originD, out Vector3 castDir)
    {
        Vector3 offset = magnetExtender.transform.TransformDirection(Vector3.forward * 2f);
        Vector3 offset2 = magnetExtender.transform.TransformDirection(Vector3.right * 2f);
        originA = magnetExtender.transform.position + offset;
        originB = magnetExtender.transform.position - offset;
        originC = magnetExtender.transform.position + offset2;
        originD = magnetExtender.transform.position - offset2;
        castDir = magnetExtender.transform.TransformDirection(Vector3.down);
    }

    public void BounceOffObject()
    {
        // Called by MagnetCollisionHandler when magnet hits a non-target object
        isExtending = false;
        isRetracting = true;
        
        if (retractCoroutine != null)
        {
            StopCoroutine(retractCoroutine);
        }
        
        if (magnetExtender != null)
        {
            Vector3 startPosition = magnetExtender.transform.localPosition;
            retractCoroutine = StartCoroutine(RetractMagnet(magnetExtender, startPosition, 0.5f));
        }

    }

    protected void AssignRayData()
    {
        if (magnetExtender == null) return;

        GetRayData(out var originA, out var originB, out var originC, out var originD, out var castDir);

        if (Physics.Raycast(originA, castDir, out var dbgHitA, magnetDetectLength, grabLayerMask))
        {
            Debug.DrawRay(originA, castDir * dbgHitA.distance, IsTargetCollider(dbgHitA.collider) ? Color.cyan : Color.red);
        }
        else
        {
            Debug.DrawRay(originA, castDir * magnetDetectLength, Color.yellow);
        }

        if (Physics.Raycast(originB, castDir, out var dbgHitB, magnetDetectLength, grabLayerMask))
        {
            Debug.DrawRay(originB, castDir * dbgHitB.distance, IsTargetCollider(dbgHitB.collider) ? Color.cyan : Color.red);
        }
        else
        {
            Debug.DrawRay(originB, castDir * magnetDetectLength, Color.yellow);
        }

        if (Physics.Raycast(originC, castDir, out var dbgHitC, magnetDetectLength, grabLayerMask))
        {
            Debug.DrawRay(originC, castDir * dbgHitC.distance, IsTargetCollider(dbgHitC.collider) ? Color.cyan : Color.red);
        }
        else
        {
            Debug.DrawRay(originC, castDir * magnetDetectLength, Color.yellow);
        }

        if (Physics.Raycast(originD, castDir, out var dbgHitD, magnetDetectLength, grabLayerMask))
        {
            Debug.DrawRay(originD, castDir * dbgHitD.distance, IsTargetCollider(dbgHitD.collider) ? Color.cyan : Color.red);
        }
        else
        {
            Debug.DrawRay(originD, castDir * magnetDetectLength, Color.yellow);
        }
    }

    protected void OnDrawGizmos()
    {
        if (magnetExtender == null) return;

        GetRayData(out var originA, out var originB, out var originC, out var originD, out var castDir);

        // Draw gizmos for all four raycasts
        if (Physics.Raycast(originA, castDir, out var hitA, magnetDetectLength, grabLayerMask))
        {
            Gizmos.color = IsTargetCollider(hitA.collider) ? Color.cyan : Color.red;
            Gizmos.DrawLine(originA, hitA.point);
            Gizmos.DrawWireSphere(hitA.point, 0.1f);
        }
        else
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(originA, originA + castDir * magnetDetectLength);
        }

        if (Physics.Raycast(originB, castDir, out var hitB, magnetDetectLength, grabLayerMask))
        {
            Gizmos.color = IsTargetCollider(hitB.collider) ? Color.cyan : Color.red;
            Gizmos.DrawLine(originB, hitB.point);
            Gizmos.DrawWireSphere(hitB.point, 0.1f);
        }
        else
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(originB, originB + castDir * magnetDetectLength);
        }

        if (Physics.Raycast(originC, castDir, out var hitC, magnetDetectLength, grabLayerMask))
        {
            Gizmos.color = IsTargetCollider(hitC.collider) ? Color.cyan : Color.red;
            Gizmos.DrawLine(originC, hitC.point);
            Gizmos.DrawWireSphere(hitC.point, 0.1f);
        }
        else
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(originC, originC + castDir * magnetDetectLength);
        }

        if (Physics.Raycast(originD, castDir, out var hitD, magnetDetectLength, grabLayerMask))
        {
            Gizmos.color = IsTargetCollider(hitD.collider) ? Color.cyan : Color.red;
            Gizmos.DrawLine(originD, hitD.point);
            Gizmos.DrawWireSphere(hitD.point, 0.1f);
        }
        else
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(originD, originD + castDir * magnetDetectLength);
        }
    }

    private string GetLayerMaskNames(LayerMask mask)
    {
        System.Collections.Generic.List<string> layers = new System.Collections.Generic.List<string>();
        for (int i = 0; i < 32; i++)
        {
            if ((mask.value & (1 << i)) != 0)
            {
                string layerName = LayerMask.LayerToName(i);
                if (!string.IsNullOrEmpty(layerName))
                {
                    layers.Add(layerName);
                }
            }
        }
        return layers.Count > 0 ? string.Join(", ", layers) : "None";
    }

    private void EnsureMagnetIndicator()
    {
        if (!showMagnetIndicator || magnetExtender == null || magnetIndicator != null)
            return;

        var lineObj = new GameObject("MagnetIndicator");
        lineObj.transform.SetParent(magnetExtender.transform);
        lineObj.transform.localPosition = Vector3.zero;
        lineObj.transform.localRotation = Quaternion.identity;
        lineObj.transform.localScale = Vector3.one;

        magnetIndicator = lineObj.AddComponent<LineRenderer>();
        magnetIndicator.useWorldSpace = true;
        magnetIndicator.positionCount = 2;
        magnetIndicator.startWidth = Mathf.Max(0.001f, indicatorWidth);
        magnetIndicator.endWidth = Mathf.Max(0.001f, indicatorWidth);
        magnetIndicator.material = new Material(Shader.Find("Sprites/Default"));
        magnetIndicator.startColor = indicatorColor;
        magnetIndicator.endColor = indicatorColor;
        magnetIndicator.enabled = false;
    }

    private void UpdateMagnetIndicator()
    {
        if (!showMagnetIndicator || magnetExtender == null)
        {
            if (magnetIndicator != null)
                magnetIndicator.enabled = false;
            return;
        }

        if (showIndicatorOnlyWhenActive && !indicatorActive)
        {
            if (magnetIndicator != null)
                magnetIndicator.enabled = false;
            return;
        }

        EnsureMagnetIndicator();
        if (magnetIndicator == null)
            return;

        Vector3 start = magnetExtender.transform.position + magnetExtender.transform.TransformDirection(indicatorOffset);
        Vector3 dir = magnetExtender.transform.TransformDirection(Vector3.down);
        float maxDist = Mathf.Max(0.01f, indicatorMaxDistance);

        Vector3 end = start + dir * maxDist;
        if (Physics.Raycast(start, dir, out var hit, maxDist, indicatorMask, QueryTriggerInteraction.Ignore))
            end = hit.point;

        Color baseColor = indicatorColor;
        if (IsIndicatorNearTarget(end))
            baseColor = indicatorHighlightColor;

        float pulseAlpha = GetPulseAlpha();
        Color pulseColor = new Color(baseColor.r, baseColor.g, baseColor.b, pulseAlpha);
        magnetIndicator.startColor = pulseColor;
        magnetIndicator.endColor = pulseColor;

        magnetIndicator.SetPosition(0, start);
        magnetIndicator.SetPosition(1, end);
        magnetIndicator.enabled = true;
    }

    private bool IsIndicatorNearTarget(Vector3 indicatorEnd)
    {
        if (targetObject == null)
            return false;

        Vector3 targetCenter = targetObject.transform.position;
        Collider targetCollider = targetObject.GetComponentInChildren<Collider>();
        if (targetCollider != null)
            targetCenter = targetCollider.bounds.center;

        return Vector3.Distance(indicatorEnd, targetCenter) <= indicatorHighlightDistance;
    }

    private float GetPulseAlpha()
    {
        float speed = Mathf.Max(0.01f, indicatorPulseSpeed);
        float t = (Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f * speed) + 1f) * 0.5f;
        float minA = Mathf.Clamp01(indicatorPulseMinAlpha);
        float maxA = Mathf.Clamp01(indicatorPulseMaxAlpha);
        if (maxA < minA)
        {
            float swap = minA;
            minA = maxA;
            maxA = swap;
        }
        return Mathf.Lerp(minA, maxA, t);
    }

    #endregion
}