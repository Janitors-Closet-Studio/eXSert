using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections;

/// <summary>
/// Mirrors a working slider's value onto a visual-only slider and exposes optional controller input.
/// Attach this to the interactive slider (the one receiving pointer/controller events).
/// </summary>
[RequireComponent(typeof(Slider))]
public class SliderValueProxy : MonoBehaviour
{
    [Header("Visual Slider (Optional)")]
    [SerializeField, Tooltip("Slider that visually mirrors the real slider's value.")]
    private Slider visualSlider;
    [SerializeField, Tooltip("When enabled, the visual slider is forced non-interactable.")]
    private bool disableVisualInteraction = true;

    [Header("Controller Input")]
    [SerializeField, Tooltip("Input action triggered by RB / Right Bumper (or any 'increase' binding).")]
    private InputActionReference increaseAction;
    [SerializeField, Tooltip("Input action triggered by LB / Left Bumper (or any 'decrease' binding).")]
    private InputActionReference decreaseAction;
    [SerializeField, Range(0.01f, 1f), Tooltip("Step size as a percentage of the slider's range when using controller input.")]
    private float controllerStepNormalized = 0.1f;
    [SerializeField, Min(0f), Tooltip("Delay before repeated slider changes begin while an input is held.")]
    private float holdRepeatDelay = 0.35f;
    [SerializeField, Min(0.01f), Tooltip("Interval between repeated slider changes while an input is held.")]
    private float holdRepeatInterval = 0.08f;

    private Slider sourceSlider;
    private Coroutine increaseRepeatRoutine;
    private Coroutine decreaseRepeatRoutine;

    private void Awake()
    {
        sourceSlider = GetComponent<Slider>();
        if (visualSlider != null)
        {
            if (disableVisualInteraction)
                visualSlider.interactable = false;
            visualSlider.minValue = sourceSlider.minValue;
            visualSlider.maxValue = sourceSlider.maxValue;
            visualSlider.wholeNumbers = sourceSlider.wholeNumbers;
            visualSlider.value = sourceSlider.value;
        }
    }

    private void OnEnable()
    {
        if (sourceSlider != null)
            sourceSlider.onValueChanged.AddListener(SyncVisualSlider);

        SubscribeInput(increaseAction, OnIncreaseStarted, OnIncreaseCanceled);
        SubscribeInput(decreaseAction, OnDecreaseStarted, OnDecreaseCanceled);
    }

    private void OnDisable()
    {
        if (sourceSlider != null)
            sourceSlider.onValueChanged.RemoveListener(SyncVisualSlider);

        UnsubscribeInput(increaseAction, OnIncreaseStarted, OnIncreaseCanceled);
        UnsubscribeInput(decreaseAction, OnDecreaseStarted, OnDecreaseCanceled);
        StopRepeat(ref increaseRepeatRoutine);
        StopRepeat(ref decreaseRepeatRoutine);
    }

    private void SyncVisualSlider(float value)
    {
        if (visualSlider != null)
            visualSlider.value = value;
    }

    private void SubscribeInput(
        InputActionReference actionReference,
        Action<InputAction.CallbackContext> startHandler,
        Action<InputAction.CallbackContext> cancelHandler)
    {
        if (actionReference == null || actionReference.action == null)
            return;

        actionReference.action.started += startHandler;
        actionReference.action.canceled += cancelHandler;
        if (!actionReference.action.enabled)
            actionReference.action.Enable();
    }

    private void UnsubscribeInput(
        InputActionReference actionReference,
        Action<InputAction.CallbackContext> startHandler,
        Action<InputAction.CallbackContext> cancelHandler)
    {
        if (actionReference == null || actionReference.action == null)
            return;

        actionReference.action.started -= startHandler;
        actionReference.action.canceled -= cancelHandler;
    }

    private void OnIncreaseStarted(InputAction.CallbackContext context)
    {
        StopRepeat(ref decreaseRepeatRoutine);
        StartRepeat(ref increaseRepeatRoutine, 1f);
    }

    private void OnDecreaseStarted(InputAction.CallbackContext context)
    {
        StopRepeat(ref increaseRepeatRoutine);
        StartRepeat(ref decreaseRepeatRoutine, -1f);
    }

    private void OnIncreaseCanceled(InputAction.CallbackContext context)
    {
        StopRepeat(ref increaseRepeatRoutine);
    }

    private void OnDecreaseCanceled(InputAction.CallbackContext context)
    {
        StopRepeat(ref decreaseRepeatRoutine);
    }

    private void StartRepeat(ref Coroutine repeatRoutine, float direction)
    {
        StopRepeat(ref repeatRoutine);
        AdjustSlider(direction);
        repeatRoutine = StartCoroutine(RepeatAdjust(direction));
    }

    private void StopRepeat(ref Coroutine repeatRoutine)
    {
        if (repeatRoutine == null)
            return;

        StopCoroutine(repeatRoutine);
        repeatRoutine = null;
    }

    private IEnumerator RepeatAdjust(float direction)
    {
        if (holdRepeatDelay > 0f)
            yield return new WaitForSecondsRealtime(holdRepeatDelay);

        float interval = Mathf.Max(0.01f, holdRepeatInterval);
        while (true)
        {
            AdjustSlider(direction);
            yield return new WaitForSecondsRealtime(interval);
        }
    }

    private void AdjustSlider(float direction)
    {
        if (sourceSlider == null)
            return;

        float range = sourceSlider.maxValue - sourceSlider.minValue;
        float stepSize = range * controllerStepNormalized;

        if (sourceSlider.wholeNumbers)
            stepSize = Mathf.Max(1f, Mathf.Round(stepSize));

        float newValue = Mathf.Clamp(sourceSlider.value + (stepSize * direction), sourceSlider.minValue, sourceSlider.maxValue);
        sourceSlider.value = sourceSlider.wholeNumbers ? Mathf.Round(newValue) : newValue;
    }
}
