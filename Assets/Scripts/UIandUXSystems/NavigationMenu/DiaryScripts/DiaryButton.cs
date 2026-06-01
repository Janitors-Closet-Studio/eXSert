/*
    Written by Brandon

    This script controls the functionality of the Diary Button
*/

using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.Events;
using UnityEngine.UI;

public class DiaryButton : MonoBehaviour, ISelectHandler
{
    private TMP_Text buttonText;
    private UnityAction onSelectAction;
    private UnityAction onClickAction;
    public Button button { get; private set; }
    private MenuEventSystemHandler diaryUI;
    [SerializeField] private Image unreadIndicator;


    private void Awake()
    {

        this.button = this.GetComponent<Button>();
        
        GameObject diaryUIObject = GameObject.FindGameObjectWithTag("DiaryUI");
        if (diaryUIObject != null)
        {
            diaryUI = diaryUIObject.GetComponent<MenuEventSystemHandler>();
            if (diaryUI != null)
            {
                diaryUI.Selectables.Add(this.button);
            }
        }
    }

    //Components get assigned moment of initlization
    public void InitializeButton(string logName, UnityAction selectAction, bool isRead)
    {
        // Ensure button is assigned (in case InitializeButton is called before Awake)
        if (this.button == null)
        {
            this.button = this.GetComponent<Button>();
        }
        
        this.buttonText = this.GetComponentInChildren<TMP_Text>();

        if (this.buttonText != null)
        {
            this.buttonText.text = logName;
        }

        SetUnreadState(isRead);
        
        this.onSelectAction = selectAction;
        
        if (this.button != null)
        {
            if (onClickAction != null)
            {
                this.button.onClick.RemoveListener(onClickAction);
                onClickAction = null;
            }

            // Add onClick listener so action triggers on click, not just select.
            if (selectAction != null)
            {
                onClickAction = () =>
                {
                    var es = EventSystem.current;
                    if (es != null)
                    {
                        es.SetSelectedGameObject(this.gameObject);
                    }

                    selectAction();
                };

                this.button.onClick.AddListener(onClickAction);
            }
        }
    }

    public void SetUnreadState(bool isRead)
    {
        if (unreadIndicator != null)
        {
            unreadIndicator.gameObject.SetActive(!isRead);
        }
    }

    public void FindAddMenusToList()
    {
        GameObject canvas = GameObject.FindGameObjectWithTag("Canvas");

        GameObject individualDiaryMenuObject = GameObject.FindGameObjectWithTag("IndividualDiaryMenu");

        if(canvas != null)
        {
            var menuToManage = canvas.GetComponent<MenuListManager>();
            if(individualDiaryMenuObject != null)
            {
                Transform child = individualDiaryMenuObject.transform.GetChild(0);
                menuToManage.AddToMenuList(child.gameObject);
            }
        }
    }
        

    public void OnSelect(BaseEventData eventData)
    {
        onSelectAction?.Invoke();
    }

    //Hides Menus
    public void AddOverlay()
    {

        GameObject overlayParent = GameObject.FindGameObjectWithTag("IndividualDiaryMenu");
        if (overlayParent != null)
        {
            Transform child = overlayParent.transform.GetChild(0);
            child.gameObject.SetActive(true);
        }
    }
}