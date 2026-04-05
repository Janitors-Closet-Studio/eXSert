/*
Written by Kyle Woo
Updated to include FlowWater Coroutine and Custom Inspector Buttons.
*/

using System.Collections;
using UnityEngine;

public class Wobble : MonoBehaviour
{
    private static readonly int FillProperty = Shader.PropertyToID("_Fill");
    private static readonly int TopColorProperty = Shader.PropertyToID("_TopColor");
    private static readonly int SideColorProperty = Shader.PropertyToID("_SideColor");

    Renderer rend;
    Material runtimeMaterial;
    Vector3 lastPos;
    Vector3 velocity;
    Vector3 lastRot;  
    Vector3 angularVelocity;
    public float MaxWobble = 0.03f;
    public float WobbleSpeed = 1f;
    public float Recovery = 1f;
    float wobbleAmountX;
    float wobbleAmountZ;
    float wobbleAmountToAddX;
    float wobbleAmountToAddZ;
    float pulse;
    float time = 0.5f;

    [Header("Liquid Fill Settings")]
    [Tooltip("Runtime fill value used by FlowWater and ResetWater after initialization.")]
    public float currentFill = 0.5f; 
    [Tooltip("If enabled, the inspector's Current Fill value is applied to the material when play mode starts. Otherwise the material's authored _Fill value is preserved.")]
    public bool applyCurrentFillOnStart = false;
    private float initialFill; // Remembers the starting point

    [Header("Flow Parameters")]
    [Tooltip("Target _Fill value to animate toward when FlowWater() is triggered.")]
    public float flowTargetFill = 1.0f;
    [Tooltip("How long the fill animation takes in seconds.")]
    public float flowDuration = 2.0f;
    [Tooltip("Extra forward/back wobble impulse applied at flow start. Higher values feel more forceful.")]
    public float flowSurgeZ = 0.05f;
    [Tooltip("Extra sideways wobble impulse applied at flow start.")]
    public float flowSurgeX = 0.0f;

    [Header("Flow Color Transition")]
    [Tooltip("If enabled, this pipe can gradually shift toward the target material colors after a chosen number of flow activations.")]
    public bool enableFlowColorTransition = false;
    [Tooltip("Reference material whose color values will be used as the destination of the lerp. This does not swap shaders or materials.")]
    public Material targetColorMaterial;
    [Tooltip("The flow activation count that should trigger the color transition. Set to 2 for a shared pipe that should change on its second pass.")]
    public int colorTransitionActivationCount = 2;
    [Tooltip("How long the color transition takes in seconds.")]
    public float colorTransitionDuration = 1.5f;
    [Tooltip("If enabled, the configured color transition only happens once.")]
    public bool transitionColorOnlyOnce = true;

    // Keep track of the coroutine so we can stop it if we hit Reset or Play again
    private Coroutine activeFlowCoroutine;
    private Coroutine activeColorTransitionCoroutine;
    private int flowActivationCount;
    private bool hasTriggeredConfiguredColorTransition;

    private bool hasTopColor;
    private bool hasSideColor;
    private Color initialTopColor;
    private Color initialSideColor;

    void Start()
    {
        rend = GetComponent<Renderer>();
        runtimeMaterial = rend.material;
        CacheInitialColors();
        InitializeFillState();
    }

    private void Update()
    {
        time += Time.deltaTime;
        
        wobbleAmountToAddX = Mathf.Lerp(wobbleAmountToAddX, 0, Time.deltaTime * (Recovery));
        wobbleAmountToAddZ = Mathf.Lerp(wobbleAmountToAddZ, 0, Time.deltaTime * (Recovery));

        pulse = 2 * Mathf.PI * WobbleSpeed;
        wobbleAmountX = wobbleAmountToAddX * Mathf.Sin(pulse * time);
        wobbleAmountZ = wobbleAmountToAddZ * Mathf.Sin(pulse * time);

        runtimeMaterial.SetFloat("_WobbleX", wobbleAmountX);
        runtimeMaterial.SetFloat("_WobbleZ", wobbleAmountZ);

        velocity = (lastPos - transform.position) / Time.deltaTime;
        angularVelocity = transform.rotation.eulerAngles - lastRot;

        wobbleAmountToAddX += Mathf.Clamp((velocity.x + (angularVelocity.z * 0.2f)) * MaxWobble, -MaxWobble, MaxWobble);
        wobbleAmountToAddZ += Mathf.Clamp((velocity.z + (angularVelocity.x * 0.2f)) * MaxWobble, -MaxWobble, MaxWobble);

        lastPos = transform.position;
        lastRot = transform.rotation.eulerAngles;
    }

    // --- FLOW FUNCTIONS ---

    public void FlowWater()
    {
        FlowWater(flowTargetFill, flowDuration, flowSurgeZ, flowSurgeX);
    }

