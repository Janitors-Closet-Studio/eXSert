/** 
    Written by Brandon W

    This script manages the water treatment puzzle, which involves turning a series of valves in the correct order to progress. 
    Each valve is associated with a water container that has a light indicator and a keycard that moves when the valve is turned.
     The script handles the logic for turning valves, updating light states, and animating keycard movement through the pipes.

    Used Co-Pilot to assist with water physics, all code was reviewed and edited to ensure proper functionality and style consistency.
**/

using UnityEngine;
using System.Collections.Generic;
using System.Collections;

[System.Serializable]
public class WaterContainerData
{
    private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
    private static readonly int LegacyColorProperty = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorProperty = Shader.PropertyToID("_EmissionColor");

    private MaterialPropertyBlock lightBulbPropertyBlock;

    public bool isTurned = false;
    public GameObject waterContainer;

    #region Light Settings Constants
    [Header("Light Settings")]
    [Tooltip("Light bulb GameObject to change color")]
    public GameObject lightBulb;

    private static Color DefaultLockedBulbBaseColor => ColorFromHex("A10000");
    private static Color DefaultLockedBulbEmissionColor => ColorFromHsv(0f, 100f, 38f);
    private static Color DefaultLockedPointLightColor => ColorFromHex("FF1E1E");
    private static Color DefaultUnlockedBulbBaseColor => ColorFromHex("1DC814");
    private static Color DefaultUnlockedBulbEmissionColor => ColorFromHsv(145f, 100f, 13f);
    private static Color DefaultUnlockedPointLightColor => ColorFromHex("44A659");

    [Tooltip("Base color of the bulb material when the water container hasnt been turned.")]
    [ColorUsage(false, true)]
    public Color lockedLightBulbColor = DefaultLockedBulbBaseColor;

    [Tooltip("Emission color of the bulb material when the valve is unturned.")]
    [ColorUsage(true, true)]
    public Color lockedLightBulbEmissionColor = DefaultLockedBulbEmissionColor;

    [Tooltip("Base color of the bulb material when the valve is turned.")]
    [ColorUsage(false, true)]
    public Color unlockedLightBulbColor = DefaultUnlockedBulbBaseColor;

    [Tooltip("Emission color of the bulb material when the valve is turned.")]
    [ColorUsage(true, true)]
    public Color unlockedLightBulbEmissionColor = DefaultUnlockedBulbEmissionColor;

    [Tooltip("Light component on the water container to change color")]
    public Light doorLight;
    [Tooltip("Color of the light when the valve is unturned")]
    public Color lockedLightColor = DefaultLockedPointLightColor;
    [Tooltip("Color of the light when the valve is turned")]
    public Color unlockedLightColor = DefaultUnlockedPointLightColor;
    [Tooltip("Speed of the light color transition")]
    public float lightFadeSpeed = 2f;

