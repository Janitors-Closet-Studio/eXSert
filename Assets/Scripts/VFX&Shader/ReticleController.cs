/*
This controller drives the enemy targeting reticle that lives on a world-space canvas.

The reticle has three main visual layers:
- base gear: visible when the target can be seen and is within detection range
- glow gear: visible when the player is inside the enemy's attack range
- arrows: only used during hard lock and death-release states

Most of the script is about coordinating those pieces without fighting Unity UI transform rules.
Lock and unlock move the arrows by local X only so their authored Y/Z layout stays intact.
The death sequence is handled separately because it needs gear blinking, slowed rotation, and an optional arrow move/fade.

Enemy-specific values such as detection range and attack range are resolved dynamically so the same reticle can be reused across multiple enemy types.
*/

using System;
using System.Collections.Generic;
using System.Reflection;
using DG.Tweening;
using Unity.Cinemachine;
using UnityEngine;
#pragma warning disable CS0414
using UnityEngine.UI;

public class ReticleController : MonoBehaviour
{
    // These states are not gameplay state. They only describe which animation/visual phase the reticle is in.
    // They are used to decide whether arrows should exist, whether death plays gears-only or full release,
    // and whether visibility changes should restore idle or locked poses.
    private enum ReticleState
    {
        Hidden,
        Idle,
        Locking,
        Locked,
        Releasing
    }
#pragma warning restore CS0414

    private struct ImageState
    {
        public Image Image;
        public float BaseAlpha;
    }

    [Header("Tracking")]
    [SerializeField] private Transform targetEnemy;
    [SerializeField] private bool billboardToCamera = true;
    [SerializeField, Tooltip("Limit fallback camera selection to specific layers.")]
    private LayerMask cameraLayerMask = ~0;
    [SerializeField, Tooltip("Flip the reticle 180 degrees if it appears backwards.")]
    private bool flipFacing;

    [Header("Visibility")]
    [SerializeField, Tooltip("Layers that block reticle visibility between the target and the player camera.")]
    private LayerMask obstacleLayers;
    [SerializeField, Min(0f), Tooltip("How quickly the base gear fades in when the player enters the visible range.")]
    private float visibleFadeDuration = 0.2f;
    [SerializeField, Min(0f), Tooltip("How quickly the reticle swaps from base gear to glow gear inside attack range.")]
    private float attackZoneFadeDuration = 0.12f;

    [Header("Bindings")]
    [SerializeField] private RectTransform baseGear;
    [SerializeField] private RectTransform glowGear;
    [SerializeField] private RectTransform leftArrow;
    [SerializeField] private RectTransform rightArrow;

    [Header("Idle")]
    [SerializeField, Tooltip("Show the idle reticle after unlock completes. Initial load still starts hidden.")]
    private bool showIdleWhenUnlocked;
    [SerializeField] private float leftArrowLockOffsetX = -0.1532f;
    [SerializeField] private float rightArrowLockOffsetX = 0.15325f;
    [SerializeField, Min(0.01f)] private float baseGearRotationDuration = 3f;
    [SerializeField, Min(0.01f)] private float glowGearRotationDuration = 2.4f;

    [Header("Lock On")]
    [SerializeField, Min(0f)] private float lockArrowDuration = 0.18f;
    [SerializeField, Min(0f)] private float glowFadeInDuration = 0.18f;
    [SerializeField] private Ease lockArrowEase = Ease.OutCubic;

    [Header("Dead")]
    [SerializeField] private Vector2 leftArrowDeathAnchoredPosition = new Vector2(0.445f, 0.45f);
    [SerializeField] private Vector2 rightArrowDeathAnchoredPosition = new Vector2(0.555f, 0.55f);
    [SerializeField, Min(1)] private int glowBlinkCount = 3;
    [SerializeField, Min(0f)] private float deathPowerDownDuration = 0.5f;
    [SerializeField, Range(0.05f, 1f)] private float deathGearSlowScale = 0.2f;
    [SerializeField, Min(0f)] private float deathArrowMoveDuration = 0.2f;
    [SerializeField, Min(0f)] private float deathFadeLeadTime = 0.05f;
    [SerializeField, Min(0f)] private float deathFadeOutDuration = 0.3f;
    [SerializeField, Min(0f)] private float fadeOutDuration = 0.2f;
    [SerializeField] private Ease unlockCrossEase = Ease.InOutCubic;

