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

public abstract class DoorHandler : MonoBehaviour
{
    private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
    private static readonly int LegacyColorProperty = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorProperty = Shader.PropertyToID("_EmissionColor");
	
	private MaterialPropertyBlock lightBulbPropertyBlock;

    // enums
    public enum DoorState { Open, Closed }
    public enum DoorLockState { Locked, Unlocked }
    public enum OneWayDoor { No, Yes}
    

    [Header("Door State & Type")]
    [Tooltip("Current state of the door (Open/Closed)")]
    public DoorState currentDoorState;
    [Tooltip("Current lock state of the door (Locked/Unlocked)")]
    public DoorLockState doorLockState = DoorLockState.Locked;
    public OneWayDoor oneWayDoor = OneWayDoor.No;


    [Header("Door Movement Settings")]
    [Tooltip("Original position of the door (auto-set)")]
    internal Vector3 doorPosOrigin;
    [Tooltip("Original rotation of the door (auto-set)")]

    private Quaternion doorRotOrigin;
    private Vector3 targetDoorPos;
    private Quaternion targetDoorRot;
    private Vector3 targetPos;


    [Tooltip("Speed at which the door opens/closes")]
    public float openSpeed = 2f;

    private bool isOpening = false;
    internal bool isOpened = false;


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
    
    private Vector3 bottomTargetPos;
    private Vector3 topTartetPos;

    // Store original parent to reparent door after using hinge pivot
    internal Transform originalParent;

    private static Color DefaultLockedBulbBaseColor => ColorFromHex("A10000");
    private static Color DefaultLockedBulbEmissionColor => ColorFromHsv(0f, 100f, 38f);
    private static Color DefaultLockedPointLightColor => ColorFromHex("FF1E1E");
    private static Color DefaultUnlockedBulbBaseColor => ColorFromHex("1DC814");
    private static Color DefaultUnlockedBulbEmissionColor => ColorFromHsv(145f, 100f, 13f);
    private static Color DefaultUnlockedPointLightColor => ColorFromHex("44A659");

    protected virtual void Awake()
    {
        doorPosOrigin = this.transform.localPosition;
        doorRotOrigin = this.transform.localRotation;
        
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
        
        OpenDoorBasedOnType();

        isOpened = true;
    }

    protected abstract void CloseDoorBasedOnType();
    protected abstract void OpenDoorBasedOnType();

    public void CloseDoor()
    {
        Debug.Log("Closing the door.");
        currentDoorState = DoorState.Closed;

        CloseDoorBasedOnType();

        isOpened = false;
    }
    
}
