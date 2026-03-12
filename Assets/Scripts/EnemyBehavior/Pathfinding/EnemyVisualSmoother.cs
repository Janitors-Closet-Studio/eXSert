// EnemyVisualSmoother.cs
// Purpose: Smooths enemy visual position/rotation to reduce jittery appearance when camera locks onto them.
// Works with: Any enemy using NavMeshAgent for movement.
// Usage: Add this component to an enemy and assign the visual model transform.
//        The component will smooth the visual's local position relative to the agent.

using UnityEngine;
using UnityEngine.AI;

public class EnemyVisualSmoother : MonoBehaviour
{
    [Header("Smoothing Settings")]
    [SerializeField, Tooltip("Time in seconds for position smoothing. Lower = more responsive, higher = smoother.")]
    private float positionSmoothTime = 0.08f;

    [SerializeField, Tooltip("Time in seconds for rotation smoothing.")]
    private float rotationSmoothTime = 0.06f;

    [SerializeField, Tooltip("The visual mesh/model root to smooth. If not set, tries to find 'Model', 'Visual', or 'Mesh' child.")]
    private Transform visualRoot;

    [SerializeField, Tooltip("Maximum distance the visual can lag behind before snapping.")]
    private float maxLagDistance = 1.0f;

    private NavMeshAgent agent;
    private Vector3 smoothedPosition;
    private Vector3 positionVelocity;
    private float rotationVelocity;
    private Vector3 initialLocalPosition;
    private bool isInitialized;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

        if (visualRoot == null)
        {
            // Try to find a common visual root
            visualRoot = transform.Find("Model") ?? transform.Find("Visual") ?? transform.Find("Mesh");
        }
    }

    private void Start()
    {
        if (visualRoot != null)
        {
            initialLocalPosition = visualRoot.localPosition;
            smoothedPosition = transform.position + initialLocalPosition;
            isInitialized = true;
        }
    }

    private void LateUpdate()
    {
        if (!isInitialized || visualRoot == null)
            return;

        Vector3 targetWorldPosition = transform.position + transform.rotation * initialLocalPosition;

        // Snap if too far behind (teleport, respawn, etc.)
        float distance = Vector3.Distance(smoothedPosition, targetWorldPosition);
        if (distance > maxLagDistance)
        {
            smoothedPosition = targetWorldPosition;
            positionVelocity = Vector3.zero;
        }
        else
        {
            // Smooth position
            smoothedPosition = Vector3.SmoothDamp(
                smoothedPosition,
                targetWorldPosition,
                ref positionVelocity,
                positionSmoothTime
            );
        }

        // Apply smoothed world position back to local space
        visualRoot.position = smoothedPosition;

        // Smooth rotation
        float currentY = visualRoot.eulerAngles.y;
        float targetY = transform.eulerAngles.y;
        float smoothedY = Mathf.SmoothDampAngle(currentY, targetY, ref rotationVelocity, rotationSmoothTime);
        visualRoot.rotation = Quaternion.Euler(visualRoot.eulerAngles.x, smoothedY, visualRoot.eulerAngles.z);
    }

    /// <summary>
    /// Call this to instantly snap the visual to the current position (e.g., on spawn/teleport).
    /// </summary>
    public void SnapToCurrentPosition()
    {
        if (visualRoot == null)
            return;

        smoothedPosition = transform.position + transform.rotation * initialLocalPosition;
        positionVelocity = Vector3.zero;
        rotationVelocity = 0f;
        visualRoot.position = smoothedPosition;
        visualRoot.rotation = transform.rotation;
    }

    /// <summary>
    /// Adjust smoothing parameters at runtime.
    /// </summary>
    public void SetSmoothingTimes(float positionTime, float rotationTime)
    {
        positionSmoothTime = Mathf.Max(0.01f, positionTime);
        rotationSmoothTime = Mathf.Max(0.01f, rotationTime);
    }
}
