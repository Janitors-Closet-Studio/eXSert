using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;


public class MenuListManager : MonoBehaviour
{
    [SerializeField] internal List<GameObject> menusToManage;

    [SerializeField] private GameObject firstMenuToOpen;
    [SerializeField] private GameObject canvas;

    // Tracks the last selected element before opening each menu (acts as a stack)
    private readonly List<Selectable> selectionHistory = new List<Selectable>();

    

    private void Start()
    {
        AddToMenuList(canvas); // Add this menu to the list on start
        if (firstMenuToOpen != null)
        {
            AddToMenuList(firstMenuToOpen);
        }
    }

    public void SetAsLastSibling(GameObject menuToMove)
    {
        if (menuToMove != firstMenuToOpen && menuToMove != canvas)
            menuToMove.transform.SetAsLastSibling();
    }

    public void AddToMenuList(GameObject menuToAdd)
    {
        if (menuToAdd == null)
        {
            return;
        }

        // Remember what was selected before opening this menu
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
        {
            Selectable previousSelection = EventSystem.current.currentSelectedGameObject.GetComponent<Selectable>();
            if (previousSelection != null)
            {
                selectionHistory.Insert(0, previousSelection);
            }
        }

        if (menusToManage.Count > 0)
        {
            GameObject currentTop = menusToManage[0];
            if (currentTop != null)
            {
                bool sameParent = menuToAdd.transform.parent == currentTop.transform.parent;
                bool keepCurrentTop = currentTop == firstMenuToOpen || currentTop == canvas;
                if (sameParent && !keepCurrentTop)
                {
                    GoBackToPreviousMenu();
                }
            }
        }

        if (menusToManage.Contains(menuToAdd))
            menusToManage.Remove(menuToAdd);
        
        FadeMenus fadeMenus = this.GetComponent<FadeMenus>();

        if (!menusToManage.Contains(menuToAdd))
        {
            menusToManage.Insert(0, menuToAdd);
            fadeMenus.StartCoroutine(fadeMenus.FadeMenu(menuToAdd, fadeMenus.fadeDuration, true));

            if(menuToAdd.tag != "LogUI" && menuToAdd.tag != "DiaryUI")
                SetAsLastSibling(menuToAdd);
            
            // Select the first selectable in the new menu
            Selectable firstSelectable = menuToAdd.GetComponent<Selectable>();
            if (firstSelectable == null)
            {
                firstSelectable = menuToAdd.GetComponentInChildren<Selectable>();
            }
            
            if (firstSelectable != null)
            {
                firstSelectable.Select();
            }
        }

        Debug.Log("Menu added to list. Current menus in list: " + menusToManage.Count);
    }

    public void SelectFirstSelectOnBack(GameObject menuToAdd)
    {
        var firstSelectable = menuToAdd.GetComponent<Selectable>();
        if (firstSelectable != null)
        {
            firstSelectable.Select();
        }
        else
        {
            var childSelectable = menuToAdd.GetComponentInChildren<Selectable>();
            if (childSelectable != null)
            {
                childSelectable.Select();
            }
        }
    }

    public void GoBackToPreviousMenu()
    {
        if (menusToManage.Count <= 2)
            return;

        GameObject currentTop = menusToManage[0];

        FadeMenus fadeMenus = this.GetComponent<FadeMenus>();
        if (currentTop != null)
            fadeMenus.StartCoroutine(fadeMenus.FadeMenu(currentTop, fadeMenus.fadeDuration, false));

        menusToManage.RemoveAt(0);
        

        if (menusToManage.Count > 0)
        {
            GameObject newTop = menusToManage[0];
            if (newTop != null)
            {
                SelectFirstSelectOnBack(newTop);
            }
        }
    }

    public void SwapBetweenMenus()
    {
        if(menusToManage.Count >= 5){
            GoBackToPreviousMenu();
        }
    }

    public void ClearMenuList()
    {
        foreach(GameObject menu in menusToManage)
        {
            menu.SetActive(false);
        }
        menusToManage.Clear();
        selectionHistory.Clear();
    }

}