    private static Color ColorFromHex(string hex)
    {
        if (ColorUtility.TryParseHtmlString($"#{hex}", out Color parsedColor))
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

    public void StartingLightColor()
    {
        ApplyLightState(lockedLightBulbColor, lockedLightBulbEmissionColor, lockedLightColor);
    }

    public void ApplyLightStateFromTurnedState()
    {
        if (isTurned)
            ApplyLightState(unlockedLightBulbColor, unlockedLightBulbEmissionColor, unlockedLightColor);
        else
            ApplyLightState(lockedLightBulbColor, lockedLightBulbEmissionColor, lockedLightColor);
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
                Debug.LogWarning("Water container light bulb object does not have a MeshRenderer component.");
                return null;
            }
        }
        else
        {
            Debug.LogWarning("Water container light bulb object is not assigned.");
            return null;
        }
    }

    private void ApplyLightState(Color bulbBaseColor, Color bulbEmissionColor, Color pointLightColor)
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

    public IEnumerator FadeLightBulbColor(
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

    public IEnumerator FadeLightStateToUnlocked(float duration)
    {
        MeshRenderer meshRenderer = GetLightMeshRenderer();

        if (duration <= 0f)
        {
            if (meshRenderer != null)
                ApplyBulbMaterialState(meshRenderer, unlockedLightBulbColor, unlockedLightBulbEmissionColor);

            if (doorLight != null)
                doorLight.color = unlockedLightColor;

            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);

            if (meshRenderer != null)
            {
                Color currentBaseColor = Color.Lerp(lockedLightBulbColor, unlockedLightBulbColor, t);
                Color currentEmissionColor = Color.Lerp(lockedLightBulbEmissionColor, unlockedLightBulbEmissionColor, t);
                ApplyBulbMaterialState(meshRenderer, currentBaseColor, currentEmissionColor);
            }

            if (doorLight != null)
                doorLight.color = Color.Lerp(lockedLightColor, unlockedLightColor, t);

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (meshRenderer != null)
            ApplyBulbMaterialState(meshRenderer, unlockedLightBulbColor, unlockedLightBulbEmissionColor);

        if (doorLight != null)
            doorLight.color = unlockedLightColor;
    }

    // Fade colors over time for smooth water container light transitions.
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
    
    #endregion

    [Header("Keycard Settings")]
    public GameObject keycard;
    [Tooltip("Keycard travel speed in local units per second.")]
    public float keycardMoveSpeed = 1f;
    public List<KeycardPositions> keycardPositions = new List<KeycardPositions>();

    public Vector3 GetKeycardInitialPos()
    {
        if (keycardPositions != null)
        {
            for (int i = 0; i < keycardPositions.Count; i++)
            {
                KeycardPositions segment = keycardPositions[i];
                if (segment != null)
                    return segment.initialPos;
            }
        }

        if (keycard != null)
            return keycard.transform.localPosition;

        return Vector3.zero;
    }

    public Vector3 GetKeycardEndPos()
    {
        if (keycardPositions != null)
        {
            for (int i = keycardPositions.Count - 1; i >= 0; i--)
            {
                KeycardPositions segment = keycardPositions[i];
                if (segment != null)
                    return segment.endPos;
            }
        }

        if (keycard != null)
            return keycard.transform.localPosition;

        return Vector3.zero;
    }
}

[System.Serializable]
public class KeycardPositions
{
    [Tooltip("Local start point for this keycard segment.")]
    public Vector3 initialPos;
    [Tooltip("Local end point for this keycard segment.")]
    public Vector3 endPos;
    [Tooltip("Pipe liquid effects to trigger when the keycard starts traveling through this segment.")]
    public List<Wobble> flowPipes = new List<Wobble>();
    [Tooltip("Pause in seconds after this segment before moving to the next one.")]
    public bool playWaterFilterSFXDuringThisSegment = false;
    public bool playWaterDrumSFXDuringThisSegment = false;
    public bool keepPlayingWaterSwishing = false;
    public bool playWaterExhaustandSuccessSFXAtStartOfThisSegment = true;
    public float delayBeforeNextPipe;
    public float startingDelay;
    public float properyKeycardRotationForThisPath;
}
public class WaterTreatmentPuzzle : PuzzlePart, IConsoleSelectable
{
    [Tooltip("Ordered water containers used by this puzzle.")]
    public List<WaterContainerData> waterContainers;
    [Header("Debug")]
    [Tooltip("Enables verbose runtime logs for puzzle interaction and keycard movement.")]
    [SerializeField] private bool verboseDebug = true;
    [Header("Keycard Water Motion")]
    [Tooltip("Maximum radial wobble distance from the segment centerline in local space.")]
    [SerializeField] private float pipeRadius = 0.08f;
    [Tooltip("How strongly the keycard is pulled back toward the centerline.")]
    [SerializeField] private float springStrength = 35f;
    [Tooltip("Drag applied to wobble velocity; higher values reduce jitter.")]
    [SerializeField] private float damping = 9f;
    [Tooltip("Strength of pseudo-random flow turbulence pushing the keycard sideways.")]
    [SerializeField] private float turbulence = 6f;
    [Tooltip("Bounce amount when wobble hits pipe radius. 0 = no bounce, 1 = full bounce.")]
    [SerializeField] private float bounceRestitution = 0.35f;
    [Tooltip("Speed of turbulence pattern changes over time.")]
    [SerializeField] private float noiseFrequency = 1.7f;
    [Tooltip("If enabled, keycard rotates to face flow and adds subtle roll from lateral motion.")]
    [SerializeField] private bool applyFlowRoll = true;
    [Tooltip("Scale of roll rotation caused by sideways wobble velocity.")]
    [SerializeField] private float flowRollAmount = 12f;
    [Tooltip("Current expected valve index in the sequence. Primarily for debugging.")]
    [SerializeField] private int currentWaterContainerIndex = 0;

