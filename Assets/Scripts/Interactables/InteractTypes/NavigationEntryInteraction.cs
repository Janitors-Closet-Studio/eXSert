/*
    Written by Brandon Wahl

    Place this script where you want a diary entry to be interacted with and collected into the player's inventory.
*/

using UnityEngine.InputSystem;
using UnityEngine;
using System.Collections;
using Unity.VisualScripting;

public class NavigationEntryInteraction : CollectableInteraction
{

    [Space(10)]
    [Header("Navigation Entry Data")]
    [SerializeField] private ScriptableObject entryData;

    [Space(10)]
    [SerializeField] private float timeToShowEntryAfterInteraction = 3f;
    
    [Space(10)]
    [Header("Entry Type")]
    [SerializeField] private bool isDiary;
    [SerializeField] private bool isLog;

    private GameObject entryContent;
    private GameObject entryHolder;

    private void OnEnable()
    {
        AssignId();
        SubscribeBasedOnDataType();
    }

    private void OnDisable()
    {
        UnSubscribeBasedOnDataType();
    }

    private void FindEntryContent()
    {
        if (isLog)
        {
            entryContent = NavigationMenu.Instance.logContent;
            entryHolder = NavigationMenu.Instance.logUI;
        }
        else if (isDiary)
        {
            entryContent = NavigationMenu.Instance.diaryContent;
            entryHolder = NavigationMenu.Instance.diaryUI;
        }
    }

    private void OnDiaryStateChange(Diaries diaries)
    {
        if (diaries.info.diaryID.Equals(this.interactId))
        {
            diaries.info.isFound = true;
        }
    }

    private void OnLogStateChange(Logs log)
    {
        if (log.info.logID.Equals(this.interactId))
        {
            log.info.isFound = true;
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
            return;
        }

        if(isLog)
        {
            var logSO = entryData as NavigationLogSO;
            
            logSO.isFound = true;

            EventsManager.Instance.logEvents.FoundLog(this.interactId);

            if(!LogManager.Instance.unreadLogs.Contains(logSO) || !logSO.isRead)
                LogManager.Instance.unreadLogs.Add(logSO);
        }
        else if(isDiary)
        {
            var diarySO = entryData as DiarySO;
            this.interactId = diarySO.diaryID;
            diarySO.isFound = true;

            // Pass diaryID (string) to FoundDiary as required
            EventsManager.Instance.diaryEvents.FoundDiary(this.interactId);
            
            if(!DiaryManager.Instance.unreadDiaries.Contains(diarySO) || !diarySO.isRead)
                DiaryManager.Instance.unreadDiaries.Add(diarySO);
        }

        StartCoroutine(ShowEntryOnPause(timeToShowEntryAfterInteraction));
    }

    private IEnumerator ShowEntryOnPause(float timeframe)
    {
        Debug.Log("Starting coroutine to show entry on pause after interaction.");
        float elapsedTime = 0f;

        if(PauseManager.Instance._pauseActionReference != null && PauseManager.Instance._pauseActionReference.action != null)
                PauseManager.Instance._pauseActionReference.action.performed += ShowEntryOnPressPause;

        while (elapsedTime < timeframe)
        {
            elapsedTime += Time.unscaledDeltaTime;
            yield return null;
        }
        Debug.Log("Finished waiting to show entry on pause. Unsubscribing from pause action.");
        if (PauseManager.Instance._pauseActionReference != null && PauseManager.Instance._pauseActionReference.action != null)
            PauseManager.Instance._pauseActionReference.action.performed -= ShowEntryOnPressPause;
    }

    private void ShowEntryOnPressPause(InputAction.CallbackContext context)
    {
        PauseManager.Instance.ShowNavigationMenu();
        FindEntryContent();

        var canvas = GameObject.FindGameObjectWithTag("Canvas");
        MenuListManager menuListManager = canvas.GetComponent<MenuListManager>();

        if (entryContent != null && entryHolder != null)
        {
            Debug.Log("Showing entry content on navigation menu.");
            SetEntryInfoIfPauseIsClicked();
            
            entryHolder.SetActive(true);
            menuListManager.AddToMenuList(entryHolder);

            entryContent.SetActive(true);
            menuListManager.AddToMenuList(entryContent);    
            entryContent.transform.SetAsLastSibling();
        } else
        {
            Debug.LogWarning("Entry content or entry holder is not assigned. Cannot show entry on navigation menu.");
        }

    }

    private void SetEntryInfoIfPauseIsClicked()
    {
        if (isLog)
        {
            var logUI = entryHolder.GetComponent<LogUI>();
            var logSO = entryData as NavigationLogSO;
            if (logSO != null)
                logUI.SetLogInfo(new Logs(logSO));
            logSO.isRead = true; // Mark log as read when viewed from navigation menu

            if(LogManager.Instance.unreadLogs.Contains(logSO))
            {
                LogManager.Instance.unreadLogs.Remove(logSO);
            }
        }
        else if (isDiary)
        {
            var diaryUI = entryHolder.GetComponent<DiaryUI>();
            var diarySO = entryData as DiarySO;
            if (diarySO != null)
                diaryUI.SetDiaryInfo(new Diaries(diarySO));
            diarySO.isRead = true; // Mark diary as read when viewed from navigation menu
            
            if(DiaryManager.Instance.unreadDiaries.Contains(diarySO))
            {
                DiaryManager.Instance.unreadDiaries.Remove(diarySO);
            }
        }
    }

}
