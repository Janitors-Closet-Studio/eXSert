using System.Collections.Generic;
using Progression;
using Progression.Checkpoints;
using Progression.Encounters;
using Singletons;
using UnityEngine;

[System.Serializable]
public enum ObjectiveEntryType
{
    Main,
    Sub
}

[System.Serializable]
public class ObjectiveData
{
    public InteractionManager interactionToActivate;
    [Tooltip("Optional source that shows this objective when it becomes active. Supports InteractionManager, ProgressionZone/Encounter, PuzzlePart, and Wave.")]
    public MonoBehaviour triggerSource;
    [Tooltip("Optional plain trigger-box source. Assign a BoxCollider trigger here when you want the objective to fire from entering a basic trigger volume without a custom script.")]
    public BoxCollider triggerZoneSource;
    [Tooltip("When the trigger source is a PuzzleInteraction, show this objective after that puzzle interaction fully completes instead of when the normal trigger fires.")]
    public bool triggerOnPuzzleInteractionComplete;
    [Tooltip("When the trigger source is a Wave, show this objective after the enemy wave completes instead of when the wave starts.")]
    public bool triggerOnWaveCompletion;
    public string objectiveID;
    [TextArea(3, 6)] public string objectiveText;
    public ObjectiveEntryType objectiveType = ObjectiveEntryType.Main;
    public bool disableInteraction;
    public InteractionManager interactionToDisable;
    public int priority = 0;
}

[System.Serializable]
public class NoticeData
{
    public InteractionManager interactionToActivate;
    [Tooltip("Optional source that shows this notice when it becomes active. Supports InteractionManager, ProgressionZone/Encounter, PuzzlePart, and Wave.")]
    public MonoBehaviour triggerSource;
    [Tooltip("Optional plain trigger-box source. Assign a BoxCollider trigger here when you want the notice to fire from entering a basic trigger volume without a custom script.")]
    public BoxCollider triggerZoneSource;
    [Tooltip("When the trigger source is a PuzzleInteraction, show this notice after that puzzle interaction fully completes instead of when the normal trigger fires.")]
    public bool triggerOnPuzzleInteractionComplete;
    [Tooltip("When the trigger source is a Wave, show this notice after the enemy wave completes instead of when the wave starts.")]
    public bool triggerOnWaveCompletion;
    public bool disableInteraction;
    public InteractionManager interactionToDisable;
    public string noticeID;
    public string noticeText;
    public string bottomText;
    public float fadeDuration = 2f;
    public float displayDuration = 4f;
    public int priority = 0;
}

public class MasterObjectiveClass : SceneSingleton<MasterObjectiveClass>
{
    [SerializeField] private NoticeManager noticeManager;
    private bool subscribedToCheckpointTriggers;

    [Header("Objectives")]
    [SerializeField] public List<ObjectiveData> objectives = new List<ObjectiveData>();

    [Header("Objective Notices")]
    [SerializeField] public List<NoticeData> objectiveNotices = new List<NoticeData>();

    protected override void Awake()
    {
        base.Awake();

        // Auto-find NoticeManager if not assigned in inspector
        if (noticeManager == null)
        {
            noticeManager = FindFirstObjectByType<NoticeManager>();
            if (noticeManager == null)
            {
                Debug.LogError($"[MasterObjectiveClass] NoticeManager not found in scene and not assigned in inspector!");
                return;
            }
        }

    }

    private void Start()
    {
        CorrectIDsIfIsEntry();
        SubscribeObjectiveTriggers();
        SubscribeNoticeTriggers();
        
    }

    protected override void OnDestroy()
    {
        UnsubscribeObjectiveTriggers();
        UnsubscribeNoticeTriggers();
        base.OnDestroy();
    }

    private Component GetObjectiveTriggerSource(ObjectiveData objective)
    {
        if (objective == null)
            return null;

        return objective.triggerSource != null
            ? objective.triggerSource
            : objective.triggerZoneSource != null
                ? objective.triggerZoneSource
            : objective.interactionToActivate;
    }

    private static string BuildGeneratedObjectiveID(Component triggerSource)
    {
        if (triggerSource == null)
            return string.Empty;

        return NormalizeNoticeID($"{triggerSource.GetType().Name}_{triggerSource.gameObject.name}");
    }

    private static string ResolveObjectiveID(string objectiveID, InteractionManager interaction, Component triggerSource)
    {
        if (!string.IsNullOrWhiteSpace(objectiveID))
            return NormalizeNoticeID(objectiveID);

        if (interaction != null)
            return NormalizeNoticeID(interaction.interactId);

        if (triggerSource is InteractionManager triggerInteraction)
            return NormalizeNoticeID(triggerInteraction.interactId);

        return BuildGeneratedObjectiveID(triggerSource);
    }