    [Header("SFX")]
    [SerializeField] private AudioClip valveTurnSFX;
    [SerializeField] private AudioClip valveTurnFailSFX;
    [SerializeField] private AudioClip keycardInWaterFilterSFX;
    [SerializeField] private AudioClip keycardInWaterDrumSFX;
    [SerializeField] private AudioClip keycardInWaterPipeSFX;
    [SerializeField] private AudioClip waterExhaustSFX;
    [SerializeField] private AudioClip filterSuccessSfx;
    [SerializeField] private AudioClip gotToEndOfPathSFX;
    [SerializeField] private AudioClip pipeHumSFX;
    [SerializeField] private AudioClip turnOnPuzzle;

    private Coroutine keycardMovementRoutine;
    private Vector2 radialOffset;
    private Vector2 radialVelocity;
    private float noiseSeedX;
    private float noiseSeedY;

    private string DebugPrefix => $"[WaterTreatmentPuzzle:{name}]";

    private void LogVerbose(string message)
    {
        if (verboseDebug)
            Debug.Log($"{DebugPrefix} {message}");
    }

    private void LogWarningVerbose(string message)
    {
        if (verboseDebug)
            Debug.LogWarning($"{DebugPrefix} {message}");
    }

    private void OnEnable()
    {
        LogVerbose($"OnEnable | activeInHierarchy={gameObject.activeInHierarchy} enabled={enabled}");
    }

    private void Start()
    {
        LogVerbose("Start called.");
    }

    private void Awake()
    {
        LogVerbose($"Awake | waterContainers={(waterContainers == null ? -1 : waterContainers.Count)} currentIndex={currentWaterContainerIndex}");

        if (waterContainers == null || waterContainers.Count == 0)
        {
            Debug.LogError("WaterTreatmentPuzzle: No water containers assigned.");
            return;
        }

        isCompleted = false;

        int firstUnturnedIndex = -1;
        for (int i = 0; i < waterContainers.Count; i++)
        {
            WaterContainerData containerData = waterContainers[i];
            if (containerData != null)
            {
                containerData.ApplyLightStateFromTurnedState();

                if (firstUnturnedIndex < 0 && !containerData.isTurned)
                    firstUnturnedIndex = i;
            }
        }

        StartKeycardFromFirstInitialPos();
        SetKeycardInteractable(false, waterContainers[currentWaterContainerIndex]);

        currentWaterContainerIndex = firstUnturnedIndex >= 0 ? firstUnturnedIndex : waterContainers.Count;
        LogVerbose($"Awake complete | firstUnturnedIndex={firstUnturnedIndex} currentIndex={currentWaterContainerIndex}");
    }

    
    private void StartKeycardFromFirstInitialPos()
    {
        if (waterContainers == null || waterContainers.Count == 0)
        {
            LogWarningVerbose("StartKeycardFromFirstInitialPos aborted: no water containers.");
            return;
        }

        if (currentWaterContainerIndex >= waterContainers.Count)
        {
            LogWarningVerbose("StartKeycardFromFirstInitialPos aborted: all containers are turned.");
            return;
        }

        WaterContainerData containerData = waterContainers[currentWaterContainerIndex];
        if (containerData == null)
        {
            LogWarningVerbose("StartKeycardFromFirstInitialPos aborted: current container data is null.");
            return;
        }

        if (containerData.keycard != null)
        {
            containerData.keycard.transform.localPosition = containerData.GetKeycardInitialPos();
            LogVerbose("StartKeycardFromFirstInitialPos set keycard to initial position.");
        }
        else
        {
            LogWarningVerbose("StartKeycardFromFirstInitialPos could not find keycard GameObject on current container.");
        }
    }

    public void TriggerCurrentValveFromInspector()
    {
        LogVerbose($"TriggerCurrentValveFromInspector called | currentIndex={currentWaterContainerIndex}");
        //TurnValveOnWaterContainer(currentWaterContainerIndex);
    }

    public override void StartPuzzle()
    {
        LogVerbose("StartPuzzle called.");
    }

    public override void EndPuzzle()
    {
        LogVerbose("EndPuzzle called.");
    }

    public override void ConsoleInteracted()
    {
        LogVerbose($"ConsoleInteracted() called | currentIndex={currentWaterContainerIndex}");
    }

