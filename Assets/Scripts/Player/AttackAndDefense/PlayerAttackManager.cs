using System;
using System.Collections;
using UnityEngine;

using Utilities.Combat;
using Utilities.Combat.Attacks;

public class PlayerAttackManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerAnimationController animationController;
    [SerializeField] private TierComboManager tierComboManager;
    [SerializeField] private AerialComboManager aerialComboManager;
    [SerializeField] private AttackDatabase attackDatabase;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField, Tooltip("Registry of VFX anchor points on the player rig.")]
    private PlayerVfxAnchorRegistry vfxAnchorRegistry;

    [Header("Special Attacks")]
    [SerializeField] private PlayerAttack guardAttackOverride;
    [SerializeField] private string guardAttackId = "G_Attack";
    [SerializeField] private PlayerAttack airDashAttackOverride;
    [SerializeField] private string airDashAttackId = "AirDash";
    [SerializeField] private PlayerAttack launcherAttackOverride;
    [SerializeField] private string launcherAttackId = "Launcher";

    [Header("Special Attack Timing")]
    [SerializeField, Range(0f, 0.3f)] private float guardAttackHitboxDelay = 0.08f;
    [SerializeField, Range(0.2f, 1.5f)] private float guardAttackAutoCancelDuration = 0.65f;
    [SerializeField, Range(0.2f, 1.5f)] private float launcherAutoCancelDuration = 0.9f;
    [SerializeField, Tooltip("Auto-generate guard attack hitbox (use only if the animation lacks events).")]
    private bool guardAttackAutoGenerateHitbox = false;

    [Header("Stance Switching")]
    // Stance swapping removed (kept for reference).
    // [SerializeField, Range(0.1f, 5f)] private float stanceCooldownTime = 1f;
    // [SerializeField] private AudioClip changeStanceAudio;

    [Header("Aerial Attack Recovery")]
    [SerializeField, Tooltip("Additional lockout applied after landing a plunge attack.")]
    [Range(0f, 2f)] private float plungeRecoveryDelay = 0.75f;
    [SerializeField, Tooltip("Cross-fade duration when exiting a plunge into combat idle.")]
    [Range(0f, 1f)] private float plungeIdleBlendTime = 0.25f;

    [Header("Debug")]
    [SerializeField, Tooltip("When enabled, logs plunge hitbox damage (base x multiplier) when the hitbox spawns, even if no enemy is hit.")]
    private bool debugPlungeDamage = true;
    [SerializeField, HideInInspector] private bool debugPlungeDamageInitialized;

    [Header("Audio")]
    [SerializeField, Tooltip("Primary AudioSource for attack and stance SFX. Defaults to SoundManager's SFX source when unset.")]
    private AudioSource attackAudioSource;
    private AudioSource fallbackSfxSource;
    // private Coroutine stanceCooldownRoutine;
    private GameObject activeHitbox;
    private PlayerAttack currentAttack;
    private Coroutine hitboxLifetimeRoutine;
    private Coroutine plungeRecoveryRoutine;
    private Coroutine guardAttackFlowRoutine;
    private Coroutine specialAttackAutoCancelRoutine;
    private Coroutine heavyMoveRoutine;
    private bool lastAttackWasAoe;
    private float currentAttackDamageMultiplier = 1f;

    [Header("Input Buffering")]
    [SerializeField, Range(0.05f, 0.6f)] private float inputBufferWindow = 0.25f;

    private enum AttackButton { None, Light, Heavy }
    private AttackButton bufferedAttackButton = AttackButton.None;
    private float bufferedAttackExpiresAt = -1f;

    public static event Action<PlayerAttack> OnAttack;

    private void Awake()
    {
        EnsureDebugDefaultsApplied();

        animationController ??= GetComponent<PlayerAnimationController>() ?? GetComponentInChildren<PlayerAnimationController>();
        tierComboManager ??= GetComponent<TierComboManager>() ?? GetComponentInChildren<TierComboManager>() ?? GetComponentInParent<TierComboManager>();
        aerialComboManager ??= GetComponent<AerialComboManager>() ?? GetComponentInChildren<AerialComboManager>() ?? GetComponentInParent<AerialComboManager>();
        characterController ??= GetComponent<CharacterController>();
        playerMovement ??= GetComponent<PlayerMovement>() ?? GetComponentInChildren<PlayerMovement>() ?? GetComponentInParent<PlayerMovement>();
        vfxAnchorRegistry ??= GetComponent<PlayerVfxAnchorRegistry>() ?? GetComponentInChildren<PlayerVfxAnchorRegistry>() ?? GetComponentInParent<PlayerVfxAnchorRegistry>();

        if (attackDatabase == null)
        {
            attackDatabase = Resources.Load<AttackDatabase>("AttackDatabase");
            if (attackDatabase == null)
                Debug.LogWarning("[PlayerAttackManager] AttackDatabase reference is missing.");
        }
    }

    private void OnValidate()
    {
        EnsureDebugDefaultsApplied();
    }

    private void EnsureDebugDefaultsApplied()
    {
        if (debugPlungeDamageInitialized)
            return;

        debugPlungeDamage = true;
        debugPlungeDamageInitialized = true;
    }

    private void Start()
    {
        fallbackSfxSource = SoundManager.Instance != null ? SoundManager.Instance.voiceSource : null;

        if (attackAudioSource == null)
        {
            attackAudioSource = SoundManager.Instance != null ? SoundManager.Instance.voiceSource : null;
        }

    }

    private void OnDisable()
    {
        // Stance switching removed.
        // if (stanceCooldownRoutine != null)
        // {
        //     StopCoroutine(stanceCooldownRoutine);
        //     stanceCooldownRoutine = null;
        // }
        if (hitboxLifetimeRoutine != null)
        {
            StopCoroutine(hitboxLifetimeRoutine);
            hitboxLifetimeRoutine = null;
        }
        if (plungeRecoveryRoutine != null)
        {
            StopCoroutine(plungeRecoveryRoutine);
            plungeRecoveryRoutine = null;
        }
        if (heavyMoveRoutine != null)
        {
            StopCoroutine(heavyMoveRoutine);
            heavyMoveRoutine = null;
        }
        StopGuardAttackFlowRoutine();
        StopSpecialAttackAutoCancelRoutine();

        ClearHitbox();
        InputReader.inputBusy = false;
    }

    private void Update()
    {
        if (ShouldIgnoreAttackInput())
        {
            ClearBufferedAttack();
            return;
        }

        if (InputReader.LightAttackTriggered)
            ProcessAttackInput(true);

        if (InputReader.HeavyAttackTriggered)
        {
            if (TryExecuteDashLauncherAttack())
                return;

            ProcessAttackInput(false);
        }

        // Stance swapping removed.
        // if (InputReader.ChangeStanceTriggered)
        //     TryChangeStance();
    }

    private void HandleGuardStateAttacks()
    {
        // Guard attacks are disabled. Kept for backwards compatibility if referenced elsewhere.
        ClearBufferedAttack();
    }

    private void ProcessAttackInput(bool lightAttack)
    {
        if (ShouldIgnoreAttackInput())
        {
            ClearBufferedAttack();
            return;
        }

        if (!InputReader.inputBusy)
        {
            if (lightAttack)
                OnLightAttack();
            else
                OnHeavyAttack();
        }
        else
        {
            BufferAttack(lightAttack);
        }
    }

    private void TryExecuteGuardAttack()
    {
        PlayerAttack guardAttack = ResolveGuardAttack();
        if (guardAttack == null)
        {
            Debug.LogWarning("[PlayerAttackManager] Guard attack data missing. Assign GuardAttackOverride or ensure ID exists in AttackDatabase.");
            return;
        }

        ExecuteAttack(
            guardAttack,
            guardAttack.attackId,
            controller => controller?.PlayGuardAttack()
        );

        StartGuardAttackFlow(guardAttack);
        ScheduleSpecialAttackAutoCancel(GetAnimationDurationOrFallback(guardAttack, guardAttackAutoCancelDuration));
    }

    private bool TryExecuteDashLauncherAttack()
    {
        if (playerMovement == null)
            return false;

        if (!playerMovement.CanTriggerLauncherFromDash)
            return false;

        PlayerAttack launcherAttack = ResolveLauncherAttack();
        if (launcherAttack == null)
        {
            Debug.LogWarning("[PlayerAttackManager] Launcher attack data missing. Assign LauncherAttackOverride or ensure ID exists in AttackDatabase.");
            return false;
        }

        if (!playerMovement.TryTriggerLauncherJump())
            return false;

        ExecuteAttack(
            launcherAttack,
            launcherAttack.attackId,
            controller => controller?.PlayLauncher()
        );

        ScheduleSpecialAttackAutoCancel(GetAnimationDurationOrFallback(launcherAttack, launcherAutoCancelDuration));

        return true;
    }

    // private void TryChangeStance()
    // {
    //     if (stanceCooldownRoutine != null)
    //         return;

    //     CombatManager.ChangeStance();

    //     PlaySfx(changeStanceAudio);

    //     stanceCooldownRoutine = StartCoroutine(StanceChangeCooldown());
    // }

    // private IEnumerator StanceChangeCooldown()
    // {
    //     yield return new WaitForSeconds(stanceCooldownTime);
    //     stanceCooldownRoutine = null;
    // }

    public void OnLightAttack()
    {
        if (CombatManager.isGuarding)
            return;

        if (InputReader.inputBusy)
            return;

        AttemptAttack(true);
    }

    public void OnHeavyAttack()
    {
        if (CombatManager.isGuarding)
            return;

        if (InputReader.inputBusy)
            return;

        AttemptAttack(false);
    }

    public void TriggerAirDashAttack()
    {
        PlayerAttack airDashAttack = ResolveAirDashAttack();
        if (airDashAttack == null)
        {
            Debug.LogWarning("[PlayerAttackManager] Air dash attack data missing. Assign AirDashAttackOverride or ensure ID exists in AttackDatabase.");
            return;
        }

        OnAttack?.Invoke(airDashAttack);
        TriggerHitboxWindow(airDashAttack, airDashAttack.hitboxDuration);
    }

    private void BufferAttack(bool lightAttack)
    {
        bufferedAttackButton = lightAttack ? AttackButton.Light : AttackButton.Heavy;
        bufferedAttackExpiresAt = Time.time + inputBufferWindow;
    }

    private void AttemptAttack(bool lightAttack)
    {
        tierComboManager?.CancelComboResetCountdown();

        PlayerAttack attackData;
        string attackId;

        if (IsGrounded())
        {
            attackId = ResolveGroundAttackId(lightAttack);
            if (string.IsNullOrEmpty(attackId))
                return;

            attackData = attackDatabase != null ? attackDatabase.GetAttack(attackId) : null;
            if (attackData == null)
            {
                Debug.LogWarning($"[PlayerAttackManager] Attack '{attackId}' not found in database.");
                return;
            }
        }
        else
        {
            if (playerMovement != null && !playerMovement.CanStartAerialCombat())
            {
                // Prevent "barely off the ground" aerial attacks. Attacks are only allowed
                // on ground, or when sufficiently high to commit to aerial combat.
                return;
            }

            attackData = ResolveAerialAttack(lightAttack);
            attackId = attackData != null ? attackData.attackId : null;

            if (attackData == null || string.IsNullOrEmpty(attackId))
            {
                Debug.LogWarning("[PlayerAttackManager] Failed to resolve aerial attack data.");
                return;
            }
        }

        ExecuteAttack(attackData, attackId);
    }

    private string ResolveGroundAttackId(bool lightAttack)
    {
        if (tierComboManager == null)
        {
            Debug.LogWarning("[PlayerAttackManager] TierComboManager missing; cannot resolve grounded attack.");
            return null;
        }

        TierComboManager.AttackStance stance = lightAttack
            ? TierComboManager.AttackStance.Single
            : TierComboManager.AttackStance.AOE;

        return lightAttack
            ? tierComboManager.RequestFastAttack(stance)
            : tierComboManager.RequestHeavyAttack(stance);
    }

    private PlayerAttack ResolveAerialAttack(bool lightAttack)
    {
        if (aerialComboManager == null)
        {
            Debug.LogWarning("[PlayerAttackManager] AerialComboManager missing; cannot resolve aerial attack.");
            return null;
        }

        return lightAttack
            ? aerialComboManager.RequestAerialLightAttack()
            : aerialComboManager.RequestAerialHeavyAttack();
    }

    private PlayerAttack ResolveGuardAttack()
    {
        if (guardAttackOverride != null)
            return guardAttackOverride;

        if (attackDatabase == null || string.IsNullOrWhiteSpace(guardAttackId))
            return null;

        return attackDatabase.GetAttack(guardAttackId);
    }

    private PlayerAttack ResolveAirDashAttack()
    {
        if (airDashAttackOverride != null)
            return airDashAttackOverride;

        if (attackDatabase == null || string.IsNullOrWhiteSpace(airDashAttackId))
            return null;

        return attackDatabase.GetAttack(airDashAttackId);
    }

    private PlayerAttack ResolveLauncherAttack()
    {
        if (launcherAttackOverride != null)
            return launcherAttackOverride;

        if (attackDatabase == null || string.IsNullOrWhiteSpace(launcherAttackId))
            return null;

        return attackDatabase.GetAttack(launcherAttackId);
    }

    private void ExecuteAttack(
        PlayerAttack attackData,
        string attackId,
        Action<PlayerAnimationController> animationOverride = null,
        bool playDefaultAnimation = true)
    {
        currentAttack = attackData;
        currentAttackDamageMultiplier = 1f;

        if (currentAttack != null
            && currentAttack.attackType == AttackType.HeavyAerial
            && aerialComboManager != null)
        {
            currentAttackDamageMultiplier = Mathf.Max(1f, aerialComboManager.ConsumePendingPlungeDamageMultiplier());

            if (debugPlungeDamage)
            {
                float scaledDamage = currentAttack.damage * currentAttackDamageMultiplier;
                Debug.Log(
                    $"[PlayerAttackManager] Plunge execute: '{currentAttack.attackId}' base={currentAttack.damage} x{currentAttackDamageMultiplier:0.##} => {scaledDamage:0.##}"
                );
            }
        }
        InputReader.inputBusy = true;

        UpdateLastAttackType(attackData);

        PlaySfx(attackData.attackSFX);

        if (animationOverride != null)
            animationOverride(animationController);
        else if (playDefaultAnimation)
            animationController?.PlayAttack(attackId);

        OnAttack?.Invoke(attackData);

        Debug.Log($"[PlayerAttackManager] Executing attack {attackData.attackName} ({attackId})");
    }

    private void ApplyAttackForwardMove(PlayerAttack attackData)
    {
        if (attackData == null)
            return;

        float distance = attackData.forwardMoveDistance;
        if (distance <= 0f)
            return;

        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.0001f)
            return;
        forward.Normalize();

        float duration = attackData.forwardMoveDuration;
        if (playerMovement != null)
        {
            playerMovement.StartAttackForwardMove(forward, distance, duration);
            return;
        }

        if (duration <= 0f)
        {
            MoveXZ(forward * distance);
            return;
        }

        if (heavyMoveRoutine != null)
            StopCoroutine(heavyMoveRoutine);

        heavyMoveRoutine = StartCoroutine(HeavyAttackForwardMoveRoutine(forward, distance, duration));
    }

    private IEnumerator HeavyAttackForwardMoveRoutine(Vector3 forward, float distance, float duration)
    {
        float elapsed = 0f;
        Vector3 totalMove = forward * distance;
        Vector3 moved = Vector3.zero;

        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            Vector3 target = totalMove * t;
            Vector3 delta = target - moved;
            if (delta.sqrMagnitude > 0f)
            {
                MoveXZ(delta);
                moved += delta;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        Vector3 finalDelta = totalMove - moved;
        if (finalDelta.sqrMagnitude > 0f)
            MoveXZ(finalDelta);

        heavyMoveRoutine = null;
    }

    private void MoveXZ(Vector3 delta)
    {
        Vector3 planarDelta = new Vector3(delta.x, 0f, delta.z);
        if (planarDelta.sqrMagnitude <= 0f)
            return;

        if (characterController != null)
            characterController.Move(planarDelta);
        else
            transform.position += planarDelta;
    }

    private bool IsGrounded()
    {
        if (characterController != null)
            return characterController.isGrounded;

        return PlayerMovement.isGrounded;
    }

    private void PlaySfx(AudioClip clip)
    {
        if (clip == null)
        {
            Debug.LogWarning("[PlayerAttackManager] PlaySfx called with null AudioClip.");
            return;
        }

        var source = attackAudioSource != null ? attackAudioSource : fallbackSfxSource;
        if (source == null)
        {
            Debug.LogError("[PlayerAttackManager] No AudioSource available! attackAudioSource and fallbackSfxSource are both null. Check SoundManager.Instance.");
            return;
        }

        source.PlayOneShot(clip);
        Debug.Log($"[PlayerAttackManager] Playing SFX: {clip.name} on {source.gameObject.name}");
    }

    private void ClearHitbox()
    {
        if (hitboxLifetimeRoutine != null)
        {
            StopCoroutine(hitboxLifetimeRoutine);
            hitboxLifetimeRoutine = null;
        }

        if (activeHitbox != null)
        {
            Destroy(activeHitbox);
            activeHitbox = null;
        }
    }

    private void SpawnHitbox(PlayerAttack attack)
    {
        ClearHitbox();

        attack.GetHitboxPose(transform.position, transform.forward, out var spawnPosition, out var spawnRotation);
        activeHitbox = attack.CreateHitBoxAt(spawnPosition, spawnRotation);
        if (activeHitbox != null)
            activeHitbox.transform.SetParent(transform, worldPositionStays: true);

        if (activeHitbox != null
            && currentAttack != null
            && ReferenceEquals(attack, currentAttack)
            && currentAttack.attackType == AttackType.HeavyAerial
            && activeHitbox.TryGetComponent<HitboxDamageManager>(out var damageManager))
        {
            float multiplier = Mathf.Max(1f, currentAttackDamageMultiplier);
            float scaledDamage = currentAttack.damage * multiplier;
            damageManager.Configure(currentAttack.attackName, scaledDamage, currentAttack.maxTargetsPerActivation);

            if (debugPlungeDamage)
            {
                Debug.Log(
                    $"[PlayerAttackManager] Plunge hitbox spawned: '{currentAttack.attackId}' base={currentAttack.damage} x{multiplier:0.##} => {scaledDamage:0.##}"
                );
            }
        }

        PlayAttackVfx(attack, spawnPosition, spawnRotation);
    }

    public void SpawnOneShotHitbox(string attackId, float activeDuration)
    {
        var attackData = attackDatabase?.GetAttack(attackId);
        if (attackData == null)
        {
            Debug.LogWarning($"[PlayerAttackManager] Cannot spawn hitbox; attack '{attackId}' missing.");
            return;
        }

        TriggerHitboxWindow(attackData, Mathf.Max(0f, activeDuration));
    }

    private void PlayAttackVfx(PlayerAttack attack, Vector3 fallbackPosition, Quaternion fallbackRotation)
    {
        if (attack == null)
            return;

        bool spawnedAny = false;
        foreach (var entry in attack.EnumerateAllVfx())
        {
            if (entry.Prefab == null)
                continue;

            spawnedAny = true;
            SpawnVfxInstance(entry, fallbackPosition, fallbackRotation);
        }

        if (!spawnedAny && attack.hitVfxPrefab != null)
        {
            // Defensive fallback for legacy data if enumeration filtered everything out
            SpawnVfxInstance(new PlayerAttack.VfxEntry(attack.hitVfxPrefab, attack.vfxAnchorId, attack.vfxLifetime), fallbackPosition, fallbackRotation);
        }
    }

    private void SpawnVfxInstance(PlayerAttack.VfxEntry entry, Vector3 fallbackPosition, Quaternion fallbackRotation)
    {
        if (entry.Prefab == null)
            return;

        Transform anchor = vfxAnchorRegistry != null && !string.IsNullOrEmpty(entry.AnchorId)
            ? vfxAnchorRegistry.ResolveAnchor(entry.AnchorId)
            : null;

        Vector3 spawnPosition = anchor != null ? anchor.position : fallbackPosition;
        Quaternion spawnRotation = anchor != null ? anchor.rotation : fallbackRotation;

        GameObject vfxInstance = Instantiate(entry.Prefab, spawnPosition, spawnRotation);
        if (anchor != null)
            vfxInstance.transform.SetParent(anchor, worldPositionStays: true);
        else
            vfxInstance.transform.SetParent(transform, worldPositionStays: true);

        float lifetime = entry.Lifetime;
        if (lifetime >= 0f)
        {
            if (lifetime == 0f)
                Destroy(vfxInstance);
            else
                Destroy(vfxInstance, lifetime);
        }
    }

    private void TriggerHitboxWindow(PlayerAttack attack, float overrideDuration)
    {
        if (attack == null)
            return;

        SpawnHitbox(attack);

        float lifetime = overrideDuration >= 0f
            ? overrideDuration
            : attack.hitboxDuration;

        BeginHitboxLifetime(lifetime);
    }

    private void StartGuardAttackFlow(PlayerAttack guardAttack)
    {
        if (!guardAttackAutoGenerateHitbox || guardAttack == null)
            return;

        StopGuardAttackFlowRoutine();
        guardAttackFlowRoutine = StartCoroutine(GuardAttackFlowRoutine(guardAttack));
    }

    private IEnumerator GuardAttackFlowRoutine(PlayerAttack guardAttack)
    {
        float delay = Mathf.Max(0f, guardAttackHitboxDelay);
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        TriggerHitboxWindow(guardAttack, guardAttack.hitboxDuration);
        guardAttackFlowRoutine = null;
    }

    private void StopGuardAttackFlowRoutine()
    {
        if (guardAttackFlowRoutine == null)
            return;

        StopCoroutine(guardAttackFlowRoutine);
        guardAttackFlowRoutine = null;
    }

    private void ScheduleSpecialAttackAutoCancel(float duration)
    {
        StopSpecialAttackAutoCancelRoutine();

        if (duration <= 0f)
            return;

        specialAttackAutoCancelRoutine = StartCoroutine(SpecialAttackAutoCancelRoutine(duration));
    }

    private IEnumerator SpecialAttackAutoCancelRoutine(float duration)
    {
        yield return new WaitForSeconds(duration);
        specialAttackAutoCancelRoutine = null;

        if (currentAttack != null)
            CompleteCancelWindow();
    }

    private void StopSpecialAttackAutoCancelRoutine()
    {
        if (specialAttackAutoCancelRoutine == null)
            return;

        StopCoroutine(specialAttackAutoCancelRoutine);
        specialAttackAutoCancelRoutine = null;
    }

    private static float GetAnimationDurationOrFallback(PlayerAttack attack, float fallback)
    {
        if (attack != null && attack.animationClip != null)
            return Mathf.Max(0.05f, attack.animationClip.length);

        return Mathf.Max(0f, fallback);
    }

    private void BeginHitboxLifetime(float duration)
    {
        if (duration <= 0f)
        {
            ClearHitbox();
            return;
        }

        if (hitboxLifetimeRoutine != null)
            StopCoroutine(hitboxLifetimeRoutine);

        hitboxLifetimeRoutine = StartCoroutine(HitboxLifetimeRoutine(duration));
    }

    private IEnumerator HitboxLifetimeRoutine(float duration)
    {
        yield return new WaitForSeconds(Mathf.Max(0f, duration));
        hitboxLifetimeRoutine = null;
        ClearHitbox();
    }

    #region Animation Event Hooks
    public void HandleAnimationHitbox()
    {
        HandleAnimationHitbox(-1f);
    }

    public void HandleAnimationHitbox(float overrideDuration)
    {
        if (currentAttack == null)
        {
            Debug.LogWarning("[PlayerAttackManager] Animation requested a hitbox but no attack is active.");
            return;
        }

        TriggerHitboxWindow(currentAttack, overrideDuration);
    }

    public void HandleAnimationCancelWindow()
    {
        StopSpecialAttackAutoCancelRoutine();
        bool needsPlungeRecovery = currentAttack != null
            && currentAttack.attackType == AttackType.HeavyAerial
            && plungeRecoveryDelay > 0f;

        if (needsPlungeRecovery)
        {
            if (plungeRecoveryRoutine != null)
                StopCoroutine(plungeRecoveryRoutine);

            plungeRecoveryRoutine = StartCoroutine(PlungeRecoveryRoutine());
        }
        else
        {
            CompleteCancelWindow();
        }
    }

    private IEnumerator PlungeRecoveryRoutine()
    {
        yield return new WaitForSeconds(plungeRecoveryDelay);
        plungeRecoveryRoutine = null;
        CompleteCancelWindow();
    }

    private void CompleteCancelWindow()
    {
        InputReader.inputBusy = false;
        playerMovement?.SuppressLocomotionAnimations(false);
        playerMovement?.ForceLocomotionRefresh();

        var finishedAttack = currentAttack;

        if (finishedAttack != null)
        {
            aerialComboManager?.HandleAttackAnimationComplete(finishedAttack);

            bool shouldReturnToCombatIdle = finishedAttack.attackType == AttackType.HeavyAerial
                && characterController != null
                && characterController.isGrounded;

            if (shouldReturnToCombatIdle)
                PlayCombatIdle(plungeIdleBlendTime);
        }

        currentAttack = null;
        currentAttackDamageMultiplier = 1f;

        if (TryConsumeBufferedAttack())
            return;

        tierComboManager?.StartComboResetCountdown();
    }

    public void HandleAnimationMoveForward()
    {
        if (currentAttack == null)
        {
            Debug.LogWarning("[PlayerAttackManager] Animation requested a forward move but no attack is active.");
            return;
        }

        ApplyAttackForwardMove(currentAttack);
    }

    private void PlayCombatIdle(float transition)
    {
        if (animationController == null)
            return;

        if (lastAttackWasAoe)
            animationController.PlayAoeIdleCombat(transition);
        else
            animationController.PlaySingleTargetIdleCombat(transition);
    }

    private void UpdateLastAttackType(PlayerAttack attackData)
    {
        if (attackData == null)
            return;

        switch (attackData.attackType)
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

    private bool TryConsumeBufferedAttack()
    {
        if (ShouldIgnoreAttackInput())
        {
            ClearBufferedAttack();
            return false;
        }

        if (bufferedAttackButton == AttackButton.None)
            return false;

        if (Time.time > bufferedAttackExpiresAt)
        {
            bufferedAttackButton = AttackButton.None;
            bufferedAttackExpiresAt = -1f;
            return false;
        }

        bool lightAttack = bufferedAttackButton == AttackButton.Light;
        bufferedAttackButton = AttackButton.None;
        bufferedAttackExpiresAt = -1f;

        AttemptAttack(lightAttack);
        return true;
    }

    private void ClearBufferedAttack()
    {
        bufferedAttackButton = AttackButton.None;
        bufferedAttackExpiresAt = -1f;
    }

    private bool ShouldIgnoreAttackInput()
    {
        if (InputReader.IsGameplayInputBlocked)
            return true;

        if (CombatManager.isGuarding || CombatManager.isParrying)
            return true;

        return animationController != null && animationController.IsParryHardLocked;
    }

    public void ForceCancelCurrentAttack(bool resetCombo = true)
    {
        if (plungeRecoveryRoutine != null)
        {
            StopCoroutine(plungeRecoveryRoutine);
            plungeRecoveryRoutine = null;
        }

        ClearBufferedAttack();
        StopGuardAttackFlowRoutine();
        StopSpecialAttackAutoCancelRoutine();
        ClearHitbox();
        playerMovement?.CancelPlungeState();
        currentAttack = null;
        currentAttackDamageMultiplier = 1f;
        InputReader.inputBusy = false;
        playerMovement?.SuppressLocomotionAnimations(false);
        playerMovement?.ForceLocomotionRefresh();

        if (resetCombo)
        {
            tierComboManager?.ResetCombo();
        }
        else
        {
            tierComboManager?.CancelComboResetCountdown();
        }
    }
    #endregion
}