    private Component GetNoticeTriggerSource(NoticeData notice)
    {
        if (notice == null)
            return null;

        return notice.triggerSource != null
            ? notice.triggerSource
            : notice.triggerZoneSource != null
                ? notice.triggerZoneSource
            : notice.interactionToActivate;
    }

    private static string ResolveNoticeID(string noticeID, InteractionManager interaction, Component triggerSource)
    {
        if (!string.IsNullOrWhiteSpace(noticeID))
            return NormalizeNoticeID(noticeID);

        if (interaction != null)
            return NormalizeNoticeID(interaction.interactId);

        if (triggerSource is InteractionManager triggerInteraction)
            return NormalizeNoticeID(triggerInteraction.interactId);

        return BuildGeneratedObjectiveID(triggerSource);
    }

    private void SubscribeObjectiveTriggers()
    {
        foreach (ObjectiveData objective in objectives)
        {
            Component triggerSource = GetObjectiveTriggerSource(objective);
            switch (triggerSource)
            {
                case BoxCollider triggerZone:
                    ObjectiveTriggerZoneRelay relay = ObjectiveTriggerZoneRelay.GetOrAdd(triggerZone);
                    relay.Triggered -= HandleTriggerZoneEntered;
                    relay.Triggered += HandleTriggerZoneEntered;
                    break;

                case PuzzlePart puzzlePart:
                    if (objective.triggerOnPuzzleInteractionComplete)
                    {
                        puzzlePart.PuzzleCompleted -= HandlePuzzleCompleted;
                        puzzlePart.PuzzleCompleted += HandlePuzzleCompleted;
                    }
                    break;

                case PuzzleInteraction puzzleInteraction:
                    if (objective.triggerOnPuzzleInteractionComplete)
                    {
                        puzzleInteraction.InteractionCompleted -= HandlePuzzleInteractionCompleted;
                        puzzleInteraction.InteractionCompleted += HandlePuzzleInteractionCompleted;
                    }
                    else
                    {
                        puzzleInteraction.InteractionEnabledStateChanged -= HandleInteractionTriggerStateChanged;
                        puzzleInteraction.InteractionEnabledStateChanged += HandleInteractionTriggerStateChanged;
                    }
                    break;

                case CheckpointBehavior:
                    EnsureCheckpointTriggerSubscription();
                    break;

                case InteractionManager interaction:
                    interaction.InteractionEnabledStateChanged -= HandleInteractionTriggerStateChanged;
                    interaction.InteractionEnabledStateChanged += HandleInteractionTriggerStateChanged;
                    break;

                case ProgressionZone zone:
                    zone.ZoneEntered -= HandleZoneEntered;
                    zone.ZoneEntered += HandleZoneEntered;
                    break;

                case Wave wave:
                    wave.OnWaveStarted -= HandleWaveStarted;
                    wave.OnWaveStarted += HandleWaveStarted;
                    wave.OnWaveComplete -= HandleWaveCompleted;
                    wave.OnWaveComplete += HandleWaveCompleted;
                    break;
            }
        }
    }

    private void UnsubscribeObjectiveTriggers()
    {
        foreach (ObjectiveData objective in objectives)
        {
            Component triggerSource = GetObjectiveTriggerSource(objective);
            switch (triggerSource)
            {
                case BoxCollider triggerZone:
                    ObjectiveTriggerZoneRelay relay = ObjectiveTriggerZoneRelay.GetOrAdd(triggerZone);
                    relay.Triggered -= HandleTriggerZoneEntered;
                    break;

                case PuzzlePart puzzlePart:
                    puzzlePart.PuzzleCompleted -= HandlePuzzleCompleted;
                    break;

                case PuzzleInteraction puzzleInteraction:
                    puzzleInteraction.InteractionCompleted -= HandlePuzzleInteractionCompleted;
                    puzzleInteraction.InteractionEnabledStateChanged -= HandleInteractionTriggerStateChanged;
                    break;

                case CheckpointBehavior:
                    RemoveCheckpointTriggerSubscription();
                    break;

                case InteractionManager interaction:
                    interaction.InteractionEnabledStateChanged -= HandleInteractionTriggerStateChanged;
                    break;

                case ProgressionZone zone:
                    zone.ZoneEntered -= HandleZoneEntered;
                    break;

                case Wave wave:
                    wave.OnWaveStarted -= HandleWaveStarted;
                    wave.OnWaveComplete -= HandleWaveCompleted;
                    break;
            }
        }
    }

