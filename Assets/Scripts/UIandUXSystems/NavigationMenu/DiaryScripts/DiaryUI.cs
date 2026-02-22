/*
    Written by Brandon

    This script will change the text and description of the diary view depending on which button is clicked.
*/

using UnityEngine;
using TMPro;
using UnityEngine.UI;
public class DiaryUI : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private GameObject contentParent;
    [SerializeField] private DiaryScrollingList scrollingList;
    [SerializeField] private TMP_Text diaryID;
    [SerializeField] private GameObject diaryDescription;
    [SerializeField] private Image diaryImage;

    //DiaryStateChange being subscribed and unsubscribed
    private void OnEnable()
    {
        if(scrollingList != null)
            scrollingList.ClearDiaryButtons(); // Clear existing buttons to prevent duplicates

        EventsManager.Instance.diaryEvents.onDiaryStateChange -= DiaryStateChange; // Unsubscribe first to prevent multiple subscriptions
        EventsManager.Instance.diaryEvents.onDiaryStateChange += DiaryStateChange;
        // Refresh all diaries to populate buttons when UI becomes active
        if (DiaryManager.Instance != null)
        {
            DiaryManager.Instance.RefreshAllDiaries();
        }
    }

    private void OnDisable()
    {
        EventsManager.Instance.diaryEvents.onDiaryStateChange -= DiaryStateChange;
    }

    //Creates the button with the info from SetDiaryInfo
    private void DiaryStateChange(Diaries diaries)
    {
        DiaryButton diaryButton = scrollingList.CreateButtonIfNotExists(diaries, () =>
        {
            SetDiaryInfo(diaries);
           
        }, diaries.info.isRead);
    }

    //Sets each diary info
    private void SetDiaryInfo(Diaries diaries)
    {
        diaryID.text = diaries.info.diaryTitle;
        diaryDescription.GetComponent<TMP_Text>().text = diaries.info.diaryDescription;
        diaries.info.isRead = true; // Mark diary as read when selected
        
        if(DiaryManager.Instance.unreadDiaries.Contains(diaries.info))
        {
            DiaryManager.Instance.unreadDiaries.Remove(diaries.info);
        }
        
        if (diaries.info.diaryImage != null && diaries.info.diaryImage.sprite != null)
        {
            diaryImage.sprite = diaries.info.diaryImage.sprite;
        }
        else
            diaryImage.sprite = null;
    }
}