    private Camera mainCam;
    private Sequence stateSequence;
    private Tween baseGearRotationTween;
    private Tween glowGearRotationTween;
    private Vector3 leftArrowStartLocalPosition;
    private Vector3 rightArrowStartLocalPosition;
    private Vector3 leftArrowLockedLocalPosition;
    private Vector3 rightArrowLockedLocalPosition;
    private bool layoutCaptured;
    private ReticleState currentState;
    private ImageState[] baseGearImages;
    private ImageState[] glowGearImages;
    private ImageState[] leftArrowImages;
    private ImageState[] rightArrowImages;
    private Image[] allReticleImages;
    private bool imageTargetsCached;
    private Transform fallbackCameraTransform;
    private BaseEnemyCore targetEnemyCore;
    private Transform cachedPlayerTransform;
    private float cachedDetectionRange = -1f;
    private Tween baseGearFadeTween;
    private Tween glowGearFadeTween;
    private float gearRotationSpeedScale = 1f;
    private bool reticleRenderVisible;
    private bool playerInAttackZone;
    private float cachedAttackRange = -1f;
    private bool externalTargetingStateActive;

    protected virtual void Awake()
    {
        ResolveReferences();
        CacheImageTargets();
        CaptureLayout();
        CacheCombatSources();
        RefreshEnemyCoreSubscription();
    }

    protected virtual void OnEnable()
    {
        ResolveReferences();
        EnsureImageTargetsCached();
        CaptureLayout();
        CacheCombatSources();
        RefreshEnemyCoreSubscription();
        ResetRuntimeState();
        StartContinuousRotation();
        HideImmediate();
    }

    protected virtual void OnDisable()
    {
        stateSequence?.Kill();
        stateSequence = null;

        baseGearFadeTween?.Kill();
        baseGearFadeTween = null;

        glowGearFadeTween?.Kill();
        glowGearFadeTween = null;

        baseGearRotationTween?.Kill();
        baseGearRotationTween = null;

        glowGearRotationTween?.Kill();
        glowGearRotationTween = null;

        UnsubscribeFromEnemyDeath();
    }

    protected virtual void LateUpdate()
    {
        Transform camTransform = ResolveCameraTransform();
        if (camTransform == null)
            return;

        // Visibility and attack-zone state are evaluated every frame because they depend on live distance,
        // line-of-sight, and camera position. Billboard rotation also happens here so the reticle stays camera-facing.
        if (!externalTargetingStateActive)
            UpdateRangeDrivenVisibility(camTransform);

        if (!billboardToCamera)
            return;

        Quaternion targetRotation = camTransform.rotation;
        if (flipFacing)
            targetRotation *= Quaternion.Euler(0f, 180f, 0f);

        transform.rotation = targetRotation;
    }

    public void SetExternalTargetingState(bool useExternalState, bool showBaseGear, bool showGlowGear, bool showArrowsState)
    {
        externalTargetingStateActive = useExternalState;

        if (!useExternalState)
            return;

        CaptureLayout();

        bool isVisible = showBaseGear || showGlowGear || showArrowsState;
        reticleRenderVisible = isVisible;
        playerInAttackZone = showGlowGear;

        // Preserve DOTween-driven release animations when the lock system is still updating this reticle.
        if (currentState == ReticleState.Releasing)
            return;

        // Preserve the arrow lock-in animation once it has started.
        if (showArrowsState)
        {
            if (currentState != ReticleState.Locked && currentState != ReticleState.Locking)
            {
                PlayLockedOn();
                return;
            }

            ApplyGearState(true, showGlowGear, instant: false);
            return;
        }

        // If unlock is currently playing, do not snap the reticle back to idle/hidden mid-sequence.
        if (currentState == ReticleState.Locking || currentState == ReticleState.Locked)
        {
            ApplyGearState(isVisible, showGlowGear, instant: false);
            return;
        }

        RestoreArrowStartPose();
        SetGroupAlpha(leftArrowImages, 0f);
        SetGroupAlpha(rightArrowImages, 0f);
        currentState = isVisible ? ReticleState.Idle : ReticleState.Hidden;

        ApplyGearState(isVisible, showGlowGear, instant: false);
    }

    public void SetTarget(Transform target)
    {
        // Called when a reticle is rebound to a different enemy transform.
        // Refresh the cached combat values and death-event subscription so the reticle continues to track the correct owner.
        targetEnemy = target;
        CacheCombatSources();
        RefreshEnemyCoreSubscription();
    }

