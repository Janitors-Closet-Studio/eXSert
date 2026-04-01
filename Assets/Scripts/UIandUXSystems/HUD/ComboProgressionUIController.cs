using System;
using System.Collections.Generic;
using UnityEngine;
using Utilities.Combat.Attacks;

public class ComboProgressionUIController : MonoBehaviour
{
    [Serializable]
    public class ComboNode
    {
        public ComboStep step;
        public GameObject icon;
    }

    [Serializable]
    public class ComboLink
    {
        public ComboStep from;
        public ComboStep to;
        public GameObject arrow;
    }

    public enum ComboStep
    {
        S1,
        S2,
        S3,
        S4,
        S5,
        A1,
        A2,
        A3
    }

    [Header("References")]
    [SerializeField] private TierComboManager tierComboManager;

    [Header("Timing")]
    [SerializeField, Range(0.1f, 5f)] private float postComboHideDelay = 1f;

    [Header("Pop Animation")]
    [SerializeField, Range(1f, 2f)] private float popScale = 1.15f;
    [SerializeField, Range(0.05f, 0.5f)] private float popDuration = 0.12f;

    [Header("Icons")]
    [SerializeField] private List<ComboNode> icons = new List<ComboNode>();

    [Header("Arrows")]
    [SerializeField] private List<ComboLink> arrows = new List<ComboLink>();

    private readonly List<ComboStep> progression = new List<ComboStep>();
    private readonly Dictionary<ComboStep, GameObject> iconLookup = new Dictionary<ComboStep, GameObject>();
    private readonly Dictionary<(ComboStep, ComboStep), GameObject> arrowLookup = new Dictionary<(ComboStep, ComboStep), GameObject>();
    private readonly Dictionary<GameObject, Vector3> baseScales = new Dictionary<GameObject, Vector3>();
    private readonly Dictionary<GameObject, Coroutine> popRoutines = new Dictionary<GameObject, Coroutine>();
    private Coroutine hideCoroutine;

    private void Awake()
    {
        BuildLookups();
    }

    private void OnEnable()
    {
        if (!SettingsManager.Instance.comboProgression)
        {
            ResetDisplay();
            return;
        }

        PlayerAttackManager.OnAttack += HandleAttack;
        if (tierComboManager == null)
            tierComboManager = FindObjectOfType<TierComboManager>(true);

        if (tierComboManager != null)
        {
            tierComboManager.ComboResetDetailed += HandleComboResetDetailed;
        }

        ResetDisplay();
    }

    private void OnDisable()
    {
        PlayerAttackManager.OnAttack -= HandleAttack;
        if (tierComboManager != null)
        {
            tierComboManager.ComboResetDetailed -= HandleComboResetDetailed;
        }
    }

    private void BuildLookups()
    {
        iconLookup.Clear();
        for (int i = 0; i < icons.Count; i++)
        {
            if (icons[i].icon == null)
                continue;

            iconLookup[icons[i].step] = icons[i].icon;
            CacheBaseScale(icons[i].icon);
        }

        arrowLookup.Clear();
        for (int i = 0; i < arrows.Count; i++)
        {
            if (arrows[i].arrow == null)
                continue;

            arrowLookup[(arrows[i].from, arrows[i].to)] = arrows[i].arrow;
            CacheBaseScale(arrows[i].arrow);
        }
    }

    private void HandleAttack(PlayerAttack attack)
    {
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
            return;

        if (attack == null)
            return;

        CancelHideTimer();

        if (!TryResolveStep(attack, out ComboStep step))
            return;

        if (progression.Count > 0 && IsTerminal(progression[progression.Count - 1]))
        {
            ResetDisplay();
        }

        if (progression.Count == 0)
        {
            ShowStep(step);
            progression.Add(step);
            return;
        }

        ComboStep previous = progression[progression.Count - 1];
        if (previous == step)
            return;

        ShowStep(step);
        ShowArrow(previous, step);
        progression.Add(step);

        if (IsTerminal(step))
            StartHideTimer();
    }

