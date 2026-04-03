/*
 * Tier-Based Combo Manager
 * 
 * Handles 3-tier combo progression (stance swapping removed).
 * Provides attack routing data for PlayerAttackManager without needing AnimFacade.
 * 
 * Tier Structure:
 * Light:  SX1,SX2 | SX3,SX4 | SX5
 * Heavy:  AY1 | AY2 | AY3
 */

using UnityEngine;
using System;
using System.Collections;
using Utilities.Combat.Attacks;

public class TierComboManager : MonoBehaviour
{
    [Header("Combo Settings")]
    [SerializeField, Range(0.5f, 3f)] private float comboResetTime = 1.5f;
    [SerializeField] private bool debugMode = false;

    [Header("Data Sources")]
    [SerializeField, Tooltip("Reference AttackDatabase so combo tiers match PlayerAttack ScriptableObjects.")]
    private AttackDatabase attackDatabase;

    // Current combo state
    private int currentTier = 1;          // 1, 2, or 3
    private bool isHeavyChain = false;    // tracking if we're in a heavy (Y) chain
    private int fastAttackIndex = 0;      // within tier: 0 or 1 for tier-1, 0 for tier-2/3
    // private AttackStance lastStance = AttackStance.Single; // Stance tracking disabled.
    
    private float comboExpireAt = -1f;
    private Coroutine resetCoroutine;

    public enum ComboResetReason
    {
        Forced,
        Timeout,
        Finisher
    }

    public event Action ComboReset;
    public event Action<ComboResetReason> ComboResetDetailed;

    public enum AttackStance
    {
        Single = 0,
        AOE = 1
    }

    void Awake()
    {
        if (attackDatabase == null)
        {
            attackDatabase = Resources.Load<AttackDatabase>("AttackDatabase");
            if (attackDatabase == null)
                Debug.LogWarning("[TierComboManager] AttackDatabase reference missing. Combo stages will fall back to internal estimates.");
        }
    }

    /// <summary>
    /// Call this when player presses fast attack (X button).
    /// Returns the attack identifier for animation/VFX systems.
    /// </summary>
    public string RequestFastAttack(AttackStance currentStance)
    {
        EnsureComboNotStale();

        // Light attacks always use the single-target chain now.
        string attackId = GetNextFastAttack(AttackStance.Single);
        PlayerAttack attackData = attackDatabase != null ? attackDatabase.GetAttack(attackId) : null;
        int executedStage = ResolveComboStageFromData(attackData, currentTier);
        bool isFinisher = attackData != null && attackData.isFinisher;
        
        AdvanceCombo(false, currentStance, attackId, executedStage, isFinisher);
        
        if (debugMode)
            Debug.Log($"Fast Attack: {attackId} | Stage(SO): {executedStage} | NextTier: {currentTier} | Stance: {currentStance}");
        
        return attackId;
    }

    /// <summary>
    /// Call this when player presses heavy attack (Y button).
    /// Returns the attack identifier for animation/VFX systems.
    /// </summary>
    public string RequestHeavyAttack(AttackStance currentStance)
    {
        EnsureComboNotStale();

        // Heavy attacks always use the AY chain now.
        string attackId = GetNextHeavyAttack(AttackStance.AOE, out int resolvedTier);
        PlayerAttack attackData = attackDatabase != null ? attackDatabase.GetAttack(attackId) : null;
        int executedStage = ResolveComboStageFromData(attackData, resolvedTier);
        bool isFinisher = attackData != null && attackData.isFinisher;
        
        AdvanceCombo(true, currentStance, attackId, executedStage, isFinisher);
        
        if (debugMode)
            Debug.Log($"Heavy Attack: {attackId} | Stage(SO): {executedStage} | NextTier: {currentTier} | Stance: {currentStance}");
        
        return attackId;
    }

    /// <summary>
    /// Determines the next fast attack based on current tier and stance.
    /// Handles cross-stance tier routing.
    /// </summary>
    private string GetNextFastAttack(AttackStance currentStance)
    {
        // Stance swapping removed: always use the single-target light chain.
        return GetSingleFastAttack();
    }

    private string GetSingleFastAttack()
    {
        switch (currentTier)
        {
            case 1:
                // Tier 1: SX1 or SX2
                return fastAttackIndex == 0 ? "SX1" : "SX2";
            case 2:
                // Tier 2: SX3 or SX4
                return fastAttackIndex == 0 ? "SX3" : "SX4";
            case 3:
                // Tier 3: SX5 (finisher)
                return "SX5";
            default:
                return "SX1";
        }
    }

    private string GetAOEFastAttack()
    {
        switch (currentTier)
        {
            case 1:
                // Tier 1: AX1 or AX2
                return fastAttackIndex == 0 ? "AX1" : "AX2";
            case 2:
                // Tier 2: AX3
                return "AX3";
            case 3:
                // Tier 3: AX4 (finisher)
                return "AX4";
            default:
                return "AX1";
        }
    }

