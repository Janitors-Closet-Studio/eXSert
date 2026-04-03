/*
    Written by Brandon Wahl

    This script handles the extension and retraction of the lattice catwalks in the hangar level.
    This script will move both the platform and the lattice (magnet) parts smoothly over time.

*/
using System.Collections;
using UnityEngine;
#pragma warning disable CS0414

public class RetractableLatticeExtendPuzzle : PuzzlePart
{
    #region Platform Variables
    [Header("Platform Variables")]
    [SerializeField] private GameObject platform;
    [Tooltip("Offset to apply to the platform when extending (local space). Set Z negative to move back, positive to move forward.")][SerializeField] private Vector3 platformExtendOffset = new Vector3(0f, 0f, -5f);
    [Tooltip("Platform movement speed in world units per second (used to compute duration as distance/speed). If <=0, falls back to `platformLerpDuration`")][SerializeField] private float platformSpeed = 5f;
    private Vector3 platformStartPos;
    private Vector3 platformTargetPos;
    private float platformDuration = 0.5f;
    protected Vector3 platformOrigin;
    #endregion

    [Space(20)]
    
    #region Lattice Variables
    [Header("Lattice (Magnet) Variables")]
    [SerializeField] private GameObject magnetOne;
    [SerializeField] private GameObject magnetTwo;
    [Tooltip("Lattice (magnet) movement speed in local units per second (used to compute duration as distance/speed)")][SerializeField] private float latticeSpeed = 2f;
    [SerializeField] private Vector3 latticeExtendOffset = new Vector3(0f, 5f, 0f);
    private Vector3 magnetOneStartPos;
    private Vector3 magnetOneTargetPos;
    private Vector3 magnetTwoStartPos;
    private Vector3 magnetTwoTargetPos;
    private float magnetOneDuration = 0.25f;
    private float magnetTwoDuration = 0.25f;

    protected Vector3 magnetOneOrigin;
    protected Vector3 magnetTwoOrigin;
    #endregion

    #region Magnet Indicator
    [Header("Magnet Indicator")]
    [SerializeField] private bool showMagnetIndicator = true;
    [SerializeField] private float indicatorMaxDistance = 50f;
    [SerializeField] private float indicatorWidth = 0.05f;
    [SerializeField] private Color indicatorColor = Color.red;
    [SerializeField] private LayerMask indicatorMask = ~0;
    [SerializeField] private bool indicatorUseLocalDown = false;
    [SerializeField] private Vector3 indicatorOffset = Vector3.zero;

    private LineRenderer magnetOneIndicator;
    private LineRenderer magnetTwoIndicator;
    #endregion

    private bool isExtending = false;

    private void Awake()
    {
        // Cache original positions once
        platformOrigin = platform != null ? platform.transform.position : Vector3.zero;
        magnetOneOrigin = magnetOne != null ? magnetOne.transform.position : Vector3.zero;
        magnetTwoOrigin = magnetTwo != null ? magnetTwo.transform.position : Vector3.zero;

        if (showMagnetIndicator)
        {
            EnsureMagnetIndicator(magnetOne != null ? magnetOne.transform : null, ref magnetOneIndicator, "MagnetIndicatorOne");
            EnsureMagnetIndicator(magnetTwo != null ? magnetTwo.transform : null, ref magnetTwoIndicator, "MagnetIndicatorTwo");
        }
#pragma warning restore CS0414
    }

    private void Update()
    {
        UpdateMagnetIndicator(magnetOne != null ? magnetOne.transform : null, ref magnetOneIndicator, "MagnetIndicatorOne");
        UpdateMagnetIndicator(magnetTwo != null ? magnetTwo.transform : null, ref magnetTwoIndicator, "MagnetIndicatorTwo");
    }

    public override void ConsoleInteracted()
    {
        throw new System.NotImplementedException();
    }

    public override void StartPuzzle()
    {
        // Prepare start/target positions
        platformStartPos = platformOrigin;
        magnetOneStartPos = magnetOneOrigin;
        magnetTwoStartPos = magnetTwoOrigin;

        // Compute world-space target using each respective object's transform to respect orientation
        if (platform != null)
            platformTargetPos = platformStartPos + platform.transform.TransformDirection(platformExtendOffset);
        else
            platformTargetPos = platformStartPos + platformExtendOffset;

        if (magnetOne != null)
            magnetOneTargetPos = magnetOneStartPos + magnetOne.transform.TransformDirection(latticeExtendOffset);
        else
            magnetOneTargetPos = magnetOneStartPos + latticeExtendOffset;

        if (magnetTwo != null)
            magnetTwoTargetPos = magnetTwoStartPos + magnetTwo.transform.TransformDirection(latticeExtendOffset);
        else
            magnetTwoTargetPos = magnetTwoStartPos + latticeExtendOffset;

        // compute durations from speeds if requested
        float platformDist = Vector3.Distance(platformStartPos, platformTargetPos);
        platformDuration = (platformSpeed > 0f) ? platformDist / platformSpeed : 0.5f;

        magnetOneDuration = 0f; 
        if (magnetOne != null)
        {
            float m1dist = Vector3.Distance(magnetOneStartPos, magnetOneTargetPos); // Distance magnet one needs to travel
            magnetOneDuration = (latticeSpeed > 0f) ? m1dist / latticeSpeed : 0.25f; // How long it should take magnet one to travel
        }

        magnetTwoDuration = 0f;
        if (magnetTwo != null)
        {
            float m2dist = Vector3.Distance(magnetTwoStartPos, magnetTwoTargetPos); // Distance magnet two needs to travel
            magnetTwoDuration = (latticeSpeed > 0f) ? m2dist / latticeSpeed : 0.25f; // How long it should take magnet two to travel
        }

        // Start smooth extension coroutine (do not flip isCompleted here; coroutine will set it)
        StopAllCoroutines();
        isCompleted = false;
        StartCoroutine(ExtendLatticeParts());
    }

