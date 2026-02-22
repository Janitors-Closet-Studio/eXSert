using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GuidelineLightRow : MonoBehaviour
{
    // Commented by Kyle Woo:
    // GuidelineLightRow runs a "chasing" animation across a list of GuidelineLightBulb references.
    // You trigger it by calling PlayOnce(), PlayIndefinitely(), or Play().
    // Internally, it uses a coroutine to step through bulbs over time.

    public enum Direction
    {
        Forward,
        Backward,
    }

    [Header("Bulbs")]
    [Tooltip(
        "Order matters: bulbs will chase in this list order (or reversed if Direction is Backward)."
    )]
    [SerializeField]
    private List<GuidelineLightBulb> bulbs = new List<GuidelineLightBulb>();

    [Tooltip("How many bulbs turn on together per step (e.g., 2 for L/R pairs).")]
    [Min(1)]
    [SerializeField]
    private int bulbsPerStep = 2;

    [Tooltip(
        "How many steps a group stays on (creates a trailing effect). 1 = only the current step stays on."
    )]
    [Min(1)]
    [SerializeField]
    private int keepOnSteps = 2;

    [Header("Timing")]
    [Tooltip("Seconds between each step in the chase.")]
    [Min(0.01f)]
    [SerializeField]
    private float stepSeconds = 0.12f;

    [Tooltip("How many times to run through the row when Play() is called.")]
    [Min(1)]
    [SerializeField]
    private int passes = 1;

    [Header("Behavior")]
    // Direction controls the traversal order of the chase.
    [SerializeField]
    private Direction direction = Direction.Forward;

    [Tooltip("If true, turns all bulbs off right before starting a new run.")]
    [SerializeField]
    private bool resetAllOffOnPlay = true;

    [Tooltip(
        "If true, any currently running chase is stopped and restarted when Play() is called."
    )]
    [SerializeField]
    private bool restartIfAlreadyPlaying = true;

    [Header("Pauses")]
    [Tooltip(
        "If enabled, the chase will pause after it lights the last group in the row, before continuing (or before turning off at the end)."
    )]
    [SerializeField]
    private bool pauseAtRowEnd = false;

    [Tooltip("Seconds to pause at the end of the row when Pause At Row End is enabled.")]
    [Min(0f)]
    [SerializeField]
    private float rowEndPauseSeconds = 1.0f;

    [Header("Latch On")]
    [Tooltip(
        "If enabled, LatchAllOn() will also dim the underlying Light intensity on each bulb (useful for completion state)."
    )]
    [SerializeField]
    private bool dimLightsWhenLatched = true;

    [Tooltip(
        "Intensity to apply to Light components while latched on (0.05 works well in this project)."
    )]
    [Min(0f)]
    [SerializeField]
    private float latchedLightIntensity = 0.05f;

    private Coroutine _playRoutine;

    // True while the chase coroutine is running.
    public bool IsPlaying => _playRoutine != null;

    // Starts the chase for the configured number of passes.
    public void Play()
    {
        if (!isActiveAndEnabled)
            return;

        if (_playRoutine != null)
        {
            if (!restartIfAlreadyPlaying)
                return;

            StopCoroutine(_playRoutine);
            _playRoutine = null;
        }

        _playRoutine = StartCoroutine(PlayRoutine());
    }

    // Convenience: runs exactly one full chase pass.
    public void PlayOnce()
    {
        if (!isActiveAndEnabled)
            return;

        if (_playRoutine != null)
        {
            if (!restartIfAlreadyPlaying)
                return;

            StopCoroutine(_playRoutine);
            _playRoutine = null;
        }

        _playRoutine = StartCoroutine(PlayOnceRoutine());
    }

    // Convenience: loops the chase until Stop() / TurnOff() is called or the object is disabled.
    public void PlayIndefinitely()
    {
        if (!isActiveAndEnabled)
            return;

        if (_playRoutine != null)
        {
            if (!restartIfAlreadyPlaying)
                return;

            StopCoroutine(_playRoutine);
            _playRoutine = null;
        }

        _playRoutine = StartCoroutine(PlayIndefinitelyRoutine());
    }

    // Stops the chase coroutine. Does NOT force bulbs off; whatever is currently lit stays lit.
    public void Stop()
    {
        if (_playRoutine != null)
        {
            StopCoroutine(_playRoutine);
            _playRoutine = null;
        }
    }

    // Stops the chase and forces all bulbs off.
    public void TurnOff()
    {
        Stop();
        SetAllOff();
    }

    // Turns ALL bulbs on and leaves them on until TurnOff() is called.
    // Intended for “puzzle completed” / “path revealed” states.
    public void LatchAllOn()
    {
        Stop();

        if (bulbs == null)
            return;

        for (int i = 0; i < bulbs.Count; i++)
        {
            var bulb = bulbs[i];
            if (bulb == null)
                continue;

            bulb.SetOn();

            if (dimLightsWhenLatched)
                bulb.SetLightIntensity(latchedLightIntensity);
        }
    }

    // Helper: immediately turns every bulb in the list off.
    public void SetAllOff()
    {
        if (bulbs == null)
            return;

        for (int i = 0; i < bulbs.Count; i++)
        {
            if (bulbs[i] != null)
                bulbs[i].SetOff();
        }
    }

    private IEnumerator PlayRoutine()
    {
        yield return RunChase(passes);
        _playRoutine = null;
    }

    private IEnumerator PlayOnceRoutine()
    {
        yield return RunChase(1);
        _playRoutine = null;
    }

    private IEnumerator PlayIndefinitelyRoutine()
    {
        while (true)
        {
            yield return RunChase(1);
        }
    }

    private IEnumerator RunChase(int passCount)
    {
        // Optionally clear everything before starting.
        if (resetAllOffOnPlay)
            SetAllOff();

        if (bulbs == null || bulbs.Count == 0)
            yield break;

        var litGroups = new Queue<int>();

        for (int pass = 0; pass < passCount; pass++)
        {
            // Step through the bulb list in the chosen direction.
            if (direction == Direction.Forward)
            {
                for (int i = 0; i < bulbs.Count; i += bulbsPerStep)
                {
                    StepToGroup(i, litGroups);
                    yield return new WaitForSeconds(stepSeconds);
                }
            }
            else
            {
                int start = ((bulbs.Count - 1) / bulbsPerStep) * bulbsPerStep;
                for (int i = start; i >= 0; i -= bulbsPerStep)
                {
                    StepToGroup(i, litGroups);
                    yield return new WaitForSeconds(stepSeconds);
                }
            }

            // Turn off any remaining lit groups at the end of this pass.
            while (litGroups.Count > 0)
            {
                TurnGroupOff(litGroups.Dequeue());
            }

            // Pause after reaching the end of the row.
            // Per design: lights should be OFF during the pause, then the next pass begins.
            if (pauseAtRowEnd && rowEndPauseSeconds > 0f)
                yield return new WaitForSeconds(rowEndPauseSeconds);
        }

        // Safety: ensure no groups remain lit (should already be empty after per-pass cleanup).
        while (litGroups.Count > 0)
            TurnGroupOff(litGroups.Dequeue());
    }

    private void StepToGroup(int groupStartIndex, Queue<int> litGroups)
    {
        if (groupStartIndex < 0 || groupStartIndex >= bulbs.Count)
            return;

        // Turns ON one "group" (bulbsPerStep bulbs) starting at groupStartIndex.
        // Turn on this group's bulbs.
        for (int i = 0; i < bulbsPerStep; i++)
        {
            int idx = groupStartIndex + i;
            if (idx < 0 || idx >= bulbs.Count)
                continue;

            if (bulbs[idx] != null)
                bulbs[idx].SetOn();
        }

        if (litGroups == null)
            return;

        // Avoid duplicates if something calls the same step twice.
        if (litGroups.Count == 0 || litGroups.Peek() != groupStartIndex)
            litGroups.Enqueue(groupStartIndex);

        // Maintain a small trail of lit groups.
        while (litGroups.Count > Mathf.Max(1, keepOnSteps))
        {
            TurnGroupOff(litGroups.Dequeue());
        }
    }

    private void TurnGroupOff(int groupStartIndex)
    {
        if (bulbs == null || bulbs.Count == 0)
            return;
        if (groupStartIndex < 0 || groupStartIndex >= bulbs.Count)
            return;

        // Turns OFF one "group" (bulbsPerStep bulbs) starting at groupStartIndex.
        for (int i = 0; i < bulbsPerStep; i++)
        {
            int idx = groupStartIndex + i;
            if (idx < 0 || idx >= bulbs.Count)
                continue;

            if (bulbs[idx] != null)
                bulbs[idx].SetOff();
        }
    }

    [ContextMenu("Collect Bulbs From Children")]
    private void CollectBulbsFromChildren()
    {
        // Editor convenience: finds all GuidelineLightBulb components under this object.
        // NOTE: Unity returns them in hierarchy order; you can reorder the list manually if needed.
        bulbs.Clear();
        GetComponentsInChildren(true, bulbs);
    }
}