    public void ShowIdleImmediate()
    {
        // Hard snap to the idle presentation with no transition.
        // Useful for initialization or when another system wants to force a clean baseline.
        stateSequence?.Kill();
        stateSequence = null;
        ApplyIdleVisuals();
        currentState = ReticleState.Idle;
    }

    public void HideImmediate()
    {
        // Hard snap to fully hidden state with no tweening.
        stateSequence?.Kill();
        stateSequence = null;
        ApplyHiddenVisuals();
        currentState = ReticleState.Hidden;
    }

    public void PlayLockedOn()
    {
        stateSequence?.Kill();
        stateSequence = null;

        // Lock-in always starts from the authored idle arrow pose so repeated lock/unlock cycles do not drift.
        // Only local X is animated here; Y and Z are intentionally preserved from the prefab layout.
        CaptureLayout();
        RestoreArrowStartPose();
        SetGroupAlpha(leftArrowImages, 0f);
        SetGroupAlpha(rightArrowImages, 0f);
        ApplyGearState(reticleRenderVisible, playerInAttackZone, instant: true);

        currentState = ReticleState.Locking;
        Sequence sequence = DOTween.Sequence().SetLink(gameObject);

        if (leftArrow != null)
            sequence.Join(TweenArrowLocalX(leftArrow, leftArrowLockedLocalPosition.x, lockArrowDuration));

        if (rightArrow != null)
            sequence.Join(TweenArrowLocalX(rightArrow, rightArrowLockedLocalPosition.x, lockArrowDuration));

        sequence.Join(TweenGroupAlpha(leftArrowImages, 1f, lockArrowDuration));
        sequence.Join(TweenGroupAlpha(rightArrowImages, 1f, lockArrowDuration));

        sequence.OnComplete(() => currentState = ReticleState.Locked);
        stateSequence = sequence;
    }

    public void PlayUnlocked()
    {
        stateSequence?.Kill();
        stateSequence = null;

        // This is the normal hard-lock cancel path.
        // It fades arrows out, moves them back to the authored start pose, and swaps glow back to base if the reticle should remain visible.
        CaptureLayout();
        bool shouldShowIdle = reticleRenderVisible;

        Sequence sequence = DOTween.Sequence().SetLink(gameObject);

        if (leftArrow != null)
            sequence.Join(TweenArrowLocalX(leftArrow, leftArrowStartLocalPosition.x, lockArrowDuration));

        if (rightArrow != null)
            sequence.Join(TweenArrowLocalX(rightArrow, rightArrowStartLocalPosition.x, lockArrowDuration));

        sequence.Join(TweenGroupAlpha(leftArrowImages, 0f, fadeOutDuration));
        sequence.Join(TweenGroupAlpha(rightArrowImages, 0f, fadeOutDuration));
        sequence.Join(TweenGroupAlpha(glowGearImages, 0f, attackZoneFadeDuration));
        sequence.Join(TweenGroupAlpha(baseGearImages, shouldShowIdle ? 1f : 0f, visibleFadeDuration));

        sequence.OnComplete(() =>
        {
            RestoreArrowStartPose();

            if (shouldShowIdle)
            {
                ApplyIdleVisuals();
                currentState = ReticleState.Idle;
            }
            else
            {
                ApplyHiddenVisuals();
                currentState = ReticleState.Hidden;
            }
        });

        stateSequence = sequence;
    }

    public void PlayTargetLost()
    {
        // AttackLockSystem calls this for a locked target death.
        // The reticle itself also calls into the same path from its own enemy-death subscription.
        PlayDeathSequence(currentState == ReticleState.Locked || currentState == ReticleState.Locking);
    }

