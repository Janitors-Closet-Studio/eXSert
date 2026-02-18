/*
    Written by Brandon Wahl

    Place this script where you want a diary entry to be interacted with and collected into the player's inventory.
*/

using UnityEngine.UI;
using UnityEngine;

public class NavigationEntryInteraction : InteractionManager
{

    [Space(10)]
    [Header("Navigation Entry Data")]
    [SerializeField] private ScriptableObject entryData;

    [SerializeField] private bool showEntryUI = false;
    
    [Space(10)]
    [Header("Entry Type")]
    [SerializeField] private bool isDiary;
    [SerializeField] private bool isLog;

    private void OnEnable()
    {
        AssignId();
        SubscribeBasedOnDataType();
        
    }

    private void OnDisable()
    {
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

    protected override void Interact()
    {
        if (string.IsNullOrEmpty(this.interactId))
        {
            Debug.LogError($"{gameObject.name}: interactId is not set! Cannot process interaction.");
            return;
        }
        
        var navigationMenu = NavigationMenu.Instance.navigationMenuGO;
        var loadFirstNavigationEntry = NavigationMenu.Instance.GetComponent<LoadFirstEntry>();

        if(isLog)
        {
            var logSO = entryData as NavigationLogSO;
            
            logSO.isFound = true;
            
            if(showEntryUI && NavigationMenu.Instance.navigationMenuGO != null)
            {
                
                var logUI = navigationMenu.transform.GetChild(0).transform.GetChild(4).gameObject;
                var logScrollingList = logUI.GetComponentInChildren<LogScrollingList>();
                var logContent = logScrollingList.contentRectTransform.gameObject;

                if(loadFirstNavigationEntry != null && logUI != null && logScrollingList != null && logContent != null){
                loadFirstNavigationEntry.StartCoroutine(loadFirstNavigationEntry.ShowScreenIfFirstEntry(logUI
                , loadFirstNavigationEntry.playerHud, logScrollingList, logContent));
                }
                else
                {
                    Debug.LogError("One or more components required for showing first log entry are null: " +
                    $"LoadFirstEntry: {loadFirstNavigationEntry != null}, LogUI: {logUI != null}, LogScrollingList: {logScrollingList != null}, LogContent: {logContent != null}");
                }
            }

            EventsManager.Instance.logEvents.FoundLog(this.interactId);
            DeactivateInteractable(this);
        }
        else if(isDiary)
        {
            var diarySO = entryData as DiarySO;
            this.interactId = diarySO.diaryID;
            diarySO.isFound = true;

            if(showEntryUI && NavigationMenu.Instance.navigationMenuGO != null)
            {
                
                var diaryUI = navigationMenu.transform.GetChild(0).transform.GetChild(5).gameObject;
                var diaryScrollingList = diaryUI.GetComponentInChildren<DiaryScrollingList>();
                var diaryContent = diaryScrollingList.contentRectTransform.gameObject;
                
                loadFirstNavigationEntry.StartCoroutine(loadFirstNavigationEntry.ShowScreenIfFirstEntry(diaryUI
                , loadFirstNavigationEntry.playerHud, diaryScrollingList, diaryContent));
            }

            EventsManager.Instance.diaryEvents.FoundDiary(this.interactId);
            DeactivateInteractable(this);
        }
    }

}