    private void SubscribeNoticeTriggers()
    {
        foreach (NoticeData notice in objectiveNotices)
        {
            Component triggerSource = GetNoticeTriggerSource(notice);
            switch (triggerSource)
            {
                case BoxCollider triggerZone:
                    ObjectiveTriggerZoneRelay relay = ObjectiveTriggerZoneRelay.GetOrAdd(triggerZone);
                    relay.Triggered -= HandleTriggerZoneEntered;
                    relay.Triggered += HandleTriggerZoneEntered;
                    break;

                case PuzzlePart puzzlePart:
                    if (notice.triggerOnPuzzleInteractionComplete)
                    {
                        puzzlePart.PuzzleCompleted -= HandlePuzzleCompleted;
                        puzzlePart.PuzzleCompleted += HandlePuzzleCompleted;
                    }
                    break;

                case PuzzleInteraction puzzleInteraction:
                    if (notice.triggerOnPuzzleInteractionComplete)
                    {
                        puzzleInteraction.InteractionCompleted -= HandlePuzzleInteractionCompleted;
                        puzzleInteraction.InteractionCompleted += HandlePuzzleInteractionCompleted;
                    }
                    else
                    {
                        puzzleInteraction.InteractionEnabledStateChanged -= HandleInteractionTriggerStateChanged;
                        puzzleInteraction.InteractionEnabledStateChanged += HandleInteractionTriggerStateChanged;
                    }
                    break;

                case CheckpointBehavior:
                    EnsureCheckpointTriggerSubscription();
                    break;

                case InteractionManager interaction:
                    interaction.InteractionEnabledStateChanged -= HandleInteractionTriggerStateChanged;
                    interaction.InteractionEnabledStateChanged += HandleInteractionTriggerStateChanged;
                    break;

                case ProgressionZone zone:
                    zone.ZoneEntered -= HandleZoneEntered;
                    zone.ZoneEntered += HandleZoneEntered;
                    break;

                case Wave wave:
                    wave.OnWaveStarted -= HandleWaveStarted;
                    wave.OnWaveStarted += HandleWaveStarted;
                    wave.OnWaveComplete -= HandleWaveCompleted;
                    wave.OnWaveComplete += HandleWaveCompleted;
                    break;
            }
        }
    }

    private void UnsubscribeNoticeTriggers()
    {
        foreach (NoticeData notice in objectiveNotices)
        {
            Component triggerSource = GetNoticeTriggerSource(notice);
            switch (triggerSource)
            {
                case BoxCollider triggerZone:
                    ObjectiveTriggerZoneRelay relay = ObjectiveTriggerZoneRelay.GetOrAdd(triggerZone);
                    relay.Triggered -= HandleTriggerZoneEntered;
                    break;

                case PuzzlePart puzzlePart:
                    puzzlePart.PuzzleCompleted -= HandlePuzzleCompleted;
                    break;

                case PuzzleInteraction puzzleInteraction:
                    puzzleInteraction.InteractionCompleted -= HandlePuzzleInteractionCompleted;
                    puzzleInteraction.InteractionEnabledStateChanged -= HandleInteractionTriggerStateChanged;
                    break;

                case CheckpointBehavior:
                    RemoveCheckpointTriggerSubscription();
                    break;

                case InteractionManager interaction:
                    interaction.InteractionEnabledStateChanged -= HandleInteractionTriggerStateChanged;
                    break;

                case ProgressionZone zone:
                    zone.ZoneEntered -= HandleZoneEntered;
                    break;

                case Wave wave:
                    wave.OnWaveStarted -= HandleWaveStarted;
                    wave.OnWaveComplete -= HandleWaveCompleted;
                    break;
            }
        }
    }

    private void EnsureCheckpointTriggerSubscription()
    {
        if (subscribedToCheckpointTriggers)
            return;

        CheckpointBehavior.OnCheckpointTriggered -= HandleCheckpointTriggered;
        CheckpointBehavior.OnCheckpointTriggered += HandleCheckpointTriggered;
        subscribedToCheckpointTriggers = true;
    }

    private void RemoveCheckpointTriggerSubscription()
    {
        if (!subscribedToCheckpointTriggers)
            return;

        CheckpointBehavior.OnCheckpointTriggered -= HandleCheckpointTriggered;
        subscribedToCheckpointTriggers = false;
    }

    private void HandleInteractionTriggerStateChanged(InteractionManager interaction, bool isEnabled)
    {
        if (isEnabled)
        {
            ShowObjectivesForTrigger(interaction);
            ShowNoticesForTrigger(interaction);
        }
    }

    private void HandleZoneEntered(ProgressionZone zone)
    {
        ShowObjectivesForTrigger(zone);
        ShowNoticesForTrigger(zone);
    }

    private void HandleWaveStarted(Wave wave)
    {
        ShowObjectivesForWave(wave, triggerOnCompletion: false);
        ShowNoticesForWave(wave, triggerOnCompletion: false);
    }

    private void HandleWaveCompleted(Wave wave)
    {
        ShowObjectivesForWave(wave, triggerOnCompletion: true);
        ShowNoticesForWave(wave, triggerOnCompletion: true);
    }

    private void HandlePuzzleInteractionCompleted(PuzzleInteraction interaction)
    {
        ShowObjectivesForPuzzleInteraction(interaction, triggerOnCompletion: true);
        ShowNoticesForPuzzleInteraction(interaction, triggerOnCompletion: true);
    }

