/*
 * Written by: Will T
 * 
 * A basic script which just handles the tutorial progression in the elevator scene.
 * Designed purely for the tutorial within the elevator sequence, not intended to be used anywhere else.
 */

using Managers.TimeLord;
using Progression.Encounters;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine.UI;
using UIandUXSystems.HUD;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using Utilities.Combat;
using Utilities.Combat.Attacks;

public class TutorialHandler : MonoBehaviour
{
    private static readonly Regex TutorialBindTokenRegex = new(@"\[\[bind:[^\]]*\]\]", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex TutorialSpriteTagRegex = new(@"<sprite[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex TutorialWhitespaceRegex = new(@"\s{2,}", RegexOptions.Compiled);

    private enum TutorialStep
    {
        None,
        SingleAttack,
        AoeAttack,
        Dash,
        Guard,
        Parry,
        Complete
    }

    #region Inspector Setup
    [Header("Objective Messages")]
    [SerializeField, TextArea] private string initialMessage;
    [SerializeField] private bool initialMessageUseSelectedIcon;
    [SerializeField] private KeybindAction initialMessageAction = KeybindAction.GP_Interact;
    [SerializeField, TextArea] private string singleTargetFightMessage;
    [SerializeField] private bool singleTargetFightMessageUseSelectedIcon;
    [SerializeField] private KeybindAction singleTargetFightMessageAction = KeybindAction.GP_FastAttackSingle;
    [SerializeField, TextArea] private string aoeTargetFightMessage;
    [SerializeField] private bool aoeTargetFightMessageUseSelectedIcon;
    [SerializeField] private KeybindAction aoeTargetFightMessageAction = KeybindAction.GP_HeavyAttackAoe;
    [SerializeField] private bool useDashStep = true;
    [SerializeField, TextArea] private string dashMessage;
    [SerializeField] private bool dashMessageUseSelectedIcon = true;
    [SerializeField] private KeybindAction dashMessageAction = KeybindAction.GP_Dash;
    [SerializeField] private bool useGuardStep = true;
    [SerializeField, TextArea] private string guardFightMessage;
    [SerializeField] private bool guardFightMessageUseSelectedIcon = true;
    [SerializeField] private KeybindAction guardFightMessageAction = KeybindAction.GP_Guard;
    [SerializeField] private bool useParryStep = true;
    [SerializeField, TextArea] private string parryFightMessage;
    [SerializeField] private bool parryFightMessageUseSelectedIcon = true;
    [SerializeField] private KeybindAction parryFightMessageAction = KeybindAction.GP_Guard;
    [SerializeField, TextArea] private string playerTurnReadyMessage;
    [SerializeField] private List<string> playerTurnReadyMessageOptions = new();
    [SerializeField] private bool playerTurnReadyMessageUseSelectedIcon;
    [SerializeField] private KeybindAction playerTurnReadyMessageAction = KeybindAction.GP_FastAttackSingle;
    [SerializeField, TextArea] private string correctButtonPressedMessage;
    [SerializeField] private List<string> correctButtonPressedMessageOptions = new();
    [SerializeField] private bool correctButtonPressedMessageUseSelectedIcon;
    [SerializeField] private KeybindAction correctButtonPressedMessageAction = KeybindAction.GP_Interact;
    [SerializeField, TextArea] private string tutorialCompleteMessage;
    [SerializeField] private bool tutorialCompleteMessageUseSelectedIcon;
    [SerializeField] private KeybindAction tutorialCompleteMessageAction = KeybindAction.GP_Interact;
    [SerializeField, Min(0f)] private float postEncounterFeedbackDelay = 1.25f;
    [SerializeField, Min(0f)] private float playerTurnMessageRestoreDelay = 0.35f;
    [SerializeField] private Color tutorialIconColor = Color.white;
    [SerializeField, Min(0.1f)] private float tutorialIconSize = 1f;
    [SerializeField, Min(0f)] private float tutorialIconGrowthPerCorrectPress = 0.08f;
    [SerializeField, Min(0.01f)] private float tutorialIconPulseDuration = 0.12f;

    [Header("Tutorial Progression References")]
    [SerializeField, CriticalReference] 
    private NavigationEntryInteraction tutorialEntry;
    // [SerializeField] private HUDMessage postEntryMessage;
    [SerializeField, CriticalReference] private CombatEncounter singleTargetFight;
    [SerializeField, CriticalReference] private CombatEncounter aoeTargetFight;
    [SerializeField] private CombatEncounter dashFight;
    [SerializeField] private CombatEncounter guardFight;
    [SerializeField] private CombatEncounter parryFight;
    [SerializeField, CriticalReference] private GameObject keycardToEnable;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private KeybindIconSwapper tutorialObjectiveIcon;
    [SerializeField] private bool keepPlayerAtFullHealthUntilTutorialComplete = true;
    [SerializeField, Range(0.05f, 0.95f)] private float playerRecoveryHealthThreshold = 0.3f;
    [SerializeField] private bool enemiesInvulnerableUntilTutorialActionSucceeds = true;
    [SerializeField] private bool makeDashEnemyHitHard = true;
    [SerializeField] private bool makeGuardEnemyHitHard = true;
    [SerializeField] private bool makeParryEnemyHitHard = true;
    [FormerlySerializedAs("dashEnemyDamageMultiplier")]
    [SerializeField, Min(1f)] private float tutorialEnemyDamageMultiplier = 5f;
    [SerializeField] private bool loadNextSceneOnComplete = true;
    [SerializeField] private SceneAsset nextScene;
    #endregion

    private bool logCollected = false;
    private bool currentStepCompleted;
    private TutorialStep currentStep;
    private bool isSubscribedToPlayerMovement;
    private readonly List<BaseEnemyCore> currentStepEnemies = new();
    private string lastPlayerTurnReadyMessage;
    private string lastCorrectButtonPressedMessage;
    private PlayerHealthBarManager playerHealth;
    private CombatEncounter currentStepEncounter;
    private Coroutine postEncounterFeedbackRoutine;
    private bool pendingPlayerTurnReadyMessage;
    private bool pendingStepInstructionRestore;
    private Coroutine stepInstructionRestoreRoutine;
    private Coroutine tutorialIconPulseRoutine;

    #region Couroutines
    private Coroutine enableRetryRoutine;
    private const float EncounterRetryInterval = 3f;

    // Monitor for destruction/tracking
    private Coroutine destroyMonitorRoutine;
    private const float DestroyMonitorInterval = 1f; // seconds between checks
    private bool wasDestroyedState = false;
    #endregion

    private void Start()
    {
        keycardToEnable.SetActive(false); // Ensures the keycard is disabled at the start of the tutorial
        ResolvePlayerMovement();
        ResolvePlayerHealth();
        SetTutorialPlayerProtection(true);

        DisplayTutorialObjective(initialMessage, initialMessageUseSelectedIcon, initialMessageAction);
    }

    private void OnEnable()
    {
        tutorialEntry.OnEntryCollected += OnEntryCollected;
        tutorialEntry.OnEntryRead += OnEntryRead;
        CombatManager.OnSuccessfulGuard += OnSuccessfulGuard;
        CombatManager.OnSuccessfulParry += OnSuccessfulParry;
        ObjectiveText.ObjectiveTypingCompleted += HandleObjectiveTypingCompleted;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        PlayerHealthBarManager.OnPlayerDamaged += HandlePlayerDamaged;
        PlayerHealthBarManager.OnPlayerHealthRegistered += HandlePlayerHealthRegistered;

        // Subscribe to PlayerAttackManager attack-type events
        PlayerAttackManager.OnSingleAttack += OnPlayerAttack;
        PlayerAttackManager.OnAoeAttack += OnPlayerAttack;

        SubscribeToPlayerMovement();

        // Subscribe to encounter completion events if they aren't already completed
        singleTargetFight.OnEncounterCompleted += OnEncounterCompleted;
        aoeTargetFight.OnEncounterCompleted += OnEncounterCompleted;

        if (dashFight != null)
            dashFight.OnEncounterCompleted += OnEncounterCompleted;

        if (guardFight != null)
            guardFight.OnEncounterCompleted += OnEncounterCompleted;

        if (parryFight != null)
            parryFight.OnEncounterCompleted += OnEncounterCompleted;
    }

    private void OnDisable()
    {
        tutorialEntry.OnEntryCollected -= OnEntryCollected;
        tutorialEntry.OnEntryRead -= OnEntryRead;
        CombatManager.OnSuccessfulGuard -= OnSuccessfulGuard;
        CombatManager.OnSuccessfulParry -= OnSuccessfulParry;
        ObjectiveText.ObjectiveTypingCompleted -= HandleObjectiveTypingCompleted;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        PlayerHealthBarManager.OnPlayerDamaged -= HandlePlayerDamaged;
        PlayerHealthBarManager.OnPlayerHealthRegistered -= HandlePlayerHealthRegistered;

        // Unsubscribe from PlayerAttackManager events
        PlayerAttackManager.OnSingleAttack -= OnPlayerAttack;
        PlayerAttackManager.OnAoeAttack -= OnPlayerAttack;

        UnsubscribeFromPlayerMovement();

        // Unsubscribe from encounter completion events
        singleTargetFight.OnEncounterCompleted -= OnEncounterCompleted;
        aoeTargetFight.OnEncounterCompleted -= OnEncounterCompleted;

        if (dashFight != null)
            dashFight.OnEncounterCompleted -= OnEncounterCompleted;

        if (guardFight != null)
            guardFight.OnEncounterCompleted -= OnEncounterCompleted;

        if (parryFight != null)
            parryFight.OnEncounterCompleted -= OnEncounterCompleted;

        if (enableRetryRoutine != null)
        {
            StopCoroutine(enableRetryRoutine);
            enableRetryRoutine = null;
        }

        if (destroyMonitorRoutine != null)
        {
            Debug.Log("[TutorialHandler] Stopping destroy monitor coroutine on disable.");
            StopCoroutine(destroyMonitorRoutine);
            destroyMonitorRoutine = null;
        }

        if (postEncounterFeedbackRoutine != null)
        {
            StopCoroutine(postEncounterFeedbackRoutine);
            postEncounterFeedbackRoutine = null;
        }

        if (stepInstructionRestoreRoutine != null)
        {
            StopCoroutine(stepInstructionRestoreRoutine);
            stepInstructionRestoreRoutine = null;
        }

        if (tutorialIconPulseRoutine != null)
        {
            StopCoroutine(tutorialIconPulseRoutine);
            tutorialIconPulseRoutine = null;
        }

        pendingPlayerTurnReadyMessage = false;
        pendingStepInstructionRestore = false;

        ReleaseCurrentStepEnemyOverrides();
        SetTutorialPlayerProtection(false);
    }

    private void OnEntryCollected(string entryId)
    {
        logCollected = true;
        BeginStep(TutorialStep.SingleAttack);
    }

    #region Combat Tutorial Handlers
    private void BeginStep(TutorialStep step)
    {
        if (postEncounterFeedbackRoutine != null)
        {
            StopCoroutine(postEncounterFeedbackRoutine);
            postEncounterFeedbackRoutine = null;
        }

        if (stepInstructionRestoreRoutine != null)
        {
            StopCoroutine(stepInstructionRestoreRoutine);
            stepInstructionRestoreRoutine = null;
        }

        currentStep = step;
        currentStepCompleted = false;
        pendingPlayerTurnReadyMessage = false;
        pendingStepInstructionRestore = false;

        switch (step)
        {
            case TutorialStep.SingleAttack:
                StartCombatTutorial(singleTargetFight, singleTargetFightMessage, singleTargetFightMessageUseSelectedIcon, singleTargetFightMessageAction);
                break;

            case TutorialStep.AoeAttack:
                StartCombatTutorial(aoeTargetFight, aoeTargetFightMessage, aoeTargetFightMessageUseSelectedIcon, aoeTargetFightMessageAction);
                break;

            case TutorialStep.Dash:
                StartCombatTutorial(dashFight, dashMessage, dashMessageUseSelectedIcon, dashMessageAction);
                break;

            case TutorialStep.Guard:
                StartCombatTutorial(guardFight, guardFightMessage, guardFightMessageUseSelectedIcon, guardFightMessageAction);
                break;

            case TutorialStep.Parry:
                StartCombatTutorial(parryFight, parryFightMessage, parryFightMessageUseSelectedIcon, parryFightMessageAction);
                break;

            case TutorialStep.Complete:
                TutorialComplete();
                break;
        }
    }

    private void StartCombatTutorial(CombatEncounter fight, string message, bool useSelectedIcon, KeybindAction selectedAction)
    {
        if (fight == null)
        {
            Debug.LogWarning($"[TutorialHandler] Combat encounter missing for step {currentStep}. Skipping to next configured step.");
            BeginStep(GetNextStepAfter(currentStep));
            return;
        }

        Debug.Log($"[TutorialHandler] Starting combat tutorial for encounter {fight.name}. Displaying message and enabling fight zone.");
        DisplayTutorialObjective(message, useSelectedIcon, selectedAction);
        ReleaseCurrentStepEnemyOverrides();
        SubscribeToCurrentStepEncounter(fight);
        fight.EnableZone(); // Enables the fight zone
        ApplyCurrentStepEnemyOverrides(fight);
    }

    private void OnPlayerAttack(PlayerAttack attack)
    {
        AttackType type = attack.attackType;

        bool shouldProcess =
            (currentStep == TutorialStep.SingleAttack && type == AttackType.LightSingle && logCollected) ||
            (currentStep == TutorialStep.AoeAttack && type == AttackType.HeavyAOE);

        if (!shouldProcess) return;

        Debug.Log($"[TutorialHandler] Player performed attack of type {type}. Updating Progress...");
        GrowTutorialObjectiveIcon();
        MarkCurrentStepComplete();
    }

    private void OnEncounterCompleted()
    {
        Debug.Log($"[TutorialHandler] Encounter completed called. Checking conditions for tutorial progression...");
        if (!currentStepCompleted)
            return;

        if (postEncounterFeedbackRoutine != null)
            StopCoroutine(postEncounterFeedbackRoutine);

        postEncounterFeedbackRoutine = StartCoroutine(ShowPostEncounterFeedbackThenAdvance(currentStep));
    }
    #endregion

    private void OnSuccessfulGuard()
    {
        if (currentStep != TutorialStep.Guard)
            return;

        Debug.Log("[TutorialHandler] Successful guard detected. Updating progress...");
        GrowTutorialObjectiveIcon();
        MarkCurrentStepComplete();
    }

    private void OnSuccessfulParry(BaseEnemy<EnemyState, EnemyTrigger> _)
    {
        if (currentStep != TutorialStep.Parry)
            return;

        Debug.Log("[TutorialHandler] Successful parry detected. Updating progress...");
        GrowTutorialObjectiveIcon();
        MarkCurrentStepComplete();
    }

    private void HandleDashPerformed()
    {
        if (currentStep != TutorialStep.Dash)
            return;

        Debug.Log("[TutorialHandler] Dash detected. Updating progress...");
        GrowTutorialObjectiveIcon();
        MarkCurrentStepComplete();
    }

    private void MarkCurrentStepComplete()
    {
        if (currentStepCompleted)
            return;

        currentStepCompleted = true;
        SetCurrentStepEnemiesDamageable(true);

        if (ShouldShowPlayerTurnReadyMessage())
            QueuePlayerTurnReadyMessage();
    }

    private void QueuePlayerTurnReadyMessage()
    {
        if (!ShouldShowPlayerTurnReadyMessage())
            return;

        pendingPlayerTurnReadyMessage = true;
        TryDisplayPendingPlayerTurnReadyMessage();
    }

    private void HandleObjectiveTypingCompleted()
    {
        TryDisplayPendingPlayerTurnReadyMessage();

        if (pendingStepInstructionRestore)
        {
            if (stepInstructionRestoreRoutine != null)
                StopCoroutine(stepInstructionRestoreRoutine);

            stepInstructionRestoreRoutine = StartCoroutine(RestoreCurrentStepInstructionAfterDelay());
        }
    }

    private void TryDisplayPendingPlayerTurnReadyMessage()
    {
        if (!pendingPlayerTurnReadyMessage || !currentStepCompleted || !ShouldShowPlayerTurnReadyMessage())
            return;

        if (ObjectiveText.IsCurrentObjectiveTyping)
            return;

        pendingPlayerTurnReadyMessage = false;
        pendingStepInstructionRestore = true;
        DisplayTutorialObjective(
            GetRandomPlayerTurnReadyMessage(),
            playerTurnReadyMessageUseSelectedIcon,
            playerTurnReadyMessageAction);
    }

    private void TryRestoreCurrentStepInstruction()
    {
        if (!pendingStepInstructionRestore || !currentStepCompleted)
            return;

        if (ObjectiveText.IsCurrentObjectiveTyping || postEncounterFeedbackRoutine != null)
            return;

        if (!TryGetCurrentStepInstruction(out string message, out bool useSelectedIcon, out KeybindAction selectedAction))
            return;

        pendingStepInstructionRestore = false;
        stepInstructionRestoreRoutine = null;
        DisplayTutorialObjective(message, useSelectedIcon, selectedAction);
    }

    private IEnumerator RestoreCurrentStepInstructionAfterDelay()
    {
        if (playerTurnMessageRestoreDelay > 0f)
            yield return new WaitForSeconds(playerTurnMessageRestoreDelay);

        TryRestoreCurrentStepInstruction();
    }

    private IEnumerator ShowPostEncounterFeedbackThenAdvance(TutorialStep completedStep)
    {
        yield return WaitForCurrentObjectiveTypingToFinish(completedStep);

        if (currentStep != completedStep || !currentStepCompleted)
        {
            postEncounterFeedbackRoutine = null;
            yield break;
        }

        if (ShouldShowPostEncounterFeedback())
        {
            DisplayTutorialObjective(
                GetRandomCorrectButtonPressedMessage(),
                correctButtonPressedMessageUseSelectedIcon,
                correctButtonPressedMessageAction);

            if (postEncounterFeedbackDelay > 0f)
                yield return new WaitForSeconds(postEncounterFeedbackDelay);
        }

        postEncounterFeedbackRoutine = null;

        if (currentStep != completedStep || !currentStepCompleted)
            yield break;

        BeginStep(GetNextStepAfter(completedStep));
    }

    private IEnumerator WaitForCurrentObjectiveTypingToFinish(TutorialStep step)
    {
        while (currentStep == step && ObjectiveText.IsCurrentObjectiveTyping)
            yield return null;
    }

    private bool ShouldShowPlayerTurnReadyMessage()
    {
        return (currentStep == TutorialStep.Dash || currentStep == TutorialStep.Guard || currentStep == TutorialStep.Parry)
            && HasConfiguredMessage(playerTurnReadyMessageOptions, playerTurnReadyMessage);
    }

    private bool ShouldShowPostEncounterFeedback()
    {
        return HasConfiguredMessage(correctButtonPressedMessageOptions, correctButtonPressedMessage);
    }

    private string GetRandomPlayerTurnReadyMessage()
    {
        return GetRandomMessage(playerTurnReadyMessageOptions, playerTurnReadyMessage, ref lastPlayerTurnReadyMessage);
    }

    private string GetRandomCorrectButtonPressedMessage()
    {
        return GetRandomMessage(correctButtonPressedMessageOptions, correctButtonPressedMessage, ref lastCorrectButtonPressedMessage);
    }

    private static bool HasConfiguredMessage(List<string> messageOptions, string fallbackMessage)
    {
        if (messageOptions != null)
        {
            for (int i = 0; i < messageOptions.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(messageOptions[i]))
                    return true;
            }
        }

        return !string.IsNullOrWhiteSpace(fallbackMessage);
    }

    private string GetRandomMessage(List<string> messageOptions, string fallbackMessage, ref string lastMessage)
    {
        if (messageOptions != null)
        {
            List<string> validMessages = new();

            for (int i = 0; i < messageOptions.Count; i++)
            {
                string message = messageOptions[i];
                if (!string.IsNullOrWhiteSpace(message))
                    validMessages.Add(message);
            }

            if (validMessages.Count > 0)
            {
                if (validMessages.Count > 1 && !string.IsNullOrWhiteSpace(lastMessage))
                {
                    for (int i = validMessages.Count - 1; i >= 0; i--)
                    {
                        if (string.Equals(validMessages[i], lastMessage, StringComparison.Ordinal))
                            validMessages.RemoveAt(i);
                    }
                }

                if (validMessages.Count > 0)
                {
                    int randomIndex = UnityEngine.Random.Range(0, validMessages.Count);
                    lastMessage = validMessages[randomIndex];
                    return lastMessage;
                }
            }
        }

        lastMessage = fallbackMessage;
        return fallbackMessage;
    }

    private TutorialStep GetNextStepAfter(TutorialStep step)
    {
        return step switch
        {
            TutorialStep.SingleAttack => TutorialStep.AoeAttack,
            TutorialStep.AoeAttack => useDashStep ? TutorialStep.Dash : (useGuardStep ? TutorialStep.Guard : (useParryStep ? TutorialStep.Parry : TutorialStep.Complete)),
            TutorialStep.Dash => useGuardStep ? TutorialStep.Guard : (useParryStep ? TutorialStep.Parry : TutorialStep.Complete),
            TutorialStep.Guard => useParryStep ? TutorialStep.Parry : TutorialStep.Complete,
            TutorialStep.Parry => TutorialStep.Complete,
            _ => TutorialStep.Complete,
        };
    }

    private bool TryGetCurrentStepInstruction(out string message, out bool useSelectedIcon, out KeybindAction selectedAction)
    {
        switch (currentStep)
        {
            case TutorialStep.Dash:
                message = dashMessage;
                useSelectedIcon = dashMessageUseSelectedIcon;
                selectedAction = dashMessageAction;
                return true;

            case TutorialStep.Guard:
                message = guardFightMessage;
                useSelectedIcon = guardFightMessageUseSelectedIcon;
                selectedAction = guardFightMessageAction;
                return true;

            case TutorialStep.Parry:
                message = parryFightMessage;
                useSelectedIcon = parryFightMessageUseSelectedIcon;
                selectedAction = parryFightMessageAction;
                return true;

            default:
                message = string.Empty;
                useSelectedIcon = false;
                selectedAction = default;
                return false;
        }
    }

    private void HandleSceneLoaded(Scene _, LoadSceneMode __)
    {
        SubscribeToPlayerMovement();
        ResolvePlayerHealth();
        SetTutorialPlayerProtection(true);
    }

    private PlayerMovement ResolvePlayerMovement()
    {
        if (IsValidPlayerMovement(playerMovement))
            return playerMovement;

        playerMovement = null;

        if (Player.TryGetPlayerObject(out GameObject playerObject) && playerObject != null)
        {
            playerMovement = playerObject.GetComponent<PlayerMovement>()
                ?? playerObject.GetComponentInChildren<PlayerMovement>(true)
                ?? playerObject.GetComponentInParent<PlayerMovement>();
        }

        if (!IsValidPlayerMovement(playerMovement))
            playerMovement = FindFirstObjectByType<PlayerMovement>();

        return playerMovement;
    }

    private void ResolvePlayerHealth()
    {
        if (playerHealth != null)
            return;

        playerHealth = PlayerHealthBarManager.Instance;

        if (playerHealth == null && Player.TryGetPlayerObject(out GameObject playerObject) && playerObject != null)
        {
            playerHealth = playerObject.GetComponent<PlayerHealthBarManager>()
                ?? playerObject.GetComponentInChildren<PlayerHealthBarManager>(true)
                ?? playerObject.GetComponentInParent<PlayerHealthBarManager>();
        }
    }

    private void HandlePlayerHealthRegistered(PlayerHealthBarManager manager)
    {
        playerHealth = manager;
        SetTutorialPlayerProtection(true);
    }

    private void HandlePlayerDamaged(float _)
    {
        if (!keepPlayerAtFullHealthUntilTutorialComplete || currentStep == TutorialStep.Complete)
            return;

        ResolvePlayerHealth();

        if (playerHealth != null && playerHealth.NormalizedHealth <= playerRecoveryHealthThreshold)
            playerHealth.ForceFullHeal();
    }

    private void SetTutorialPlayerProtection(bool enabled)
    {
        ResolvePlayerHealth();

        if (playerHealth == null)
            return;

        if (!enabled || currentStep == TutorialStep.Complete)
        {
            playerHealth.SetInvulnerable(false);
            return;
        }

        playerHealth.SetInvulnerable(false);
    }

    private void ApplyCurrentStepEnemyOverrides(CombatEncounter fight)
    {
        if (fight == null)
            return;

        foreach (BaseEnemyCore enemy in fight.GetTrackedEnemies())
            ApplyCurrentStepEnemyOverride(enemy);
    }

    private void ApplyCurrentStepEnemyOverride(BaseEnemyCore enemy)
    {
        if (enemy == null)
            return;

        if (!currentStepEnemies.Contains(enemy))
            currentStepEnemies.Add(enemy);

        enemy.ClearRuntimeCombatOverrides();

        bool keepInvulnerable = enemiesInvulnerableUntilTutorialActionSucceeds &&
            (currentStep == TutorialStep.Dash || currentStep == TutorialStep.Guard || currentStep == TutorialStep.Parry);

        enemy.SetIncomingDamageEnabled(!keepInvulnerable);

        if (ShouldIncreaseEnemyDamageForCurrentStep())
            enemy.SetOutgoingDamageMultiplier(tutorialEnemyDamageMultiplier);
    }

    private bool ShouldIncreaseEnemyDamageForCurrentStep()
    {
        return currentStep switch
        {
            TutorialStep.Dash => makeDashEnemyHitHard,
            TutorialStep.Guard => makeGuardEnemyHitHard,
            TutorialStep.Parry => makeParryEnemyHitHard,
            _ => false,
        };
    }

    private void HandleCurrentStepEnemySpawned(BaseEnemyCore enemy)
    {
        if (currentStepCompleted)
            return;

        ApplyCurrentStepEnemyOverride(enemy);
    }

    private void SubscribeToCurrentStepEncounter(CombatEncounter encounter)
    {
        if (currentStepEncounter == encounter)
            return;

        UnsubscribeFromCurrentStepEncounter();
        currentStepEncounter = encounter;

        if (currentStepEncounter != null)
            currentStepEncounter.OnEnemySpawned += HandleCurrentStepEnemySpawned;
    }

    private void UnsubscribeFromCurrentStepEncounter()
    {
        if (currentStepEncounter == null)
            return;

        currentStepEncounter.OnEnemySpawned -= HandleCurrentStepEnemySpawned;
        currentStepEncounter = null;
    }

    private void SetCurrentStepEnemiesDamageable(bool enabled)
    {
        foreach (BaseEnemyCore enemy in currentStepEnemies)
        {
            if (enemy == null)
                continue;

            enemy.SetIncomingDamageEnabled(enabled);
        }
    }

    private void ReleaseCurrentStepEnemyOverrides()
    {
        UnsubscribeFromCurrentStepEncounter();

        foreach (BaseEnemyCore enemy in currentStepEnemies)
        {
            if (enemy == null)
                continue;

            enemy.ClearRuntimeCombatOverrides();
        }

        currentStepEnemies.Clear();
    }

    private void SubscribeToPlayerMovement()
    {
        PlayerMovement resolvedPlayerMovement = ResolvePlayerMovement();

        if (!IsValidPlayerMovement(resolvedPlayerMovement))
            return;

        if (isSubscribedToPlayerMovement)
            UnsubscribeFromPlayerMovement();

        playerMovement = resolvedPlayerMovement;
        playerMovement.DashPerformed += HandleDashPerformed;
        isSubscribedToPlayerMovement = true;
    }

    private void UnsubscribeFromPlayerMovement()
    {
        if (!isSubscribedToPlayerMovement)
            return;

        if (IsValidPlayerMovement(playerMovement))
            playerMovement.DashPerformed -= HandleDashPerformed;

        isSubscribedToPlayerMovement = false;
    }

    private static bool IsValidPlayerMovement(PlayerMovement movement)
    {
        return movement != null && movement.gameObject != null;
    }

    private void DisplayTutorialObjective(string source, bool showSelectedIcon, KeybindAction selectedAction)
    {
        ObjectiveManager.SetMainObjective(BuildMessage(source));
        UpdateTutorialObjectiveIcon(showSelectedIcon, selectedAction);
    }

    private string BuildMessage(string source, bool useSelectedIcon, KeybindAction selectedAction)
    {
        return BuildMessage(source);
    }

    private static string BuildMessage(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return string.Empty;

        string cleaned = TutorialBindTokenRegex.Replace(source, string.Empty);
        cleaned = TutorialSpriteTagRegex.Replace(cleaned, string.Empty);
        cleaned = TutorialWhitespaceRegex.Replace(cleaned, " ");
        return cleaned.Trim();
    }

    private void UpdateTutorialObjectiveIcon(bool shouldShowIcon, KeybindAction action)
    {
        if (tutorialObjectiveIcon == null)
            return;

        tutorialObjectiveIcon.gameObject.SetActive(shouldShowIcon);

        if (shouldShowIcon)
        {
            tutorialObjectiveIcon.SetAction(action);
            ResetTutorialObjectiveIconVisuals();
        }
    }

    private void HideTutorialObjectiveIcon()
    {
        if (tutorialObjectiveIcon == null)
            return;

        tutorialObjectiveIcon.gameObject.SetActive(false);
        ResetTutorialObjectiveIconVisuals();
    }

    private void ResetTutorialObjectiveIconVisuals()
    {
        if (tutorialObjectiveIcon == null)
            return;

        tutorialObjectiveIcon.transform.localScale = Vector3.one * tutorialIconSize;

        Image iconImage = tutorialObjectiveIcon.targetImage != null
            ? tutorialObjectiveIcon.targetImage
            : tutorialObjectiveIcon.GetComponent<Image>();

        if (iconImage != null)
            iconImage.color = tutorialIconColor;
    }

    private void GrowTutorialObjectiveIcon()
    {
        if (tutorialObjectiveIcon == null || !tutorialObjectiveIcon.gameObject.activeSelf)
            return;

        if (tutorialIconPulseRoutine != null)
            StopCoroutine(tutorialIconPulseRoutine);

        tutorialIconPulseRoutine = StartCoroutine(PulseTutorialObjectiveIcon());
    }

    private IEnumerator PulseTutorialObjectiveIcon()
    {
        Vector3 baseScale = Vector3.one * tutorialIconSize;
        Vector3 targetScale = Vector3.one * (tutorialIconSize + tutorialIconGrowthPerCorrectPress);
        float halfDuration = Mathf.Max(0.01f, tutorialIconPulseDuration * 0.5f);

        yield return AnimateTutorialIconScale(baseScale, targetScale, halfDuration);
        yield return AnimateTutorialIconScale(targetScale, baseScale, halfDuration);

        tutorialObjectiveIcon.transform.localScale = baseScale;
        tutorialIconPulseRoutine = null;
    }

    private IEnumerator AnimateTutorialIconScale(Vector3 fromScale, Vector3 toScale, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            tutorialObjectiveIcon.transform.localScale = Vector3.LerpUnclamped(fromScale, toScale, t);
            yield return null;
        }

        tutorialObjectiveIcon.transform.localScale = toScale;
    }

