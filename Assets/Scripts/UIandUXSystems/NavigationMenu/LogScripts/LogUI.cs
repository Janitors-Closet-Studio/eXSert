/*
    Contains all of the different pieces of text that will be changed depending on which log is selected

    Written by Brandon Wahl
*/

using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Unity.VisualScripting;
using UnityEngine.InputSystem;
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

    [Header("Controller Scrolling")]
    [SerializeField] private float controllerScrollSpeed = 2f;
    [SerializeField] private float controllerScrollDeadzone = 0.35f;

    private const string IndividualLogMenuTag = "IndividualLogMenu";

    private ScrollRect detailScrollRect;
    private bool resetDetailScrollPending;

    //LogStateChange beong subscribed and unsubscribed
    private void OnEnable()
    {
        if(scrollingList != null)
            scrollingList.ClearLogButtons(); // Clear existing buttons to prevent duplicates

        if (EventsManager.Instance != null && EventsManager.Instance.logEvents != null)
        {
            EventsManager.Instance.logEvents.onLogStateChange -= LogStateChange; // Unsubscribe first to prevent multiple subscriptions
            EventsManager.Instance.logEvents.onLogStateChange += LogStateChange;
        }
        // Refresh all logs to populate buttons when UI becomes active
        if (LogManager.Instance != null)
        {
            LogManager.Instance.RefreshAllLogs();
        }
    }

    private void OnDisable()
    {
        if (EventsManager.Instance != null && EventsManager.Instance.logEvents != null)
            EventsManager.Instance.logEvents.onLogStateChange -= LogStateChange;
    }

    private void Update()
    {
        ResetDetailScrollIfNeeded();
        HandleControllerScroll();
    }

    //Creates the button with the info from SetLogInfo
    private void LogStateChange(Logs log)
    {
        scrollingList.CreateButtonIfNotExists(log, () =>
        {
            SetLogInfo(log);
           
        }, log.info.isRead);
    }

    //Sets each log info
    internal void SetLogInfo(Logs log)
    {
        DebugLogSettingsM.ConditionalLog(DebugLogCategory.UI, $"Setting log info for {log.info.logID}");
        logName.text = log.info.logName;
        logDescription.GetComponent<TMP_Text>().text = log.info.logDescription;
        logLocation.text = log.info.locationFound;
        logId_Date.text = log.info.logID;
        bool wasUnread = !log.info.isRead;
        log.info.MarkAsRead();

        if(LogManager.Instance.unreadLogs.Contains(log.info))
        {
            LogManager.Instance.unreadLogs.Remove(log.info);
        }

         // Update the log image, and handle case where there may not be an image assigned
        
        if (log.info.logImage != null && log.info.logImage.sprite != null)
            logImage.sprite = log.info.logImage.sprite;
        else
            logImage.sprite = null;

        resetDetailScrollPending = true;
        ResetDetailScrollIfNeeded();

        if (wasUnread && EventsManager.Instance != null && EventsManager.Instance.logEvents != null)
            EventsManager.Instance.logEvents.LogStateChange(log);
    }

    private void HandleControllerScroll()
    {
        ScrollRect activeScrollRect = GetDetailScrollRect();
        if (activeScrollRect == null || !activeScrollRect.gameObject.activeInHierarchy || !activeScrollRect.vertical)
            return;

        RectTransform contentRect = activeScrollRect.content;
        RectTransform viewportRect = activeScrollRect.viewport;
        if (contentRect == null || viewportRect == null || contentRect.rect.height <= viewportRect.rect.height)
            return;

        Gamepad gamepad = Gamepad.current;
        if (gamepad == null)
            return;

        float scrollInput = gamepad.rightStick.ReadValue().y;
        if (Mathf.Abs(scrollInput) < controllerScrollDeadzone)
            return;

        float nextPosition = activeScrollRect.verticalNormalizedPosition + (scrollInput * controllerScrollSpeed * Time.unscaledDeltaTime);
        activeScrollRect.verticalNormalizedPosition = Mathf.Clamp01(nextPosition);
    }

    private void ResetDetailScrollIfNeeded()
    {
        if (!resetDetailScrollPending)
            return;

        ScrollRect activeScrollRect = GetDetailScrollRect();
        if (activeScrollRect == null || !activeScrollRect.gameObject.activeInHierarchy)
            return;

        Canvas.ForceUpdateCanvases();
        activeScrollRect.StopMovement();
        activeScrollRect.verticalNormalizedPosition = 1f;
        resetDetailScrollPending = false;
    }

    private ScrollRect GetDetailScrollRect()
    {
        if (detailScrollRect != null)
            return detailScrollRect;

        GameObject detailMenuObject = GameObject.FindGameObjectWithTag(IndividualLogMenuTag);
        if (detailMenuObject == null)
            return null;

        detailScrollRect = detailMenuObject.GetComponentInChildren<ScrollRect>(true);
        return detailScrollRect;
    }

}
