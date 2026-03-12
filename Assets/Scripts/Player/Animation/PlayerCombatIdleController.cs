using UnityEngine;
using Utilities.Combat;
using Utilities.Combat.Attacks;

/// <summary>
/// Drives attack-type-aware idle playback (breathing, combat, weapon-check) using gameplay signals rather than animator logic.
/// </summary>
[DisallowMultipleComponent]
public class PlayerCombatIdleController : MonoBehaviour
{
    private const float WeaponCheckCrossfade = 0.3f;
    private const float WeaponCheckCompletionThreshold = 0.99f;
    private const string SingleTargetBreathingState = "ST_Breathing";
    private const string SingleTargetCombatState = "ST_Idle_Combat";
    private const string AreaCombatState = "AOE_Idle_Combat";
    private const string SingleTargetWeaponCheckState = "ST_Idle_WC";
    private const string AreaWeaponCheckState = "AOE_Idle_WC";

    private enum IdlePose
    {
        Breathing,
        Combat,
        WeaponCheck,
    }

    [Header("References")]
    [SerializeField]
    private PlayerAnimationController animationController;
    [SerializeField]
    private CharacterController characterController;
    [SerializeField]
    private PlayerMovement playerMovement;

    [Header("Timing")]
    [SerializeField]
    [Tooltip("Seconds the player stays flagged as in combat after an attack or taking damage.")]
    private float combatDuration = 5f;




    [SerializeField]
    [Tooltip("Minimum squared magnitude required to treat MoveInput as real movement.")]
    private float movementThreshold = 0.02f;

    private float combatTimer;
    private bool weaponCheckActive;
    private bool weaponCheckHasBegun;
    private string activeWeaponCheckState = string.Empty;
    private string lastStateName = string.Empty;
    private IdlePose currentPose = IdlePose.Breathing;
    private bool inputBusyLastFrame;
    private bool guardActiveLastFrame;
    private bool lastAttackWasAoe;

    public bool IsInCombat => combatTimer > 0f;

    private void Awake()
    {
        animationController ??= GetComponent<PlayerAnimationController>();
        characterController ??= GetComponent<CharacterController>();
        if (characterController == null)
            characterController = GetComponentInParent<CharacterController>();
        playerMovement ??= GetComponent<PlayerMovement>()
            ?? GetComponentInParent<PlayerMovement>()
            ?? GetComponentInChildren<PlayerMovement>();
    }

    private void Start()
    {
        ForceImmediateIdle();
    }

    private void OnEnable()
    {
        PlayerAttackManager.OnAttack += HandleAttackEvent;
        PlayerHealthBarManager.OnPlayerDamaged += HandleDamageEvent;
    }

    private void OnDisable()
    {
        PlayerAttackManager.OnAttack -= HandleAttackEvent;
        PlayerHealthBarManager.OnPlayerDamaged -= HandleDamageEvent;
    }

    private void HandleAttackEvent(PlayerAttack _)
    {
        UpdateLastAttackType(_);
        EnterCombatState();
    }

    private void HandleDamageEvent(float _)
    {
        EnterCombatState();
    }

    private void EnterCombatState()
    {
        combatTimer = combatDuration;
        ResetBreathingTimer();
        if (weaponCheckActive)
            CancelWeaponCheck(false);
    }

    private void Update()
    {
        if (animationController == null)
            return;

        bool guardActive = CombatManager.isGuarding;
        if (guardActive)
        {
            guardActiveLastFrame = true;
            ResetBreathingTimer();
            if (weaponCheckActive)
                CancelWeaponCheck(false);
            return;
        }

        if (guardActiveLastFrame)
        {
            guardActiveLastFrame = false;
            lastStateName = string.Empty;
        }

        bool inputBusy = InputReader.inputBusy;
        if (inputBusy)
        {
            inputBusyLastFrame = true;
            ResetBreathingTimer();
            if (weaponCheckActive)
                CancelWeaponCheck(false);
            return;
        }

        if (playerMovement != null && playerMovement.IsJumpPending)
        {
            ResetBreathingTimer();
            return;
        }

        if (inputBusyLastFrame)
        {
            inputBusyLastFrame = false;
            lastStateName = string.Empty;
        }

        bool grounded = characterController != null
            ? characterController.isGrounded
            : playerMovement != null && PlayerMovement.isGrounded;
        bool hasMovementInput = playerMovement != null
            ? playerMovement.HasEffectiveMovementInput
            : InputReader.MoveInput.sqrMagnitude >= movementThreshold;

        if (combatTimer > 0f)
            combatTimer = Mathf.Max(0f, combatTimer - Time.deltaTime);

        if (!grounded)
        {
            ResetBreathingTimer();
            return;
        }

        if (weaponCheckActive)
        {
            if (hasMovementInput || combatTimer > 0f)
            {
                CancelWeaponCheck(true);
            }
            else if (HasWeaponCheckFinished())
            {
                CancelWeaponCheck(true);
            }
            else
            {
                return;
            }
        }

        if (hasMovementInput)
        {
            ResetBreathingTimer();
            return;
        }

        bool inCombat = combatTimer > 0f;
        IdlePose desiredPose = inCombat ? IdlePose.Combat : IdlePose.Breathing;

        bool breathingActive = desiredPose == IdlePose.Breathing
            && !weaponCheckActive
            && IsPoseActive(IdlePose.Breathing);
        if (breathingActive && HasBreathingFinished())
        {
            PlayWeaponCheck();
            return;
        }

        EnsureIdlePose(desiredPose);
    }