    private void HandlePuzzleCompleted(PuzzlePart puzzlePart)
    {
        ShowObjectivesForPuzzlePart(puzzlePart, triggerOnCompletion: true);
        ShowNoticesForPuzzlePart(puzzlePart, triggerOnCompletion: true);
    }

    private void HandleCheckpointTriggered(CheckpointBehavior checkpoint)
    {
        ShowObjectivesForTrigger(checkpoint);
        ShowNoticesForTrigger(checkpoint);
    }

    private void HandleTriggerZoneEntered(BoxCollider triggerZone)
    {
        ShowObjectivesForTrigger(triggerZone);
        ShowNoticesForTrigger(triggerZone);
    }

    private void ShowObjectivesForPuzzleInteraction(PuzzleInteraction interaction, bool triggerOnCompletion)
    {
        if (interaction == null)
            return;

        foreach (ObjectiveData objective in objectives)
        {
            if (objective == null || GetObjectiveTriggerSource(objective) != interaction)
                continue;

            if (objective.triggerOnPuzzleInteractionComplete != triggerOnCompletion)
                continue;

            string effectiveObjectiveID = ResolveObjectiveID(objective.objectiveID, objective.interactionToActivate, interaction);
            if (string.IsNullOrWhiteSpace(effectiveObjectiveID))
            {
                Debug.LogWarning($"[MasterObjectiveClass] Objective triggered by {interaction.name} has no valid objective ID.");
                continue;
            }

            objective.objectiveID = effectiveObjectiveID;
            ShowObjective(effectiveObjectiveID);
        }
    }

    private void ShowObjectivesForPuzzlePart(PuzzlePart puzzlePart, bool triggerOnCompletion)
    {
        if (puzzlePart == null)
            return;

        foreach (ObjectiveData objective in objectives)
        {
            if (objective == null || GetObjectiveTriggerSource(objective) != puzzlePart)
                continue;

            if (objective.triggerOnPuzzleInteractionComplete != triggerOnCompletion)
                continue;

            string effectiveObjectiveID = ResolveObjectiveID(objective.objectiveID, objective.interactionToActivate, puzzlePart);
            if (string.IsNullOrWhiteSpace(effectiveObjectiveID))
            {
                Debug.LogWarning($"[MasterObjectiveClass] Objective triggered by {puzzlePart.name} has no valid objective ID.");
                continue;
            }

            objective.objectiveID = effectiveObjectiveID;
            ShowObjective(effectiveObjectiveID);
        }
    }

    private void ShowObjectivesForWave(Wave wave, bool triggerOnCompletion)
    {
        if (wave == null)
            return;

        foreach (ObjectiveData objective in objectives)
        {
            if (objective == null || GetObjectiveTriggerSource(objective) != wave)
                continue;

            if (objective.triggerOnWaveCompletion != triggerOnCompletion)
                continue;

            string effectiveObjectiveID = ResolveObjectiveID(objective.objectiveID, objective.interactionToActivate, wave);
            if (string.IsNullOrWhiteSpace(effectiveObjectiveID))
            {
                Debug.LogWarning($"[MasterObjectiveClass] Objective triggered by {wave.name} has no valid objective ID.");
                continue;
            }

            objective.objectiveID = effectiveObjectiveID;
            ShowObjective(effectiveObjectiveID);
        }
    }

    private void ShowObjectivesForTrigger(Component triggerSource)
    {
        if (triggerSource == null)
            return;

        foreach (ObjectiveData objective in objectives)
        {
            if (objective == null || GetObjectiveTriggerSource(objective) != triggerSource)
                continue;

            if ((triggerSource is PuzzleInteraction || triggerSource is PuzzlePart) && objective.triggerOnPuzzleInteractionComplete)
                continue;

            string effectiveObjectiveID = ResolveObjectiveID(objective.objectiveID, objective.interactionToActivate, triggerSource);
            if (string.IsNullOrWhiteSpace(effectiveObjectiveID))
            {
                Debug.LogWarning($"[MasterObjectiveClass] Objective triggered by {triggerSource.name} has no valid objective ID.");
                continue;
            }

            objective.objectiveID = effectiveObjectiveID;
            ShowObjective(effectiveObjectiveID);
        }
    }

    private void ShowNoticesForPuzzleInteraction(PuzzleInteraction interaction, bool triggerOnCompletion)
    {
        if (interaction == null)
            return;

        foreach (NoticeData notice in objectiveNotices)
        {
            if (notice == null || GetNoticeTriggerSource(notice) != interaction)
                continue;

            if (notice.triggerOnPuzzleInteractionComplete != triggerOnCompletion)
                continue;

            ShowConfiguredNotice(notice, interaction);
        }
    }

    private void ShowNoticesForPuzzlePart(PuzzlePart puzzlePart, bool triggerOnCompletion)
    {
        if (puzzlePart == null)
            return;

        foreach (NoticeData notice in objectiveNotices)
        {
            if (notice == null || GetNoticeTriggerSource(notice) != puzzlePart)
                continue;

            if (notice.triggerOnPuzzleInteractionComplete != triggerOnCompletion)
                continue;

            ShowConfiguredNotice(notice, puzzlePart);
        }
    }