    private void PlayDeathSequence(bool includeArrows)
    {
        // `includeArrows` means the target died while the player was actively hard-locked onto it.
        // In that case we play the full release: gears power down, arrows move to the death pose, and everything fades.
        // If the target was not locked, we only power down the gears so the enemy still looks like it lost power.
        if (currentState == ReticleState.Releasing)
            return;

        stateSequence?.Kill();
        stateSequence = null;

        CaptureLayout();

        // Force both gears visible at the start of the power-down so the glow can blink on top of a stable base gear.
        SetGroupAlpha(baseGearImages, 1f);
        SetGroupAlpha(glowGearImages, 1f);

        if (includeArrows)
        {
            // For locked targets, start from the locked arrow pose so the release begins from the visual state the player already sees.
            RestoreArrowLockedPose();
            SetGroupAlpha(leftArrowImages, 1f);
            SetGroupAlpha(rightArrowImages, 1f);
        }
        else
        {
            // No arrows are shown for non-locked targets; only the gear shutdown is presented.
            RestoreArrowStartPose();
            SetGroupAlpha(leftArrowImages, 0f);
            SetGroupAlpha(rightArrowImages, 0f);
        }

        currentState = ReticleState.Releasing;
        Sequence sequence = DOTween.Sequence().SetLink(gameObject);

        // Rotation slows while the glow blinks to sell the "power shutting down" effect.
        sequence.Join(
            DOTween.To(
                    () => gearRotationSpeedScale,
                    SetGearRotationSpeedScale,
                    deathGearSlowScale,
                    deathPowerDownDuration)
                .SetEase(Ease.OutQuad)
                .SetLink(gameObject));
        sequence.Join(CreateGlowPowerDownSequence());

        if (includeArrows)
        {
            Sequence arrowMoveSequence = DOTween.Sequence().SetLink(gameObject);

            if (leftArrow != null)
                arrowMoveSequence.Join(
                    leftArrow
                        .DOAnchorPos(leftArrowDeathAnchoredPosition, deathArrowMoveDuration)
                        .SetEase(unlockCrossEase));

            if (rightArrow != null)
                arrowMoveSequence.Join(
                    rightArrow
                        .DOAnchorPos(rightArrowDeathAnchoredPosition, deathArrowMoveDuration)
                        .SetEase(unlockCrossEase));

            // Begin fading slightly before the arrows finish traveling so the release feels continuous instead of stopping, then disappearing.
            arrowMoveSequence.Insert(
                Mathf.Max(0f, deathArrowMoveDuration - deathFadeLeadTime),
                TweenAllGroupsToZero(Mathf.Max(0.01f, deathFadeOutDuration)));

            sequence.Append(arrowMoveSequence);
        }
        else
        {
            sequence.Append(TweenGearGroupsToZero(Mathf.Max(0.01f, deathFadeOutDuration)));
        }

        sequence.OnComplete(() =>
        {
            SetGearRotationSpeedScale(1f);
            RestoreArrowStartPose();

            ApplyHiddenVisuals();
            currentState = ReticleState.Hidden;
        });

        stateSequence = sequence;
    }

    private void StartContinuousRotation()
    {
        baseGearRotationTween?.Kill();
        glowGearRotationTween?.Kill();

        if (baseGear != null)
        {
            // Base and glow gears spin in opposite directions and stay on local Z only.
            // Using local Z prevents the UI from tumbling when the parent is billboarded toward the camera.
            Vector3 baseGearEuler = baseGear.localEulerAngles;
            baseGearRotationTween = baseGear
                .DOLocalRotate(
                    new Vector3(baseGearEuler.x, baseGearEuler.y, baseGearEuler.z - 360f),
                    baseGearRotationDuration,
                    RotateMode.FastBeyond360)
                .SetEase(Ease.Linear)
                .SetLoops(-1, LoopType.Restart)
                .SetLink(gameObject);
        }

        if (glowGear != null)
        {
            Vector3 glowGearEuler = glowGear.localEulerAngles;
            glowGearRotationTween = glowGear
                .DOLocalRotate(
                    new Vector3(glowGearEuler.x, glowGearEuler.y, glowGearEuler.z + 360f),
                    glowGearRotationDuration,
                    RotateMode.FastBeyond360)
                .SetEase(Ease.Linear)
                .SetLoops(-1, LoopType.Restart)
                .SetLink(gameObject);
        }

            SetGearRotationSpeedScale(1f);
    }

    private void ApplyIdleVisuals()
    {
        // Idle means: gears may still be visible based on range, but arrows are gone and glow is not forced.
        CaptureLayout();
        RestoreArrowStartPose();
        SetGroupAlpha(leftArrowImages, 0f);
        SetGroupAlpha(rightArrowImages, 0f);
        ApplyGearState(reticleRenderVisible, false, instant: true);
    }

    private void ApplyHiddenVisuals()
    {
        // Hidden means every visual group is off regardless of range/attack state.
        CaptureLayout();
        RestoreArrowStartPose();
        SetGroupAlpha(leftArrowImages, 0f);
        SetGroupAlpha(rightArrowImages, 0f);
        ApplyGearState(false, false, instant: true);
    }