    public override void EndPuzzle()
    {
        if (isCompleted)
        {
            // Prepare start/target positions for retraction
            platformStartPos = platform != null ? platform.transform.position : Vector3.zero;
            platformTargetPos = platformOrigin;

            magnetOneStartPos = magnetOne != null ? magnetOne.transform.position : Vector3.zero;
            magnetOneTargetPos = magnetOneOrigin;

            magnetTwoStartPos = magnetTwo != null ? magnetTwo.transform.position : Vector3.zero;
            magnetTwoTargetPos = magnetTwoOrigin;

            // compute durations from speeds if requested
            float platformDist = Vector3.Distance(platformStartPos, platformTargetPos);
            platformDuration = (platformSpeed > 0f) ? platformDist / platformSpeed : 0.5f;

            magnetOneDuration = 0f;
            if (magnetOne != null)
            {
                float m1dist = Vector3.Distance(magnetOneStartPos, magnetOneTargetPos);
                magnetOneDuration = (latticeSpeed > 0f) ? m1dist / latticeSpeed : 0.25f;
            }

            magnetTwoDuration = 0f;
            if (magnetTwo != null)
            {
                float m2dist = Vector3.Distance(magnetTwoStartPos, magnetTwoTargetPos);
                magnetTwoDuration = (latticeSpeed > 0f) ? m2dist / latticeSpeed : 0.25f;
            }

            // Start smooth retraction coroutine
            StopAllCoroutines();
            StartCoroutine(ExtendLatticeParts());
        }
    }

    #region Coroutines
    private IEnumerator ExtendPlatform()
    {

        if (platform == null)
            yield break;

        // compute platform duration based on speed if requested
        float pDur = platformDuration;

        // if duration is <= 0, snap and finish
        if (pDur <= 0f)
        {
            platform.transform.position = platformTargetPos;
            yield break;
        }

        isExtending = true;
        float elapsedPlat = 0f;
        while (elapsedPlat < pDur)
        {
            float t = Mathf.Clamp01(elapsedPlat / pDur);
            platform.transform.position = Vector3.Lerp(platformStartPos, platformTargetPos, t);
            elapsedPlat += Time.deltaTime;
            yield return null;
        }

        // Ensure final position
        platform.transform.position = platformTargetPos;
        isExtending = false;
    }

    private IEnumerator ExtendMagnets()
    {
        if (magnetOne == null && magnetTwo == null)
            yield break;

        // compute magnet durations based on speeds if requested
        float m1 = magnetOneDuration;
        float m2 = magnetTwoDuration;

        // if both durations are <= 0, snap them and finish
        if (m1 <= 0f && m2 <= 0f)
        {
            if (magnetOne != null) magnetOne.transform.position = magnetOneTargetPos;
            if (magnetTwo != null) magnetTwo.transform.position = magnetTwoTargetPos;
            yield break;
        }

        // animate simultaneously using per-magnet durations, use max to drive the loop
        float maxDur = Mathf.Max(m1 > 0f ? m1 : 0f, m2 > 0f ? m2 : 0f); // Assign max duration based on which magnet takes longer
        if (maxDur <= 0f) maxDur = 0.0001f; // Assign small value to avoid division by zero

        isExtending = true;
        float elapsedMag = 0f;

        // Loop until the longest duration is reached
        while (elapsedMag < maxDur)
        {
            float t1 = (m1 > 0f) ? Mathf.Clamp01(elapsedMag / m1) : 1f; // Calculates for t (duration of lerp) for magnet one
            float t2 = (m2 > 0f) ? Mathf.Clamp01(elapsedMag / m2) : 1f; // Calculates for t (duration of lerp) for magnet two

            if (magnetOne != null)
                magnetOne.transform.position = Vector3.Lerp(magnetOneStartPos, magnetOneTargetPos, t1);
            if (magnetTwo != null)
                magnetTwo.transform.position = Vector3.Lerp(magnetTwoStartPos, magnetTwoTargetPos, t2);

            elapsedMag += Time.deltaTime;

            yield return null;
        }

        // Ensure final position
        if (magnetOne != null) 
            magnetOne.transform.position = magnetOneTargetPos;
        if (magnetTwo != null) 
            magnetTwo.transform.position = magnetTwoTargetPos;
        isExtending = false;
    }