    private void ShowNoticesForWave(Wave wave, bool triggerOnCompletion)
    {
        if (wave == null)
            return;

        foreach (NoticeData notice in objectiveNotices)
        {
            if (notice == null || GetNoticeTriggerSource(notice) != wave)
                continue;

            if (notice.triggerOnWaveCompletion != triggerOnCompletion)
                continue;

            ShowConfiguredNotice(notice, wave);
        }
    }

    private void ShowNoticesForTrigger(Component triggerSource)
    {
        if (triggerSource == null)
            return;

        foreach (NoticeData notice in objectiveNotices)
        {
            if (notice == null || GetNoticeTriggerSource(notice) != triggerSource)
                continue;

            if ((triggerSource is PuzzleInteraction || triggerSource is PuzzlePart) && notice.triggerOnPuzzleInteractionComplete)
                continue;

            if (triggerSource is Wave && notice.triggerOnWaveCompletion)
                continue;

            ShowConfiguredNotice(notice, triggerSource);
        }
    }

    private void ShowConfiguredNotice(NoticeData notice, Component triggerSource)
    {
        if (notice == null)
            return;

        string effectiveNoticeID = ResolveNoticeID(notice.noticeID, notice.interactionToActivate, triggerSource);
        if (string.IsNullOrWhiteSpace(effectiveNoticeID))
        {
            Debug.LogWarning($"[MasterObjectiveClass] Notice triggered by {triggerSource.name} has no valid notice ID.");
            return;
        }

        notice.noticeID = effectiveNoticeID;
        ApplyNoticeEffects(notice);

        if (noticeManager == null)
            noticeManager = FindFirstObjectByType<NoticeManager>();

        if (noticeManager == null)
        {
            Debug.LogError("[MasterObjectiveClass] NoticeManager is null and could not be found. Cannot show notice.");
            return;
        }

        noticeManager.ShowNotice(
            notice.noticeText,
            notice.bottomText,
            ResolveFadeDuration(notice.fadeDuration),
            ResolveDisplayDuration(notice.displayDuration),
            notice.priority
        );
    }

    public void ShowAttachedNoticesForTrigger(Component triggerSource)
    {
        ShowNoticesForTrigger(triggerSource);
    }

    private ObjectiveData FindIdInObjectives(string idToFind)
    {
        string normalizedTargetID = NormalizeNoticeID(idToFind);
        if (string.IsNullOrEmpty(normalizedTargetID))
            return null;

        foreach (ObjectiveData objective in objectives)
        {
            if (objective == null)
                continue;

            if (NormalizeNoticeID(objective.objectiveID) == normalizedTargetID)
            {
                Debug.Log($"Found objective with ID {normalizedTargetID}: {objective.objectiveText}");
                return objective;
            }
        }

        Debug.LogWarning($"Objective with ID {normalizedTargetID} not found in objectives.");
        return null;
    }

    private NoticeData FindIdInNotices(string idToFind)
    {
        string normalizedTargetID = NormalizeNoticeID(idToFind);
        if (string.IsNullOrEmpty(normalizedTargetID))
            return null;

        foreach (NoticeData notice in objectiveNotices)
        {
            if (NormalizeNoticeID(notice.noticeID) == normalizedTargetID)
            {
                Debug.Log($"Found notice with ID {normalizedTargetID}: {notice.noticeText}");
                return notice;
            }
        }
        Debug.LogWarning($"Notice with ID {normalizedTargetID} not found in objectiveNotices.");
        return null;
    }

    private NoticeData TryFindNoticeById(string idToFind)
    {
        string normalizedTargetID = NormalizeNoticeID(idToFind);
        if (string.IsNullOrEmpty(normalizedTargetID))
            return null;

        foreach (NoticeData notice in objectiveNotices)
        {
            if (notice == null)
                continue;

            if (NormalizeNoticeID(notice.noticeID) == normalizedTargetID)
                return notice;
        }

        return null;
    }

    private NoticeData TryFindNoticeByInteraction(InteractionManager interaction)
    {
        if (interaction == null)
            return null;

        foreach (NoticeData notice in objectiveNotices)
        {
            if (notice == null)
                continue;

            if (notice.interactionToActivate == interaction)
                return notice;
        }

        return null;
    }

    private static bool HasConfiguredNoticeContent(NoticeData notice)
    {
        return notice != null
            && (!string.IsNullOrWhiteSpace(notice.noticeText)
                || !string.IsNullOrWhiteSpace(notice.bottomText));
    }

    private static float ResolveFadeDuration(float fadeDuration)
    {
        return fadeDuration > 0f ? fadeDuration : 2f;
    }