    private void ResolveReferences()
    {
        // Allows the controller to auto-bind children by convention when fields are not wired manually.
        baseGear ??= FindRectTransform("BaseGear");
        glowGear ??= FindRectTransform("GlowGear") ?? FindRectTransform("Glow Gear");
        leftArrow ??= FindRectTransform("LArrow") ?? FindRectTransform("LeftArrow") ?? FindRectTransform("Left Arrow");
        rightArrow ??= FindRectTransform("RArrow") ?? FindRectTransform("RightArrow") ?? FindRectTransform("Right Arrow");
    }

    private void CacheImageTargets()
    {
        imageTargetsCached = true;

        // Cache all Image references once so fades operate on grouped alpha instead of repeated GetComponent calls.
        baseGearImages = CaptureImages(baseGear);
        glowGearImages = CaptureImages(glowGear);
        leftArrowImages = CaptureImages(leftArrow);
        rightArrowImages = CaptureImages(rightArrow);
        allReticleImages = GetComponentsInChildren<Image>(true);
    }

    private void EnsureImageTargetsCached()
    {
        if (!imageTargetsCached || baseGearImages == null || glowGearImages == null || leftArrowImages == null || rightArrowImages == null || allReticleImages == null)
            CacheImageTargets();
    }

    private void ResetRuntimeState()
    {
        externalTargetingStateActive = false;
        reticleRenderVisible = false;
        playerInAttackZone = false;
        cachedDetectionRange = -1f;
        cachedAttackRange = -1f;
        currentState = ReticleState.Hidden;
        SetGearRotationSpeedScale(1f);
    }

    private void CacheCombatSources()
    {
        // These values are expensive or awkward to discover repeatedly, so they are cached and refreshed when the target changes.
        cachedPlayerTransform = ResolvePlayerTransform();
        cachedDetectionRange = ResolveDetectionRange();
        cachedAttackRange = ResolveAttackRange();
    }

    private void UpdateRangeDrivenVisibility(Transform camTransform)
    {
        // Visibility is a combination of two checks:
        // 1. camera is within the enemy's detection range
        // 2. nothing blocks line of sight between target and camera
        // Attack glow is a separate layer driven by attack range once the reticle is already visible.
        bool shouldBeVisible = CanReticleBeSeen(camTransform);
        bool shouldUseAttackZone = shouldBeVisible && IsPlayerInsideAttackZone(camTransform);

        if (reticleRenderVisible == shouldBeVisible && playerInAttackZone == shouldUseAttackZone)
            return;

        reticleRenderVisible = shouldBeVisible;
        playerInAttackZone = shouldUseAttackZone;
        ApplyGearState(shouldBeVisible, shouldUseAttackZone, instant: false);

        if (!shouldBeVisible)
        {
            SetGroupAlpha(leftArrowImages, 0f);
            SetGroupAlpha(rightArrowImages, 0f);
        }
        else if (currentState == ReticleState.Locked)
        {
            RestoreArrowLockedPose();
            SetGroupAlpha(leftArrowImages, 1f);
            SetGroupAlpha(rightArrowImages, 1f);
        }
        else if (currentState == ReticleState.Idle || currentState == ReticleState.Hidden)
        {
            RestoreArrowStartPose();
            SetGroupAlpha(leftArrowImages, 0f);
            SetGroupAlpha(rightArrowImages, 0f);
        }
    }

    private bool CanReticleBeSeen(Transform camTransform)
    {
        if (camTransform == null || targetEnemy == null)
            return true;

        // Detection-range culling is used as the cheap first gate before doing a physics raycast.
        // This keeps the reticle from rendering for far-away enemies that should not be visually "engaged" yet.
        float distanceToCamera = Vector3.Distance(camTransform.position, targetEnemy.position);
        float visibleDistance = cachedDetectionRange;
        if (visibleDistance <= 0f)
            visibleDistance = ResolveDetectionRange();

        if (visibleDistance > 0f && distanceToCamera > visibleDistance)
            return false;

        Vector3 rayOrigin = targetEnemy.position;
        Vector3 directionToCamera = camTransform.position - rayOrigin;
        float rayDistance = directionToCamera.magnitude;
        if (rayDistance <= 0.0001f)
            return true;

        return !Physics.Raycast(
            rayOrigin,
            directionToCamera.normalized,
            rayDistance,
            obstacleLayers,
            QueryTriggerInteraction.Ignore);
    }

