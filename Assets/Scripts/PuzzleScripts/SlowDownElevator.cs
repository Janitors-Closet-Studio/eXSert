/* 
    Written by Brandon Wahl
    This Script handles the slowing down of an elevator after the player uses the keycard on the elevator console.
    It smoothly decelerates the elevator walls, swaps in the wall with the door at the appropriate time,
    and then triggers the rail drop and platform extension animations in sequence.

    Use CoPilot to help write the function for wrapping the elevator walls and the use of out cubic easing.

*/

using System.Collections;
using System.Collections.Generic;
using Progression.Encounters;
using UnityEngine;
using UnityEngine.SceneManagement;
public class SlowDownElevator : MonoBehaviour
{
#pragma warning disable CS0618
    private struct SparkParticleBinding
    {
        public GameObject RootObject;
        public ParticleSystem ParticleSystem;
        public float InitialRateOverTimeMultiplier;
    }

    #region Inspector Setup
    [Header("Required References")]
    [SerializeField, CriticalReference] private ElevatorWalls _elevatorWalls;
    [SerializeField, CriticalReference] private BasicEncounter basicEncounter;
    [SerializeField] private GameObject platform;

    [Header("Deceleration")]
    [SerializeField] [Range(0.1f, 10f)] private float decelerationDuration = 2f;

    [Header("Debug")]
    [Tooltip("Speed used for the debug button when the elevator isn't already running (mirrors the speed you'd normally set on ElevatorWalls).")]
    [SerializeField] [Range(0f, 50f)] private float debugFallbackSpeed = 10f;

    [Header("Rail Drop")]
    [SerializeField] private GameObject railToGoDown;
    [SerializeField] [Range(0.1f, 10f)] private float railDropDuration = 3.5f;

    [Header("Platform Extension")]
    [SerializeField] private GameObject platformToExtend;
    [SerializeField] [Range(0.1f, 10f)] private float platformExtendDuration = 3.5f;

    [Header("Animation Timing")]
    [SerializeField] [Range(0f, 5f)] private float delayBeforeDrop = 0.5f;
    [SerializeField] [Range(0f, 5f)] private float delayBetweenAnimations = 0.5f;

    [Header("Final Snap")]
    [SerializeField] [Range(0.05f, 2f)] private float snapDuration = 0.4f;
    [Tooltip("When enabled, the wall travels slightly past the final position before smoothly snapping back, adding a bounce feel.")]
    [SerializeField] private bool overshootEnabled = false;
    [Tooltip("How far past the final position the wall travels before snapping back. Only used when Overshoot Enabled is true.\n1.0 = no extra travel, 1.2 = 20% further than the stop point.")]
    [SerializeField] [Range(1f, 1.2f)] private float overshootMultiplier = 1.1f;

    [Header("Wall Offset")]
    [SerializeField] private float wallHeight = 2.0f; // Set this to your actual wall prefab height
    [SerializeField] private float finalDoorWallLocalY = -0.03f;

    [Header("Camera Shake")]
    [SerializeField] private float shakeAmplitude = 1.5f;
    [SerializeField] private float shakeFrequency = 2.0f;
    [SerializeField] private float shakeDuration = 0.1f;
    [SerializeField] private float timeToResetShake = 0.1f;

    [Header("SFX")]
    [SerializeField] private AudioClip elevatorDecelerateSFX;
    [SerializeField] private AudioClip elevatorBell;

    [Header("Slowdown VFX")]
    [SerializeField] private List<GameObject> slowdownSparkVfx = new();

    #endregion

    // Internal state
    internal bool _isDecelerating = false;
    private float _initialSpeed = 0f;
    private float _decelerationTimer = 0f;
    private float _actualDecelerationDuration = 0f;
    private float _totalDecelerationDistance = 0f;
    private float _initialDoorWallY = 0f;
    private float _initialElevatorWallY = 0f;
    private float _initialBelowWallY = 0f;
    private float _doorWallYAtSwap = 0f;
    private float _distanceToSwap = 0f;
    private bool _soundFadeStarted = false;
    private bool _swapped = false;
    private Coroutine _decelerationCoroutine;
    private readonly List<SparkParticleBinding> _sparkParticleBindings = new();

