using System.Collections.Generic;
using Singletons;
using UnityEngine;

[System.Serializable]
public class NoticeData
{
    public InteractionManager interactionToActivate;
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
        CorrectIDIfIsEntry();
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

    private void CorrectIDIfIsEntry()
    {
        foreach (NoticeData notice in objectiveNotices)
        {
            if (notice.interactionToActivate is NavigationEntryInteraction)
            {
                notice.noticeID = NormalizeNoticeID(notice.interactionToActivate.interactId);
                Debug.Log($"Corrected notice ID for entry interaction {notice.interactionToActivate.interactId}");
            }
        }
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
            // Update the existing notice
            existingNotice.interactionToActivate = interaction;
            existingNotice.noticeID = effectiveNoticeID;
            existingNotice.noticeText = noticeText;
            existingNotice.bottomText = bottomText;
            existingNotice.fadeDuration = fadeDuration;
            existingNotice.displayDuration = displayDuration;
            existingNotice.priority = priority;
            RemoveDuplicateNoticesById(effectiveNoticeID, existingNotice);
        }
        else
        {
            // Create new notice
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
        }

        // Show the notice
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