    public void ConsoleInteracted(PuzzleInteraction interaction)
    {
        if (interaction == null)
        {
            LogWarningVerbose("ConsoleInteracted(PuzzleInteraction) received null sender; using current index.");
            PlaySFX(valveTurnFailSFX);
            return;
        }

        LogVerbose($"ConsoleInteracted(PuzzleInteraction) called | sender={interaction.name} consoleIndex={interaction.ConsoleIndex}");
       TurnValveOnWaterContainer(interaction.ConsoleIndex);
    }

    private void PlaySFX(AudioClip clip)
    {
        if (clip == null)
            return;

        SoundManager.Instance.sfxSource.PlayOneShot(clip);
    }

    private IEnumerator PlayStartingSFX()
    {
        SoundManager.Instance.puzzleSource.PlayOneShot(turnOnPuzzle);
        yield return new WaitForSeconds(turnOnPuzzle.length);
    }

    public void TurnValveOnWaterContainer(int containerIndex)
    {

        if (containerIndex < 0 || containerIndex >= waterContainers.Count)
        {
            Debug.LogError($"Container index {containerIndex} is out of range.");
            PlaySFX(valveTurnFailSFX);
            return;
        }

        if (containerIndex != currentWaterContainerIndex)
        {
            Debug.LogWarning($"{DebugPrefix} Container index {containerIndex} is out of sequence. Expected index: {currentWaterContainerIndex}.");
            PlaySFX(valveTurnFailSFX);
            return;
        }

        WaterContainerData containerData = waterContainers[containerIndex];
        if (containerData == null)
        {
            Debug.LogError($"Container data at index {containerIndex} is null.");
            PlaySFX(valveTurnFailSFX);
            return;
        }

        if (containerData.isTurned)
        {
            Debug.Log($"{DebugPrefix} Container at index {containerIndex} is already turned.");
            PlaySFX(valveTurnFailSFX);
            return;
        }

        StartCoroutine(PlayStartingSFX());

        SoundManager.Instance.puzzleSource.clip = pipeHumSFX;
        SoundManager.Instance.puzzleSource.Play();
        Debug.Log($"{DebugPrefix} TurnValveOnWaterContainer called with index {containerIndex}.");

        Debug.Log($"{DebugPrefix} Attempting to turn valve on water container at index {containerIndex}. Current expected index: {currentWaterContainerIndex}.");
        LogVerbose($"Before validation | activeInHierarchy={gameObject.activeInHierarchy} enabled={enabled} listCount={(waterContainers == null ? -1 : waterContainers.Count)}");
        PlaySFX(valveTurnSFX);

        LogVerbose($"TurnValveOnWaterContainer accepted | index={containerIndex}");
        UpdateWaterContainerState(containerData);
    }

    private void UpdateWaterContainerState(WaterContainerData containerData)
    {
        LogVerbose($"UpdateWaterContainerState start | isTurned(before)={containerData.isTurned} currentIndex(before)={currentWaterContainerIndex}");
        containerData.isTurned = true;
        LogVerbose($"UpdateWaterContainerState progressed | currentIndex(after)={currentWaterContainerIndex}");

        StartCoroutine(containerData.FadeLightStateToUnlocked(containerData.lightFadeSpeed));

        if (keycardMovementRoutine != null)
        {
            LogVerbose("Stopping existing keycard movement coroutine.");
            StopCoroutine(keycardMovementRoutine);
        }

        keycardMovementRoutine = StartCoroutine(MoveKeycardPath(containerData));
        LogVerbose("Started new keycard movement coroutine.");
    }

