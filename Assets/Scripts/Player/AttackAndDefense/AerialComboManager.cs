/*
 * Aerial Combo Manager
 * 
 * Handles aerial attack combos with different rules than ground combos:
 * - 2 Fast attacks (AerialX1, AerialX2) 
 * - 1 Heavy plunge attack (AerialY1)
 * - After 2 fast attacks, player falls unless they dash or plunge
 * - Heavy attack immediately plunges player down
 * - Can air dash once to reset aerial attacks (X X Dash X X Y possible)
 * - Fast attacks can be canceled by dash
 * 
 * Usage: X X Y, X Y, Y, X X Dash X X Y, X Dash X X Y, etc.
 */

using UnityEngine;
using Utilities.Combat.Attacks;

public class AerialComboManager : MonoBehaviour
{
    [Header("Aerial Combo Settings")]
    [SerializeField, Range(0.5f, 2f)] private float comboResetTime = 1.0f;
    [SerializeField] private bool debugMode = false;

    [Header("Plunge Damage Scaling")]
    [SerializeField, Tooltip("Damage multiplier applied per completed aerial light combo chain (AX1+AX2). Final multiplier = multiplier^count.")]
    [Range(1f, 5f)] private float plungeDamageMultiplierPerCombo = 1.25f;
    [SerializeField, Tooltip("When enabled, uses per-aerial-attack counting (AX1 and AX2 each count). Useful if designers want scaling even for partial chains.")]
    private bool useAttackBasedPlungeScaling = false;
    [SerializeField, Tooltip("Damage multiplier applied per aerial light attack used (AX1/AX2). Only used when 'Use Attack Based Plunge Scaling' is enabled. Final multiplier = multiplier^count.")]
    [Range(1f, 5f)] private float plungeDamageMultiplierPerAerialAttack = 1.12f;

    [Header("References")]
    [SerializeField] private CharacterController characterController;
    [SerializeField] private PlayerAnimationController animationController;
    [SerializeField] private AttackDatabase attackDatabase;
    [SerializeField, Tooltip("Movement controller used for plunge behavior (PlayerMovement)")]
    private PlayerMovement playerMovement;

    [Header("Attack Data")]
    [Tooltip("PlayerAttack assets for aerial light chain (X1 -> X2). Leave null to fall back to AttackDatabase IDs.")]
    [SerializeField] private PlayerAttack[] aerialLightChain = new PlayerAttack[2];
    [SerializeField, Tooltip("PlayerAttack asset for the plunge attack (heavy aerial).")]
    private PlayerAttack aerialHeavyAttack;
    [SerializeField, Tooltip("Fallback ID for the first aerial light attack if no asset is assigned.")]
    private string fallbackLightAttack1Id = "AC_X1";
    [SerializeField, Tooltip("Fallback ID for the second aerial light attack if no asset is assigned.")]
    private string fallbackLightAttack2Id = "AC_X2";
    [SerializeField, Tooltip("Fallback ID for the aerial heavy plunge attack if no asset is assigned.")]
    private string fallbackHeavyAttackId = "Plunge";

    // Aerial combo state
    private int aerialFastCount = 0;  // 0, 1, or 2
    private bool hasUsedAirDash = false;
    private bool hasUsedAerialHeavy = false;
    private bool isInAerialCombo = false;
    private float lastAerialAttackTime = -999f;
    private bool pendingAutoFall = false;

    // Plunge scaling counters (reset on landing or plunge execution)
    private int plungeScalingComboCount;
    private int plungeScalingAttackCount;
    private float pendingPlungeDamageMultiplier = 1f;

    private const int MAX_AERIAL_FAST_ATTACKS = 2;

    // Public property for external checks
    public bool HasUsedAerialHeavy => hasUsedAerialHeavy;

    public float ConsumePendingPlungeDamageMultiplier()
    {
        float multiplier = Mathf.Max(1f, pendingPlungeDamageMultiplier);
        pendingPlungeDamageMultiplier = 1f;
        return multiplier;
    }

