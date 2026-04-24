/*
Written by Brandon Wahl

Uses the health interfaces to increase or decreae hp amount and sets the healthbar accordingly

*/

using System;
using System.Collections;
using UI.Loading;
using UnityEngine;
using UnityEngine.Serialization;
using Progression.Checkpoints;
using UnityEngine.SceneManagement;
using Utilities.Combat.Attacks;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class PlayerHealthBarManager : MonoBehaviour, IHealthSystem, IDataPersistenceManager
{
    [Serializable]
    public readonly struct HealthSnapshot
    {
        public readonly float current;
        public readonly float max;

        public float Normalized => max <= 0f ? 0f : current / max;

        public HealthSnapshot(float current, float max)
        {
            this.current = Mathf.Max(0f, current);
            this.max = Mathf.Max(0f, max);
        }
    }

    public static event Action<float> OnPlayerDamaged;
    public static event Action<float> OnPlayerHealed;
    public static event Action<HealthSnapshot> OnPlayerHealthChanged;
    public static event Action OnPlayerDied;
    public static event Action<PlayerHealthBarManager> OnPlayerHealthRegistered;

    #region Inspector Setup
    [Header("Health Settings")]
    [SerializeField, Min(1f)] private float maxHealth = 500f;
    [SerializeField] private float currentHealth = -1f;
    [SerializeField, Range(0f, 1f)] private float startingHealthPercent = 1f;
    [SerializeField, Tooltip("When true, all incoming damage is ignored.")] private bool invulnerable = false;

    [Header("Death Handling")]
    [SerializeField, Tooltip("Automatically restart from the active checkpoint when the player dies.")]
    private bool restartFromCheckpointOnDeath = true;
    [SerializeField, Tooltip("Destroy the player GameObject after death once cleanup logic runs.")]
    private bool destroyPlayerOnDeath = false;
    [FormerlySerializedAs("deathPoseHoldSeconds")]
    [SerializeField, Range(0f, 6f), Tooltip("Seconds to wait after triggering the death animation before the loading fade may begin.")]
    private float deathFadeDelaySeconds = 2f;
    [SerializeField, Range(0f, 1f), Tooltip("Normalized time within the death animation that must be reached before triggering the loading fade. Set to 0 to rely only on the delay.")]
    private float deathFadeNormalizedThreshold = 0f;

    [Header("Reactions")]
    [SerializeField, Tooltip("When enabled, generic incoming damage can trigger random flinch stagger. Keep disabled if stagger should only come from explicitly configured sources.")]
    private bool enableGenericDamageFlinch = false;
    [SerializeField, Range(0f, 1f)] private float flinchChance = 0.2f;
    [SerializeField, Range(0f, 2f)] private float flinchLockSeconds = 0.35f;

    [Header("Defense")]
    [SerializeField, Tooltip("When enabled, dashing grants brief invincibility (i-frames).")]
    private bool enableDashInvincibility = true;
    [SerializeField, Range(0.05f, 2f), Tooltip("Failsafe duration in case dash i-frame end animation event is missed. Set to a small value to avoid accidental permanent invulnerability.")]
    private float dashInvincibilityFailsafeSeconds = 0.5f;

    [Header("Out of Combat Regeneration")]
    [SerializeField, Tooltip("When enabled, player health passively regenerates after being out of combat for a delay.")]
    private bool enableOutOfCombatRegen = true;
    [SerializeField, Min(0f), Tooltip("Seconds without combat activity before passive regeneration starts.")]
    private float outOfCombatRegenDelaySeconds = 10f;
    [SerializeField, Min(0f), Tooltip("Health regenerated per second as a percent of max health (e.g. 0.5 = 0.5% of max HP per second).")]
    private float outOfCombatRegenRatePercentPerSecond = 0.5f;
    [SerializeField, Range(0f, 100f), Tooltip("Maximum percent of max health that passive regeneration can restore up to.")]
    private float outOfCombatRegenMaxPercent = 35f;

    [Header("References")]
    [SerializeField] private PlayerAnimationController animationController;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerAttackManager attackManager;

    [Header("UI")]
    [SerializeField] private HealthBar healthBar;

    [Header("SFX")]
    [SerializeField] private AudioClip[] playerHurtSFX;
    [SerializeField] private AudioClip[] impactSFX;
    [SerializeField] private AudioClip[] playerDeathSFX;

    [Header("Lose Health Rumble Settings")]
    [SerializeField] private float _rumbleLowFrequency = 0.5f;
    [SerializeField] private float _rumbleHighFrequency = 0.5f;
    [SerializeField] private float _rumbleDuration = 0.5f;

    [Header("Gain Health Rumble Settings")]
    [SerializeField] private float _gainHealthRumbleLowFrequency = 0.5f;
    [SerializeField] private float _gainHealthRumbleHighFrequency = 0.5f;
    [SerializeField] private float _gainHealthRumbleDuration = 0.5f;

    [Header("Death Rumble Settings")]
    [SerializeField] private float _deathRumbleLowFrequency = 0.5f;
    [SerializeField] private float _deathRumbleHighFrequency = 0.5f;
    [SerializeField] private float _deathRumbleDuration = 0.5f;

    [Header("Debug")]
    [SerializeField, Tooltip("Damage applied when using the debug buttons.")]
    private float debugDamageAmount = 100f;
    #endregion

    public static PlayerHealthBarManager Instance { get; private set; }

    float IHealthSystem.currentHP => CurrentHealth;
    float IHealthSystem.maxHP => MaxHealth;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public float NormalizedHealth => maxHealth <= 0f ? 0f : currentHealth / maxHealth;
    public bool IsDead => isDead;

    private bool isDead;
    private Coroutine flinchRoutine;
    private Coroutine deathSequenceRoutine;
    private bool deathInputLockOwned;
    private bool waitingForRespawnHeal;
    private bool suppressNextFlinch;
    private bool dashInvincibilityActive;
    private float dashInvincibilityFailsafeUntilUnscaledTime;
    private bool attackManagerDisabledByDeath;
    private bool hasLoadedPersistentHealth;
    private float defaultMaxHealth;
    private float defaultCurrentHealth;
    private int lastKnownSceneHandle = -1;
    private float lastCombatActivityTime;

    #region Unity MonoBehaviour Functions
    private void Awake()
    {
        Instance = this;
        lastKnownSceneHandle = gameObject.scene.handle;
        Player.ClearCachedPlayerObject();
        lastCombatActivityTime = Time.unscaledTime;

        if (animationController == null) animationController = GetComponentInChildren<PlayerAnimationController>();
        if (playerMovement == null) playerMovement = GetComponent<PlayerMovement>();
        if (attackManager == null) attackManager = GetComponent<PlayerAttackManager>();

        if (currentHealth < 0f)
        {
            currentHealth = Mathf.Clamp(maxHealth * Mathf.Clamp01(startingHealthPercent), 0f, maxHealth);
        }

        defaultMaxHealth = Mathf.Max(1f, maxHealth);
        defaultCurrentHealth = Mathf.Clamp(currentHealth, 0f, defaultMaxHealth);

        NotifyHealthChanged();
        OnPlayerHealthRegistered?.Invoke(this);
    }

    private void OnDestroy()
    {
        Player.ClearCachedPlayerObject();

        if (Instance == this)
        {
            Instance = null;
            OnPlayerHealthRegistered?.Invoke(null);
        }
    }

    /*
     * OnEnable and OnDisable are used to set up event subscriptions for revive functionality.
     * It also checks the boolean in Player to determine whether the player is considered active or not.
     */
    private void OnEnable() 
    {
        Player.ClearCachedPlayerObject();
        Player.SetActive(true);
        Player.RespawnPlayer += HandleRespawnRequested;
        PlayerAttackManager.OnAttack += HandlePlayerAttackPerformed;
        CheckpointBehavior.SubscribeToPlayerRespawn();

        dashInvincibilityActive = false;
        dashInvincibilityFailsafeUntilUnscaledTime = 0f;

        RefreshRegistration();
    }
    private void OnDisable() 
    { 
        Player.ClearCachedPlayerObject();
        Player.SetActive(false); 
        Player.RespawnPlayer -= HandleRespawnRequested;
        PlayerAttackManager.OnAttack -= HandlePlayerAttackPerformed;
        CheckpointBehavior.UnsubscribeFromPlayerRespawn();
        LoadingScreenController.OnLoadingScreenShown -= HandleLoadingScreenShown;
        waitingForRespawnHeal = false;

        dashInvincibilityActive = false;
        dashInvincibilityFailsafeUntilUnscaledTime = 0f;
    }

    private void Update()
    {
        if (lastKnownSceneHandle == gameObject.scene.handle)
        {
            ApplyOutOfCombatRegeneration(Time.deltaTime);
            return;
        }

        lastKnownSceneHandle = gameObject.scene.handle;
        Player.ClearCachedPlayerObject();
        RefreshRegistration();
        ApplyOutOfCombatRegeneration(Time.deltaTime);
    }

    private void OnTransformParentChanged()
    {
        Player.ClearCachedPlayerObject();
        RefreshRegistration();
    }
    #endregion

    public void HealHP(float hp)
    {
        if (isDead || hp <= 0f)
            return;

        RumbleManager.Instance.RumblePulse(_gainHealthRumbleLowFrequency, _gainHealthRumbleHighFrequency, _gainHealthRumbleDuration);

        float previous = currentHealth;
        currentHealth = Mathf.Min(maxHealth, currentHealth + hp);
        float actual = currentHealth - previous;
        if (actual <= 0f)
            return;

        OnPlayerHealed?.Invoke(actual);
        NotifyHealthChanged();
    }

    private void LoseHPPass(float damage)
    {
        LoseHP(damage, _rumbleDuration, _rumbleLowFrequency, _rumbleHighFrequency);
    }

    public void LoseHP(float damage, float _rumbleDuration, float _rumbleLowFrequency, float _rumbleHighFrequency)
    {
        if (isDead || invulnerable || IsTemporarilyInvincible() || damage <= 0f)
            return;

        MarkCombatActivity();

        RumbleManager.Instance.RumblePulse(_rumbleLowFrequency, _rumbleHighFrequency, _rumbleDuration);

        float previous = currentHealth;
        currentHealth = Mathf.Max(0f, currentHealth - damage);

        if (playerHurtSFX != null && playerHurtSFX.Length > 0)
        {
            int index = UnityEngine.Random.Range(0, playerHurtSFX.Length);
            AudioClip clip = playerHurtSFX[index];
            if (clip != null)
            {
                SoundManager soundManager = SoundManager.Instance;
                AudioSource source = soundManager != null ? soundManager.voiceSource : null;
                if (source != null)
                {
                    source.PlayOneShot(clip);
                }
                else if (!PlayerMovement.IsTestingOrDebugMode)
                {
                    Debug.LogError("[PlayerHealthBarManager] Cannot play hurt SFX because SoundManager.voiceSource is missing.");
                }
            }
        }

        if (impactSFX != null && impactSFX.Length > 0)
        {
            int index = UnityEngine.Random.Range(0, impactSFX.Length);
            AudioClip clip = impactSFX[index];
            if (clip != null)
            {
                SoundManager soundManager = SoundManager.Instance;
                AudioSource source = soundManager != null ? soundManager.sfxSource : null;
                if (source != null)
                {
                    source.PlayOneShot(clip);
                }
                else if (!PlayerMovement.IsTestingOrDebugMode)
                {
                    Debug.LogError("[PlayerHealthBarManager] Cannot play impact SFX because SoundManager.sfxSource is missing.");
                }
            }
        }

        float actual = previous - currentHealth;
        if (actual <= 0f)
            return;

        OnPlayerDamaged?.Invoke(actual);
        NotifyHealthChanged();

        bool skipFlinchThisHit = suppressNextFlinch;
        suppressNextFlinch = false;

        if (enableGenericDamageFlinch && currentHealth > 0f && !skipFlinchThisHit)
        {
            TryTriggerFlinch();
        }

        if (currentHealth <= 0f)
        {
            HandleDeath(true);
        }
    }

    public void ForceFullHeal(bool notifyListeners = true)
    {
        ResetDeathSequenceState();
        isDead = false;
        currentHealth = maxHealth;
        if (notifyListeners)
        {
            NotifyHealthChanged();
        }
    }

    public void RestoreDesignTimeDefaults(bool fullHeal = true)
    {
        ResetDeathSequenceState();
        isDead = false;
        maxHealth = Mathf.Max(1f, defaultMaxHealth);
        currentHealth = fullHeal ? maxHealth : Mathf.Clamp(defaultCurrentHealth, 0f, maxHealth);
        NotifyHealthChanged();
    }

    private void Revive() => Revive(1f);
    private void Revive(float percentOfMax = 1f)
    {
        ResetDeathSequenceState();
        isDead = false;
        currentHealth = Mathf.Clamp(maxHealth * Mathf.Clamp01(percentOfMax), 0f, maxHealth);
        NotifyHealthChanged();
    }

    private void HandleRespawnRequested()
    {
        if (waitingForRespawnHeal)
            return;

        waitingForRespawnHeal = true;
        LoadingScreenController.OnLoadingScreenShown += HandleLoadingScreenShown;
    }

    private void HandleLoadingScreenShown()
    {
        if (!waitingForRespawnHeal)
            return;

        LoadingScreenController.OnLoadingScreenShown -= HandleLoadingScreenShown;
        waitingForRespawnHeal = false;
        Revive();
    }

    public void SetMaxHealth(float newMaxHealth)
    {
        maxHealth = Mathf.Max(1f, newMaxHealth);
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        NotifyHealthChanged();
    }
    
    public void SetCurrentHealth(float newCurrentHealth)
    {
        currentHealth = Mathf.Clamp(newCurrentHealth, 0f, maxHealth);

        if (PlayerMovement.IsTestingOrDebugMode && currentHealth <= 0f)
            currentHealth = Mathf.Max(1f, maxHealth * 0.1f);

        NotifyHealthChanged();

        if (currentHealth <= 0f)
        {
            HandleDeath(true);
        }
    }

    public void SuppressNextFlinch()
    {
        suppressNextFlinch = true;
    }

    public void BeginDashInvincibilityWindow()
    {
        if (!enableDashInvincibility)
            return;

        dashInvincibilityActive = true;
        dashInvincibilityFailsafeUntilUnscaledTime = Time.unscaledTime + Mathf.Max(0.05f, dashInvincibilityFailsafeSeconds);
    }

    public void EndDashInvincibilityWindow()
    {
        dashInvincibilityActive = false;
        dashInvincibilityFailsafeUntilUnscaledTime = 0f;
    }

    private bool IsTemporarilyInvincible()
    {
        if (dashInvincibilityActive && Time.unscaledTime > dashInvincibilityFailsafeUntilUnscaledTime)
        {
            dashInvincibilityActive = false;
            dashInvincibilityFailsafeUntilUnscaledTime = 0f;
        }

        return dashInvincibilityActive;
    }

    public void LoadData(GameData data)
    {
        if (hasLoadedPersistentHealth)
            return;

        maxHealth = data.maxHealth > 0 ? data.maxHealth : maxHealth;
        currentHealth = Mathf.Clamp(data.health, 0f, maxHealth);
        isDead = currentHealth <= 0f;
        hasLoadedPersistentHealth = true;
        if (!isDead)
        {
            ResetDeathSequenceState();
        }
        NotifyHealthChanged();
    }

    public void SaveData(GameData data)
    {
        data.maxHealth = maxHealth;
        data.health = currentHealth;
    }

    public void HandleDeath(bool playDeathAnimation)
    {
        if (isDead) return;

        if (PlayerMovement.IsTestingOrDebugMode)
        {
            isDead = false;
            currentHealth = Mathf.Max(1f, currentHealth);
            NotifyHealthChanged();
            return;
        }

        isDead = true;
        

        currentHealth = 0f;

        RumbleManager.Instance.RumblePulse(_deathRumbleLowFrequency, _deathRumbleHighFrequency, _deathRumbleDuration);

        CancelFlinchRoutine();
        attackManager?.ForceCancelCurrentAttack();

        OnPlayerDied?.Invoke();

        if (deathSequenceRoutine != null) StopCoroutine(deathSequenceRoutine);

        deathSequenceRoutine = StartCoroutine(DeathSequenceRoutine(playDeathAnimation));

        if (!CutsceneManager.IsCutscenePlaying && Time.timeScale > 0f && playerDeathSFX != null)
        {
            SoundManager soundManager = SoundManager.Instance;
            AudioSource source = soundManager != null ? soundManager.voiceSource : null;
            if (source != null)
                source.PlayOneShot(playerDeathSFX[UnityEngine.Random.Range(0, playerDeathSFX.Length)]);
            else if (!PlayerMovement.IsTestingOrDebugMode)
                Debug.LogError("[PlayerHealthBarManager] Cannot play death SFX because SoundManager.voiceSource is missing.");
        }
    }

    private void NotifyHealthChanged()
    {
        var snapshot = new HealthSnapshot(currentHealth, maxHealth);
        if (healthBar != null)
        {
            healthBar.SetHealth(snapshot.current, snapshot.max);
        }
        OnPlayerHealthChanged?.Invoke(snapshot);
    }

    private void RefreshRegistration()
    {
        if (!isActiveAndEnabled)
            return;

        OnPlayerHealthRegistered?.Invoke(this);
        NotifyHealthChanged();
    }

    private void HandlePlayerAttackPerformed(PlayerAttack attack)
    {
        MarkCombatActivity();
    }

    private void MarkCombatActivity()
    {
        lastCombatActivityTime = Time.unscaledTime;
    }

    private void ApplyOutOfCombatRegeneration(float deltaTime)
    {
        if (!enableOutOfCombatRegen || isDead || deltaTime <= 0f)
            return;

        float capPercent = Mathf.Clamp(outOfCombatRegenMaxPercent, 0f, 100f);
        float regenCapHealth = maxHealth * (capPercent / 100f);
        if (currentHealth >= regenCapHealth)
            return;

        float requiredDelay = Mathf.Max(0f, outOfCombatRegenDelaySeconds);
        if ((Time.unscaledTime - lastCombatActivityTime) < requiredDelay)
            return;

        float regenPercentPerSecond = Mathf.Max(0f, outOfCombatRegenRatePercentPerSecond);
        if (regenPercentPerSecond <= 0f)
            return;

        float healAmount = maxHealth * (regenPercentPerSecond / 100f) * deltaTime;
        if (healAmount <= 0f)
            return;

        float previous = currentHealth;
        currentHealth = Mathf.Min(regenCapHealth, currentHealth + healAmount);

        float actual = currentHealth - previous;
        if (actual <= 0f)
            return;

        OnPlayerHealed?.Invoke(actual);
        NotifyHealthChanged();
    }

    private void TryTriggerFlinch()
    {
        if (isDead)
            return;

        if (flinchChance <= 0f)
            return;

        if (flinchRoutine != null)
            return;

        if (UnityEngine.Random.value > flinchChance)
            return;

        if (animationController == null && playerMovement == null && attackManager == null)
            return;

        flinchRoutine = StartCoroutine(FlinchRoutine());
    }

    private IEnumerator FlinchRoutine()
    {
        attackManager?.ForceCancelCurrentAttack(resetCombo: false);
        playerMovement?.ApplyExternalStun(flinchLockSeconds);
        animationController?.PlayHit();

        float timer = Mathf.Max(0.05f, flinchLockSeconds);
        while (timer > 0f)
        {
            timer -= Time.deltaTime;
            yield return null;
        }

        flinchRoutine = null;
    }

    public void ApplyForcedStagger(float duration, bool resetCombo = true)
    {
        if (isDead)
            return;

        if (duration <= 0f)
            duration = flinchLockSeconds;

        if (flinchRoutine != null)
        {
            StopCoroutine(flinchRoutine);
            flinchRoutine = null;
        }

        flinchRoutine = StartCoroutine(ForcedStaggerRoutine(duration, resetCombo));
    }

    private IEnumerator ForcedStaggerRoutine(float duration, bool resetCombo)
    {
        attackManager?.ForceCancelCurrentAttack(resetCombo: resetCombo);
        playerMovement?.ApplyExternalStun(duration);
        animationController?.PlayHit();

        float timer = Mathf.Max(0.05f, duration);
        while (timer > 0f)
        {
            timer -= Time.deltaTime;
            yield return null;
        }

        flinchRoutine = null;
    }

    private void CancelFlinchRoutine()
    {
        if (flinchRoutine == null)
            return;

        StopCoroutine(flinchRoutine);
        flinchRoutine = null;
    }

    private IEnumerator DeathSequenceRoutine(bool playDeathAnimation)
    {
        if (attackManager != null && attackManager.enabled)
        {
            attackManager.enabled = false;
            attackManagerDisabledByDeath = true;
        }

        playerMovement?.EnterDeathState();
        AcquireDeathInputLock();
        if(playDeathAnimation) animationController?.PlayDeath();

        yield return WaitForDeathFadeTiming(playDeathAnimation);

        bool canRespawnAtCheckpoint = CheckpointBehavior.currentCheckpoint != null || PlayerMovement.IsTestingOrDebugMode;

        if (restartFromCheckpointOnDeath && canRespawnAtCheckpoint)
        {
            Player.TriggerRespawn();
        }
        else if (destroyPlayerOnDeath)
        {
            Destroy(gameObject);
        }
        else if (restartFromCheckpointOnDeath && !canRespawnAtCheckpoint)
        {
            animationController?.FreezeCurrentPose();
            Debug.LogWarning("[PlayerHealthBarManager] Player is dead with no active checkpoint. Holding final death pose.");
            deathSequenceRoutine = null;
            yield break;
        }

        ReleaseDeathSequenceLocks();
        deathSequenceRoutine = null;
    }

    private void AcquireDeathInputLock()
    {
        if (InputReader.inputBusy)
        {
            deathInputLockOwned = false;
            return;
        }

        InputReader.inputBusy = true;
        deathInputLockOwned = true;
    }

    private void ReleaseDeathSequenceLocks()
    {
        if (deathInputLockOwned)
        {
            if (InputReader.inputBusy)
                InputReader.inputBusy = false;
            deathInputLockOwned = false;
        }
    }

    private void ResetDeathSequenceState()
    {
        if (deathSequenceRoutine != null)
        {
            StopCoroutine(deathSequenceRoutine);
            deathSequenceRoutine = null;
        }

        ReleaseDeathSequenceLocks();

        if (attackManagerDisabledByDeath && attackManager != null)
        {
            attackManager.enabled = true;
            attackManagerDisabledByDeath = false;
        }

        playerMovement?.ExitDeathState();
    }

    private IEnumerator WaitForDeathFadeTiming(bool playDeathAnimation)
    {
        float delay = playDeathAnimation && animationController != null
            ? Mathf.Max(0f, deathFadeDelaySeconds)
            : 0.5f;

        if (delay > 0f)
        {
            yield return new WaitForSecondsRealtime(delay);
        }

        if (!playDeathAnimation || animationController == null)
            yield break;

        float threshold = Mathf.Clamp01(deathFadeNormalizedThreshold);
        if (threshold <= 0f)
            yield break;

        float timeout = 2f;
        float elapsed = 0f;
        while (animationController.IsPlayingDeath(out float normalized))
        {
            if (normalized >= threshold)
                break;

            elapsed += Time.unscaledDeltaTime;
            if (elapsed >= timeout)
                break;

            yield return null;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Only react to colliders that are both enemies and expose attack data
        if (!other.CompareTag("Enemy"))
            return;

        if (!other.TryGetComponent<IAttackSystem>(out var attack))
            return;

        LoseHPPass(attack.damageAmount);
    }

    public void SetInvulnerable(bool value) => invulnerable = value;

#if UNITY_EDITOR
    [ContextMenu("Debug/Apply Damage")]
    private void ContextApplyDebugDamage()
    {
        DebugApplyDamage();
    }

    [ContextMenu("Debug/Kill Player")]
    private void ContextKillPlayer()
    {
        DebugKillPlayer();
    }

    public void DebugApplyDamage()
    {
        if (!Application.isPlaying)
            return;

        float amount = Mathf.Max(1f, debugDamageAmount);
        LoseHP(amount, 0.5f, 0.5f, 0.5f);
    }

    public void DebugKillPlayer()
    {
        if (!Application.isPlaying)
            return;

        LoseHP(maxHealth * 2f, 0.5f, 0.5f, 0.5f);
    }
#endif
}

#if UNITY_EDITOR
[CustomEditor(typeof(PlayerHealthBarManager))]
public sealed class PlayerHealthBarManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            var manager = (PlayerHealthBarManager)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Debug Tools", EditorStyles.boldLabel);
            if (GUILayout.Button("Apply Debug Damage"))
            {
                manager.DebugApplyDamage();
            }
            if (GUILayout.Button("Kill Player"))
            {
                manager.DebugKillPlayer();
            }
        }
    }
}
#endif