    [SerializeField] private float _pointToSwitchWallsY;

    public void Debug_RunFullSequence()
    {
        if (!Application.isPlaying)
            return;

        StopAllCoroutines();
        _decelerationCoroutine = null;
        _isDecelerating = false;

        // If the elevator walls aren't moving (tutorial never ran), kick them off at the debug speed.
        if (_elevatorWalls.elevatorSpeed <= 0.01f)
        {
            _elevatorWalls.elevatorSpeed = debugFallbackSpeed;
            _elevatorWalls.isMoving = true;
        }

        SetUpStateToSlowWalls();
    }

    private void Awake()
    {
        // Ensure the puzzle starts idle until explicitly triggered
        _isDecelerating = false;
        _soundFadeStarted = false;
        _swapped = false;
        _decelerationTimer = 0f;
        _initialSpeed = 0f;
        CacheSlowdownSparkVfxBindings();
        SetSlowdownSparkVfxActive(false);
    }

    private void OnEnable() => basicEncounter.OnEncounterCompleted += SetUpStateToSlowWalls;
    private void OnDisable()
    {
        basicEncounter.OnEncounterCompleted -= SetUpStateToSlowWalls;
        SetSlowdownSparkVfxActive(false);
    }

    /// <summary>
    /// Initiates the elevator deceleration process.
    /// Automatically called when all enemies are defeated.
    /// </summary>
    public void SetUpStateToSlowWalls()
    {
        _soundFadeStarted = false;
        _decelerationTimer = 0f;
        _isDecelerating = true;
        _swapped = false;
        _initialSpeed = _elevatorWalls.elevatorSpeed;
        _elevatorWalls.isMoving = false;
        SetSlowdownSparkVfxActive(true);
        EnsureAmbienceDoesntRestart(findElevatorMusicBoxInScene());
        
        // Stop ElevatorWalls script from moving the walls immediately
        _elevatorWalls.elevatorSpeed = 0f;
        
        EnsureProperWallStates();

        // Always target finalDoorWallLocalY in world space as the true stop point.
        // When overshoot is enabled the multiplier carries the wall past that point;
        // the smooth snap then eases it back. endYPos is no longer used as the distance target.
        float targetWorldY;
        if (_elevatorWalls.wallWithDoor != null)
        {
            Transform parent = _elevatorWalls.wallWithDoor.transform.parent;
            targetWorldY = parent != null
                ? parent.TransformPoint(new Vector3(0f, finalDoorWallLocalY, 0f)).y
                : finalDoorWallLocalY;
        }
        else
        {
            targetWorldY = _elevatorWalls.endYPos;
        }

        // Compute distances along the wrapped path: start -> swap -> target (wrapping past yBounds to restartPoint)
        _distanceToSwap = Mathf.Abs(_initialElevatorWallY - _pointToSwitchWallsY);

        float distanceSwapToEnd;
        if (targetWorldY >= _pointToSwitchWallsY)
        {
            // Target is above swap: go down to yBounds, wrap to restartPoint, then down to target
            float toBottom = Mathf.Abs(_pointToSwitchWallsY - _elevatorWalls.yBounds);
            float fromTopToEnd = Mathf.Abs(_elevatorWalls.restartPoint - targetWorldY);
            distanceSwapToEnd = toBottom + fromTopToEnd;
        }
        else
        {
            // Target is below swap: straight distance
            distanceSwapToEnd = Mathf.Abs(_pointToSwitchWallsY - targetWorldY);
        }

        _totalDecelerationDistance = (_distanceToSwap + distanceSwapToEnd) * (overshootEnabled ? overshootMultiplier : 1f);
        _actualDecelerationDuration = (_initialSpeed > 0.01f) ? (2f * _totalDecelerationDistance / _initialSpeed) : decelerationDuration;
        
        if (_decelerationCoroutine != null)
        {
            StopCoroutine(_decelerationCoroutine);
        }
        _decelerationCoroutine = StartCoroutine(SlowDownWalls());
    }