    /// <summary>
    /// Cross-stance fast attack routing.
    /// When stance changes mid-combo, jump to the target tier in new stance.
    /// </summary>
    private string GetCrossStanceFastAttack(AttackStance newStance, int tier)
    {
        if (newStance == AttackStance.AOE)
        {
            // Switching from Single to AOE
            switch (tier)
            {
                case 2: return "AX3";  // Tier-2 AOE
                case 3: return "AX4";  // Tier-3 AOE finisher
                default: return "AX1";
            }
        }
        else // Switching to Single
        {
            // Switching from AOE to Single
            switch (tier)
            {
                case 2: return fastAttackIndex == 0 ? "SX3" : "SX4";  // Tier-2 Single
                case 3: return "SX5";  // Tier-3 Single finisher
                default: return "SX1";
            }
        }
    }

    /// <summary>
    /// Determines the next heavy attack based on current tier and stance.
    /// </summary>
    private string GetNextHeavyAttack(AttackStance currentStance, out int resolvedTier)
    {
        resolvedTier = ResolveHeavyTier(currentStance);

        return "AY" + Mathf.Clamp(resolvedTier, 1, 3);
    }

    private int ResolveHeavyTier(AttackStance currentStance)
    {
        int tier = currentTier;

        if (tier == 1 && fastAttackIndex > 0)
        {
            // Already performed SX1/AX1, treat heavy as stage 2
            tier = 2;
        }
        else if (tier == 2 && fastAttackIndex > 0)
        {
            // After SX3, escalate heavy straight to stage 3
            tier = 3;
        }

        return tier;
    }

    /// <summary>
    /// Advances the combo state after an attack is executed.
    /// </summary>
    private void AdvanceCombo(bool isHeavy, AttackStance currentStance, string attackId, int executedStage, bool forceFinisher)
    {
        // Update stance tracking
        // lastStance = currentStance;
        isHeavyChain = isHeavy;

        if (forceFinisher || executedStage >= 3)
        {
            ResetCombo(ComboResetReason.Finisher);
            return;
        }

        if (isHeavy)
        {
            fastAttackIndex = 0;
            currentTier = Mathf.Clamp(executedStage + 1, 1, 3);
            return;
        }

        if (executedStage == 1)
        {
            fastAttackIndex++;
            bool reachedEnd = fastAttackIndex >= 2 || attackId.EndsWith("2");
            if (reachedEnd)
            {
                currentTier = 2;
                fastAttackIndex = 0;
            }
            else
            {
                currentTier = 1;
            }
        }
        else if (executedStage == 2)
        {
            if (currentStance == AttackStance.Single)
            {
                fastAttackIndex++;
                bool reachedEnd = fastAttackIndex >= 2 || attackId.EndsWith("4");
                if (reachedEnd)
                {
                    currentTier = 3;
                    fastAttackIndex = 0;
                }
                else
                {
                    currentTier = 2;
                }
            }
            else
            {
                currentTier = 3;
                fastAttackIndex = 0;
            }
        }
        else
        {
            ResetCombo(ComboResetReason.Forced);
            return;
        }

    }

    /// <summary>
    /// Begins (or restarts) the combo reset countdown after an attack finishes.
    /// </summary>
    public void StartComboResetCountdown()
    {
        if (!gameObject.activeInHierarchy)
            return;

        comboExpireAt = Time.time + Mathf.Max(0f, comboResetTime);

        if (resetCoroutine != null)
            StopCoroutine(resetCoroutine);

        resetCoroutine = StartCoroutine(ComboResetTimer());
    }

    /// <summary>
    /// Cancels any pending combo reset countdown (e.g., when a new attack starts).
    /// </summary>
    public void CancelComboResetCountdown()
    {
        comboExpireAt = -1f;

        if (resetCoroutine != null)
        {
            StopCoroutine(resetCoroutine);
            resetCoroutine = null;
        }
    }

    private int ResolveComboStageFromData(PlayerAttack attackData, int fallbackStage)
    {
        return attackData != null ? Mathf.Clamp(attackData.comboStage, 1, 3) : Mathf.Clamp(fallbackStage, 1, 3);
    }

    private void EnsureComboNotStale()
    {
        if (IsComboAtInitialState())
            return;

        if (comboExpireAt > 0f && Time.time >= comboExpireAt)
            ResetCombo(ComboResetReason.Timeout);
    }

    private bool IsComboAtInitialState()
    {
        return currentTier == 1 && fastAttackIndex == 0 && !isHeavyChain;
    }

    /// <summary>
    /// Resets combo to initial state.
    /// </summary>
    public void ResetCombo(ComboResetReason reason = ComboResetReason.Forced)
    {
        currentTier = 1;
        fastAttackIndex = 0;
        isHeavyChain = false;

        CancelComboResetCountdown();

        ComboReset?.Invoke();
        ComboResetDetailed?.Invoke(reason);
        
        if (debugMode)
            Debug.Log("Combo Reset");
    }

    /// <summary>
    /// Coroutine to reset combo after inactivity.
    /// </summary>
    private IEnumerator ComboResetTimer()
    {
        float elapsed = 0f;
        while (elapsed < comboResetTime)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        ResetCombo(ComboResetReason.Timeout);
    }

    /// <summary>
    /// Public accessors for debugging/UI.
    /// </summary>
    public int CurrentTier => currentTier;
    public bool IsHeavyChain => isHeavyChain;
    public int FastAttackIndex => fastAttackIndex;

    // Call this from animation events if needed
    public void OnComboFinisher()
    {
        ResetCombo(ComboResetReason.Finisher);
    }
}
