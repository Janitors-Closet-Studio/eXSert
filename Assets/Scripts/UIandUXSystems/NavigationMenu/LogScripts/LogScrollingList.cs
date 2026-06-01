/*
    Handles the logic for the scrolling list which contains the log buttons
    Ensures that no button can have a duplicate as well.

    Written by Brandon Wahl
*/

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class LogScrollingList : MonoBehaviour
{


    [Header("Components")]
    [SerializeField] private GameObject contentParent;

    [Header("Log Entry Button")]
    [SerializeField] private GameObject logEntryButtonPrefab;

    [Header("Rect Transforms")]
    [SerializeField] private RectTransform scrollRectTransform;
    [SerializeField] internal RectTransform contentRectTransform;
    [SerializeField] private float scrollPadding = 8f;

    private ScrollRect scrollRect;
    private RectTransform viewportRectTransform;
    private Dictionary<string, LogButton> idToButtonMap = new Dictionary<string, LogButton>(); //Dict to hold id of buttons

    private void Awake()
    {
        CacheScrollReferences();
    }

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
                ConfigureLogButton(logButton, log, selectAction, isRead);
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
        ConfigureLogButton(logButton, log, selectAction, isRead);

        idToButtonMap[log.info.logID] = logButton;

        return logButton;
    }

    private void ConfigureLogButton(LogButton logButton, Logs log, UnityAction selectAction, bool isRead)
    {
        RectTransform buttonRectTranform = logButton.GetComponent<RectTransform>();

        logButton.InitializeButton(log.info.logName, () =>
        {
            selectAction();
            UpdateScrolling(buttonRectTranform);
        }, isRead);
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
        CacheScrollReferences();
        if (buttonRectTransform == null || contentRectTransform == null || viewportRectTransform == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRectTransform);

        float hiddenLength = contentRectTransform.rect.height - viewportRectTransform.rect.height;
        if (hiddenLength <= 0f)
        {
            if (scrollRect != null)
            {
                scrollRect.verticalNormalizedPosition = 1f;
            }
            else
            {
                contentRectTransform.anchoredPosition = new Vector2(contentRectTransform.anchoredPosition.x, 0f);
            }

            return;
        }

        Bounds buttonBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewportRectTransform, buttonRectTransform);
        Rect viewportRect = viewportRectTransform.rect;
        float targetY = contentRectTransform.anchoredPosition.y;

        if (buttonBounds.max.y > viewportRect.yMax - scrollPadding)
        {
            targetY -= buttonBounds.max.y - (viewportRect.yMax - scrollPadding);
        }
        else if (buttonBounds.min.y < viewportRect.yMin + scrollPadding)
        {
            targetY += (viewportRect.yMin + scrollPadding) - buttonBounds.min.y;
        }
        else
        {
            return;
        }

        targetY = Mathf.Clamp(targetY, 0f, hiddenLength);

        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 1f - Mathf.Clamp01(targetY / hiddenLength);
        }
        else
        {
            contentRectTransform.anchoredPosition = new Vector2(contentRectTransform.anchoredPosition.x, targetY);
        }
    }

    private void CacheScrollReferences()
    {
        if (scrollRectTransform == null)
        {
            scrollRectTransform = transform as RectTransform;
        }

        if (scrollRect == null)
        {
            scrollRect = GetComponent<ScrollRect>();
        }

        if (contentRectTransform == null && scrollRect != null)
        {
            contentRectTransform = scrollRect.content;
        }

        if (scrollRect != null)
        {
            viewportRectTransform = scrollRect.viewport != null ? scrollRect.viewport : scrollRectTransform;
        }
    }
}
