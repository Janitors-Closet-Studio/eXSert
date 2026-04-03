/*
Written by Kyle Woo
Updated to include FlowWater Coroutine and Custom Inspector Buttons.
*/

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Wobble : MonoBehaviour
{
    Renderer rend;
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
    [Tooltip("Sets the initial liquid level")]
    public float currentFill = 0.5f; 
    private float initialFill; // Remembers the starting point

    [Header("Test Flow Parameters (For Button)")]
    public float testTargetFill = 1.0f;
    public float testDuration = 2.0f;
    public float testSurgeZ = 0.05f;
    public float testSurgeX = 0.0f;

    // Keep track of the coroutine so we can stop it if we hit Reset or Play again
    private Coroutine activeFlowCoroutine;

    void Start()
    {
        rend = GetComponent<Renderer>();
        initialFill = currentFill; // Save the starting state
        rend.material.SetFloat("_Fill", currentFill);
    }

    private void Update()
    {
        time += Time.deltaTime;
        
        wobbleAmountToAddX = Mathf.Lerp(wobbleAmountToAddX, 0, Time.deltaTime * (Recovery));
        wobbleAmountToAddZ = Mathf.Lerp(wobbleAmountToAddZ, 0, Time.deltaTime * (Recovery));

        pulse = 2 * Mathf.PI * WobbleSpeed;
        wobbleAmountX = wobbleAmountToAddX * Mathf.Sin(pulse * time);
        wobbleAmountZ = wobbleAmountToAddZ * Mathf.Sin(pulse * time);

        rend.material.SetFloat("_WobbleX", wobbleAmountX);
        rend.material.SetFloat("_WobbleZ", wobbleAmountZ);

        velocity = (lastPos - transform.position) / Time.deltaTime;
        angularVelocity = transform.rotation.eulerAngles - lastRot;

        wobbleAmountToAddX += Mathf.Clamp((velocity.x + (angularVelocity.z * 0.2f)) * MaxWobble, -MaxWobble, MaxWobble);
        wobbleAmountToAddZ += Mathf.Clamp((velocity.z + (angularVelocity.x * 0.2f)) * MaxWobble, -MaxWobble, MaxWobble);

        lastPos = transform.position;
        lastRot = transform.rotation.eulerAngles;
    }

    // --- FLOW FUNCTIONS ---

    public void TestFlowWater()
    {
        FlowWater(testTargetFill, testDuration, testSurgeZ, testSurgeX);
    }

    public void FlowWater(float targetFill, float duration, float surgeForceZ, float surgeForceX)
    {
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
            rend.material.SetFloat("_Fill", currentFill);

            float currentSurgeZ = Mathf.Lerp(surgeForceZ, 0, elapsedTime / duration);
            float currentSurgeX = Mathf.Lerp(surgeForceX, 0, elapsedTime / duration);
            
            wobbleAmountToAddZ += currentSurgeZ * Time.deltaTime;
            wobbleAmountToAddX += currentSurgeX * Time.deltaTime;

            yield return null; 
        }

        currentFill = targetFill;
        rend.material.SetFloat("_Fill", currentFill);
    }

    // --- RESET FUNCTION ---
    public void ResetWater()
    {
        if (activeFlowCoroutine != null) StopCoroutine(activeFlowCoroutine);
        
        currentFill = initialFill; // Snap back to start
        wobbleAmountToAddX = 0f;   // Kill the momentum
        wobbleAmountToAddZ = 0f;
        
        rend.material.SetFloat("_Fill", currentFill);
    }
}