    private static float ResolveDisplayDuration(float displayDuration)
    {
        return displayDuration > 0f ? displayDuration : 4f;
    }

    private static string NormalizeNoticeID(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return string.Empty;

        return id.Trim().ToLowerInvariant();
    }

    private void RemoveDuplicateObjectivesById(string normalizedObjectiveID, ObjectiveData keepObjective)
    {
        if (string.IsNullOrEmpty(normalizedObjectiveID) || keepObjective == null)
            return;

        for (int i = objectives.Count - 1; i >= 0; i--)
        {
            ObjectiveData candidate = objectives[i];
            if (candidate == null || candidate == keepObjective)
                continue;

            if (NormalizeNoticeID(candidate.objectiveID) == normalizedObjectiveID)
            {
                Debug.LogWarning($"[MasterObjectiveClass] Removing duplicate objective entry with ID {normalizedObjectiveID}.");
                objectives.RemoveAt(i);
            }
        }
    }

    private void RemoveDuplicateNoticesById(string normalizedNoticeID, NoticeData keepNotice)
    {
        if (string.IsNullOrEmpty(normalizedNoticeID) || keepNotice == null)
            return;

        for (int i = objectiveNotices.Count - 1; i >= 0; i--)
        {
            NoticeData candidate = objectiveNotices[i];
            if (candidate == null || candidate == keepNotice)
                continue;

            if (NormalizeNoticeID(candidate.noticeID) == normalizedNoticeID)
            {
                Debug.LogWarning($"[MasterObjectiveClass] Removing duplicate notice entry with ID {normalizedNoticeID}.");
                objectiveNotices.RemoveAt(i);
            }
        }
    }

    private void CorrectIDsIfIsEntry()
    {
        foreach (ObjectiveData objective in objectives)
        {
            Component triggerSource = GetObjectiveTriggerSource(objective);

            if (objective?.interactionToActivate is NavigationEntryInteraction)
            {
                objective.objectiveID = NormalizeNoticeID(objective.interactionToActivate.interactId);
                Debug.Log($"Corrected objective ID for entry interaction {objective.interactionToActivate.interactId}");
            }
            else if (objective != null && string.IsNullOrWhiteSpace(objective.objectiveID))
            {
                objective.objectiveID = ResolveObjectiveID(objective.objectiveID, objective.interactionToActivate, triggerSource);
            }
        }

        foreach (NoticeData notice in objectiveNotices)
        {
            if (notice?.interactionToActivate is NavigationEntryInteraction)
            {
                notice.noticeID = NormalizeNoticeID(notice.interactionToActivate.interactId);
                Debug.Log($"Corrected notice ID for entry interaction {notice.interactionToActivate.interactId}");
            }
            else if (notice != null && string.IsNullOrWhiteSpace(notice.noticeID))
            {
                notice.noticeID = ResolveNoticeID(notice.noticeID, notice.interactionToActivate, GetNoticeTriggerSource(notice));
            }
        }
    }

    private void ApplyInteractionState(InteractionManager interaction, bool isEnabled)
    {
        if (interaction == null)
            return;

        interaction.SetInteractionEnabled(isEnabled);
    }

    private void ApplyObjectiveEffects(ObjectiveData objective)
    {
        if (objective == null)
            return;

        ApplyInteractionState(objective.interactionToActivate, true);

        if (objective.disableInteraction)
            ApplyInteractionState(objective.interactionToDisable, false);
    }

    private void ApplyNoticeEffects(NoticeData notice)
    {
        if (notice == null)
            return;

        ApplyInteractionState(notice.interactionToActivate, true);

        if (notice.disableInteraction)
            ApplyInteractionState(notice.interactionToDisable, false);
    }

    private static void ShowObjectiveData(ObjectiveData objectiveToShow)
    {
        if (objectiveToShow == null || string.IsNullOrWhiteSpace(objectiveToShow.objectiveText))
            return;

        switch (objectiveToShow.objectiveType)
        {
            case ObjectiveEntryType.Sub:
                ObjectiveManager.RemoveSubObjective(objectiveToShow.objectiveID);
                ObjectiveManager.AddSubObjective(objectiveToShow.objectiveID, objectiveToShow.objectiveText);
                break;

            case ObjectiveEntryType.Main:
            default:
                ObjectiveManager.SetMainObjective(objectiveToShow.objectiveText);
                break;
        }
    }

    public void ShowObjective(string objectiveID)
    {
        ObjectiveData objectiveToShow = FindIdInObjectives(objectiveID);
        if (objectiveToShow == null)
            return;

        ApplyObjectiveEffects(objectiveToShow);
        ShowObjectiveData(objectiveToShow);
    }

    public void CreateAndShowObjective(
        InteractionManager interaction,
        string objectiveID,
        string objectiveText,
        ObjectiveEntryType objectiveType = ObjectiveEntryType.Main,
        int priority = 0,
        bool disableInteraction = false,
        InteractionManager interactionToDisable = null)
    {
        CreateAndShowObjective(interaction, interaction, objectiveID, objectiveText, objectiveType, priority, disableInteraction, interactionToDisable);
    }