    private IEnumerator RetractMagnets()
    {
        if (magnetOne == null && magnetTwo == null)
            yield break;

        // compute magnet durations based on speeds if requested (computed earlier in StartPuzzle)
        float m1 = magnetOneDuration;
        float m2 = magnetTwoDuration;

        // if both durations are <= 0, snap them and finish
        if (m1 <= 0f && m2 <= 0f)
        {
            if (magnetOne != null) magnetOne.transform.position = magnetOneTargetPos;
            if (magnetTwo != null) magnetTwo.transform.position = magnetTwoTargetPos;
            yield break;
        }

        // animate simultaneously using per-magnet durations
        float maxDur = Mathf.Max(m1 > 0f ? m1 : 0f, m2 > 0f ? m2 : 0f); // Assign max duration based on which magnet takes longer
        if (maxDur <= 0f) maxDur = 0.0001f; // Assign small value to avoid division by zero
        isExtending = true;
        float elapsedMag = 0f;
        
        // Loop until the longest duration is reached
        while (elapsedMag < maxDur)
        {
            float t1 = (m1 > 0f) ? Mathf.Clamp01(elapsedMag / m1) : 1f; // Calculates for t (duration of lerp) for magnet one
            float t2 = (m2 > 0f) ? Mathf.Clamp01(elapsedMag / m2) : 1f; // Calculates for t (duration of lerp) for magnet two
            if (magnetOne != null)
                magnetOne.transform.position = Vector3.Lerp(magnetOneStartPos, magnetOneTargetPos, t1);
            if (magnetTwo != null)
                magnetTwo.transform.position = Vector3.Lerp(magnetTwoStartPos, magnetTwoTargetPos, t2);
            elapsedMag += Time.deltaTime;
            yield return null;
        }

        // Ensure final position
        if (magnetOne != null) magnetOne.transform.position = magnetOneTargetPos;
        if (magnetTwo != null) magnetTwo.transform.position = magnetTwoTargetPos;
        isExtending = false;
    }

    private IEnumerator RetractPlatform()
    {
        if (platform == null)
            yield break;

        // compute platform duration based on speed if requested (computed earlier in StartPuzzle)
        float pDur = platformDuration;

        // if duration is <= 0, snap and finish
        if (pDur <= 0f)
        {
            platform.transform.position = platformTargetPos;
            yield break;
        }

        isExtending = true;
        float elapsedPlat = 0f;
        while (elapsedPlat < pDur)
        {
            float t = Mathf.Clamp01(elapsedPlat / pDur);
            platform.transform.position = Vector3.Lerp(platformStartPos, platformTargetPos, t);
            elapsedPlat += Time.deltaTime;
            yield return null;
        }

        // Ensure final position
        platform.transform.position = platformTargetPos;
        isExtending = false;
    }

    // Master coroutine to extend or retract all parts in sequence
    IEnumerator ExtendLatticeParts()
    {
        if (!isCompleted)
        {
            // extend magnets then platform, waiting for each coroutine to finish
            yield return StartCoroutine(ExtendMagnets());
            yield return StartCoroutine(ExtendPlatform());

            // mark completed after successful extension
            isCompleted = true;
            yield break;
        }
        else
        {
            // retract magnets then platform
            yield return StartCoroutine(RetractMagnets());
            yield return StartCoroutine(RetractPlatform());

            // mark not completed after retraction
            isCompleted = false;
            yield break;
        }
    }
    #endregion

    private void EnsureMagnetIndicator(Transform origin, ref LineRenderer line, string name)
    {
        if (origin == null || line != null)
            return;

        var lineObj = new GameObject(name);
        lineObj.transform.SetParent(origin);
        lineObj.transform.localPosition = Vector3.zero;
        lineObj.transform.localRotation = Quaternion.identity;
        lineObj.transform.localScale = Vector3.one;

        line = lineObj.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.startWidth = Mathf.Max(0.001f, indicatorWidth);
        line.endWidth = Mathf.Max(0.001f, indicatorWidth);
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = indicatorColor;
        line.endColor = indicatorColor;
        line.enabled = false;
    }

    private void UpdateMagnetIndicator(Transform origin, ref LineRenderer line, string name)
    {
        if (!showMagnetIndicator || origin == null)
        {
            if (line != null)
                line.enabled = false;
            return;
        }

        EnsureMagnetIndicator(origin, ref line, name);
        if (line == null)
            return;

        Vector3 start = origin.position + origin.TransformDirection(indicatorOffset);
        Vector3 dir = indicatorUseLocalDown ? -origin.up : Vector3.down;
        float maxDist = Mathf.Max(0.01f, indicatorMaxDistance);

        Vector3 end = start + dir * maxDist;
        if (Physics.Raycast(start, dir, out var hit, maxDist, indicatorMask, QueryTriggerInteraction.Ignore))
            end = hit.point;

        line.SetPosition(0, start);
        line.SetPosition(1, end);
        line.enabled = true;
    }
}