    private IEnumerator MoveKeycardPath(WaterContainerData containerData)
    {
        if (containerData.keycard == null)
        {
            LogWarningVerbose("MoveKeycardPath aborted because keycard is null.");
            keycardMovementRoutine = null;
            yield break;
        }

        Transform keycardTransform = containerData.keycard.transform;
    radialOffset = Vector2.zero;
    radialVelocity = Vector2.zero;
    noiseSeedX = UnityEngine.Random.value * 1000f;
    noiseSeedY = UnityEngine.Random.value * 1000f + 41.73f;

        if (containerData.keycardPositions != null && containerData.keycardPositions.Count > 0)
        {
            LogVerbose($"MoveKeycardPath using segmented path | segments={containerData.keycardPositions.Count}");
            // Move through authored segments in order so designers can route through multiple pipes/filters.
            for (int i = 0; i < containerData.keycardPositions.Count; i++)
            {
                KeycardPositions segment = containerData.keycardPositions[i];

                keycardTransform.localRotation = Quaternion.Euler(keycardTransform.localRotation.eulerAngles.x, segment.properyKeycardRotationForThisPath, keycardTransform.localRotation.eulerAngles.z);

                if (segment == null)
                {
                    LogWarningVerbose($"MoveKeycardPath segment {i} is null; skipping.");
                    continue;
                }

                if (segment.startingDelay > 0f)
                {
                    LogVerbose($"MoveKeycardPath segment {i} starting delay | delay={segment.startingDelay}");
                    yield return new WaitForSeconds(segment.startingDelay);
                }

                LogVerbose($"MoveKeycardPath segment {i} | start={segment.initialPos} end={segment.endPos} delay={segment.delayBeforeNextPipe}");
                TriggerSegmentFlow(segment, i);

                if (segment.playWaterExhaustandSuccessSFXAtStartOfThisSegment)
                {
                    PlaySFX(waterExhaustSFX);
                    PlaySFX(filterSuccessSfx);
                }
                SoundManager.Instance.sfxSource.clip = null;

                yield return MoveKeycardBetween(keycardTransform, segment.initialPos, segment.endPos, containerData.keycardMoveSpeed);

                if (segment.playWaterFilterSFXDuringThisSegment)
                {
                    SoundManager.Instance.sfxSource.clip = keycardInWaterFilterSFX;
                    SoundManager.Instance.sfxSource.Play();
                }
                else if (segment.playWaterDrumSFXDuringThisSegment)
                {
                    SoundManager.Instance.sfxSource.clip = keycardInWaterDrumSFX;
                    SoundManager.Instance.sfxSource.Play();
                }

                if (segment.delayBeforeNextPipe > 0f)
                    yield return new WaitForSeconds(segment.delayBeforeNextPipe);
            }
        }
        else
        {
            LogWarningVerbose("MoveKeycardPath has no segmented path; using fallback start/end positions.");
            yield return MoveKeycardBetween(keycardTransform, containerData.GetKeycardInitialPos(), containerData.GetKeycardEndPos(), containerData.keycardMoveSpeed);
        }

        // Always stop SFX after movement completes
        SoundManager.Instance.sfxSource.Stop();
        SoundManager.Instance.puzzleSource.Stop();

        // After the full path finishes, place the keycard at the next valve's start position (if not already at the final destination)
        if (currentWaterContainerIndex < waterContainers.Count && waterContainers[currentWaterContainerIndex] != null)
        {
            LogVerbose($"MoveKeycardPath handoff | nextIndex={currentWaterContainerIndex}");
            AdvanceToNextValidContainerIndex();
            keycardTransform.localPosition = waterContainers[currentWaterContainerIndex].GetKeycardInitialPos();
            PlaySFX(gotToEndOfPathSFX);
        }

        

        // If puzzle is finished, set keycard interactable
        if (currentWaterContainerIndex >= waterContainers.Count && waterContainers != null && waterContainers.Count > 0)
        {
            Debug.Log($"{DebugPrefix} MoveKeycardPath reached end of path; all containers turned. Setting final keycard interactable.");
            SetKeycardInteractable(true, waterContainers[waterContainers.Count - 1]);
            PlaySFX(gotToEndOfPathSFX);
        }

        keycardMovementRoutine = null;
        LogVerbose("MoveKeycardPath complete.");
    }

    private void TriggerSegmentFlow(KeycardPositions segment, int segmentIndex)
    {
        if (segment == null || segment.flowPipes == null || segment.flowPipes.Count == 0)
            return;

        for (int i = 0; i < segment.flowPipes.Count; i++)
        {
            Wobble wobble = segment.flowPipes[i];
            if (wobble == null)
            {
                LogWarningVerbose($"MoveKeycardPath segment {segmentIndex} has a missing flow pipe reference at slot {i}.");
                continue;
            }

            wobble.FlowWater();
        }
    }

    private void SetKeycardInteractable(bool interactable, WaterContainerData containerData)
    {
        InteractionManager keycardInteraction = containerData.keycard.GetComponent<InteractionManager>();
        if (keycardInteraction != null)
        {
            keycardInteraction.SetInteractionEnabled(interactable);
            isCompleted = true;
        }
    }

    private Vector3 GetAbsoluteFinalKeyPosition(WaterContainerData containerData)
    {
        Vector3 finalPosition;

        if (containerData.keycardPositions != null && containerData.keycardPositions.Count > 0)
        {
            KeycardPositions lastSegment = containerData.keycardPositions[containerData.keycardPositions.Count - 1];
            finalPosition = lastSegment.endPos;
            
            return finalPosition;
        }
        else
        {
            finalPosition = containerData.GetKeycardEndPos();
            return finalPosition;
        }
    }

