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

        // If this menu is already at the top, do nothing to prevent flashing
        if (menusToManage.Count > 0 && menusToManage[0] == menuToAdd)
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

            if(menuToAdd != firstMenuToOpen && menuToAdd != canvas)
                fadeMenus.FadeMenuSafe(menuToAdd, fadeMenus.fadeDuration, true);

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
        if (menuToAdd == null)
            return;

        Selectable target = GetValidSelectionFromHistory(menuToAdd);
        if (target == null)
            target = GetFirstValidSelectable(menuToAdd);

        if (target != null)
            SetSelected(target);
    }

    public void GoBackToPreviousMenu()
    {
        if (menusToManage.Count <= 2)
            return;

        GameObject currentTop = menusToManage[0];

        FadeMenus fadeMenus = this.GetComponent<FadeMenus>();
        if (currentTop != null)
            fadeMenus.FadeMenuSafe(currentTop, fadeMenus.fadeDuration, false);

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

    private Selectable GetValidSelectionFromHistory(GameObject targetMenu)
    {
        if (selectionHistory.Count == 0)
            return null;

        Selectable remembered = selectionHistory[0];
        selectionHistory.RemoveAt(0);

        if (remembered == null || !remembered.IsInteractable() || !remembered.gameObject.activeInHierarchy)
            return null;

        return remembered.transform.IsChildOf(targetMenu.transform) ? remembered : null;
    }

    private static Selectable GetFirstValidSelectable(GameObject root)
    {
        if (root == null)
            return null;

        Selectable rootSelectable = root.GetComponent<Selectable>();
        if (rootSelectable != null && rootSelectable.IsInteractable() && rootSelectable.gameObject.activeInHierarchy)
            return rootSelectable;

        Selectable[] childSelectables = root.GetComponentsInChildren<Selectable>(true);
        foreach (Selectable selectable in childSelectables)
        {
            if (selectable != null && selectable.IsInteractable() && selectable.gameObject.activeInHierarchy)
                return selectable;
        }

        return null;
    }

    private static void SetSelected(Selectable selectable)
    {
        if (selectable == null)
            return;

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(selectable.gameObject);
        }

        selectable.Select();
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