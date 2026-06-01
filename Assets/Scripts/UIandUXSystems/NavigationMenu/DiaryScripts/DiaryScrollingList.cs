/*
    Written by Brandon

    This script is assigned to a scroll view that includes the diaries that the player collects. This script will handle instantiating
    the diary button if the button associated with that id has not be created yet. It also helps format the scroll view so it doesn't become
    broken on scroll. 
*/

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
public class DiaryScrollingList : MonoBehaviour
{
    public GameObject selectedButton;

    [Header("Components")]
    [SerializeField] private GameObject contentParent;

    [Header("Diary Entry Button")]
    [SerializeField] private GameObject diaryEntryButtonPrefab;

    [Header("Rect Transforms")]
    [SerializeField] private RectTransform scrollRectTransform;
    [SerializeField] internal RectTransform contentRectTransform;
    [SerializeField] private float scrollPadding = 8f;

    private ScrollRect scrollRect;
    private RectTransform viewportRectTransform;
    private Dictionary<string, DiaryButton> idToButtonMap = new Dictionary<string, DiaryButton>(); //Dict to hold id of buttons

    private void Awake()
    {
        CacheScrollReferences();
    }

    void Update()
    {
        if (EventSystem.current.currentSelectedGameObject != null)
        {
            selectedButton = EventSystem.current.currentSelectedGameObject;
            Debug.Log("Selected Button: " + selectedButton.name);
        }
    }

    //If the button for a diary doesn't already exist, this function will make it
    public DiaryButton CreateButtonIfNotExists(Diaries diary, UnityAction selectAction, bool isRead)
    {
        DiaryButton diaryButton = null;

        if (diary.info.isFound)
        {
            Debug.Log($"Diary {diary.info.diaryID} is marked as found, checking if button exists...");
            if (!idToButtonMap.ContainsKey(diary.info.diaryID))
            {
                Debug.Log($"Creating button for diary {diary.info.diaryID}");
                diaryButton = InstantiateDiaryButton(diary, selectAction, isRead);
            }
            else
            {
                Debug.Log($"Button for diary {diary.info.diaryID} already exists");
                diaryButton = idToButtonMap[diary.info.diaryID];
                ConfigureDiaryButton(diaryButton, diary, selectAction, isRead);
            }
            return diaryButton;
        }
        else
        {
            Debug.Log($"Diary {diary.info.diaryID} is NOT marked as found (isFound={diary.info.isFound}), skipping button creation");
            return diaryButton;
        }
    }

    //Used by the function above to instantiate the button into the content parent in the scroll list
    private DiaryButton InstantiateDiaryButton(Diaries diaries, UnityAction selectAction, bool isRead)
    {
        DiaryButton diaryButton = Instantiate(
            diaryEntryButtonPrefab,
            contentParent.transform).GetComponent<DiaryButton>();

        diaryButton.gameObject.name = diaries.info.diaryID + "_button"; //assigns name in inspector
        ConfigureDiaryButton(diaryButton, diaries, selectAction, isRead);

        idToButtonMap[diaries.info.diaryID] = diaryButton;

        return diaryButton;
    }

    private void ConfigureDiaryButton(DiaryButton diaryButton, Diaries diaries, UnityAction selectAction, bool isRead)
    {
        RectTransform buttonRectTranform = diaryButton.GetComponent<RectTransform>();

        diaryButton.InitializeButton(diaries.info.diaryTitle, () =>
        {
            selectAction();
            UpdateScrolling(buttonRectTranform);
        }, isRead);
    }

    public void ClearDiaryButtons()
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
