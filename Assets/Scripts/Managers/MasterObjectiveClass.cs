using System.Collections.Generic;
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
    public string objectiveID;
    [TextArea] public string objectiveText;
    public ObjectiveEntryType objectiveType = ObjectiveEntryType.Main;
    public bool disableInteraction;
    public InteractionManager interactionToDisable;
    public int priority = 0;
}

[System.Serializable]
public class NoticeData
{
    public InteractionManager interactionToActivate;
    public bool disableInteraction;
    public InteractionManager interactionToDisable;
    public string noticeID;
    public string noticeText;
    public string bottomText;
    public float fadeDuration = 0.5f;
    public float displayDuration = 1.5f;
    public int priority = 0;
}

public class MasterObjectiveClass : SceneSingleton<MasterObjectiveClass>
{
    [SerializeField] private NoticeManager noticeManager;

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
            if (objective?.interactionToActivate is NavigationEntryInteraction)
            {
                objective.objectiveID = NormalizeNoticeID(objective.interactionToActivate.interactId);
                Debug.Log($"Corrected objective ID for entry interaction {objective.interactionToActivate.interactId}");
            }
        }

        foreach (NoticeData notice in objectiveNotices)
        {
            if (notice?.interactionToActivate is NavigationEntryInteraction)
            {
                notice.noticeID = NormalizeNoticeID(notice.interactionToActivate.interactId);
                Debug.Log($"Corrected notice ID for entry interaction {notice.interactionToActivate.interactId}");
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
        string effectiveObjectiveID = NormalizeNoticeID(!string.IsNullOrWhiteSpace(objectiveID)
            ? objectiveID
            : interaction != null ? interaction.interactId : string.Empty);

        if (string.IsNullOrWhiteSpace(effectiveObjectiveID))
        {
            Debug.LogWarning("[MasterObjectiveClass] CreateAndShowObjective called without a valid objectiveID or interaction.interactId.");
            return;
        }

        ObjectiveData existingObjective = FindIdInObjectives(effectiveObjectiveID);
        if (existingObjective != null)
        {
            Debug.Log($"Objective with ID {effectiveObjectiveID} already exists, updating and showing.");
            existingObjective.interactionToActivate = interaction;
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

        noticeManager.ShowNotice(noticeToShow.noticeText, noticeToShow.bottomText, noticeToShow.fadeDuration, noticeToShow.displayDuration, noticeToShow.priority);
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

        NoticeData existingNotice = FindIdInNotices(effectiveNoticeID);
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

        noticeManager.ShowNotice(noticeText, bottomText, fadeDuration, displayDuration, priority);
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
