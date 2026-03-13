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
    [SerializeField, Range(0f, 1f)] private float flinchChance = 0.2f;
    [SerializeField, Range(0f, 2f)] private float flinchLockSeconds = 0.35f;

    [Header("Defense")]
    [SerializeField, Tooltip("When enabled, dashing grants brief invincibility (i-frames).")]
    private bool enableDashInvincibility = true;
    [SerializeField, Range(0f, 1f), Tooltip("Real-time seconds of invincibility granted as soon as a dash starts.")]
    private float dashInvincibilitySeconds = 0.35f;

    [Header("References")]
    [SerializeField] private PlayerAnimationController animationController;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerAttackManager attackManager;

    [Header("UI")]
    [SerializeField] private HealthBar healthBar;

    [Header("SFX")]
    [SerializeField] private AudioClip[] playerHurtSFX;
    [SerializeField] private AudioClip playerDeathSFX;

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
    private float invincibleUntilUnscaledTime;
    private float defaultMaxHealth;
    private float defaultCurrentHealth;

    #region Unity MonoBehaviour Functions
    private void Awake()
    {
        Instance = this;

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
        Player.SetActive(true);
        Player.RespawnPlayer += HandleRespawnRequested;
        CheckpointBehavior.SubscribeToPlayerRespawn();

        if (playerMovement != null)
        {
            playerMovement.DashPerformed += HandleDashPerformed;
        }
    }
    private void OnDisable() 
    { 
        Player.SetActive(false); 
        Player.RespawnPlayer -= HandleRespawnRequested;
        CheckpointBehavior.UnsubscribeFromPlayerRespawn();
        LoadingScreenController.OnLoadingScreenShown -= HandleLoadingScreenShown;
        waitingForRespawnHeal = false;

        if (playerMovement != null)
        {
            playerMovement.DashPerformed -= HandleDashPerformed;
        }
    }
    #endregion

    public void HealHP(float hp)
    {
        if (isDead || hp <= 0f)
            return;

        float previous = currentHealth;
        currentHealth = Mathf.Min(maxHealth, currentHealth + hp);
        float actual = currentHealth - previous;
        if (actual <= 0f)
            return;

        OnPlayerHealed?.Invoke(actual);
        NotifyHealthChanged();
    }

    public void LoseHP(float damage)
    {
        if (isDead || invulnerable || IsTemporarilyInvincible() || damage <= 0f)
            return;

        float previous = currentHealth;
        currentHealth = Mathf.Max(0f, currentHealth - damage);
        if (playerHurtSFX != null && playerHurtSFX.Length > 0)
        {
            int index = UnityEngine.Random.Range(0, playerHurtSFX.Length);
            SoundManager.Instance.voiceSource.PlayOneShot(playerHurtSFX[index]);
        }
        float actual = previous - currentHealth;
        if (actual <= 0f)
            return;

        OnPlayerDamaged?.Invoke(actual);
        NotifyHealthChanged();

        bool skipFlinchThisHit = suppressNextFlinch;
        suppressNextFlinch = false;

        if (currentHealth > 0f && !skipFlinchThisHit)
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

    private void HandleDashPerformed()
    {
        if (!enableDashInvincibility)
            return;

        if (dashInvincibilitySeconds <= 0f)
            return;

        float until = Time.unscaledTime + dashInvincibilitySeconds;
        if (until > invincibleUntilUnscaledTime)
        {
            invincibleUntilUnscaledTime = until;
        }
    }

    private bool IsTemporarilyInvincible()
    {
        return Time.unscaledTime < invincibleUntilUnscaledTime;
    }

    public void LoadData(GameData data)
    {
        maxHealth = data.maxHealth > 0 ? data.maxHealth : maxHealth;
        currentHealth = Mathf.Clamp(data.health, 0f, maxHealth);
        isDead = currentHealth <= 0f;
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

        isDead = true;
        

        currentHealth = 0f;

        CancelFlinchRoutine();
        attackManager?.ForceCancelCurrentAttack();

        OnPlayerDied?.Invoke();

        if (deathSequenceRoutine != null) StopCoroutine(deathSequenceRoutine);

        deathSequenceRoutine = StartCoroutine(DeathSequenceRoutine(playDeathAnimation));

        if (!CutsceneManager.IsCutscenePlaying && Time.timeScale > 0f)
            SoundManager.Instance.voiceSource.PlayOneShot(playerDeathSFX);
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

    private void CancelFlinchRoutine()
    {
        if (flinchRoutine == null)
            return;

        StopCoroutine(flinchRoutine);
        flinchRoutine = null;
    }

    private IEnumerator DeathSequenceRoutine(bool playDeathAnimation)
    {
        playerMovement?.EnterDeathState();
        AcquireDeathInputLock();
        if(playDeathAnimation) animationController?.PlayDeath();

        yield return WaitForDeathFadeTiming(playDeathAnimation);

        if (restartFromCheckpointOnDeath) Player.TriggerRespawn();

        else if (destroyPlayerOnDeath)
            Destroy(gameObject);

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

        LoseHP(attack.damageAmount);
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
        LoseHP(amount);
    }

    public void DebugKillPlayer()
    {
        if (!Application.isPlaying)
            return;

        LoseHP(maxHealth * 2f);
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
