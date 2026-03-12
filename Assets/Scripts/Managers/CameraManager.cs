 /*
 * CameraManager.cs
 * 
 * Centralized camera state management for gameplay, guard, ultimates, cutscenes, etc.
 * Uses priority-based camera switching with smooth transitions.
 * 
 * Usage:
 * - Assign Cinemachine cameras in Inspector
 * - Call SwitchToCamera() with CameraState enum
 * - Automatically handles priority switching and orbit preservation
 */

using UnityEngine;
using Unity.Cinemachine;

public class CameraManager : MonoBehaviour
{
    public enum CameraState
    {
        Gameplay,       // Normal movement/combat camera
        Guard,          // Guard/block camera (closer, defensive angle)
        Ultimate,       // Ultimate skill camera (dynamic, cinematic)
        Cutscene,       // Cutscene camera (scripted sequences)
        Special         // Special animations/events
    }

    [Header("Cinemachine Camera References")]
    [SerializeField] private CinemachineCamera gameplayCamera;
    [SerializeField] private CinemachineCamera guardCamera;
    [SerializeField] private CinemachineCamera ultimateCamera;
    [SerializeField] private CinemachineCamera cutsceneCamera;
    [SerializeField] private CinemachineCamera specialCamera;

    [Header("Settings")]
    [SerializeField] private bool preserveOrbitOnSwitch = true;
    [SerializeField] private int activePriority = 20;
    [SerializeField] private int inactivePriority = 0;

    // Cached orbital follow components for orbit preservation
    private CinemachineOrbitalFollow gameplayOrbit;
    private CinemachineOrbitalFollow guardOrbit;
    private CinemachineOrbitalFollow ultimateOrbit;
    private CinemachineOrbitalFollow cutsceneOrbit;
    private CinemachineOrbitalFollow specialOrbit;

    // Singleton pattern for easy access
    public static CameraManager Instance { get; private set; }

    // Current active camera state
    public CameraState CurrentState { get; private set; } = CameraState.Gameplay;
    public static CinemachineCamera ActiveCamera => Instance.GetCameraForState(Instance.CurrentState);

    private void Awake()
    {
        // Singleton setup
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Cache orbital follow components
        CacheOrbitalComponents();

        // Set initial camera priorities
        SetCameraActive(gameplayCamera, true);
    }

    private void CacheOrbitalComponents()
    {
        if (gameplayCamera != null)
            gameplayOrbit = gameplayCamera.GetComponent<CinemachineOrbitalFollow>();

        if (guardCamera != null)
            guardOrbit = guardCamera.GetComponent<CinemachineOrbitalFollow>();

        if (ultimateCamera != null)
            ultimateOrbit = ultimateCamera.GetComponent<CinemachineOrbitalFollow>();

        if (cutsceneCamera != null)
            cutsceneOrbit = cutsceneCamera.GetComponent<CinemachineOrbitalFollow>();

        if (specialCamera != null)
            specialOrbit = specialCamera.GetComponent<CinemachineOrbitalFollow>();

        ValidateSetup();
    }

    private void ValidateSetup()
    {
        if (gameplayCamera == null)
            Debug.LogWarning("[CameraManager] Gameplay camera not assigned!");

        if (guardCamera == null)
            Debug.LogWarning("[CameraManager] Guard camera not assigned - guard mode won't have camera!");

        // Other cameras are optional
    }

    /// <summary>
    /// Switch to a specific camera state with optional orbit preservation
    /// </summary>
    public void SwitchToCamera(CameraState targetState)
    {
        if (CurrentState == targetState)
        {
            Debug.Log($"[CameraManager] Already in {targetState} camera state");
            return;
        }

        CameraState previousState = CurrentState;
        CurrentState = targetState;

        // Get source and target cameras
        CinemachineCamera sourceCamera = GetCameraForState(previousState);
        CinemachineCamera targetCamera = GetCameraForState(targetState);
        CinemachineOrbitalFollow sourceOrbit = GetOrbitForState(previousState);
        CinemachineOrbitalFollow targetOrbit = GetOrbitForState(targetState);

        if (targetCamera == null)
        {
            Debug.LogError($"[CameraManager] Target camera for {targetState} is null!");
            CurrentState = previousState; // Revert
            return;
        }

        // Preserve orbit if enabled and both have orbital follow
        if (preserveOrbitOnSwitch && sourceOrbit != null && targetOrbit != null)
        {
            CopyOrbitValues(sourceOrbit, targetOrbit);
        }

        // Switch priorities
        SetCameraActive(sourceCamera, false);
        SetCameraActive(targetCamera, true);

        Debug.Log($"[CameraManager] Switched from {previousState} to {targetState} camera");
    }

    /// <summary>
    /// Quick switch to gameplay camera (most common operation)
    /// </summary>
    public void SwitchToGameplay() => SwitchToCamera(CameraState.Gameplay);

    /// <summary>
    /// Quick switch to guard camera
    /// </summary>
    public void SwitchToGuard() => SwitchToCamera(CameraState.Guard);

    /// <summary>
    /// Quick switch to ultimate camera
    /// </summary>
    public void SwitchToUltimate() => SwitchToCamera(CameraState.Ultimate);

    /// <summary>
    /// Quick switch to cutscene camera
    /// </summary>
    public void SwitchToCutscene() => SwitchToCamera(CameraState.Cutscene);

    /// <summary>
    /// Quick switch to special camera
    /// </summary>
    public void SwitchToSpecial() => SwitchToCamera(CameraState.Special);

    private CinemachineCamera GetCameraForState(CameraState state)
    {
        return state switch
        {
            CameraState.Gameplay => gameplayCamera,
            CameraState.Guard => guardCamera,
            CameraState.Ultimate => ultimateCamera,
            CameraState.Cutscene => cutsceneCamera,
            CameraState.Special => specialCamera,
            _ => gameplayCamera
        };
    }

    private CinemachineOrbitalFollow GetOrbitForState(CameraState state)
    {
        return state switch
        {
            CameraState.Gameplay => gameplayOrbit,
            CameraState.Guard => guardOrbit,
            CameraState.Ultimate => ultimateOrbit,
            CameraState.Cutscene => cutsceneOrbit,
            CameraState.Special => specialOrbit,
            _ => gameplayOrbit
        };
    }

    private void SetCameraActive(CinemachineCamera camera, bool active)
    {
        if (camera == null) return;
        camera.Priority = active ? activePriority : inactivePriority;
    }

    private void CopyOrbitValues(CinemachineOrbitalFollow source, CinemachineOrbitalFollow target)
    {
        if (source == null || target == null) return;

        target.HorizontalAxis.Value = source.HorizontalAxis.Value;
        target.VerticalAxis.Value = source.VerticalAxis.Value;
        target.RadialAxis.Value = source.RadialAxis.Value;
    }

    /// <summary>
    /// Get the currently active camera
    /// </summary>
    public CinemachineCamera GetActiveCamera() => GetCameraForState(CurrentState);

    /// <summary>
    /// Check if a specific camera state is active
    /// </summary>
    public bool IsInState(CameraState state) => CurrentState == state;

    /// <summary>
    /// Toggle orbit preservation on/off at runtime
    /// </summary>
    public void SetOrbitPreservation(bool enabled)
    {
        preserveOrbitOnSwitch = enabled;
        Debug.Log($"[CameraManager] Orbit preservation: {(enabled ? "ON" : "OFF")}");
    }
}