    private void TutorialComplete()
    {
        Debug.Log($"[TutorialHandler] Tutorial complete! All conditions met.");

        currentStep = TutorialStep.Complete;
        currentStepCompleted = true;
        ReleaseCurrentStepEnemyOverrides();
        SetTutorialPlayerProtection(false);

        keycardToEnable.SetActive(true); // Enables the keycard to allow progression to the next scene

        DisplayTutorialObjective(tutorialCompleteMessage, tutorialCompleteMessageUseSelectedIcon, tutorialCompleteMessageAction);
        HideTutorialObjectiveIcon();

        if (loadNextSceneOnComplete && nextScene != null)
        {
            SceneLoader.Load(nextScene, loadScreen: false); // Loads the next scene for the player
        }
    }

    #region -------------- CURRENTLY DEPRECATED FUNCTIONALITY --------------------------------

    // Specifically waits for the game to resume before enabling the fight.
    // Doing it while game is paused breaks everything for SOME REASON
    // Doing any sort of logic during or near the pausing of the game causes really weird bugs
    // So it is not being used currently
    private void OnEntryRead()
    {
        // If a retry coroutine is already running, stop it before starting a new one.
        if (enableRetryRoutine != null)
        {
            Debug.Log("[TutorialHandler] Stopping existing retry coroutine before starting a new one.");
            StopCoroutine(enableRetryRoutine);
            enableRetryRoutine = null;
        }

        // Start a coroutine that will try to enable the encounter every 3 seconds until it succeeds.
        enableRetryRoutine = StartCoroutine(TryEnableEncounterUntilSuccess());

        // PauseCoordinator.OnResumed += PauseCoordinator_OnResumed;

        void PauseCoordinator_OnResumed()
        {
            PauseCoordinator.OnResumed -= PauseCoordinator_OnResumed;

            
        }
    }

