/*
    Handles the logic for the scrolling list which contains the log buttons
    Ensures that no button can have a duplicate as well.

    Written by Brandon Wahl
*/

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class LogScrollingList : MonoBehaviour
{


    [Header("Components")]
    [SerializeField] private GameObject contentParent;

    [Header("Log Entry Button")]
    [SerializeField] private GameObject logEntryButtonPrefab;

    [Header("Rect Transforms")]
    [SerializeField] private RectTransform scrollRectTransform;
    [SerializeField] internal RectTransform contentRectTransform;
    private Dictionary<string, LogButton> idToButtonMap = new Dictionary<string, LogButton>(); //Dict to hold id of buttons
    //If the button for a log doesn't already exist, this function will make it
    public LogButton CreateButtonIfNotExists(Logs log, UnityAction selectAction, bool isRead)
    {
        LogButton logButton = null;

        if (log.info.isFound)
        {
            Debug.Log($"Log {log.info.logID} is marked as found, checking if button exists...");
            if (!idToButtonMap.ContainsKey(log.info.logID))
            {
                Debug.Log($"Creating button for log {log.info.logID}");
                logButton = InstantiateLogButton(log, selectAction, isRead);
            }
            else
            {
                Debug.Log($"Button for log {log.info.logID} already exists");
                logButton = idToButtonMap[log.info.logID];
            }
            return logButton;
        }
        else
        {
            Debug.Log($"Log {log.info.logID} is NOT marked as found (isFound={log.info.isFound}), skipping button creation");
            return logButton;
        }
    }

    //Used by the function above to instantiate the button into the content parent in the scroll list
    private LogButton InstantiateLogButton(Logs log, UnityAction selectAction, bool isRead)
    {
        LogButton logButton = Instantiate(
            logEntryButtonPrefab,
            contentParent.transform).GetComponent<LogButton>();

        logButton.gameObject.name = log.info.logID + "_button"; //assigns name in inspector

        RectTransform buttonRectTranform = logButton.GetComponent<RectTransform>();

        logButton.InitializeButton(log.info.logName, () =>
        {
            selectAction();
            UpdateScrolling(buttonRectTranform);
        }, isRead);

        idToButtonMap[log.info.logID] = logButton;

        return logButton;
    }

    public void ClearLogButtons()
    {
        foreach (var kvp in idToButtonMap)
        {
            if (kvp.Value != null)
                Destroy(kvp.Value.gameObject);
        }
        idToButtonMap.Clear();
    }

    //So whenever you scroll down the menu will dynamically shift the scroll list
    private void UpdateScrolling(RectTransform buttonRectTransform)
    {
        float buttonYMin = Mathf.Abs(buttonRectTransform.anchoredPosition.y);
        float buttonYMax = buttonYMin + buttonRectTransform.rect.height;

        float contentYMin = contentRectTransform.anchoredPosition.y;
        float contentYMax = contentYMin + scrollRectTransform.rect.height;

        //If the player is off screen then it will extend to show "hidden" logs
        if (buttonYMax > contentYMax)
        {
            contentRectTransform.anchoredPosition = new Vector2(
                contentRectTransform.anchoredPosition.x,
                buttonYMax - scrollRectTransform.rect.height
            );
        }
        else
        {
            contentRectTransform.anchoredPosition = new Vector2(
                contentRectTransform.anchoredPosition.x,
                buttonYMin
            );
        }
    }
}
