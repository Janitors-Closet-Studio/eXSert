
using eXsert;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.InputSystem;

using Utilities.Combat;

public class ThirdPersonCameraController : MonoBehaviour
{
    [Header("Guard Mode - Over Shoulder Settings")]
    [SerializeField] private float guardRadius = 2.5f;     // Close over-shoulder distance
    [SerializeField] private float guardHeightOffset = 0.5f; // Added height offset for shoulder view
    [SerializeField] private float zoomLerpSpeed = 8f;     // Transition speed

    [Header("Combat Camera Settings")]
    [SerializeField] private bool useCombatCameraAdjustments = true;
    [SerializeField, Range(1f, 2.5f)] private float combatRadiusMultiplier = 1.2f;
    [SerializeField, Range(40f, 120f)] private float combatFieldOfView = 66f;
    [SerializeField, Range(1f, 20f)] private float combatFovLerpSpeed = 8f;
    
    private CinemachineCamera cmCamera;
    private CinemachineOrbitalFollow orbital;

    private CinemachineInputAxisController axisController;

    // Store original Three Ring settings to restore them
    private float originalRadius;
    private Vector3 originalTargetOffset;
    private float originalFieldOfView;
    
    // Current state
    private bool wasGuarding = false;
    private bool wasInCombat = false;
    private bool isTransitioning = false;

    private void Start()
    {        
        cmCamera = GetComponent<CinemachineCamera>();
        orbital = cmCamera?.GetComponent<CinemachineOrbitalFollow>();
        axisController = GetComponent<CinemachineInputAxisController>();
        if (orbital == null)
        {
            Debug.LogError("ThirdPersonCameraController: CinemachineOrbitalFollow not found on this Camera.");
            enabled = false;
            return;
        }

        // Store the original Three Ring settings from Inspector
        StoreOriginalSettings();
    }

    private void Update()
    {
        if (orbital == null) return;
        
        if (CombatManager.isGuarding != wasGuarding)
        {
            wasGuarding = CombatManager.isGuarding;
            
            if (CombatManager.isGuarding)
            {
                Debug.Log("Camera: Entering Guard Mode - Over Shoulder View");
                EnterGuardMode();
            }
            else
            {
                Debug.Log("Camera: Exiting Guard Mode - Restoring Three Ring");
                ExitGuardMode();
            }
        }

        // Only update camera position if we're transitioning or in guard mode
        if (isTransitioning || CombatManager.isGuarding)
        {
            UpdateCameraTransition();
        }

       
    }

    private void StoreOriginalSettings()
    {
        // Store the original Three Ring setup from your Inspector settings
        originalRadius = orbital.Radius;
        originalTargetOffset = orbital.TargetOffset;
        originalFieldOfView = cmCamera != null ? cmCamera.Lens.FieldOfView : 60f;
        
        Debug.Log($"Stored original settings - Radius: {originalRadius}, Offset: {originalTargetOffset}");
        
        // Debug: Check properties we can access
        Debug.Log($"Target Offset: {orbital.TargetOffset}");
        
        // Try to check if this has ring settings
        // In Cinemachine, Three Ring might use different properties
    }

    private void EnterGuardMode()
    {
        isTransitioning = true;
        // We'll smoothly transition to guard settings in UpdateCameraTransition()
    }

    private void ExitGuardMode()
    {
        isTransitioning = true;
        // We'll smoothly transition back to original Three Ring settings
    }

    private void UpdateCameraTransition()
    {
        float targetRadius = originalRadius;
        Vector3 targetOffset = originalTargetOffset;

        if (CombatManager.isGuarding)
        {
            targetRadius = guardRadius;
            targetOffset = new Vector3(originalTargetOffset.x, originalTargetOffset.y + guardHeightOffset, originalTargetOffset.z);
        }
        else if (useCombatCameraAdjustments && CombatManager.isInCombat)
        {
            targetRadius = originalRadius * Mathf.Max(1f, combatRadiusMultiplier);
        }

        float newRadius = Mathf.Lerp(orbital.Radius, targetRadius, Time.deltaTime * zoomLerpSpeed);
        Vector3 newOffset = Vector3.Lerp(orbital.TargetOffset, targetOffset, Time.deltaTime * zoomLerpSpeed);

        orbital.Radius = newRadius;
        orbital.TargetOffset = newOffset;

        if (cmCamera != null)
        {
            float targetFov = (useCombatCameraAdjustments && !CombatManager.isGuarding && CombatManager.isInCombat)
                ? combatFieldOfView
                : originalFieldOfView;

            var lens = cmCamera.Lens;
            lens.FieldOfView = Mathf.Lerp(lens.FieldOfView, targetFov, Time.deltaTime * combatFovLerpSpeed);
            cmCamera.Lens = lens;
        }

        bool reachedRadius = Mathf.Abs(orbital.Radius - targetRadius) < 0.01f;
        bool reachedOffset = Vector3.SqrMagnitude(orbital.TargetOffset - targetOffset) < 0.0001f;
        bool reachedFov = true;
        if (cmCamera != null)
        {
            float targetFov = (useCombatCameraAdjustments && !CombatManager.isGuarding && CombatManager.isInCombat)
                ? combatFieldOfView
                : originalFieldOfView;
            reachedFov = Mathf.Abs(cmCamera.Lens.FieldOfView - targetFov) < 0.05f;
        }

        isTransitioning = !(reachedRadius && reachedOffset && reachedFov);
    }

    // Public method to reset to original settings if needed
    public void ResetToOriginalSettings()
    {
        if (orbital != null)
        {
            orbital.Radius = originalRadius;
            orbital.TargetOffset = originalTargetOffset;
            if (cmCamera != null)
            {
                var lens = cmCamera.Lens;
                lens.FieldOfView = originalFieldOfView;
                cmCamera.Lens = lens;
            }
            isTransitioning = false;
            wasGuarding = false;
            wasInCombat = false;
            Debug.Log("Camera reset to original Three Ring settings");
        }
    }

    // Debug visualization
    private void OnDrawGizmos()
    {
        if (orbital != null)
        {
            // Blue = Normal Three Ring, Red = Guard Mode, Yellow = Transitioning
            if (isTransitioning)
                Gizmos.color = Color.yellow;
            else if (CombatManager.isGuarding)
                Gizmos.color = Color.red;
            else
                Gizmos.color = Color.blue;
                
            Gizmos.DrawWireSphere(transform.position, orbital.Radius);
        }
    }
}