    private void Awake()
    {
        characterController ??= GetComponent<CharacterController>();
        if (characterController == null)
            Debug.LogError("AerialComboManager: CharacterController not found!");

        animationController ??= GetComponent<PlayerAnimationController>()
            ?? GetComponentInChildren<PlayerAnimationController>()
            ?? GetComponentInParent<PlayerAnimationController>();

        playerMovement ??= GetComponent<PlayerMovement>()
            ?? GetComponentInChildren<PlayerMovement>()
            ?? GetComponentInParent<PlayerMovement>();

        attackDatabase ??= Resources.Load<AttackDatabase>("AttackDatabase");
        if (attackDatabase == null)
            Debug.LogWarning("AerialComboManager: AttackDatabase missing. Assign in inspector to resolve fallback IDs.");

        // Ensure the light chain always has two slots so designers only worry about data entries
        if (aerialLightChain == null || aerialLightChain.Length < MAX_AERIAL_FAST_ATTACKS)
        {
            var resized = new PlayerAttack[MAX_AERIAL_FAST_ATTACKS];
            if (aerialLightChain != null)
            {
                for (int i = 0; i < aerialLightChain.Length && i < resized.Length; i++)
                    resized[i] = aerialLightChain[i];
            }

            aerialLightChain = resized;
        }
    }

    void Update()
    {
        // Reset aerial combo when grounded
        if (characterController != null && characterController.isGrounded && isInAerialCombo)
        {
            ResetAerialCombo();
        }
    }

    /// <summary>
    /// Request aerial fast attack. Returns PlayerAttack data or null if not allowed.
    /// </summary>
    public PlayerAttack RequestAerialLightAttack()
    {
        if (!CanPerformAerialFastAttack())
        {
            if (debugMode)
                Debug.LogWarning("Cannot perform aerial fast attack - requirements not met");
            return null;
        }

        aerialFastCount++;
        plungeScalingAttackCount++;
        lastAerialAttackTime = Time.time;
        isInAerialCombo = true;

        PlayerAttack attackData = ResolveLightAttackForStep(aerialFastCount - 1);
        if (attackData == null)
        {
            aerialFastCount = Mathf.Max(0, aerialFastCount - 1);
            plungeScalingAttackCount = Mathf.Max(0, plungeScalingAttackCount - 1);
            if (debugMode)
                Debug.LogWarning("Aerial light attack data missing. Ensure PlayerAttack is assigned or exists in AttackDatabase.");
            return null;
        }

        if (aerialFastCount >= MAX_AERIAL_FAST_ATTACKS)
            plungeScalingComboCount++;

        if (aerialFastCount >= MAX_AERIAL_FAST_ATTACKS && !hasUsedAirDash)
            pendingAutoFall = true;

        if (debugMode)
            Debug.Log($"Aerial Fast Attack: {attackData.attackId} | Count: {aerialFastCount}/{MAX_AERIAL_FAST_ATTACKS}");

        return attackData;
    }

    [System.Obsolete("Use RequestAerialLightAttack to get PlayerAttack data directly.")]
    public string RequestAerialFastAttack()
    {
        PlayerAttack attack = RequestAerialLightAttack();
        return attack != null ? attack.attackId : null;
    }

    /// <summary>
    /// Request aerial heavy (plunge) attack. Only allowed once per airtime.
    /// </summary>
    public PlayerAttack RequestAerialHeavyAttack()
    {
        if (!CanPerformAerialHeavyAttack())
        {
            if (debugMode)
                Debug.LogWarning("Cannot perform aerial heavy - requirements not met");
            return null;
        }

        hasUsedAerialHeavy = true;
        lastAerialAttackTime = Time.time;
        isInAerialCombo = true;
        pendingAutoFall = false;

        // Compute a one-shot multiplier for the plunge attack based on aerial attacks performed earlier this airtime.
        int count = useAttackBasedPlungeScaling ? plungeScalingAttackCount : plungeScalingComboCount;
        float stepMultiplier = useAttackBasedPlungeScaling ? plungeDamageMultiplierPerAerialAttack : plungeDamageMultiplierPerCombo;
        pendingPlungeDamageMultiplier = Mathf.Pow(Mathf.Max(1f, stepMultiplier), Mathf.Max(0, count));

        // Reset counters after plunge is executed (per design).
        plungeScalingComboCount = 0;
        plungeScalingAttackCount = 0;

        PlayerAttack heavyAttack = ResolveHeavyAttack();
        if (heavyAttack == null)
        {
            if (debugMode)
                Debug.LogWarning("No PlayerAttack assigned for aerial plunge.");
            hasUsedAerialHeavy = false;
            pendingPlungeDamageMultiplier = 1f;
            return null;
        }

        if (playerMovement != null)
            playerMovement.StartPlunge();
        else if (debugMode)
            Debug.LogWarning("AerialComboManager: PlayerMovement not assigned; plunge movement skipped.");

        if (debugMode)
            Debug.Log($"Aerial Heavy Attack: {heavyAttack.attackId} - plunge movement triggered");

        return heavyAttack;
    }