    // Coroutine that repeatedly attempts to enable the encounter every EncounterRetryInterval seconds.
    // Stops when the encounter is successfully enabled or when it's clear the GameObject has been destroyed.
    private IEnumerator TryEnableEncounterUntilSuccess()
    {
        while (true)
        {
            // If the serialized reference isn't assigned yet, retry after delay.
            if (singleTargetFight == null)
            {
                Debug.Log($"[TutorialHandler] singleTargetFight is null. Retrying in {EncounterRetryInterval} seconds.");
                yield return new WaitForSecondsRealtime(EncounterRetryInterval);
                continue;
            }

            // If the referenced object's GameObject has been destroyed, abort retries.
            if (singleTargetFight.gameObject == null)
            {
                Debug.LogWarning("[TutorialHandler] singleTargetFight GameObject appears destroyed. Aborting retries.");
                enableRetryRoutine = null;
                yield break;
            }

            // Attempt to enable the encounter and stop retrying.
            singleTargetFight.EnableZone();
            enableRetryRoutine = null;
            yield break;
        }
    }

    // Continuously watches the singleTargetFight reference and logs transitions between alive/null/destroyed.
    // Useful to detect when/if the CombatEncounter becomes destroyed during gameplay.
    private IEnumerator MonitorSingleTargetFight()
    {
        wasDestroyedState = (singleTargetFight == null) || (singleTargetFight != null && singleTargetFight.gameObject == null);

        // Initial state log
        Debug.Log($"[TutorialHandler] Monitor started. singleTargetFight initial destroyedState = {wasDestroyedState}.");

        while (true)
        {
            bool referenceIsNull = singleTargetFight == null; // Unity's overloaded == handles destroyed objects
            bool gameObjectMissing = false;

            if (!referenceIsNull)
            {
                // Accessing gameObject is safe here because referenceIsNull is false.
                gameObjectMissing = singleTargetFight.gameObject == null;
            }

            bool isDestroyedNow = referenceIsNull || gameObjectMissing;

            if (isDestroyedNow != wasDestroyedState)
            {
                wasDestroyedState = isDestroyedNow;

                if (isDestroyedNow)
                {
                    Debug.LogError($"[TutorialHandler] DETECTED: singleTargetFight became null/destroyed at time {Time.realtimeSinceStartup:F2}s. " +
                                   $"Reference null: {referenceIsNull}, GameObject missing: {gameObjectMissing}");
                }
                else
                {
                    Debug.Log($"[TutorialHandler] singleTargetFight reference restored/assigned at time {Time.realtimeSinceStartup:F2}s.");
                }
            }

            yield return new WaitForSecondsRealtime(DestroyMonitorInterval);
        }
    }
    #endregion
}