    private void EnsureProperWallStates()
    {
        // Ensure proper initial wall states
        if(_elevatorWalls.elevatorWall != null)
        {
            _elevatorWalls.elevatorWall.SetActive(true);
            _initialElevatorWallY = _elevatorWalls.elevatorWall.transform.position.y;
        }
        if(_elevatorWalls.wallWithDoor != null)
        {
            _elevatorWalls.wallWithDoor.SetActive(false); // Only show at swap
            _initialDoorWallY = _elevatorWalls.wallWithDoor.transform.position.y;
        }
        if(_elevatorWalls.wallBelow != null)
            _initialBelowWallY = _elevatorWalls.wallBelow.transform.position.y;
    }

    /// <summary>
    /// Updates the elevator deceleration over time.
    /// Smoothly reduces elevator speed and triggers follow-up animations when complete.
    /// </summary>
    private IEnumerator SlowDownWalls()
    {
        SoundManager.Instance.puzzleSource.Stop();
        SoundManager.Instance.puzzleSource.clip = elevatorDecelerateSFX;
        SoundManager.Instance.puzzleSource.Play();
        SoundManager.Instance.ambienceSource.Stop();

        while (_decelerationTimer < _actualDecelerationDuration)
        {
            CameraManager.Instance?.ShakeCamera(shakeAmplitude, shakeFrequency, shakeDuration, timeToResetShake); 
            _decelerationTimer += Time.deltaTime;
            float decelerationProgress = Mathf.Clamp01(_decelerationTimer / _actualDecelerationDuration);
            UpdateSlowdownSparkVfxEmission(1f - decelerationProgress);
            
            // Apply ease-out quadratic curve for smooth deceleration
            float easedProgress = 1f - (1f - decelerationProgress) * (1f - decelerationProgress);
            
            if(_elevatorWalls != null)
            {
                // Stop ElevatorWalls script from moving the walls 
                _elevatorWalls.elevatorSpeed = 0f;
                
                // Calculate distance traveled based on eased deceleration progress
                float distanceTraveled = _totalDecelerationDistance * easedProgress;
                float loopHeight = _elevatorWalls.restartPoint - _elevatorWalls.yBounds;
                float rawY = _initialElevatorWallY - distanceTraveled; // moving downward along virtual track
                float currentY = WrapY(rawY, loopHeight);

                // Before swap: move elevatorWall manually
                if(!_swapped && _elevatorWalls.elevatorWall != null)
                {
                    Vector3 elevatorPos = _elevatorWalls.elevatorWall.transform.position;
                    elevatorPos.y = currentY;  // wrapped movement
                    _elevatorWalls.elevatorWall.transform.position = elevatorPos;
                    
                }

                // Trigger swap the first time we pass the raw swap height (off-screen), before wrapping back
                if(!_swapped && rawY <= _pointToSwitchWallsY)
                {
                    _swapped = true;
                    if(_elevatorWalls.wallWithDoor != null && _elevatorWalls.elevatorWall != null)
                    {
                        _elevatorWalls.wallWithDoor.transform.position = new Vector3(
                            _elevatorWalls.elevatorWall.transform.position.x,
                            currentY,
                            _elevatorWalls.elevatorWall.transform.position.z);
                        _elevatorWalls.wallWithDoor.SetActive(true);
                        _doorWallYAtSwap = currentY;
                    }
                    if(_elevatorWalls.elevatorWall != null)
                        _elevatorWalls.elevatorWall.SetActive(false);
                }

                // Move wallWithDoor only after swap
                if(_elevatorWalls.wallWithDoor != null)
                {
                    Vector3 doorPos = _elevatorWalls.wallWithDoor.transform.position;
                    doorPos.y = currentY; // keep in sync after swap
                    _elevatorWalls.wallWithDoor.transform.position = doorPos;
                }
                
                // Move wallBelow to maintain offset from the active wall
                if(_elevatorWalls.wallBelow != null)
                {
                    Vector3 belowPos = _elevatorWalls.wallBelow.transform.position;
                    float loopHeightbelow = _elevatorWalls.restartPoint - _elevatorWalls.yBounds;
                    float referenceY = !_swapped && _elevatorWalls.elevatorWall != null ? _elevatorWalls.elevatorWall.transform.position.y : (_elevatorWalls.wallWithDoor != null ? _elevatorWalls.wallWithDoor.transform.position.y : currentY);
                    belowPos.y = WrapY(referenceY + wallHeight, loopHeightbelow); // Move down, wrap, and reappear above
                    _elevatorWalls.wallBelow.transform.position = belowPos;
                }
            }
            
            yield return null;
        }

        SoundManager.Instance.puzzleSource.Stop();
        SoundManager.Instance.puzzleSource.PlayOneShot(elevatorBell);
        if (overshootEnabled)
            yield return StartCoroutine(SmoothSnapDoorWallToFinalLocalY());
        else
            SnapDoorWallToFinalLocalY();
        SetSlowdownSparkVfxActive(false);

        
        // Complete when total distance traveled is done
        _isDecelerating = false;
        _decelerationCoroutine = null;
        yield return StartCoroutine(DropRailAndExtendPlatform());
    }

