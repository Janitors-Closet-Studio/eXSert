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
    private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
    private static readonly int LegacyColorProperty = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorProperty = Shader.PropertyToID("_EmissionColor");

    private MaterialPropertyBlock lightBulbPropertyBlock;

    public enum DoorState { Open, Closed }
    public enum DoorLockState { Locked, Unlocked }
    public enum DoorType { OpenUnconvential, OpenOut, OpenIn }
    

    [Header("Door State & Type")]
    [Tooltip("Current state of the door (Open/Closed)")]
    public DoorState currentDoorState;
    [Tooltip("Current lock state of the door (Locked/Unlocked)")]
    public DoorLockState doorLockState = DoorLockState.Locked;
    [Tooltip("Type of door movement")]
    public DoorType doorType;


    [Header("Door Movement Settings")]
    [Tooltip("Original position of the door (auto-set)")]
    internal Vector3 doorPosOrigin;
    [Tooltip("Original rotation of the door (auto-set)")]
    private Quaternion doorRotOrigin;
    private Vector3 targetDoorPos;
    private Quaternion targetDoorRot;
    private Vector3 targetPos;

    [Tooltip("Only used for OpenUnconvential door type. List of door parts to move and their movement settings.")]
    public List<DoorPartMovement> doorParts = new List<DoorPartMovement>();

    [Tooltip("Speed at which the door opens/closes")]
    [SerializeField] private float openSpeed = 2f;

    private bool isOpening = false;
    internal bool isOpened = false;


    [Header("Hinge Settings (for OpenOut/OpenIn)")]
    [Tooltip("Optional hinge pivot. If null, a pivot GameObject will be created at the door origin.")]
    [ShowIfDoorType(DoorHandler.DoorType.OpenOut, DoorHandler.DoorType.OpenIn)]
    [SerializeField] private Transform hingePivot;

    [Header("Door Light Settings")]
    [Tooltip("Light bulb GameObject to change color")]
    public GameObject lightBulb;

    [Tooltip("Base color of the bulb material when the door is locked.")]
    [ColorUsage(false, true)]
    public Color lockedLightBulbColor = DefaultLockedBulbBaseColor;

    [Tooltip("Emission color of the bulb material when the door is locked.")]
    [ColorUsage(true, true)]
    public Color lockedLightBulbEmissionColor = DefaultLockedBulbEmissionColor;

    [Tooltip("Base color of the bulb material when the door is unlocked.")]
    [ColorUsage(false, true)]
    public Color unlockedLightBulbColor = DefaultUnlockedBulbBaseColor;

    [Tooltip("Emission color of the bulb material when the door is unlocked.")]
    [ColorUsage(true, true)]
    public Color unlockedLightBulbEmissionColor = DefaultUnlockedBulbEmissionColor;

    [Tooltip("Light component on the door to change color")]
    public Light doorLight;
    [Tooltip("Color of the light when the door is locked")]
    public Color lockedLightColor = DefaultLockedPointLightColor;
    [Tooltip("Color of the light when the door is unlocked")]
    public Color unlockedLightColor = DefaultUnlockedPointLightColor;

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

    private static Color DefaultLockedBulbBaseColor => ColorFromHex("A10000");
    private static Color DefaultLockedBulbEmissionColor => ColorFromHsv(0f, 100f, 38f);
    private static Color DefaultLockedPointLightColor => ColorFromHex("FF1E1E");
    private static Color DefaultUnlockedBulbBaseColor => ColorFromHex("1DC814");
    private static Color DefaultUnlockedBulbEmissionColor => ColorFromHsv(145f, 100f, 13f);
    private static Color DefaultUnlockedPointLightColor => ColorFromHex("44A659");

    private void Awake()
    {
        doorPosOrigin = this.transform.localPosition;
        doorRotOrigin = this.transform.localRotation;
        
        // If a hinge pivot is provided, store its original rotation
        if (hingePivot != null)
        {
            hingeOriginalRot = hingePivot.rotation;
        }

        StartingLightColor();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
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
                StartCoroutine(NotAllowReentryCoroutine());
                break;
        }
    }

    public virtual IEnumerator NotAllowReentryCoroutine()
    {
        // Default doors do not use one-way re-entry behavior.
        yield break;
    }

    public void UnlockDoor()
    {
        if (doorLockState == DoorLockState.Unlocked)
            return;

        doorLockState = DoorLockState.Unlocked;
        DoorHandlerCoroutines();
    }

    public void LockDoor()
    {
        if (doorLockState == DoorLockState.Locked)
            return;

        StopAllCoroutines();
        doorLockState = DoorLockState.Locked;
        ApplyDoorLightState(lockedLightBulbColor, lockedLightBulbEmissionColor, lockedLightColor);
    }

    private void StartingLightColor()
    {
        if (DoorLockState.Locked == doorLockState)
        {
            ApplyDoorLightState(lockedLightBulbColor, lockedLightBulbEmissionColor, lockedLightColor);
        }
        else
        {
            ApplyDoorLightState(unlockedLightBulbColor, unlockedLightBulbEmissionColor, unlockedLightColor);
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
        StartCoroutine(
            FadeLightBulbColor(
                lockedLightBulbColor,
                unlockedLightBulbColor,
                lockedLightBulbEmissionColor,
                unlockedLightBulbEmissionColor,
                lightFadeSpeed
            )
        );
        StartCoroutine(FadeColorIntoEachother(lockedLightColor, unlockedLightColor, lightFadeSpeed));
    }

    private IEnumerator FadeLightBulbColor(
        Color fromBaseColor,
        Color toBaseColor,
        Color fromEmissionColor,
        Color toEmissionColor,
        float duration
    )
    {
        MeshRenderer meshRenderer = GetLightMeshRenderer();
        if (meshRenderer == null)
            yield break;

        if (duration <= 0f)
        {
            ApplyBulbMaterialState(meshRenderer, toBaseColor, toEmissionColor);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            Color currentBaseColor = Color.Lerp(fromBaseColor, toBaseColor, t);
            Color currentEmissionColor = Color.Lerp(fromEmissionColor, toEmissionColor, t);
            ApplyBulbMaterialState(meshRenderer, currentBaseColor, currentEmissionColor);
            elapsed += Time.deltaTime;
            yield return null;
        }

        ApplyBulbMaterialState(meshRenderer, toBaseColor, toEmissionColor);
    }

    // Fade Color into eachother over time, used for light color transitions when opening/closing and locking/unlocking the door
    private IEnumerator FadeColorIntoEachother(Color fromColor, Color toColor, float duration)
    {
        if (doorLight == null)
            yield break;

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

    private void ApplyDoorLightState(Color bulbBaseColor, Color bulbEmissionColor, Color pointLightColor)
    {
        MeshRenderer meshRenderer = GetLightMeshRenderer();
        if (meshRenderer != null)
            ApplyBulbMaterialState(meshRenderer, bulbBaseColor, bulbEmissionColor);

        if (doorLight != null)
            doorLight.color = pointLightColor;
    }

    private void ApplyBulbMaterialState(MeshRenderer meshRenderer, Color baseColor, Color emissionColor)
    {
        if (meshRenderer == null)
            return;

        lightBulbPropertyBlock ??= new MaterialPropertyBlock();

        Material[] materials = meshRenderer.sharedMaterials;
        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];
            if (material == null)
                continue;

            meshRenderer.GetPropertyBlock(lightBulbPropertyBlock, i);

            if (material.HasProperty(BaseColorProperty))
                lightBulbPropertyBlock.SetColor(BaseColorProperty, baseColor);

            if (material.HasProperty(LegacyColorProperty))
                lightBulbPropertyBlock.SetColor(LegacyColorProperty, baseColor);

            if (material.HasProperty(EmissionColorProperty))
            {
                lightBulbPropertyBlock.SetColor(EmissionColorProperty, emissionColor);
            }

            meshRenderer.SetPropertyBlock(lightBulbPropertyBlock, i);
        }
    }

    private static Color ColorFromHex(string hex)
    {
        if (UnityEngine.ColorUtility.TryParseHtmlString($"#{hex}", out Color parsedColor))
            return parsedColor;

        return Color.white;
    }

    private static Color ColorFromHsv(float hueDegrees, float saturationPercent, float valuePercent, float intensity = 0f)
    {
        Color color = Color.HSVToRGB(hueDegrees / 360f, saturationPercent / 100f, valuePercent / 100f);
        if (!Mathf.Approximately(intensity, 0f))
            color *= Mathf.Pow(2f, intensity);

        return color;
    }

    public void OpenDoor()
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
        isOpened = true;
    }

    public void CloseDoor()
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

        isOpened = false;
    }

    private void OpenOut()
    {
        // Use hinge pivot to rotate outwards so the door stays locked in its socket
        EnsurePivot();
        hingeStartRot = hingePivot.rotation;
        hingeTargetRot = hingeOriginalRot * Quaternion.Euler(0f, -90f, 0f);
        StartHingeAnimation(hingeStartRot, hingeTargetRot, 1f / openSpeed);
    }

    private void OpenIn()
    {
        // Use hinge pivot to rotate inwards so the door stays locked in its socket
        EnsurePivot();
        hingeStartRot = hingePivot.rotation;
        hingeTargetRot = hingeOriginalRot * Quaternion.Euler(0f, 90f, 0f);
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
            go.transform.position = transform.position;
            go.transform.rotation = transform.rotation;
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
