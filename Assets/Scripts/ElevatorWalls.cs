/*
    Written by Brandon Wahl
    This Script manages the continuous movement of elevator walls to create an infinite elevator effect.
    It moves the walls downward at a specified speed and resets their position when they go below a certain point.

*/

using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

/// <summary>
/// Manages the continuous movement of elevator walls.
/// Creates an infinite elevator effect by moving walls downward and resetting them at the top.
/// </summary>
public class ElevatorWalls : MonoBehaviour
{
    [Header("Wall References")]
    [SerializeField] internal GameObject elevatorWall;
    [SerializeField] internal GameObject wallBelow;
    [SerializeField] internal GameObject wallWithDoor;
    
    internal bool isMoving = true;

    [Header("Movement Settings")]
    [Tooltip("When a wall goes at/below this Y, it respawns above the other wall")]
    [SerializeField, FormerlySerializedAs("yBounds")] [Range(-50f, 0f)] private float disappearY = -22.4f;
    
    [Tooltip("Speed at which elevator walls move downward")]
    [SerializeField] [Range(0f, 50f)] internal float elevatorSpeed = 0f;

    // Deprecated inspector knobs kept for scene compatibility; spacing is auto-detected now.
    [SerializeField, HideInInspector, FormerlySerializedAs("restartPoint")] private float restartPoint_DEPRECATED = 28f;
    [SerializeField, HideInInspector, FormerlySerializedAs("wallSpacing")] private float wallSpacing_DEPRECATED = 24.8f;

    [SerializeField] private GameObject elevatorPlatform;
    internal float endYPos;

    private float _detectedWallSpacing = 24.8f;

    private float LoopHeight => 2f * _detectedWallSpacing;

    // Backwards-compatibility for existing scripts that referenced these members.
    // Keep them out of the inspector to preserve the simplified UX.
    [HideInInspector]
    [System.Obsolete("Use 'disappearY' (Disappear Y) instead.")]
    internal float yBounds
    {
        get => disappearY;
        set => disappearY = value;
    }

    [HideInInspector]
    [System.Obsolete("Restart point is auto-managed now; keep this only for legacy code.")]
    internal float restartPoint
    {
        // Provide a stable virtual loop top so legacy wrap code can compute loop height.
        // With two wall segments spaced by S, the full wrap period is 2S.
        get => disappearY + LoopHeight;
        set
        {
            // Legacy setter: interpret as changing the virtual loop height.
            restartPoint_DEPRECATED = value;
            float loopHeight = value - disappearY;
            if (loopHeight > 0.01f)
                _detectedWallSpacing = loopHeight * 0.5f;
        }
    }

    private void Awake()
    {
        if(elevatorPlatform != null)
        {
            endYPos = elevatorPlatform.transform.position.y - 0.5f; // assuming platform height is 1 unit
        }

        DetectWallSpacing();
    }

    private void Start()
    {
        wallWithDoor.gameObject.SetActive(false);

        StartCoroutine(MoveWall(elevatorWall));
        StartCoroutine(MoveWall(wallBelow));
        StartCoroutine(MoveWall(wallWithDoor));
    } 

    private void DetectWallSpacing()
    {
        // Primary case: two wall segments are assigned.
        if (elevatorWall != null && wallBelow != null)
        {
            float spacing = Mathf.Abs(wallBelow.transform.position.y - elevatorWall.transform.position.y);
            if (spacing > 0.01f)
            {
                _detectedWallSpacing = spacing;
                return;
            }
        }

        // Fallback to whatever was set historically, otherwise 25.
        _detectedWallSpacing = wallSpacing_DEPRECATED > 0.01f ? wallSpacing_DEPRECATED : 25f;
    }

    private float GetRespawnY(GameObject wall)
    {
        float highestOtherY = float.NegativeInfinity;

        if (elevatorWall != null && elevatorWall != wall && elevatorWall.activeInHierarchy)
            highestOtherY = Mathf.Max(highestOtherY, elevatorWall.transform.position.y);
        if (wallBelow != null && wallBelow != wall && wallBelow.activeInHierarchy)
            highestOtherY = Mathf.Max(highestOtherY, wallBelow.transform.position.y);
        if (wallWithDoor != null && wallWithDoor != wall && wallWithDoor.activeInHierarchy)
            highestOtherY = Mathf.Max(highestOtherY, wallWithDoor.transform.position.y);

        // If only one wall exists, just move it up by one segment.
        if (highestOtherY == float.NegativeInfinity)
            return wall != null ? wall.transform.position.y + _detectedWallSpacing : restartPoint_DEPRECATED;

        // Add a small buffer to ensure walls connect without gaps
        float buffer = 0.5f; // Adjust as needed for your scale
        return highestOtherY + _detectedWallSpacing - buffer;
    }

    /// <summary>
    /// Moves a single elevator wall downward and resets it when it goes below bounds.
    /// </summary>
    /// <param name="wall">The wall GameObject to move</param>
    private IEnumerator MoveWall(GameObject wall)
    {
        if(wall == null)
            yield break;

        while(isMoving)
        {
            Vector3 position = wall.transform.position;
            position.y -= elevatorSpeed * Time.deltaTime;
            wall.transform.position = position;

            // Reset wall to top when it goes below bounds - preserve original X and Z
            if(position.y <= disappearY)
            {
                // Respawn directly above the currently highest other wall.
                position.y = GetRespawnY(wall);
                wall.transform.position = position;
            }

            yield return null;
        }
    }
}