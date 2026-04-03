/*
    Contains all of the different pieces of text that will be changed depending on which log is selected

    Written by Brandon Wahl
*/

using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Unity.VisualScripting;
public class LogUI : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private GameObject contentParent;
    [SerializeField] private LogScrollingList scrollingList;
    [SerializeField] private TMP_Text logName;
    [SerializeField] private GameObject logDescription;
    [SerializeField] private TMP_Text logLocation;
    [SerializeField] private TMP_Text logId_Date;
    [SerializeField] private Image logImage;

    //LogStateChange beong subscribed and unsubscribed
    private void OnEnable()
    {
        if(scrollingList != null)
            scrollingList.ClearLogButtons(); // Clear existing buttons to prevent duplicates

        EventsManager.Instance.logEvents.onLogStateChange -= LogStateChange; // Unsubscribe first to prevent multiple subscriptions
        EventsManager.Instance.logEvents.onLogStateChange += LogStateChange;
        // Refresh all logs to populate buttons when UI becomes active
        if (LogManager.Instance != null)
        {
            LogManager.Instance.RefreshAllLogs();
        }
    }

    private void OnDisable()
    {
        EventsManager.Instance.logEvents.onLogStateChange -= LogStateChange;
    }

    //Creates the button with the info from SetLogInfo
    private void LogStateChange(Logs log)
    {
        LogButton logButton = scrollingList.CreateButtonIfNotExists(log, () =>
        {
            SetLogInfo(log);
           
        }, log.info.isRead);
    }

    //Sets each log info
    internal void SetLogInfo(Logs log)
    {
        Debug.Log($"Setting log info for {log.info.logID}");
        logName.text = log.info.logName;
        logDescription.GetComponent<TMP_Text>().text = log.info.logDescription;
        logLocation.text = log.info.locationFound;
        logId_Date.text = log.info.logID;
        log.info.MarkAsFound(); // Mark log as read when selected

        if(LogManager.Instance.unreadLogs.Contains(log.info))
        {
            LogManager.Instance.unreadLogs.Remove(log.info);
        }

         // Update the log image, and handle case where there may not be an image assigned
        
        if (log.info.logImage != null && log.info.logImage.sprite != null)
            logImage.sprite = log.info.logImage.sprite;
        else
            logImage.sprite = null;
    }

}
