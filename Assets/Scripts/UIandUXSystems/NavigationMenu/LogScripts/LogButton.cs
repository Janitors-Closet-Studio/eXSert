/*
    Handles the button logic for the dynamic log system.

    Written by Brandon Wahl
*/

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.UI;

public class LogButton : MonoBehaviour, ISelectHandler
{
    private TMP_Text buttonText;
    private UnityAction onSelectAction;
    private UnityAction onClickAction;
    public Button button { get; private set; }
    private MenuEventSystemHandler logUI;
    [SerializeField] private Image unreadIndicator;

    private void Awake()
    {

        this.button = this.GetComponent<Button>();
        
        GameObject logUIObject = GameObject.FindGameObjectWithTag("LogUI");
        if (logUIObject != null)
        {
            logUI = logUIObject.GetComponent<MenuEventSystemHandler>();

            if (logUI != null)
                logUI.Selectables.Add(this.button);
        }
    }


    //Components get assigned moment of initlization
    public void InitializeButton(string logName, UnityAction selectAction, bool isRead)
    {
        // Ensure button is assigned (in case InitializeButton is called before Awake)
        if (this.button == null)
            this.button = this.GetComponent<Button>();

        this.buttonText = this.GetComponentInChildren<TMP_Text>();

        if (this.buttonText != null)
            this.buttonText.text = logName;

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
                        es.SetSelectedGameObject(this.gameObject);

                    selectAction();
                };

                this.button.onClick.AddListener(onClickAction);
            }
        }
    }

    public void SetUnreadState(bool isRead)
    {
        if (unreadIndicator != null)
            unreadIndicator.gameObject.SetActive(!isRead);
    }

    public void OnSelect(BaseEventData eventData)
    {
        onSelectAction?.Invoke();
    }

    public void FindAddMenusToList()
    {
        GameObject canvas = GameObject.FindGameObjectWithTag("Canvas");

        GameObject individualLogMenuObject = GameObject.FindGameObjectWithTag("IndividualLogMenu");

        if(canvas != null)
        {
            var menuToManage = canvas.GetComponent<MenuListManager>();
            if(individualLogMenuObject != null)
            {
                Transform child = individualLogMenuObject.transform.GetChild(0);
                SetUnreadState(true);
                menuToManage.AddToMenuList(child.gameObject);
            }   
        }
    }

    //Hides Menus
    public void AddOverlay()
    {

        GameObject overlayParent = GameObject.FindGameObjectWithTag("IndividualLogMenu");
        if (overlayParent != null)
        {
            Transform child = overlayParent.transform.GetChild(0);
            child.gameObject.SetActive(true);
        } 
    }
}
