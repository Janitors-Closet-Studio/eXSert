/*
    Written by Brandon Wahl

    This script handles the different types of door interactions with realistic door movements.
    Doors can open upwards, outwards, or inwards depending on the door type set in the inspector.
    Lock management is handled by the DoorInteractions component (part of UnlockableInteraction system).
*/


using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine.ProBuilder.Shapes;




#if UNITY_EDITOR
using UnityEditor;
#endif
//Once the pieces are in the list, you can set which axes they move on and their min/max positions
[System.Serializable]
public class DoorPartMovement
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
public class DoorHandler : MonoBehaviour
{
    public enum DoorState { Open, Closed }

    public enum DoorLockState { Locked, Unlocked }

    public enum DoorType { OpenUnconvential, OpenOut, OpenIn }

    public DoorState currentDoorState;
    public DoorLockState doorLockState = DoorLockState.Locked;
    public DoorType doorType;
    

    internal Vector3 doorPosOrigin;
    private Quaternion doorRotOrigin;
    private Vector3 targetDoorPos;
    private Quaternion targetDoorRot;
    private Vector3 targetPos;

    [Tooltip("Only used for OpenUnconvential door type. List of door parts to move and their movement settings.")]
    public List<DoorPartMovement> doorParts = new List<DoorPartMovement>();
    
    [SerializeField] private float openSpeed = 2f;

    private bool isOpening = false;
    private bool isOpened = false;

    [Tooltip("Optional hinge pivot. If null, a pivot GameObject will be created at the door origin.")]
    [ShowIfDoorType(DoorHandler.DoorType.OpenOut, DoorHandler.DoorType.OpenIn)]
    [SerializeField] private Transform hingePivot;

    [Header("Door Light Settings")]
    [Tooltip("Light bulb to change color")]
    public GameObject lightBulb;
    [Tooltip("Color of the light bulb when the door is locked")]
    public Color lockedLightBulbColor;
    [Tooltip("Color of the light bulb when the door is unlocked")]
    public Color unlockedLightBulbColor;

    [Tooltip("Light component on the door to change color")]
    public Light doorLight;
    [Tooltip("Color of the light when the door is locked")]
    public Color lockedLightColor;
    [Tooltip("Color of the light when the door is unlocked")]
    public Color unlockedLightColor;

    [Tooltip("Speed of the light color transition")]
    public float lightFadeSpeed = 2f;

    // Hinge variables that are used for OpenIn and OpenOut door types
    private Quaternion hingeStartRot;
    private Quaternion hingeTargetRot;
    private Quaternion hingeOriginalRot; // Store the original hinge rotation before any animation
    private Coroutine hingeAnimCoroutine = null;
    private Vector3 bottomTargetPos;
    private Vector3 topTartetPos;

    // Store original parent to reparent door after using hinge pivot
    private Transform originalParent;

    private void Awake()
    {
        doorPosOrigin = this.transform.position;
        doorRotOrigin = this.transform.rotation;
        
        // If a hinge pivot is provided, store its original rotation
        if (hingePivot != null)
        {
            hingeOriginalRot = hingePivot.rotation;
        }

        StartingLightColor();
    }

    /// <summary>
    /// Toggles the door between Open and Closed states.
    /// Lock checking is handled by DoorInteractions component.
    /// </summary>
    public void Interact()
    {
        switch (currentDoorState)
        {
            case DoorState.Open:
                CloseDoor();
                break;
            case DoorState.Closed:
                OpenDoor();
                break;
        }


    }

    // Intializes the door light color based on the current lock and door state
    private void StartingLightColor()
    {
        if (DoorLockState.Locked == doorLockState)
        {
            doorLight.color = lockedLightColor;
        }
        else
        {
            doorLight.color = unlockedLightColor;
        }
    }

