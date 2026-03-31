/*
    Written by Brandon Wahl

    Place this script where you want a diary entry to be interacted with and collected into the player's inventory.
*/

using UnityEngine.UI;
using UnityEngine;

public class NavigationEntryInteraction : CollectableInteraction
{

    [Space(10)]
    [Header("Navigation Entry Data")]
    [SerializeField] private ScriptableObject entryData;

    
    [Space(10)]
    [Header("Entry Type")]
    [SerializeField] private bool isDiary;
    [SerializeField] private bool isLog;

    public override void OnEnable()
    {
        base.OnEnable();

        AssignId();
        SubscribeBasedOnDataType();
        
    }

    public override void OnDisable()
    {
        base.OnDisable();

        UnSubscribeBasedOnDataType();
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
        if(isDiary)
        {
            var diarySO = entryData as DiarySO;
            EventsManager.Instance.diaryEvents.onDiaryStateChange += OnDiaryStateChange;
        }
        else if(isLog)
        {
            var logSO = entryData as NavigationLogSO;
            EventsManager.Instance.logEvents.onLogStateChange += OnLogStateChange;
        }
    }

    private void UnSubscribeBasedOnDataType()
    {
        if (isDiary)
        {
            var diarySO = entryData as DiarySO;
            if (EventsManager.Instance != null && EventsManager.Instance.diaryEvents != null)
                EventsManager.Instance.diaryEvents.onDiaryStateChange -= OnDiaryStateChange;
        }
        else if (isLog)
        {
            var logSO = entryData as NavigationLogSO;
            if (EventsManager.Instance != null && EventsManager.Instance.logEvents != null)
                EventsManager.Instance.logEvents.onLogStateChange -= OnLogStateChange;
        }
    }

    private void AssignId()
    {
        if(isDiary)
        {
            var diarySO = entryData as DiarySO;
            this.interactId = diarySO.diaryID;
        }
        else if(isLog)
        {
            var logSO = entryData as NavigationLogSO;
            this.interactId = logSO.logID;
        }
    }

    protected override void ExecuteInteraction()
    {
        if (string.IsNullOrEmpty(this.interactId))
        {
            Debug.LogError($"{gameObject.name}: interactId is not set! Cannot process interaction.");
            return;
        }

        if(isLog)
        {
            var logSO = entryData as NavigationLogSO;
            
            logSO.isFound = true;

            EventsManager.Instance.logEvents.FoundLog(this.interactId);

            LogManager.Instance.unreadLogs.Add(logSO);
        }
        else if(isDiary)
        {
            var diarySO = entryData as DiarySO;
            this.interactId = diarySO.diaryID;
            diarySO.isFound = true;

            EventsManager.Instance.diaryEvents.FoundDiary(this.interactId);
            
            DiaryManager.Instance.unreadDiaries.Add(diarySO);
        }
    }

}
