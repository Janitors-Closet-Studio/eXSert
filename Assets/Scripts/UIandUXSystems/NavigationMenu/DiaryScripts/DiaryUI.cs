/*
    Written by Brandon

    This script will change the text and description of the diary view depending on which button is clicked.
*/

using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.Serialization;
using UnityEngine.InputSystem;
public class DiaryUI : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private GameObject contentParent;
    [SerializeField] private DiaryScrollingList scrollingList;
    [FormerlySerializedAs("diaryID")]
    [SerializeField] private TMP_Text diaryTitle;
    [SerializeField] private TMP_Text diaryIdText;
    [SerializeField] private TMP_Text diaryDescription;
    [SerializeField] private Image diaryImage;

    [Header("Controller Scrolling")]
    [SerializeField] private float controllerScrollSpeed = 2f;
    [SerializeField] private float controllerScrollDeadzone = 0.35f;

    private const string IndividualDiaryMenuTag = "IndividualDiaryMenu";

    private ScrollRect detailScrollRect;
    private bool resetDetailScrollPending;

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

    private void Update()
    {
        ResetDetailScrollIfNeeded();
        HandleControllerScroll();
    }

    //Creates the button with the info from SetDiaryInfo
    private void DiaryStateChange(Diaries diaries)
    {
        scrollingList.CreateButtonIfNotExists(diaries, () =>
        {
            SetDiaryInfo(diaries);
           
        }, diaries.info.isRead);
    }

    //Sets each diary info
    internal void SetDiaryInfo(Diaries diaries)
    {
        diaryTitle.text = diaries.info.diaryTitle;
        if (diaryIdText != null)
            diaryIdText.text = diaries.info.diaryID;
        diaryDescription.text = diaries.info.diaryDescription;
        bool wasUnread = !diaries.info.isRead;
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

        resetDetailScrollPending = true;
        ResetDetailScrollIfNeeded();

        if (wasUnread && EventsManager.Instance != null && EventsManager.Instance.diaryEvents != null)
        {
            EventsManager.Instance.diaryEvents.DiaryStateChange(diaries);
        }
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

        GameObject detailMenuObject = GameObject.FindGameObjectWithTag(IndividualDiaryMenuTag);
        if (detailMenuObject == null)
            return null;

        detailScrollRect = detailMenuObject.GetComponentInChildren<ScrollRect>(true);
        return detailScrollRect;
    }
}