    public void CreateAndShowObjective(
        InteractionManager interaction,
        MonoBehaviour triggerSource,
        string objectiveID,
        string objectiveText,
        ObjectiveEntryType objectiveType = ObjectiveEntryType.Main,
        int priority = 0,
        bool disableInteraction = false,
        InteractionManager interactionToDisable = null)
    {
        string effectiveObjectiveID = ResolveObjectiveID(objectiveID, interaction, triggerSource);

        if (string.IsNullOrWhiteSpace(effectiveObjectiveID))
        {
            Debug.LogWarning("[MasterObjectiveClass] CreateAndShowObjective called without a valid objectiveID, interaction.interactId, or trigger source.");
            return;
        }

        ObjectiveData existingObjective = FindIdInObjectives(effectiveObjectiveID);
        if (existingObjective != null)
        {
            Debug.Log($"Objective with ID {effectiveObjectiveID} already exists, updating and showing.");
            existingObjective.interactionToActivate = interaction;
            existingObjective.triggerSource = triggerSource;
            existingObjective.triggerZoneSource = null;
            existingObjective.objectiveID = effectiveObjectiveID;
            existingObjective.objectiveText = objectiveText;
            existingObjective.objectiveType = objectiveType;
            existingObjective.priority = priority;
            existingObjective.disableInteraction = disableInteraction;
            existingObjective.interactionToDisable = interactionToDisable;
            RemoveDuplicateObjectivesById(effectiveObjectiveID, existingObjective);
            ApplyObjectiveEffects(existingObjective);
            ShowObjectiveData(existingObjective);
            return;
        }

        ObjectiveData newObjective = new ObjectiveData
        {
            interactionToActivate = interaction,
            triggerSource = triggerSource,
            triggerZoneSource = null,
            triggerOnWaveCompletion = existingObjective?.triggerOnWaveCompletion ?? false,
            objectiveID = effectiveObjectiveID,
            objectiveText = objectiveText,
            objectiveType = objectiveType,
            priority = priority,
            disableInteraction = disableInteraction,
            interactionToDisable = interactionToDisable
        };

        objectives.Add(newObjective);
        RemoveDuplicateObjectivesById(effectiveObjectiveID, newObjective);
        Debug.Log($"Created and added objective with ID {newObjective.objectiveID} (interaction: {interaction?.interactId ?? "null"}, type: {newObjective.objectiveType})");

        ApplyObjectiveEffects(newObjective);
        ShowObjectiveData(newObjective);
    }

    public void CreateAndShowMainObjective(
        InteractionManager interaction,
        string objectiveID,
        string objectiveText,
        int priority = 0,
        bool disableInteraction = false,
        InteractionManager interactionToDisable = null)
    {
        CreateAndShowObjective(interaction, objectiveID, objectiveText, ObjectiveEntryType.Main, priority, disableInteraction, interactionToDisable);
    }

    public void CreateAndShowMainObjective(
        InteractionManager interaction,
        MonoBehaviour triggerSource,
        string objectiveID,
        string objectiveText,
        int priority = 0,
        bool disableInteraction = false,
        InteractionManager interactionToDisable = null)
    {
        CreateAndShowObjective(interaction, triggerSource, objectiveID, objectiveText, ObjectiveEntryType.Main, priority, disableInteraction, interactionToDisable);
    }

    public void CreateAndShowSubObjective(
        InteractionManager interaction,
        string objectiveID,
        string objectiveText,
        int priority = 0,
        bool disableInteraction = false,
        InteractionManager interactionToDisable = null)
    {
        CreateAndShowObjective(interaction, objectiveID, objectiveText, ObjectiveEntryType.Sub, priority, disableInteraction, interactionToDisable);
    }

    public void CreateAndShowSubObjective(
        InteractionManager interaction,
        MonoBehaviour triggerSource,
        string objectiveID,
        string objectiveText,
        int priority = 0,
        bool disableInteraction = false,
        InteractionManager interactionToDisable = null)
    {
        CreateAndShowObjective(interaction, triggerSource, objectiveID, objectiveText, ObjectiveEntryType.Sub, priority, disableInteraction, interactionToDisable);
    }

    public void CompleteObjective(string objectiveID)
    {
        ObjectiveData objective = FindIdInObjectives(objectiveID);
        if (objective == null)
            return;

        if (objective.objectiveType == ObjectiveEntryType.Sub)
        {
            ObjectiveManager.CompleteSubObjective(objective.objectiveID);
            return;
        }

        ObjectiveManager.ClearMainObjective();
    }

    public void RemoveObjective(string objectiveID)
    {
        ObjectiveData objective = FindIdInObjectives(objectiveID);
        if (objective == null)
            return;

        if (objective.objectiveType == ObjectiveEntryType.Sub)
            ObjectiveManager.RemoveSubObjective(objective.objectiveID);
        else
            ObjectiveManager.ClearMainObjective();
    }