    internal MeshRenderer GetLightMeshRenderer()
    {
        if (lightBulb != null)
        {
            MeshRenderer meshRenderer = lightBulb.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                return meshRenderer;
            }
            else
            {
                Debug.LogWarning("Door light object does not have a MeshRenderer component.");
                return null;
            }
        }
        else
        {
            Debug.LogWarning("Door light object is not assigned.");
            return null;
        }
    }

    public void DoorHandlerCoroutines()
    {
        StartCoroutine(FadeLightBulbHDRColor(lockedLightBulbColor, unlockedLightBulbColor, lightFadeSpeed));
        StartCoroutine(FadeColorIntoEachother(lockedLightColor, unlockedLightColor, lightFadeSpeed));
    }

    private IEnumerator FadeLightBulbHDRColor(Color fromColor, Color toColor, float duration)
    {
        MeshRenderer meshRenderer = GetLightMeshRenderer();
        if (meshRenderer == null)
            yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            Color currentColor = Color.Lerp(fromColor, toColor, t);
            meshRenderer.material.EnableKeyword("_EMISSION");
            meshRenderer.material.SetColor("_EmissionColor", currentColor);
            elapsed += Time.deltaTime;
            yield return null;
        }
        meshRenderer.material.SetColor("_EmissionColor", toColor);
    }

    // Fade Color into eachother over time, used for light color transitions when opening/closing and locking/unlocking the door
    private IEnumerator FadeColorIntoEachother(Color fromColor, Color toColor, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            doorLight.color = Color.Lerp(fromColor, toColor, t);
            elapsed += Time.deltaTime;
            yield return null;
        }
        doorLight.color = toColor;
    }

    private void OpenDoor()
    {
        Debug.Log("Opening the door.");
        currentDoorState = DoorState.Open;

        if (doorLockState == DoorLockState.Locked)
            doorLockState = DoorLockState.Unlocked;
        
        switch (doorType)
        {
            case DoorType.OpenUnconvential:
                OpenUnconvential();
                break;
            case DoorType.OpenOut:
                OpenOut();
                break;
            case DoorType.OpenIn:
                OpenIn();
                break;
        }
    }

    private void CloseDoor()
    {
        Debug.Log("Closing the door.");
        currentDoorState = DoorState.Closed;

        switch (doorType)
        {
            case DoorType.OpenUnconvential:
                CloseUnconvential();
                break;
            case DoorType.OpenOut:
                EnsurePivot();
                StartHingeAnimation(hingePivot.rotation, hingeOriginalRot, 1f / openSpeed);
                break;
            case DoorType.OpenIn:
                EnsurePivot();
                StartHingeAnimation(hingePivot.rotation, hingeOriginalRot, 1f / openSpeed);
                break;
        }
    }

    private void OpenOut()
    {
        // Use hinge pivot to rotate outwards so the door stays locked in its socket
        EnsurePivot();
        hingeStartRot = hingePivot.rotation;
        hingeTargetRot = hingeStartRot * Quaternion.Euler(0f, -90f, 0f);
        StartHingeAnimation(hingeStartRot, hingeTargetRot, 1f / openSpeed);
    }

    private void OpenIn()
    {
        // Use hinge pivot to rotate inwards so the door stays locked in its socket
        EnsurePivot();
        hingeStartRot = hingePivot.rotation;
        hingeTargetRot = hingeStartRot * Quaternion.Euler(0f, 90f, 0f);
        StartHingeAnimation(hingeStartRot, hingeTargetRot, 1f / openSpeed); 
    }

    // These two functions handle the OpenUp door type
    private void OpenUnconvential()
    {
        // Store closed local positions at the moment of opening
        foreach (var partMove in doorParts)
        {
            if (partMove.partObject != null)
                partMove.closedLocalPosition = partMove.partObject.transform.localPosition;
        }
        StopAllCoroutines();
        StartCoroutine(OpenUnconventialCoroutine());
    }

    private void CloseUnconvential()
    {
        StopAllCoroutines();
        StartCoroutine(CloseUnconventialCoroutine());
    }

    // Ensure the hinge pivot exists and is parent of the door
    private void EnsurePivot()
    {
        // If the hinge pivot doesn't exist, create it at the door's origin
        if (hingePivot == null)
        {
            GameObject go = new GameObject(this.gameObject.name + "PivotPoint");
            go.transform.position = doorPosOrigin;
            go.transform.rotation = doorRotOrigin;
            // parent pivot to the door's original parent to keep hierarchy
            go.transform.SetParent(this.transform.parent, true);
            hingePivot = go.transform;
            // Store the original hinge rotation for closing animation (first time only)
            hingeOriginalRot = hingePivot.rotation;
            // store original parent and reparent the door to hinge while preserving world transform
            originalParent = this.transform.parent;
            this.transform.SetParent(hingePivot, true);
        }
        else
        {
            //if there is a pivot point already, just make sure the door is parented to it
            if (this.transform.parent != hingePivot)
            {
                originalParent = this.transform.parent;
                this.transform.SetParent(hingePivot, true);
            }
        }
    }

    // Start hinge animation coroutine
    private void StartHingeAnimation(Quaternion from, Quaternion to, float duration)
    {
        if (hingeAnimCoroutine != null)
            StopCoroutine(hingeAnimCoroutine);
        hingeAnimCoroutine = StartCoroutine(AnimateHinge(from, to, duration));
    }

    // Coroutine to animate the hinge rotation smoothly over time. from == starting rotation, to == target rotation
    private IEnumerator AnimateHinge(Quaternion from, Quaternion to, float duration)
    {
        if (hingePivot == null)
            yield break;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            // Animate the hinge rotation smoothly over time
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            hingePivot.rotation = Quaternion.Slerp(from, to, t);
            elapsed += Time.deltaTime;
            yield return null;
        }
        hingePivot.rotation = to;
        hingeAnimCoroutine = null;
    }

    // Coroutines for opening and closing the door upwards
    private IEnumerator OpenUnconventialCoroutine()
    {
        if (doorParts == null || doorParts.Count == 0)
        {
            yield break;
        }

        // Prepare target local positions for each part
        List<GameObject> movingParts = new List<GameObject>();
        List<Vector3> targetLocalPositions = new List<Vector3>();

        foreach (var partMove in doorParts)
        {
            if (partMove.partObject == null) continue;
            Vector3 offset = new Vector3(
                partMove.moveX ? partMove.distToOpenParts : 0f,
                partMove.moveY ? partMove.distToOpenParts : 0f,
                partMove.moveZ ? partMove.distToOpenParts : 0f
            );
            movingParts.Add(partMove.partObject);
            // Use closedLocalPosition as base
            targetLocalPositions.Add(partMove.closedLocalPosition + offset);
        }

        if (movingParts.Count == 0)
        {
            yield break;
        }

        bool allAtTarget = false;
        while (!allAtTarget)
        {
            allAtTarget = true;
            for (int i = 0; i < movingParts.Count; i++)
            {
                var part = movingParts[i];
                var target = targetLocalPositions[i];
                if (part == null) continue;
                if (Vector3.Distance(part.transform.localPosition, target) > 0.01f)
                {
                    float t = Mathf.Clamp01(openSpeed * Time.deltaTime);
                    part.transform.localPosition = Vector3.Lerp(part.transform.localPosition, target, t);
                    allAtTarget = false;
                }
            }
            yield return null;
        }
        isOpened = true;
        yield return null;
    }

    private IEnumerator CloseUnconventialCoroutine()
    {
        if (doorParts == null || doorParts.Count == 0)
        {
            yield break;
        }

        // Prepare closed local positions for each part (return to closedLocalPosition)
        List<GameObject> movingParts = new List<GameObject>();
        List<Vector3> closedLocalPositions = new List<Vector3>();

        foreach (var partMove in doorParts)
        {
            if (partMove.partObject == null) continue;
            movingParts.Add(partMove.partObject);
            closedLocalPositions.Add(partMove.closedLocalPosition);
        }

        if (movingParts.Count == 0)
        {
            yield break;
        }

        bool allAtClosed = false;
        while (!allAtClosed)
        {
            allAtClosed = true;
            for (int i = 0; i < movingParts.Count; i++)
            {
                var part = movingParts[i];
                var target = closedLocalPositions[i];
                if (part == null) continue;
                if (Vector3.Distance(part.transform.localPosition, target) > 0.01f)
                {
                    float t = Mathf.Clamp01(openSpeed * Time.deltaTime);
                    part.transform.localPosition = Vector3.Lerp(part.transform.localPosition, target, t);
                    allAtClosed = false;
                }
            }
            yield return null;
        }
        isOpened = false;
        yield return null;
    }

}