    private void HandleComboResetDetailed(TierComboManager.ComboResetReason reason)
    {
        if (reason == TierComboManager.ComboResetReason.Finisher)
        {
            StartHideTimer();
            return;
        }

        ResetDisplay();
    }

    private void ResetDisplay()
    {
        CancelHideTimer();
        progression.Clear();
        foreach (var entry in iconLookup)
            entry.Value.SetActive(false);

        foreach (var entry in arrowLookup)
            entry.Value.SetActive(false);
    }

    private void StartHideTimer()
    {
        CancelHideTimer();
        if (postComboHideDelay <= 0f)
        {
            ResetDisplay();
            return;
        }

        hideCoroutine = StartCoroutine(HideAfterDelay());
    }

    private void CancelHideTimer()
    {
        if (hideCoroutine == null)
            return;

        StopCoroutine(hideCoroutine);
        hideCoroutine = null;
    }

    private System.Collections.IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(postComboHideDelay);
        hideCoroutine = null;
        ResetDisplay();
    }

    private void ShowStep(ComboStep step)
    {
        if (iconLookup.TryGetValue(step, out GameObject icon) && icon != null)
        {
            icon.SetActive(true);
            PlayPop(icon);
        }
    }

    private void ShowArrow(ComboStep from, ComboStep to)
    {
        if (arrowLookup.TryGetValue((from, to), out GameObject arrow) && arrow != null)
        {
            arrow.SetActive(true);
            PlayPop(arrow);
        }
    }

    private void CacheBaseScale(GameObject target)
    {
        if (target == null || baseScales.ContainsKey(target))
            return;

        baseScales[target] = target.transform.localScale;
    }

    private void PlayPop(GameObject target)
    {
        if (target == null || !isActiveAndEnabled || !gameObject.activeInHierarchy)
            return;

        CacheBaseScale(target);

        if (popRoutines.TryGetValue(target, out Coroutine routine) && routine != null)
            StopCoroutine(routine);

        popRoutines[target] = StartCoroutine(PopRoutine(target));
    }

    private System.Collections.IEnumerator PopRoutine(GameObject target)
    {
        if (!baseScales.TryGetValue(target, out Vector3 baseScale))
            baseScale = target.transform.localScale;

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, popDuration);
        Vector3 startScale = baseScale * popScale;
        target.transform.localScale = startScale;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            target.transform.localScale = Vector3.Lerp(startScale, baseScale, t);
            yield return null;
        }

        target.transform.localScale = baseScale;
        popRoutines[target] = null;
    }

    private static bool IsTerminal(ComboStep step)
    {
        return step == ComboStep.S5 || step == ComboStep.A3;
    }

    private static bool TryResolveStep(PlayerAttack attack, out ComboStep step)
    {
        step = default;

        if (attack == null)
            return false;

        string attackId = attack.attackId ?? string.Empty;
        attackId = attackId.Trim();

        if (attackId.StartsWith("SX", StringComparison.OrdinalIgnoreCase))
        {
            if (TryParseStage(attackId, 2, out int stage))
            {
                step = (ComboStep)Enum.Parse(typeof(ComboStep), $"S{Mathf.Clamp(stage, 1, 5)}");
                return true;
            }
        }

        if (attackId.StartsWith("AY", StringComparison.OrdinalIgnoreCase)
            || attackId.StartsWith("AX", StringComparison.OrdinalIgnoreCase))
        {
            if (TryParseStage(attackId, 2, out int stage))
            {
                step = (ComboStep)Enum.Parse(typeof(ComboStep), $"A{Mathf.Clamp(stage, 1, 3)}");
                return true;
            }
        }

        return false;
    }

    private static bool TryParseStage(string attackId, int startIndex, out int stage)
    {
        stage = 0;
        if (attackId.Length <= startIndex)
            return false;

        string digits = attackId.Substring(startIndex);
        return int.TryParse(digits, out stage);
    }
}