    public void ShowNotice(string noticeID)
    {
        if (noticeManager == null)
        {
            noticeManager = FindFirstObjectByType<NoticeManager>();
            if (noticeManager == null)
            {
                Debug.LogError("[MasterObjectiveClass] NoticeManager is null and could not be found. Cannot show notice.");
                return;
            }
        }

        NoticeData noticeToShow = FindIdInNotices(noticeID);
        if (noticeToShow == null)
            return;

        ApplyNoticeEffects(noticeToShow);

        noticeManager.ShowNotice(
            noticeToShow.noticeText,
            noticeToShow.bottomText,
            ResolveFadeDuration(noticeToShow.fadeDuration),
            ResolveDisplayDuration(noticeToShow.displayDuration),
            noticeToShow.priority
        );
    }

    /// <summary>
    /// Creates a new notice and immediately shows it. This is the main API for showing notices.
    /// </summary>
    public void CreateAndShowNotice(InteractionManager interaction, string noticeID, string noticeText, string bottomText, float fadeDuration = 2f, float displayDuration = 4f, int priority = 0)
    {
        if (noticeManager == null)
        {
            noticeManager = FindFirstObjectByType<NoticeManager>();
            if (noticeManager == null)
            {
                Debug.LogError("[MasterObjectiveClass] NoticeManager is null and could not be found. Cannot show notice.");
                return;
            }
        }

        string effectiveNoticeID = NormalizeNoticeID(!string.IsNullOrWhiteSpace(noticeID)
            ? noticeID
            : interaction != null ? interaction.interactId : string.Empty);

        if (string.IsNullOrWhiteSpace(effectiveNoticeID))
        {
            Debug.LogWarning("[MasterObjectiveClass] CreateAndShowNotice called without a valid noticeID or interaction.interactId.");
            return;
        }

        NoticeData configuredNotice = TryFindNoticeById(effectiveNoticeID);
        if (!HasConfiguredNoticeContent(configuredNotice))
        {
            NoticeData interactionNotice = TryFindNoticeByInteraction(interaction);
            if (HasConfiguredNoticeContent(interactionNotice))
                configuredNotice = interactionNotice;
        }

        if (HasConfiguredNoticeContent(configuredNotice))
        {
            if (string.IsNullOrWhiteSpace(configuredNotice.noticeID))
                configuredNotice.noticeID = effectiveNoticeID;

            ApplyNoticeEffects(configuredNotice);
            noticeManager.ShowNotice(
                configuredNotice.noticeText,
                configuredNotice.bottomText,
                ResolveFadeDuration(configuredNotice.fadeDuration),
                ResolveDisplayDuration(configuredNotice.displayDuration),
                configuredNotice.priority
            );
            return;
        }

        NoticeData existingNotice = TryFindNoticeById(effectiveNoticeID);
        if (existingNotice != null)
        {
            Debug.Log($"Notice with ID {effectiveNoticeID} already exists, updating and showing.");
            existingNotice.interactionToActivate = interaction;
            existingNotice.noticeID = effectiveNoticeID;
            existingNotice.noticeText = noticeText;
            existingNotice.bottomText = bottomText;
            existingNotice.fadeDuration = fadeDuration;
            existingNotice.displayDuration = displayDuration;
            existingNotice.priority = priority;
            RemoveDuplicateNoticesById(effectiveNoticeID, existingNotice);
            ApplyNoticeEffects(existingNotice);
        }
        else
        {
            NoticeData newNotice = new NoticeData
            {
                noticeID = effectiveNoticeID,
                noticeText = noticeText,
                bottomText = bottomText,
                fadeDuration = fadeDuration,
                displayDuration = displayDuration,
                priority = priority,
                interactionToActivate = interaction
            };
            objectiveNotices.Add(newNotice);
            RemoveDuplicateNoticesById(effectiveNoticeID, newNotice);
            Debug.Log($"Created and added notice with ID {newNotice.noticeID} (interaction: {interaction?.interactId ?? "null"})");
            ApplyNoticeEffects(newNotice);
        }

        noticeManager.ShowNotice(noticeText, bottomText, ResolveFadeDuration(fadeDuration), ResolveDisplayDuration(displayDuration), priority);
    }

    public void HideCollectUI()
    {
        if (noticeManager != null)
            noticeManager.HideCollectUI();
    }

    public void CancelCurrentCollectNotice(bool turnOffUI = false)
    {
        if (noticeManager != null)
            noticeManager.CancelCurrentCollectNotice(turnOffUI);
    }

    public void ClearNotice()
    {
        if (noticeManager != null)
            noticeManager.ClearNotice();
    }

    public void ForceStopNoticeCoroutines()
    {
        noticeManager.ForceStopNoticeCoroutines();
    }
}