    private IEnumerator MoveKeycardBetween(Transform keycardTransform, Vector3 startPos, Vector3 endPos, float moveSpeed)
    {
        Vector3 flowVector = endPos - startPos;
        Vector3 flowDirection = flowVector.sqrMagnitude > 0.000001f ? flowVector.normalized : Vector3.forward;
        float segmentDistance = flowVector.magnitude;
        // Keep travel speed constant across all segment lengths.
        float segmentDuration = moveSpeed > 0f ? segmentDistance / moveSpeed : 0f;
        SoundManager.Instance.sfxSource.clip = keycardInWaterPipeSFX;
        SoundManager.Instance.sfxSource.Play();

        if (segmentDuration <= 0f)
        {
            StepWaterWobble(keycardTransform, endPos, flowDirection, 0f);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < segmentDuration)
        {
            float t = Mathf.Clamp01(elapsed / segmentDuration);
            Vector3 centerPos = Vector3.Lerp(startPos, endPos, t);
            // Layer water wobble on top of deterministic centerline movement.
            StepWaterWobble(keycardTransform, centerPos, flowDirection, Time.deltaTime);
            elapsed += Time.deltaTime;
            yield return null;
        }

        StepWaterWobble(keycardTransform, endPos, flowDirection, 0f);

    }

    private void AdvanceToNextValidContainerIndex()
    {
        int initialIndex = currentWaterContainerIndex;
        while (currentWaterContainerIndex < waterContainers.Count)
        {
            WaterContainerData containerData = waterContainers[currentWaterContainerIndex];
            if (containerData != null && !containerData.isTurned)
                break;

            currentWaterContainerIndex++;
        }

        if (verboseDebug && initialIndex != currentWaterContainerIndex)
            Debug.Log($"{DebugPrefix} AdvanceToNextValidContainerIndex moved from {initialIndex} to {currentWaterContainerIndex}.");
    }

    private void StepWaterWobble(Transform keycardTransform, Vector3 centerPos, Vector3 flowDirection, float dt)
    {
        // Build an orthonormal basis around flow direction to simulate radial motion in pipe cross-section.
        Vector3 upReference = Mathf.Abs(Vector3.Dot(flowDirection, Vector3.up)) > 0.9f ? Vector3.right : Vector3.up;
        Vector3 right = Vector3.Normalize(Vector3.Cross(upReference, flowDirection));
        Vector3 up = Vector3.Normalize(Vector3.Cross(flowDirection, right));

        if (dt > 0f)
        {
            float noiseTime = Time.time * noiseFrequency;
            float nx = (Mathf.PerlinNoise(noiseSeedX, noiseTime) - 0.5f) * 2f;
            float ny = (Mathf.PerlinNoise(noiseSeedY, noiseTime) - 0.5f) * 2f;
            Vector2 turbulenceForce = new Vector2(nx, ny) * turbulence;

            // Damped spring + turbulence approximation for water-driven wobble.
            Vector2 acceleration = turbulenceForce - springStrength * radialOffset - damping * radialVelocity;
            radialVelocity += acceleration * dt;
            radialOffset += radialVelocity * dt;

            float radius = Mathf.Max(0f, pipeRadius);
            float mag = radialOffset.magnitude;
            if (mag > radius && mag > 0.00001f)
            {
                Vector2 normal = radialOffset / mag;
                radialOffset = normal * radius;

                // Reflect outward velocity component to create soft wall bounces inside the pipe radius.
                float outwardVelocity = Vector2.Dot(radialVelocity, normal);
                if (outwardVelocity > 0f)
                    radialVelocity -= (1f + bounceRestitution) * outwardVelocity * normal;
            }
        }

        Vector3 wobbleOffset = right * radialOffset.x + up * radialOffset.y;
        keycardTransform.localPosition = centerPos + wobbleOffset;

        if (applyFlowRoll)
        {
            Quaternion lookRotation = Quaternion.LookRotation(flowDirection, up);
            float roll = Mathf.Clamp(radialVelocity.x * flowRollAmount, -25f, 25f);
            keycardTransform.localRotation = lookRotation * Quaternion.Euler(0f, 0f, roll);
        }
    }
}