    /// <summary>
    /// Coroutine that sequences the rail drop and platform extension animations.
    /// </summary>
    private IEnumerator DropRailAndExtendPlatform()
    {
        yield return new WaitForSeconds(delayBeforeDrop);
        yield return StartCoroutine(AnimateObject(railToGoDown, Vector3.down * 5, railDropDuration, "Rail dropped!"));
        yield return new WaitForSeconds(delayBetweenAnimations);
        yield return StartCoroutine(AnimateObject(platformToExtend, Vector3.forward * 3, platformExtendDuration, "Platform extended!"));
    }

    /// <summary>
    /// Smoothly animates an object to a new position using easing.
    /// </summary>
    private IEnumerator AnimateObject(GameObject targetObject, Vector3 movement, float duration, string completionMessage)
    {
        if(targetObject == null)
        {
            yield break;
        }

        Vector3 startPosition = targetObject.transform.position;
        Vector3 endPosition = startPosition + movement;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float easedProgress = EaseOutCubic(progress);
            targetObject.transform.position = Vector3.Lerp(startPosition, endPosition, easedProgress);
            yield return null;
        }

        targetObject.transform.position = endPosition;
    }

    private IEnumerator SmoothSnapDoorWallToFinalLocalY()
    {
        if (_elevatorWalls == null || _elevatorWalls.wallWithDoor == null)
            yield break;

        Transform doorTransform = _elevatorWalls.wallWithDoor.transform;
        Vector3 startLocal = doorTransform.localPosition;
        Vector3 endLocal = startLocal;
        endLocal.y = finalDoorWallLocalY;

        float elapsed = 0f;
        while (elapsed < snapDuration)
        {
            elapsed += Time.deltaTime;
            float t = EaseOutCubic(Mathf.Clamp01(elapsed / snapDuration));
            Vector3 pos = doorTransform.localPosition;
            pos.y = Mathf.Lerp(startLocal.y, endLocal.y, t);
            doorTransform.localPosition = pos;
            yield return null;
        }

        Vector3 finalPos = doorTransform.localPosition;
        finalPos.y = finalDoorWallLocalY;
        doorTransform.localPosition = finalPos;
    }

    private void SnapDoorWallToFinalLocalY()
    {
        if (_elevatorWalls == null || _elevatorWalls.wallWithDoor == null)
        {
            return;
        }

        Transform doorTransform = _elevatorWalls.wallWithDoor.transform;
        Vector3 localPosition = doorTransform.localPosition;
        localPosition.y = finalDoorWallLocalY;
        doorTransform.localPosition = localPosition;
    }

    /// <summary>
    /// Easing function for smooth deceleration effect.
    /// Provides cubic easing out curve (fast start, slow finish).
    /// </summary>
    private static float EaseOutCubic(float t)
    {
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    // Wrap a Y position into the looping range to preserve spacing when passing bounds
    private float WrapY(float rawY, float loopHeight)
    {
        if(loopHeight <= 0.0001f)
            return rawY;

        float normalized = rawY - _elevatorWalls.yBounds;
        float beforeRepeat = normalized;
        normalized = Mathf.Repeat(normalized, loopHeight);
        float result = _elevatorWalls.yBounds + normalized;
            
        return result;
    }

    private void SetSlowdownSparkVfxActive(bool isActive)
    {
        CacheSlowdownSparkVfxBindings();

        if (slowdownSparkVfx == null)
            return;

        for (int i = 0; i < slowdownSparkVfx.Count; i++)
        {
            GameObject vfxObject = slowdownSparkVfx[i];
            if (vfxObject == null)
                continue;

            ApplySparkRates(isActive ? 1f : 0f, vfxObject);
            vfxObject.SetActive(isActive);

            if (isActive)
                PlaySparkSystems(vfxObject);
            else
                StopSparkSystems(vfxObject);
        }
    }

    private void UpdateSlowdownSparkVfxEmission(float normalizedEmission)
    {
        if (_sparkParticleBindings.Count == 0)
            return;

        float clampedEmission = Mathf.Clamp01(normalizedEmission);

        for (int i = 0; i < slowdownSparkVfx.Count; i++)
        {
            GameObject vfxObject = slowdownSparkVfx[i];
            if (vfxObject == null || !vfxObject.activeInHierarchy)
                continue;

            ApplySparkRates(clampedEmission, vfxObject);
        }
    }

    private void CacheSlowdownSparkVfxBindings()
    {
        _sparkParticleBindings.Clear();

        if (slowdownSparkVfx == null)
            return;

        for (int i = 0; i < slowdownSparkVfx.Count; i++)
        {
            GameObject vfxObject = slowdownSparkVfx[i];
            if (vfxObject == null)
                continue;

            ParticleSystem[] particleSystems = vfxObject.GetComponentsInChildren<ParticleSystem>(true);
            for (int particleIndex = 0; particleIndex < particleSystems.Length; particleIndex++)
            {
                ParticleSystem particleSystem = particleSystems[particleIndex];
                if (particleSystem == null)
                    continue;

                ParticleSystem.EmissionModule emission = particleSystem.emission;
                _sparkParticleBindings.Add(new SparkParticleBinding
                {
                    RootObject = vfxObject,
                    ParticleSystem = particleSystem,
                    InitialRateOverTimeMultiplier = emission.rateOverTimeMultiplier
                });
            }
        }
    }

    private void ApplySparkRates(float normalizedEmission, GameObject rootObject)
    {
        for (int i = 0; i < _sparkParticleBindings.Count; i++)
        {
            SparkParticleBinding binding = _sparkParticleBindings[i];
            if (binding.RootObject != rootObject || binding.ParticleSystem == null)
                continue;

            ParticleSystem.EmissionModule emission = binding.ParticleSystem.emission;
            emission.rateOverTimeMultiplier = binding.InitialRateOverTimeMultiplier * normalizedEmission;
        }
    }

    private void PlaySparkSystems(GameObject rootObject)
    {
        for (int i = 0; i < _sparkParticleBindings.Count; i++)
        {
            SparkParticleBinding binding = _sparkParticleBindings[i];
            if (binding.RootObject != rootObject || binding.ParticleSystem == null)
                continue;

            binding.ParticleSystem.Play(true);
        }
    }

    private void StopSparkSystems(GameObject rootObject)
    {
        for (int i = 0; i < _sparkParticleBindings.Count; i++)
        {
            SparkParticleBinding binding = _sparkParticleBindings[i];
            if (binding.RootObject != rootObject || binding.ParticleSystem == null)
                continue;

            binding.ParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private MusicBox findElevatorMusicBoxInScene()
    {
        MusicBox[] musicBoxes = FindObjectsOfType<MusicBox>();
        foreach (MusicBox box in musicBoxes)
        {
            if (box.sceneName == "Elevator")
            {
                return box;
            }
        }
        Debug.LogWarning("No MusicBox found for the current scene. Elevator ambience will not be stopped properly.");
        return null;
    }

    private void EnsureAmbienceDoesntRestart(MusicBox musicBox)
    {
        if (musicBox != null)
        {
            musicBox.StopPlayingAmbience();
        }
        else
        {
            Debug.LogWarning("Cannot stop ambience on MusicBox because none was found in the scene.");
        }
    
    }
}
