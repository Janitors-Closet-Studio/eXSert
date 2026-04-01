using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public class BirdFlightCycle : MonoBehaviour
{
    private enum BirdState
    {
        IdleAtOrigin = 0,
        LiftOffToDestination = 1,
        FlyingToDestination = 2,
        LandingAtDestination = 3,
        IdleAtDestination = 4,
        LiftOffToOrigin = 5,
        FlyingToOrigin = 6,
        LandingAtOrigin = 7,
    }

    [Header("References")]
    [SerializeField]
    private Animator animator;

    [SerializeField]
    [Tooltip("Path from the first perch to the landing perch.")]
    private BirdSplinePath outboundPath;

    [SerializeField]
    [Tooltip("Optional custom path for the return trip. Leave empty to reverse the outbound path.")]
    private BirdSplinePath returnPath;

    [Header("Detection")]
    [SerializeField]
    [Min(0.5f)]
    [Tooltip("How close the player needs to get before the bird takes off.")]
    private float detectionRadius = 8f;

    [SerializeField]
    [Tooltip(
        "If enabled, the origin bird will wait for the player to leave before it can be startled again."
    )]
    private bool requirePlayerToLeaveBeforeRestart = true;

    [Header("Flight Timing")]
    [SerializeField]
    [Min(0.01f)]
    [Tooltip("Units per second while the bird is following the spline.")]
    private float flightSpeed = 4f;

    [SerializeField]
    [Min(0f)]
    [Tooltip("How long the lift-off animation should play before moving along the spline.")]
    private float liftOffDuration = 0.55f;

    [SerializeField]
    [Min(0f)]
    [Tooltip("How long the landing animation should play before returning to idle.")]
    private float landingDuration = 0.45f;

    [SerializeField]
    [Min(0f)]
    [Tooltip("How long the bird waits on the destination perch before flying back.")]
    private float idleAtDestinationDuration = 30f;

    [SerializeField]
    [Min(0.01f)]
    [Tooltip("How quickly the bird rotates to face the flight direction.")]
    private float rotationLerpSpeed = 10f;

    [SerializeField]
    [Min(0f)]
    [Tooltip("Distance from takeoff over which the bird ramps up to full speed.")]
    private float accelerationDistance = 1.5f;

    [SerializeField]
    [Min(0f)]
    [Tooltip("Distance before landing over which the bird ramps down from full speed.")]
    private float decelerationDistance = 2f;

    [SerializeField]
    [Min(0f)]
    [Tooltip("Distance before touchdown where the landing animation should begin playing.")]
    private float landingAnimationLeadDistance = 1.5f;

    [SerializeField]
    [Range(0.05f, 1f)]
    [Tooltip("Minimum fraction of flight speed used during takeoff and landing easing.")]
    private float minimumFlightSpeedMultiplier = 0.2f;

    [Header("Animation State Names")]
    [SerializeField]
    private string perchingIdleState = "Perching_Idle";

    [SerializeField]
    private string liftOffState = "Lift_Off";

    [SerializeField]
    private string flyingState = "Flying";

    [SerializeField]
    private string landingState = "Landing";

    [SerializeField]
    [Min(0f)]
    [Tooltip("Crossfade duration used when switching between bird animation states.")]
    private float animationCrossFade = 0.08f;

    [Header("Perch Alignment")]
    [SerializeField]
    [Tooltip(
        "When enabled, the bird snaps to the perch rotation while idle and when landing completes."
    )]
    private bool snapToPerchRotation = true;

    [Header("Debug")]
    [SerializeField]
    private bool drawDetectionRadius = true;

    private BirdState currentState;
    private Transform cachedPlayerTransform;
    private float nextPlayerLookupTime;
    private float stateTimer;
    private float flightProgress;
    private float activePathLength;
    private bool activePathReversed;
    private BirdSplinePath activePath;
    private Transform activeTargetPerch;
    private Vector3 fallbackFlightStart;
    private Vector3 fallbackFlightEnd;
    private bool waitingForPlayerExit;
    private bool isInitialized;
    private bool landingAnimationTriggeredInFlight;
    private float landingAnimationElapsed;

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        InitializeAtOrigin();
    }

    private void OnValidate()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }
    }

    private void Update()
    {
        if (!HasRequiredSetup())
        {
            return;
        }

        if (!isInitialized)
        {
            InitializeAtOrigin();
        }

        ResolvePlayerTransform();

        switch (currentState)
        {
            case BirdState.IdleAtOrigin:
                UpdateIdleAtOrigin();
                break;
            case BirdState.LiftOffToDestination:
                UpdateLiftOff(BirdState.FlyingToDestination);
                break;
            case BirdState.FlyingToDestination:
                UpdateFlight(BirdState.LandingAtDestination);
                break;
            case BirdState.LandingAtDestination:
                UpdateLanding(BirdState.IdleAtDestination);
                break;
            case BirdState.IdleAtDestination:
                UpdateIdleAtDestination();
                break;
            case BirdState.LiftOffToOrigin:
                UpdateLiftOff(BirdState.FlyingToOrigin);
                break;
            case BirdState.FlyingToOrigin:
                UpdateFlight(BirdState.LandingAtOrigin);
                break;
            case BirdState.LandingAtOrigin:
                UpdateLanding(BirdState.IdleAtOrigin);
                break;
        }
    }

    [ContextMenu("Start Flight To Destination")]
    public void DebugStartOutboundFlight()
    {
        if (!HasRequiredSetup())
        {
            return;
        }

        BeginOutboundCycle();
    }

    [ContextMenu("Start Flight To Origin")]
    public void DebugStartReturnFlight()
    {
        if (!HasRequiredSetup())
        {
            return;
        }

        BeginReturnCycle();
    }

    [ContextMenu("Reset Bird To Origin")]
    public void DebugResetToOrigin()
    {
        InitializeAtOrigin();
    }

    public void EditorPreviewAlongPath(float normalizedProgress, bool previewReturnTrip)
    {
        if (outboundPath == null || !outboundPath.HasValidPath)
        {
            return;
        }

        BirdSplinePath previewPath = outboundPath;
        bool reversePreview = false;

        if (previewReturnTrip)
        {
            if (returnPath != null && returnPath.HasValidPath)
            {
                previewPath = returnPath;
            }
            else
            {
                previewPath = outboundPath;
                reversePreview = true;
            }
        }

        float progress = Mathf.Clamp01(normalizedProgress);
        float pathProgress = reversePreview ? 1f - progress : progress;

        Vector3 previewPosition = previewPath.GetPoint(pathProgress);
        Vector3 previewDirection = previewPath.GetTangent(pathProgress);
        if (reversePreview)
        {
            previewDirection = -previewDirection;
        }

        transform.position = previewPosition;

        if (snapToPerchRotation && previewDirection.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(previewDirection.normalized, Vector3.up);
        }

        if (progress <= 0f)
        {
            if (previewReturnTrip)
            {
                SnapToEditorDestination();
            }
            else
            {
                SnapToEditorOrigin();
            }
        }
        else if (progress >= 1f)
        {
            if (previewReturnTrip)
            {
                SnapToEditorOrigin();
            }
            else
            {
                SnapToEditorDestination();
            }
        }
    }

    public bool CanEditorPreview()
    {
        return outboundPath != null && outboundPath.HasValidPath;
    }

    public float GetEditorPreviewDuration(bool previewReturnTrip)
    {
        BirdSplinePath previewPath = outboundPath;

        if (previewReturnTrip && returnPath != null && returnPath.HasValidPath)
        {
            previewPath = returnPath;
        }

        if (previewPath == null || !previewPath.HasValidPath)
        {
            return 0f;
        }

        float safeSpeed = Mathf.Max(0.01f, flightSpeed);
        float pathLength = Mathf.Max(0.01f, previewPath.ApproximateLength);
        return pathLength / safeSpeed;
    }

    public void SnapToEditorOrigin()
    {
        transform.position = GetOriginPosition();
        if (snapToPerchRotation)
        {
            transform.rotation = GetOriginRotation();
        }
    }

    public void SnapToEditorDestination()
    {
        transform.position = GetDestinationPosition();
        if (snapToPerchRotation)
        {
            transform.rotation = GetDestinationRotation();
        }
    }

    private bool HasRequiredSetup()
    {
        return animator != null && outboundPath != null && outboundPath.HasValidPath;
    }

    private void InitializeAtOrigin()
    {
        if (!HasRequiredSetup())
        {
            return;
        }

        currentState = BirdState.IdleAtOrigin;
        stateTimer = 0f;
        flightProgress = 0f;
        waitingForPlayerExit = false;
        isInitialized = true;
        landingAnimationTriggeredInFlight = false;
        landingAnimationElapsed = 0f;

        SnapToPerch(GetOriginPerch());
        PlayState(perchingIdleState);
    }

    private void UpdateIdleAtOrigin()
    {
        SnapToPerch(GetOriginPerch());

        bool playerIsNearby = IsPlayerNearby();
        if (!playerIsNearby)
        {
            waitingForPlayerExit = false;
            return;
        }

        if (requirePlayerToLeaveBeforeRestart && waitingForPlayerExit)
        {
            return;
        }

        BeginOutboundCycle();
    }

    private void UpdateIdleAtDestination()
    {
        SnapToPerch(GetDestinationPerch());
        stateTimer -= Time.deltaTime;

        if (stateTimer <= 0f)
        {
            BeginReturnCycle();
        }
    }

    private void UpdateLiftOff(BirdState nextState)
    {
        stateTimer -= Time.deltaTime;
        if (stateTimer > 0f)
        {
            return;
        }

        currentState = nextState;
        PlayState(flyingState);
    }

    private void UpdateFlight(BirdState landingStateToEnter)
    {
        float safeLength = Mathf.Max(activePathLength, 0.01f);
        float remainingDistanceBeforeMove = Mathf.Max(
            0f,
            (1f - Mathf.Clamp01(flightProgress)) * safeLength
        );

        TryStartLandingAnimationEarly(remainingDistanceBeforeMove);

        float currentSpeed = GetCurrentFlightSpeed(safeLength);
        flightProgress += (currentSpeed / safeLength) * Time.deltaTime;
        if (landingAnimationTriggeredInFlight)
        {
            landingAnimationElapsed += Time.deltaTime;
        }

        float clampedProgress = Mathf.Clamp01(flightProgress);
        transform.position = EvaluateActiveFlightPoint(clampedProgress);
        RotateAlongFlightPath(clampedProgress);

        if (flightProgress < 1f)
        {
            return;
        }

        SnapToPerch(activeTargetPerch);
        currentState = landingStateToEnter;
        stateTimer = Mathf.Max(0f, landingDuration - landingAnimationElapsed);

        if (!landingAnimationTriggeredInFlight)
        {
            PlayState(landingState);
        }
    }

    private float GetCurrentFlightSpeed(float pathLength)
    {
        float baseSpeed = Mathf.Max(0.01f, flightSpeed);
        float travelledDistance = Mathf.Clamp01(flightProgress) * pathLength;
        float remainingDistance = Mathf.Max(0f, pathLength - travelledDistance);

        float accelerationMultiplier = EvaluateSpeedRamp(travelledDistance, accelerationDistance);
        float decelerationMultiplier = EvaluateSpeedRamp(remainingDistance, decelerationDistance);
        float speedMultiplier = Mathf.Min(accelerationMultiplier, decelerationMultiplier);

        return baseSpeed * speedMultiplier;
    }

    private float EvaluateSpeedRamp(float distance, float rampDistance)
    {
        if (rampDistance <= 0.01f)
        {
            return 1f;
        }

        float normalizedDistance = Mathf.Clamp01(distance / rampDistance);
        float normalizedDistanceSquared = normalizedDistance * normalizedDistance;
        float easedProgress = normalizedDistanceSquared * (3f - 2f * normalizedDistance);
        return Mathf.Lerp(minimumFlightSpeedMultiplier, 1f, easedProgress);
    }

    private void UpdateLanding(BirdState idleStateToEnter)
    {
        SnapToPerch(activeTargetPerch);
        stateTimer -= Time.deltaTime;
        if (stateTimer > 0f)
        {
            return;
        }

        currentState = idleStateToEnter;
        PlayState(perchingIdleState);

        if (idleStateToEnter == BirdState.IdleAtDestination)
        {
            stateTimer = idleAtDestinationDuration;
            SnapToPerch(GetDestinationPerch());
        }
        else
        {
            stateTimer = 0f;
            SnapToPerch(GetOriginPerch());
        }
    }

    private void BeginOutboundCycle()
    {
        activePath = outboundPath;
        activePathReversed = false;
        activeTargetPerch = GetDestinationPerch();
        fallbackFlightStart = outboundPath.GetStartPosition();
        fallbackFlightEnd = outboundPath.GetEndPosition();
        activePathLength = Mathf.Max(
            activePath.ApproximateLength,
            Vector3.Distance(fallbackFlightStart, fallbackFlightEnd)
        );
        flightProgress = 0f;
        landingAnimationTriggeredInFlight = false;
        landingAnimationElapsed = 0f;
        currentState = BirdState.LiftOffToDestination;
        stateTimer = liftOffDuration;
        waitingForPlayerExit = true;
        PlayState(liftOffState);
    }

    private void BeginReturnCycle()
    {
        if (returnPath != null && returnPath.HasValidPath)
        {
            activePath = returnPath;
            activePathReversed = false;
        }
        else
        {
            activePath = outboundPath;
            activePathReversed = true;
        }

        activeTargetPerch = GetOriginPerch();
        fallbackFlightStart = outboundPath.GetEndPosition();
        fallbackFlightEnd = outboundPath.GetStartPosition();
        activePathLength = Mathf.Max(
            activePath.ApproximateLength,
            Vector3.Distance(fallbackFlightStart, fallbackFlightEnd)
        );
        flightProgress = 0f;
        landingAnimationTriggeredInFlight = false;
        landingAnimationElapsed = 0f;
        currentState = BirdState.LiftOffToOrigin;
        stateTimer = liftOffDuration;
        PlayState(liftOffState);
    }

    private void TryStartLandingAnimationEarly(float remainingDistance)
    {
        if (landingAnimationTriggeredInFlight)
        {
            return;
        }

        float triggerDistance = landingAnimationLeadDistance;
        if (triggerDistance <= 0f)
        {
            triggerDistance = decelerationDistance;
        }

        if (triggerDistance <= 0f || remainingDistance > triggerDistance)
        {
            return;
        }

        landingAnimationTriggeredInFlight = true;
        landingAnimationElapsed = 0f;
        PlayState(landingState);
    }

    private Transform GetOriginPerch()
    {
        return outboundPath != null ? outboundPath.StartPoint : null;
    }

    private Transform GetDestinationPerch()
    {
        return outboundPath != null ? outboundPath.EndPoint : null;
    }

    private Vector3 GetOriginPosition()
    {
        return outboundPath != null ? outboundPath.GetStartPosition() : transform.position;
    }

    private Vector3 GetDestinationPosition()
    {
        return outboundPath != null ? outboundPath.GetEndPosition() : transform.position;
    }

    private Quaternion GetOriginRotation()
    {
        return outboundPath != null ? outboundPath.GetStartRotation() : transform.rotation;
    }

    private Quaternion GetDestinationRotation()
    {
        return outboundPath != null ? outboundPath.GetEndRotation() : transform.rotation;
    }

    private Vector3 EvaluateActiveFlightPoint(float normalizedProgress)
    {
        if (activePath == null || !activePath.HasValidPath)
        {
            return Vector3.Lerp(fallbackFlightStart, fallbackFlightEnd, normalizedProgress);
        }

        float pathProgress = activePathReversed ? 1f - normalizedProgress : normalizedProgress;
        return activePath.GetPoint(pathProgress);
    }

    private void RotateAlongFlightPath(float normalizedProgress)
    {
        Vector3 direction;

        if (activePath == null || !activePath.HasValidPath)
        {
            direction = fallbackFlightEnd - fallbackFlightStart;
        }
        else
        {
            float pathProgress = activePathReversed ? 1f - normalizedProgress : normalizedProgress;
            direction = activePath.GetTangent(pathProgress);
            if (activePathReversed)
            {
                direction = -direction;
            }
        }

        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationLerpSpeed * Time.deltaTime
        );
    }

    private void SnapToPerch(Transform perch)
    {
        if (perch != null)
        {
            transform.position = perch.position;
            if (snapToPerchRotation)
            {
                transform.rotation = perch.rotation;
            }

            return;
        }

        if (
            currentState == BirdState.IdleAtDestination
            || currentState == BirdState.LandingAtDestination
        )
        {
            transform.position = GetDestinationPosition();
            if (snapToPerchRotation)
            {
                transform.rotation = GetDestinationRotation();
            }

            return;
        }

        transform.position = GetOriginPosition();
        if (snapToPerchRotation)
        {
            transform.rotation = GetOriginRotation();
        }
    }

    private bool IsPlayerNearby()
    {
        if (cachedPlayerTransform == null)
        {
            return false;
        }

        float maxDistance = detectionRadius * detectionRadius;
        return (cachedPlayerTransform.position - transform.position).sqrMagnitude <= maxDistance;
    }

    private void ResolvePlayerTransform()
    {
        if (cachedPlayerTransform != null && cachedPlayerTransform.gameObject.activeInHierarchy)
        {
            return;
        }

        if (Time.time < nextPlayerLookupTime)
        {
            return;
        }

        nextPlayerLookupTime = Time.time + 0.5f;

        if (PlayerPresenceManager.IsPlayerPresent)
        {
            cachedPlayerTransform = PlayerPresenceManager.PlayerTransform;
            return;
        }

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        cachedPlayerTransform = playerObject != null ? playerObject.transform : null;
    }

    private void PlayState(string stateName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(stateName))
        {
            return;
        }

        int stateHash = Animator.StringToHash(stateName);
        if (!animator.HasState(0, stateHash))
        {
            return;
        }

        animator.CrossFadeInFixedTime(stateHash, animationCrossFade, 0, 0f);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDetectionRadius)
        {
            return;
        }

        Gizmos.color = new Color(1f, 0.5f, 0.15f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}