    private bool IsPlayerInsideAttackZone(Transform camTransform)
    {
        // Attack-zone checks are based on the player position when available, with the camera as a fallback reference.
        // This lets the base gear show visibility while the glow specifically communicates immediate threat range.
        Transform playerTransform = cachedPlayerTransform;
        if (playerTransform == null)
        {
            playerTransform = ResolvePlayerTransform();
            cachedPlayerTransform = playerTransform;
        }

        if (targetEnemy == null)
            return false;

        Vector3 referencePosition = playerTransform != null ? playerTransform.position : camTransform.position;
        float attackRange = cachedAttackRange;
        if (attackRange <= 0f)
        {
            attackRange = ResolveAttackRange();
            cachedAttackRange = attackRange;
        }

        if (attackRange <= 0f)
            return false;

        return Vector3.Distance(targetEnemy.position, referencePosition) <= attackRange;
    }

    private void ApplyGearState(bool isVisible, bool useAttackGlow, bool instant)
    {
        // Base gear represents detectable/visible state.
        // Glow gear represents attack pressure and replaces the base when the player is inside attack range.
        baseGearFadeTween?.Kill();
        glowGearFadeTween?.Kill();

        float baseTarget = isVisible && !useAttackGlow ? 1f : 0f;
        float glowTarget = isVisible && useAttackGlow ? 1f : 0f;
        float duration = useAttackGlow ? attackZoneFadeDuration : visibleFadeDuration;

        if (instant || duration <= 0f)
        {
            SetGroupAlpha(baseGearImages, baseTarget);
            SetGroupAlpha(glowGearImages, glowTarget);
            return;
        }

        baseGearFadeTween = TweenGroupAlpha(baseGearImages, baseTarget, duration)?.SetLink(gameObject);
        glowGearFadeTween = TweenGroupAlpha(glowGearImages, glowTarget, duration)?.SetLink(gameObject);
    }

