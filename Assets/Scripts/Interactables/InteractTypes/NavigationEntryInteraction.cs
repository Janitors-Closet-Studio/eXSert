/*
    Written by Brandon Wahl

    Place this script where you want a diary entry to be interacted with and collected into the player's inventory.
*/

using UnityEngine.UI;
using UnityEngine;
using System;

public class NavigationEntryInteraction : CollectableInteraction
{
    private enum EntryType
    {
        None,
        Diary,
        Log
    }

    [Space(10)]
    [Header("Navigation Entry Data")]
    [SerializeField] private ScriptableObject entryData;

    
    [Space(10)]
    [Header("Entry Type")]
    [SerializeField] private bool isDiary;
    [SerializeField] private bool isLog;

    public event Action<string> OnEntryCollected;
    public event Action OnEntryRead;

    protected override void OnEnable()
    {
        base.OnEnable();
        AssignId();
        SubscribeBasedOnDataType();
        
    }

    protected override void OnDisable()
    {
        UnSubscribeBasedOnDataType();
        base.OnDisable();
    }

    private void OnDiaryStateChange(Diaries diaries)
    {
        if (diaries.info.diaryID.Equals(this.interactId))
        {
            Debug.Log("Diary with id " + this.interactId + " updated to state: Is Found " + diaries.info.isFound);
        }
    }

    private void OnLogStateChange(Logs log)
    {
        if (log.info.logID.Equals(this.interactId))
        {
            Debug.Log("Log with id " + this.interactId + " updated to state: Is Found " + log.info.isFound);
        }
    }

    private void SubscribeBasedOnDataType()
    {
        EntryType entryType = ResolveEntryType();
        if (entryType == EntryType.Diary)
        {
            if (EventsManager.Instance != null && EventsManager.Instance.diaryEvents != null)
                EventsManager.Instance.diaryEvents.onDiaryStateChange += OnDiaryStateChange;
        }
        else if (entryType == EntryType.Log)
        {
            if (EventsManager.Instance != null && EventsManager.Instance.logEvents != null)
                EventsManager.Instance.logEvents.onLogStateChange += OnLogStateChange;

            ((NavigationLogSO) entryData).LogRead += OnEntryRead;
        }
    }

    private void UnSubscribeBasedOnDataType()
    {
        EntryType entryType = ResolveEntryType();
        if (entryType == EntryType.Diary)
        {
            if (EventsManager.Instance != null && EventsManager.Instance.diaryEvents != null)
                EventsManager.Instance.diaryEvents.onDiaryStateChange -= OnDiaryStateChange;
        }
        else if (entryType == EntryType.Log)
        {
            if (EventsManager.Instance != null && EventsManager.Instance.logEvents != null)
                EventsManager.Instance.logEvents.onLogStateChange -= OnLogStateChange;

            ((NavigationLogSO)entryData).LogRead -= OnEntryRead;
        }
    }

    private void AssignId()
    {
        EntryType entryType = ResolveEntryType();

        if (entryType == EntryType.Diary)
        {
            var diarySO = entryData as DiarySO;
            if (diarySO == null)
            {
                Debug.LogError($"{gameObject.name}: Entry data is not a valid DiarySO.");
                return;
            }

            this.interactId = diarySO.diaryID;
        }
        else if (entryType == EntryType.Log)
        {
            var logSO = entryData as NavigationLogSO;
            if (logSO == null)
            {
                Debug.LogError($"{gameObject.name}: Entry data is not a valid NavigationLogSO.");
                return;
            }

            this.interactId = logSO.logID;
        }
        else
        {
            this.interactId = string.Empty;
            Debug.LogError($"{gameObject.name}: Entry type is not configured. Assign entry data and set exactly one type.");
        }
    }

    protected override void ExecuteInteraction()
    {
        AssignId();

        if (string.IsNullOrEmpty(this.interactId))
        {
            Debug.LogError($"{gameObject.name}: interactId is not set! Cannot process interaction.");
            return;
        }

        EntryType entryType = ResolveEntryType();

        if (entryType == EntryType.Log)
        {
            var logSO = entryData as NavigationLogSO;
            if (logSO == null)
            {
                Debug.LogError($"{gameObject.name}: Missing or invalid NavigationLogSO.");
                return;
            }

            logSO.isFound = true;

            if (EventsManager.Instance != null && EventsManager.Instance.logEvents != null)
                EventsManager.Instance.logEvents.FoundLog(this.interactId);

            if (LogManager.Instance != null)
                LogManager.Instance.unreadLogs.Add(logSO);
        }
        else if (entryType == EntryType.Diary)
        {
            var diarySO = entryData as DiarySO;
            if (diarySO == null)
            {
                Debug.LogError($"{gameObject.name}: Missing or invalid DiarySO.");
                return;
            }

            this.interactId = diarySO.diaryID;
            diarySO.isFound = true;

            if (EventsManager.Instance != null && EventsManager.Instance.diaryEvents != null)
                EventsManager.Instance.diaryEvents.FoundDiary(this.interactId);
            
            if (DiaryManager.Instance != null)
                DiaryManager.Instance.unreadDiaries.Add(diarySO);
        }
        else
        {
            Debug.LogError($"{gameObject.name}: Cannot execute interaction because entry type is invalid.");
            return;
        }

        Debug.Log($"Collected entry with id: {this.interactId}");
        OnEntryCollected?.Invoke(interactId);
    }

    private EntryType ResolveEntryType()
    {
        if (entryData is DiarySO)
        {
            isDiary = true;
            isLog = false;
            return EntryType.Diary;
        }

        if (entryData is NavigationLogSO)
        {
            isLog = true;
            isDiary = false;
            return EntryType.Log;
        }

        if (isDiary && !isLog)
            return EntryType.Diary;

        if (isLog && !isDiary)
            return EntryType.Log;

        return EntryType.None;
    }

}