    [System.Obsolete("Use RequestAerialHeavyAttack to get PlayerAttack data directly.")]
    public string RequestAerialHeavyAttackId()
    {
        PlayerAttack attack = RequestAerialHeavyAttack();
        return attack != null ? attack.attackId : null;
    }

    /// <summary>
    /// Called when player performs an air dash.
    /// Resets aerial fast attack count once per air time.
    /// </summary>
    public bool TryAirDash()
    {
        if (hasUsedAirDash)
        {
            if (debugMode)
                Debug.LogWarning("Air dash already used - cannot reset aerial attacks");
            return false;
        }

        // Air dash resets fast attack count
        aerialFastCount = 0;
        hasUsedAirDash = true;
        pendingAutoFall = false;
        
        if (debugMode)
            Debug.Log("Air dash performed - aerial fast attacks reset");

        return true;
    }

    /// <summary>
    /// Check if player can perform another aerial fast attack.
    /// </summary>
    public bool CanPerformAerialFastAttack()
    {
        if (characterController != null && characterController.isGrounded)
            return false;

        if (playerMovement != null && !playerMovement.CanStartAerialCombat())
            return false;

        if (InputReader.inputBusy)
            return false;

        return aerialFastCount < MAX_AERIAL_FAST_ATTACKS;
    }

    private bool CanPerformAerialHeavyAttack()
    {
        if (characterController != null && characterController.isGrounded)
            return false;

        if (playerMovement != null && !playerMovement.CanStartAerialCombat())
            return false;

        if (hasUsedAerialHeavy)
            return false;

        if (InputReader.inputBusy)
            return false;

        return true;
    }

    /// <summary>
    /// Check if player has reached max aerial attacks and should fall.
    /// </summary>
    public bool ShouldFallAfterAnimation()
    {
        return pendingAutoFall;
    }

    /// <summary>
    /// Reset aerial combo state (called on landing or timeout).
    /// </summary>
    public void ResetAerialCombo()
    {
        aerialFastCount = 0;
        hasUsedAirDash = false;
        hasUsedAerialHeavy = false;  // Reset heavy flag for next airtime
        isInAerialCombo = false;
        pendingAutoFall = false;

        plungeScalingComboCount = 0;
        plungeScalingAttackCount = 0;
        pendingPlungeDamageMultiplier = 1f;

        if (debugMode)
            Debug.Log("Aerial combo reset");
    }

    /// <summary>
    /// Called when player lands to reset air dash availability.
    /// </summary>
    public void OnLanded()
    {
        ResetAerialCombo();
    }

    // Public accessors for debugging/UI
    public int AerialFastCount => aerialFastCount;
    public bool HasUsedAirDash => hasUsedAirDash;
    public bool IsInAerialCombo => isInAerialCombo;
    public bool CanAirDash => !hasUsedAirDash && characterController != null && !characterController.isGrounded;

    public void HandleAttackAnimationComplete(PlayerAttack completedAttack)
    {
        if (completedAttack == null)
            return;

        bool isAerialAttack = completedAttack.attackType == AttackType.LightAerial
            || completedAttack.attackType == AttackType.HeavyAerial;

        if (!isAerialAttack)
            return;

        if (completedAttack.attackType == AttackType.LightAerial && pendingAutoFall)
        {
            pendingAutoFall = false;
            animationController?.PlayFalling();
        }
    }

    private PlayerAttack ResolveLightAttackForStep(int index)
    {
        if (index < 0)
            index = 0;

        if (aerialLightChain != null && index < aerialLightChain.Length && aerialLightChain[index] != null)
            return aerialLightChain[index];

        string fallbackId = index == 0 ? fallbackLightAttack1Id : fallbackLightAttack2Id;
        return LookupAttack(fallbackId);
    }

    private PlayerAttack ResolveHeavyAttack()
    {
        if (aerialHeavyAttack != null)
            return aerialHeavyAttack;

        return LookupAttack(fallbackHeavyAttackId);
    }

    private PlayerAttack LookupAttack(string attackId)
    {
        if (string.IsNullOrWhiteSpace(attackId) || attackDatabase == null)
            return null;

        return attackDatabase.GetAttack(attackId);
    }
}