    public void FlowWater(float targetFill, float duration, float surgeForceZ, float surgeForceX)
    {
        flowActivationCount++;
        TryTriggerConfiguredColorTransition();

        // Stop any current flow so they don't fight each other
        if (activeFlowCoroutine != null) StopCoroutine(activeFlowCoroutine);
        activeFlowCoroutine = StartCoroutine(AnimateFlow(targetFill, duration, surgeForceZ, surgeForceX));
    }

    private IEnumerator AnimateFlow(float targetFill, float duration, float surgeForceZ, float surgeForceX)
    {
        float elapsedTime = 0f;
        float startFill = currentFill;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            
            currentFill = Mathf.Lerp(startFill, targetFill, elapsedTime / duration);
            runtimeMaterial.SetFloat(FillProperty, currentFill);

            float currentSurgeZ = Mathf.Lerp(surgeForceZ, 0, elapsedTime / duration);
            float currentSurgeX = Mathf.Lerp(surgeForceX, 0, elapsedTime / duration);
            
            wobbleAmountToAddZ += currentSurgeZ * Time.deltaTime;
            wobbleAmountToAddX += currentSurgeX * Time.deltaTime;

            yield return null; 
        }

        currentFill = targetFill;
        runtimeMaterial.SetFloat(FillProperty, currentFill);
        activeFlowCoroutine = null;
    }

    private void InitializeFillState()
    {
        if (runtimeMaterial == null || !runtimeMaterial.HasProperty(FillProperty))
        {
            initialFill = currentFill;
            return;
        }

        if (applyCurrentFillOnStart)
        {
            initialFill = currentFill;
            runtimeMaterial.SetFloat(FillProperty, currentFill);
            return;
        }

        initialFill = runtimeMaterial.GetFloat(FillProperty);
        currentFill = initialFill;
    }

    private void TryTriggerConfiguredColorTransition()
    {
        if (!enableFlowColorTransition || targetColorMaterial == null)
            return;

        if (colorTransitionActivationCount <= 0)
            return;

        if (transitionColorOnlyOnce && hasTriggeredConfiguredColorTransition)
            return;

        if (flowActivationCount < colorTransitionActivationCount)
            return;

        if (activeColorTransitionCoroutine != null)
            StopCoroutine(activeColorTransitionCoroutine);

        activeColorTransitionCoroutine = StartCoroutine(AnimateConfiguredColorTransition());
        hasTriggeredConfiguredColorTransition = true;
    }

    private IEnumerator AnimateConfiguredColorTransition()
    {
        float duration = Mathf.Max(0.01f, colorTransitionDuration);
        float elapsedTime = 0f;

        Color startTopColor = hasTopColor ? runtimeMaterial.GetColor(TopColorProperty) : default;
        Color startSideColor = hasSideColor ? runtimeMaterial.GetColor(SideColorProperty) : default;

        Color targetTopColor = targetColorMaterial.HasProperty(TopColorProperty)
            ? targetColorMaterial.GetColor(TopColorProperty)
            : startTopColor;
        Color targetSideColor = targetColorMaterial.HasProperty(SideColorProperty)
            ? targetColorMaterial.GetColor(SideColorProperty)
            : startSideColor;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / duration);

            if (hasTopColor)
                runtimeMaterial.SetColor(TopColorProperty, Color.Lerp(startTopColor, targetTopColor, t));

            if (hasSideColor)
                runtimeMaterial.SetColor(SideColorProperty, Color.Lerp(startSideColor, targetSideColor, t));

            yield return null;
        }

        if (hasTopColor)
            runtimeMaterial.SetColor(TopColorProperty, targetTopColor);

        if (hasSideColor)
            runtimeMaterial.SetColor(SideColorProperty, targetSideColor);

        activeColorTransitionCoroutine = null;
    }

    private void CacheInitialColors()
    {
        if (runtimeMaterial == null)
            return;

        hasTopColor = runtimeMaterial.HasProperty(TopColorProperty);
        hasSideColor = runtimeMaterial.HasProperty(SideColorProperty);

        if (hasTopColor)
            initialTopColor = runtimeMaterial.GetColor(TopColorProperty);

        if (hasSideColor)
            initialSideColor = runtimeMaterial.GetColor(SideColorProperty);
    }

    // --- RESET FUNCTION ---
    public void ResetWater()
    {
        if (activeFlowCoroutine != null) StopCoroutine(activeFlowCoroutine);
        activeFlowCoroutine = null;

        if (activeColorTransitionCoroutine != null) StopCoroutine(activeColorTransitionCoroutine);
        activeColorTransitionCoroutine = null;
        flowActivationCount = 0;
        hasTriggeredConfiguredColorTransition = false;
        
        currentFill = initialFill; // Snap back to start
        wobbleAmountToAddX = 0f;   // Kill the momentum
        wobbleAmountToAddZ = 0f;
        
        runtimeMaterial.SetFloat(FillProperty, currentFill);

        if (hasTopColor)
            runtimeMaterial.SetColor(TopColorProperty, initialTopColor);

        if (hasSideColor)
            runtimeMaterial.SetColor(SideColorProperty, initialSideColor);
    }
}