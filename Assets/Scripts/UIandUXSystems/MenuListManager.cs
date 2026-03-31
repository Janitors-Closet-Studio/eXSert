using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;


public class MenuListManager : MonoBehaviour
{
    [SerializeField] internal List<GameObject> menusToManage;

    [SerializeField] internal List<GameObject> menusToBlock;
    [SerializeField] internal List<GameObject> settingPageMenus;

    [SerializeField] private GameObject firstMenuToOpen;
    [SerializeField] private GameObject canvas;

    // Tracks the last selected element before opening each menu (acts as a stack)
    private readonly List<Selectable> selectionHistory = new List<Selectable>();
    private readonly WaitForSecondsRealtime controlsPollInterval = new WaitForSecondsRealtime(0.1f);

    

    private void Start()
    {
        AddToMenuList(canvas); // Add this menu to the list on start
        if (firstMenuToOpen != null)
        {
            AddToMenuList(firstMenuToOpen);
        }

        StartCoroutine(ListenForChangesInControls());
    }

    private IEnumerator ListenForChangesInControls()
    {
        string currentControls = null;

        while (true)
        {
            var playerInput = InputReader.PlayerInput;
            if (playerInput == null)
            {
                yield return null;
                continue;
            }

            string latestControls = playerInput.currentControlScheme;
            if (currentControls == null)
            {
                currentControls = latestControls;
            }
            else if (latestControls != currentControls)
            {
                currentControls = latestControls;

                if (menusToManage != null && menusToManage.Count > 0)
                    EnsureSelectionForMenu(menusToManage[0]);
            }

            yield return controlsPollInterval;
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

        EnsureHierarchyIsActive(menuToAdd);

        RemoveOtherOpenSettingPageMenu(menuToAdd);

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
                    RemoveCurrentTopForSiblingSwitch();
                }
            }
        }

        if (menusToManage.Contains(menuToAdd))
            menusToManage.Remove(menuToAdd);
        
        FadeMenus fadeMenus = this.GetComponent<FadeMenus>();

        if (!menusToManage.Contains(menuToAdd))
        {
            menusToManage.Insert(0, menuToAdd);

            if(menuToAdd != firstMenuToOpen && menuToAdd != canvas && !menusToBlock.Contains(menuToAdd))
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
                SetSelected(firstSelectable);
            }
        }

        Debug.Log("Menu added to list. Current menus in list: " + menusToManage.Count);
    }

    // Ensures the entire parent chain of the menu is active so that it can be properly displayed and interacted with.
    private void EnsureHierarchyIsActive(GameObject menuToAdd)
    {
        if (menuToAdd == null)
            return;

        List<Transform> chain = new List<Transform>();
        Transform current = menuToAdd.transform;

        while (current != null)
        {
            chain.Add(current);

            if (canvas != null && current.gameObject == canvas)
                break;

            current = current.parent;
        }

        for (int i = chain.Count - 1; i >= 0; i--)
        {
            GameObject node = chain[i].gameObject;
            if (!node.activeSelf)
                node.SetActive(true);
        }
    }

    // When opening a settings page, we want to ensure any other open settings pages are closed so that we don't have multiple open on top of each other. This method checks if the menu being added is a settings page, and if so, fades out any other open settings pages and removes them from the menu list.
    private void RemoveOtherOpenSettingPageMenu(GameObject menuToAdd)
    {
        if (menuToAdd == null || settingPageMenus == null || !settingPageMenus.Contains(menuToAdd))
            return;

        if (menusToManage == null || menusToManage.Count == 0)
            return;

        for (int i = menusToManage.Count - 1; i >= 0; i--)
        {
            GameObject openMenu = menusToManage[i];
            if (openMenu == null || openMenu == menuToAdd || !settingPageMenus.Contains(openMenu))
                continue;

            FadeMenus fadeMenus = GetComponent<FadeMenus>();
            if (fadeMenus != null && !menusToBlock.Contains(openMenu))
                fadeMenus.FadeMenuSafe(openMenu, fadeMenus.fadeDuration, false);

            menusToManage.RemoveAt(i);
        }
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
        if (currentTop != null && !menusToBlock.Contains(currentTop))
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

    // If the new menu shares a parent with the current top menu, we fade out the current top menu and remove it from the list so that the new menu can take its place without both being active at the same time. This prevents issues with multiple open menus sharing the same parent and causing input problems or visual clutter.
    private void RemoveCurrentTopForSiblingSwitch()
    {
        if (menusToManage.Count == 0)
            return;

        GameObject currentTop = menusToManage[0];
        FadeMenus fadeMenus = this.GetComponent<FadeMenus>();

        if (currentTop != null && !menusToBlock.Contains(currentTop))
            fadeMenus.FadeMenuSafe(currentTop, fadeMenus.fadeDuration, false);

        menusToManage.RemoveAt(0);
    }

    private void EnsureSelectionForMenu(GameObject menu)
    {
        Selectable currentSelection = EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null
            ? EventSystem.current.currentSelectedGameObject.GetComponent<Selectable>()
            : null;

        if (currentSelection != null
            && currentSelection.IsInteractable()
            && currentSelection.gameObject.activeInHierarchy
            && currentSelection.transform.IsChildOf(menu.transform))
        {
            return;
        }

        Selectable fallback = GetFirstValidSelectable(menu);
        if (fallback != null)
            SetSelected(fallback);
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
        if (ShouldIgnoreMenuSwap())
            return;

        if (menusToManage.Count >= 5)
            GoBackToPreviousMenu();
    }

    // Overload for UnityEvent<float> sources like Slider.onValueChanged.
    public void SwapBetweenMenus(float _)
    {
        if (ShouldIgnoreMenuSwap())
            return;

        if (menusToManage.Count >= 5)
            GoBackToPreviousMenu();
    }

    private static bool ShouldIgnoreMenuSwap()
    {
        if (EventSystem.current == null)
            return false;

        GameObject selected = EventSystem.current.currentSelectedGameObject;
        if (selected == null)
            return false;

        // Prevent sliders from unintentionally triggering menu stack pop on value changes.
        return selected.GetComponentInParent<Slider>() != null;
    }

    public void ClearMenuList()
    {
        foreach(GameObject menu in menusToManage)
        {
            if (!IsProtectedMenu(menu))
                menu.SetActive(false);
        }
        menusToManage.Clear();
        selectionHistory.Clear();
    }

    private bool IsProtectedMenu(GameObject menu)
    {
        return menu != null && (menu == canvas || menu == firstMenuToOpen);
    }

}