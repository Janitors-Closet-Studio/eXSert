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
    private Dictionary<string, DiaryButton> idToButtonMap = new Dictionary<string, DiaryButton>(); //Dict to hold id of buttons

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

        RectTransform buttonRectTranform = diaryButton.GetComponent<RectTransform>();

        diaryButton.InitializeButton(diaries.info.diaryTitle, () =>
        {
            selectAction();
            UpdateScrolling(buttonRectTranform);
        }, isRead);

        idToButtonMap[diaries.info.diaryID] = diaryButton;

        return diaryButton;
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