    private Transform ResolvePlayerTransform()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        return playerObject != null ? playerObject.transform : null;
    }

    private float ResolveAttackRange()
    {
        if (targetEnemy == null)
            return -1f;

        Component[] components = targetEnemy.GetComponents<Component>();
        foreach (Component component in components)
        {
            if (component == null)
                continue;

            Type componentType = component.GetType();
            FieldInfo attackRangeField = componentType.GetField("attackRange", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (attackRangeField != null && attackRangeField.FieldType == typeof(float))
                return (float)attackRangeField.GetValue(component);

            FieldInfo attackBoxSizeField = componentType.GetField("attackBoxSize", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            FieldInfo attackBoxDistanceField = componentType.GetField("attackBoxDistance", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (attackBoxSizeField == null || attackBoxDistanceField == null)
                continue;

            if (attackBoxSizeField.FieldType != typeof(Vector3) || attackBoxDistanceField.FieldType != typeof(float))
                continue;

            Vector3 attackBoxSize = (Vector3)attackBoxSizeField.GetValue(component);
            float attackBoxDistance = (float)attackBoxDistanceField.GetValue(component);
            return (Mathf.Max(attackBoxSize.x, attackBoxSize.z) * 0.5f) + attackBoxDistance;
        }

        return -1f;
    }

    private float ResolveDetectionRange()
    {
        if (targetEnemy == null)
            return -1f;

        Component[] components = targetEnemy.GetComponents<Component>();
        foreach (Component component in components)
        {
            if (component == null)
                continue;

            Type componentType = component.GetType();
            FieldInfo detectionRangeField = componentType.GetField(
                "detectionRange",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (detectionRangeField != null && detectionRangeField.FieldType == typeof(float))
                return (float)detectionRangeField.GetValue(component);
        }

        return -1f;
    }

    private void SetReticleRenderVisible(bool isVisible)
    {
        if (allReticleImages == null)
            return;

        foreach (Image image in allReticleImages)
        {
            if (image == null)
                continue;

            image.enabled = isVisible;
        }
    }

    private void CaptureLayout()
    {
        if (layoutCaptured)
            return;

        if (leftArrow != null)
        {
            // Preserve the prefab-authored arrow pose exactly as the source of truth.
            // The locked pose is derived by shifting only local X so animation never rewrites the authored Y/Z layout.
            leftArrowStartLocalPosition = leftArrow.localPosition;

            leftArrowLockedLocalPosition = leftArrowStartLocalPosition;
            leftArrowLockedLocalPosition.x += leftArrowLockOffsetX;
        }

        if (rightArrow != null)
        {
            rightArrowStartLocalPosition = rightArrow.localPosition;

            rightArrowLockedLocalPosition = rightArrowStartLocalPosition;
            rightArrowLockedLocalPosition.x += rightArrowLockOffsetX;
        }

        layoutCaptured = true;
    }

    private RectTransform FindRectTransform(string childName)
    {
        if (string.IsNullOrWhiteSpace(childName))
            return null;

        Transform[] descendants = GetComponentsInChildren<Transform>(true);
        foreach (Transform descendant in descendants)
        {
            if (descendant != null && descendant.name == childName)
                return descendant as RectTransform;
        }

        return null;
    }

    private static ImageState[] CaptureImages(RectTransform root)
    {
        // Capture the current alpha as the image's authored baseline so later fades respect partially transparent artwork.
        if (root == null)
            return System.Array.Empty<ImageState>();

        Image[] images = root.GetComponentsInChildren<Image>(true);
        if (images == null || images.Length == 0)
            return System.Array.Empty<ImageState>();

        var states = new List<ImageState>(images.Length);
        foreach (Image image in images)
        {
            if (image == null)
                continue;

            states.Add(new ImageState
            {
                Image = image,
                BaseAlpha = image.color.a
            });
        }

        return states.ToArray();
    }

    private static void SetGroupAlpha(ImageState[] states, float alpha)
    {
        if (states == null)
            return;

        float clampedAlpha = Mathf.Clamp01(alpha);
        for (int index = 0; index < states.Length; index++)
        {
            Image image = states[index].Image;
            if (image == null)
                continue;

            Color color = image.color;
            color.a = states[index].BaseAlpha * clampedAlpha;
            image.color = color;
        }
    }

    private static float GetGroupAlpha(ImageState[] states)
    {
        if (states == null || states.Length == 0)
            return 0f;

        Image image = states[0].Image;
        if (image == null)
            return 0f;

        float baseAlpha = Mathf.Max(states[0].BaseAlpha, 0.0001f);
        return image.color.a / baseAlpha;
    }

    private static Tween TweenGroupAlpha(ImageState[] states, float endValue, float duration)
    {
        if (states == null || states.Length == 0)
            return null;

        // Tween a normalized group alpha rather than raw color alpha.
        // This preserves the original per-image alpha values while still letting the whole group fade as one unit.
        return DOTween.To(
            () => GetGroupAlpha(states),
            value => SetGroupAlpha(states, value),
            Mathf.Clamp01(endValue),
            duration);
    }

    private Sequence TweenAllGroupsToZero(float duration)
    {
        // Used by the death release where the entire reticle, including arrows, needs to disappear together.
        Sequence sequence = DOTween.Sequence().SetLink(gameObject);
        sequence.Join(TweenGroupAlpha(baseGearImages, 0f, duration));
        sequence.Join(TweenGroupAlpha(glowGearImages, 0f, duration));
        sequence.Join(TweenGroupAlpha(leftArrowImages, 0f, duration));
        sequence.Join(TweenGroupAlpha(rightArrowImages, 0f, duration));
        return sequence;
    }

    private Sequence TweenGearGroupsToZero(float duration)
    {
        // Used by non-locked deaths where only the gears are visible and arrows should remain absent.
        Sequence sequence = DOTween.Sequence().SetLink(gameObject);
        sequence.Join(TweenGroupAlpha(baseGearImages, 0f, duration));
        sequence.Join(TweenGroupAlpha(glowGearImages, 0f, duration));
        return sequence;
    }

    private Sequence CreateGlowPowerDownSequence()
    {
        // Alternate glow on/off to simulate a brief power failure before shutdown.
        // The final step leaves glow off so the base gear is what remains before the fade completes.
        Sequence sequence = DOTween.Sequence().SetLink(gameObject);
        int transitionCount = Mathf.Max(1, (glowBlinkCount * 2) - 1);
        float stepDuration = transitionCount > 0 ? deathPowerDownDuration / transitionCount : deathPowerDownDuration;

        for (int index = 0; index < transitionCount; index++)
        {
            float targetAlpha = index % 2 == 0 ? 0f : 1f;
            sequence.Append(TweenGroupAlpha(glowGearImages, targetAlpha, stepDuration));
        }

        return sequence;
    }

    private Tween TweenArrowLocalX(RectTransform rectTransform, float targetLocalX, float duration)
    {
        if (rectTransform == null)
            return null;

        // Lock/unlock motion intentionally changes only local X.
        // This avoids fighting anchored-position layout and preserves the arrow art's authored Y/Z transform.
        return DOTween.To(
                () => rectTransform.localPosition.x,
                value => SetArrowLocalX(rectTransform, value),
                targetLocalX,
                duration)
            .SetEase(lockArrowEase)
            .SetLink(gameObject);
    }

    private void RestoreArrowStartPose()
    {
        if (leftArrow != null)
            leftArrow.localPosition = leftArrowStartLocalPosition;

        if (rightArrow != null)
            rightArrow.localPosition = rightArrowStartLocalPosition;
    }

    private void RestoreArrowLockedPose()
    {
        if (leftArrow != null)
            leftArrow.localPosition = leftArrowLockedLocalPosition;

        if (rightArrow != null)
            rightArrow.localPosition = rightArrowLockedLocalPosition;
    }

    private void SetGearRotationSpeedScale(float scale)
    {
        // DOTween timeScale is used instead of rebuilding rotation tweens so power-down can slow gears smoothly in-place.
        gearRotationSpeedScale = Mathf.Max(0f, scale);

        if (baseGearRotationTween != null)
            baseGearRotationTween.timeScale = gearRotationSpeedScale;

        if (glowGearRotationTween != null)
            glowGearRotationTween.timeScale = gearRotationSpeedScale;
    }

    private void RefreshEnemyCoreSubscription()
    {
        // Reticle death playback is driven by the enemy core event, so we keep this subscription aligned with the current target.
        BaseEnemyCore resolvedEnemyCore = ResolveEnemyCore();
        if (targetEnemyCore == resolvedEnemyCore)
            return;

        UnsubscribeFromEnemyDeath();
        targetEnemyCore = resolvedEnemyCore;
        SubscribeToEnemyDeath();
    }

    private void SubscribeToEnemyDeath()
    {
        if (targetEnemyCore == null)
            return;

        targetEnemyCore.OnDeath -= HandleEnemyDeath;
        targetEnemyCore.OnDeath += HandleEnemyDeath;
    }

    private void UnsubscribeFromEnemyDeath()
    {
        if (targetEnemyCore == null)
            return;

        targetEnemyCore.OnDeath -= HandleEnemyDeath;
    }

    private void HandleEnemyDeath(BaseEnemyCore enemy)
    {
        if (enemy == null || enemy != targetEnemyCore)
            return;

        // If the reticle was never hard-locked, only gears power down.
        // If the reticle was locked, arrows are included and move to the authored death pose.
        PlayDeathSequence(currentState == ReticleState.Locked || currentState == ReticleState.Locking);
    }

    private BaseEnemyCore ResolveEnemyCore()
    {
        Transform enemyTransform = targetEnemy != null ? targetEnemy : transform;
        return enemyTransform != null ? enemyTransform.GetComponentInParent<BaseEnemyCore>() : null;
    }

    private static void SetArrowLocalX(RectTransform rectTransform, float localX)
    {
        if (rectTransform == null)
            return;

        // Only overwrite X so the current Y/Z from the authored pose or active animation state remain untouched.
        Vector3 localPosition = rectTransform.localPosition;
        localPosition.x = localX;
        rectTransform.localPosition = localPosition;
    }

    private Transform ResolveCameraTransform()
    {
        if (CameraManager.Instance != null)
        {
            CinemachineCamera cineCam = CameraManager.Instance.GetActiveCamera();
            if (cineCam != null)
                return cineCam.transform;
        }

        if (fallbackCameraTransform == null || !fallbackCameraTransform.gameObject.activeInHierarchy)
            fallbackCameraTransform = FindFallbackCameraTransform();

        return fallbackCameraTransform;
    }

    private Transform FindFallbackCameraTransform()
    {
        Camera best = null;
        foreach (Camera cam in Camera.allCameras)
        {
            if (cam == null || !cam.enabled)
                continue;
            if ((cameraLayerMask.value & (1 << cam.gameObject.layer)) == 0)
                continue;

            if (best == null || cam.depth > best.depth)
                best = cam;
        }

        if (best == null)
            best = Camera.main;

        mainCam = best;
        return best != null ? best.transform : null;
    }
}