    private void PlayWeaponCheck()
    {
        activeWeaponCheckState = GetWeaponCheckStateForLastAttack();
        weaponCheckHasBegun = false;
        SetIdlePose(IdlePose.WeaponCheck, WeaponCheckCrossfade);
    }

    private void CancelWeaponCheck(bool returnToBreathing)
    {
        if (!weaponCheckActive)
            return;

        weaponCheckActive = false;
        weaponCheckHasBegun = false;
        activeWeaponCheckState = string.Empty;
        ResetBreathingTimer();

        if (returnToBreathing)
            SetIdlePose(IdlePose.Breathing, WeaponCheckCrossfade);
    }

    private bool HasWeaponCheckFinished()
    {
        if (string.IsNullOrEmpty(activeWeaponCheckState))
            return true;

        if (!weaponCheckHasBegun)
        {
            if (animationController.IsPlaying(activeWeaponCheckState, out _))
                weaponCheckHasBegun = true;
            return false;
        }

        if (!animationController.IsPlaying(activeWeaponCheckState, out float normalizedTime))
            return weaponCheckHasBegun;

        return normalizedTime >= WeaponCheckCompletionThreshold;
    }

    private void SetIdlePose(IdlePose pose, float transitionOverride = -1f)
    {
        string stateName = ResolveStateNameForPose(pose);
        bool poseAllowsRefresh = pose == IdlePose.WeaponCheck;

        if (!poseAllowsRefresh && stateName == lastStateName && !NeedsStateRefresh(stateName))
            return;

        switch (pose)
        {
            case IdlePose.Breathing:
                animationController.PlaySingleTargetBreathing(transitionOverride);
                weaponCheckActive = false;
                activeWeaponCheckState = string.Empty;
                ResetBreathingTimer();
                break;
            case IdlePose.Combat:
                if (lastAttackWasAoe)
                    animationController.PlayAoeIdleCombat();
                else
                    animationController.PlaySingleTargetIdleCombat();
                weaponCheckActive = false;
                activeWeaponCheckState = string.Empty;
                ResetBreathingTimer();
                break;
            case IdlePose.WeaponCheck:
                activeWeaponCheckState = stateName;
                if (stateName == AreaWeaponCheckState)
                    animationController.PlayAoeIdleWorld(transitionOverride);
                else
                    animationController.PlaySingleTargetIdleWorld(transitionOverride);
                weaponCheckActive = true;
                ResetBreathingTimer();
                break;
        }

        currentPose = pose;
        lastStateName = stateName;
    }

    private void ForceImmediateIdle()
    {
        if (animationController == null)
            return;

        lastStateName = string.Empty;
        currentPose = IdlePose.Breathing;
        ResetBreathingTimer();
        SetIdlePose(IdlePose.Breathing, 0f);
    }

    private void EnsureIdlePose(IdlePose pose)
    {
        string desiredState = ResolveStateNameForPose(pose);
        bool needsStanceRefresh = desiredState != lastStateName;
        bool statePlaying = !string.IsNullOrEmpty(desiredState)
            && animationController.IsPlaying(desiredState, out _);

        if (!needsStanceRefresh && pose == currentPose && statePlaying)
            return;

        SetIdlePose(pose);
    }

    private bool IsPoseActive(IdlePose pose)
    {
        string stateName = ResolveStateNameForPose(pose);
        return !string.IsNullOrEmpty(stateName) && animationController.IsPlaying(stateName, out _);
    }

    private string ResolveStateNameForPose(IdlePose pose)
    {
        switch (pose)
        {
            case IdlePose.Breathing:
                return SingleTargetBreathingState;
            case IdlePose.Combat:
                return lastAttackWasAoe ? AreaCombatState : SingleTargetCombatState;
            case IdlePose.WeaponCheck:
                return !string.IsNullOrEmpty(activeWeaponCheckState)
                    ? activeWeaponCheckState
                    : GetWeaponCheckStateForLastAttack();
            default:
                return string.Empty;
        }
    }

    private bool NeedsStateRefresh(string stateName)
    {
        if (string.IsNullOrEmpty(stateName))
            return true;

        return !animationController.IsPlaying(stateName, out _);
    }

    private void ResetBreathingTimer() { }

    private bool HasBreathingFinished()
    {
        if (!animationController.IsPlaying(SingleTargetBreathingState, out float normalizedTime))
            return false;

        return normalizedTime >= WeaponCheckCompletionThreshold;
    }

    private string GetWeaponCheckStateForLastAttack()
    {
        return lastAttackWasAoe ? AreaWeaponCheckState : SingleTargetWeaponCheckState;
    }

    private void UpdateLastAttackType(PlayerAttack attack)
    {
        if (attack == null)
            return;

        switch (attack.attackType)
        {
            case AttackType.LightSingle:
            case AttackType.HeavySingle:
                lastAttackWasAoe = false;
                break;
            case AttackType.HeavyAOE:
                lastAttackWasAoe = true;
                break;
        }
    }
}

