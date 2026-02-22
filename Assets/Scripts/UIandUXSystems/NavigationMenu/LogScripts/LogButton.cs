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

        
        if(!isRead && unreadIndicator != null)
            unreadIndicator.gameObject.SetActive(true);
        else
            unreadIndicator.gameObject.SetActive(false);
    

        this.onSelectAction = selectAction;
        
        // Add onClick listener so action triggers on click, not just select
        if (this.button != null && selectAction != null)
        {
            this.button.onClick.AddListener(() =>
            {
                // Ensure EventSystem selection updates for mouse clicks
                var es = UnityEngine.EventSystems.EventSystem.current;
                if (es != null)
                    es.SetSelectedGameObject(this.gameObject);

                selectAction();
            });
        }
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (onSelectAction != null)
        {
            onSelectAction();
        }
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
                unreadIndicator.gameObject.SetActive(false);
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
