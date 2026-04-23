        
    
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
    public List<Selectable> selectionHistory = new List<Selectable>();
    private readonly WaitForSecondsRealtime controlsPollInterval = new WaitForSecondsRealtime(0.1f);

    // Guard flag to prevent double back
    private bool backGuardActive = false;
    private float backGuardCooldown = 0.15f; // seconds
    private FadeMenus _fadeMenus;

    private FadeMenus FadeMenusComponent => _fadeMenus != null ? _fadeMenus : (_fadeMenus = GetComponent<FadeMenus>());
    

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

    // Sets the selected menu so they are last in sibling order, so they can appear on top
    public void SetAsLastSibling(GameObject menuToMove)
    {
        if (menuToMove != firstMenuToOpen && menuToMove != canvas)
            menuToMove.transform.SetAsLastSibling();
    }

    // Added for new transparent background. this will disable the previous menu but keep
    // it for going back.
    public void DisablePreviousWithoutRemovingFromList(GameObject menuToDisable)
    {
        if (menuToDisable == null)
            return;

        menuToDisable.SetActive(false);

    }

    // Central function to add a menu to the stack and handle all related logic (selection, sibling order, fading, etc.)
    public void AddToMenuList(GameObject menuToAdd)
    {
        if (menuToAdd == null)
            return;

        PushCurrentSelectionToHistory();

        EnsureHierarchyIsActive(menuToAdd);
        RemoveOtherOpenSettingPageMenu(menuToAdd);
        ClearOpenSubmenusOnSettingsSwitch(menuToAdd);
        ReplaceTopMenuIfSwitchingSiblingSubmenu(menuToAdd);

        if (menusToManage.Contains(menuToAdd))
            menusToManage.Remove(menuToAdd);

        if (!menusToManage.Contains(menuToAdd))
        {
            menusToManage.Insert(0, menuToAdd);
            if(menuToAdd != firstMenuToOpen && menuToAdd != canvas && !IsBlockedFromFade(menuToAdd))
                ShowMenu(menuToAdd);
            if(menuToAdd.tag != "LogUI" && menuToAdd.tag != "DiaryUI")
                SetAsLastSibling(menuToAdd);
        }

        // Always run selection logic, even if already at top
        GameObject selectedObj = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        bool sliderSelected = selectedObj != null && selectedObj.GetComponentInParent<Slider>() != null;
        Selectable firstSelectable = menuToAdd.GetComponent<Selectable>();
        if (firstSelectable == null)
            firstSelectable = menuToAdd.GetComponentInChildren<Selectable>();
        if (firstSelectable != null && !sliderSelected)
            SetSelected(firstSelectable);

        DebugLogSettingsM.ConditionalLog(DebugLogCategory.UI, "Menu added to list. Current menus in list: " + menusToManage.Count);
    }

    // When switching between peer submenus in the same settings section,
    // replace the current top menu so panels do not stack visually.
    private void ReplaceTopMenuIfSwitchingSiblingSubmenu(GameObject menuToAdd)
    {
        if (menusToManage == null || menusToManage.Count == 0 || menuToAdd == null)
            return;

        GameObject currentTop = menusToManage[0];
        if (currentTop == null || currentTop == menuToAdd)
            return;

        bool sharesSettingsRoot = ShareSettingsRoot(currentTop, menuToAdd);
        if (!sharesSettingsRoot)
            return;

        bool isParentChildTransition =
            currentTop.transform.IsChildOf(menuToAdd.transform) ||
            menuToAdd.transform.IsChildOf(currentTop.transform);
        if (isParentChildTransition)
            return;

        CloseAndRemoveMenuAt(0);
    }

    // Returns true if both menus share the same root settings page in the hierarchy, 
    // indicating they are peer submenus within the same section.
    private bool ShareSettingsRoot(GameObject a, GameObject b)
    {
        if (a == null || b == null || settingPageMenus == null)
            return false;

        foreach (GameObject root in settingPageMenus)
        {
            if (root == null)
                continue;

            bool aUnderRoot = a == root || a.transform.IsChildOf(root.transform);
            if (!aUnderRoot)
                continue;

            bool bUnderRoot = b == root || b.transform.IsChildOf(root.transform);
            if (bUnderRoot)
                return true;
        }

        return false;
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


    // Checks if the menu being added is a settings page, and if so, 
    // fades out any other open settings pages and removes them from the menu list.
    private void RemoveOtherOpenSettingPageMenu(GameObject menuToAdd)
    {
        if (menuToAdd == null || settingPageMenus == null || !settingPageMenus.Contains(menuToAdd))
            return;

        CloseManagedMenusWhere(openMenu =>
            openMenu != null &&
            openMenu != menuToAdd &&
            settingPageMenus.Contains(openMenu));
    }

    // Opening a new top-level settings page should clear any stacked submenu panels so
    // returning later starts from a clean page (no previously open submenu overlays).
    private void ClearOpenSubmenusOnSettingsSwitch(GameObject menuToAdd)
    {
        if (menuToAdd == null || settingPageMenus == null || !settingPageMenus.Contains(menuToAdd))
            return;

        CloseManagedMenusWhere(openMenu =>
            openMenu != null &&
            !settingPageMenus.Contains(openMenu) &&
            IsDescendantOfAnySettingsPage(openMenu));
    }

    private void CloseManagedMenusWhere(System.Predicate<GameObject> shouldClose)
    {
        if (menusToManage == null || menusToManage.Count == 0 || shouldClose == null)
            return;

        for (int i = menusToManage.Count - 1; i >= 0; i--)
        {
            GameObject openMenu = menusToManage[i];
            if (!shouldClose(openMenu))
                continue;

            CloseAndRemoveMenuAt(i);
        }
    }

    // Returns true if the menu is a descendant of any of the top-level settings pages, 
    // indicating it is a submenu that should be closed when switching between settings sections.
    private bool IsDescendantOfAnySettingsPage(GameObject menu)
    {
        if (menu == null || settingPageMenus == null)
            return false;

        foreach (GameObject settingsPage in settingPageMenus)
        {
            if (settingsPage == null)
                continue;

            if (menu.transform.IsChildOf(settingsPage.transform))
                return true;
        }

        return false;
    }

    // Central back function to return to the previous menu, with guards against edge cases and double-back issues.
    public void GoBackToPreviousMenu()
    {
        if (backGuardActive)
            return;
        StartCoroutine(BackGuardCooldown());

        if (menusToManage.Count <= 2)
            return;

        GameObject currentTop = menusToManage[0];
        GameObject previousMenu = menusToManage[1];

        // Keep the revealed menu fully visible during back transitions to prevent self-fades.
        EnsureHierarchyIsActive(previousMenu);
        previousMenu.SetActive(true);
        CanvasGroup previousCanvasGroup = previousMenu.GetComponent<CanvasGroup>();
        if (previousCanvasGroup != null)
            previousCanvasGroup.alpha = 1f;

        // Remove outgoing menu from stack first, then resolve a valid selection for the revealed menu.
        menusToManage.RemoveAt(0);
        EnsureSelectionForMenu(previousMenu);

        // Fade out only the outgoing menu.
        CloseMenu(currentTop);

    }

    // Cooldown so back cant be spammed
    private IEnumerator BackGuardCooldown()
    {
        backGuardActive = true;
        yield return new WaitForSecondsRealtime(backGuardCooldown);
        backGuardActive = false;
    }

    // Finds a valid selectable in the menu and sets it as selected
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

    // Finds the first selectable component in the menu hierarchy that is active and interactable, or returns null if none found.
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

    // Sets the given selectable as the current selection in the EventSystem, if possible.
    private static void SetSelected(Selectable selectable)
    {
        if (selectable == null)
            return;


        selectable.Select();
    }

    // Swaps to the previous menu in the stack, 
    // or if coming from a slider interaction, just goes back one menu without affecting selection
    public void SwapBetweenMenus(int numberOfMenusToGoBack)
    {
        if (ShouldIgnoreMenuSwap())
            return;

        if (numberOfMenusToGoBack < 2)
            return;

        if (menusToManage.Count >= numberOfMenusToGoBack)
            GoBackToPreviousMenu();
    }

    // Same as above but with a float parameter so it can be used by a slider for onValueChanged
    public void SwapBetweenMenus(float _)
    {
        EventSystem currentEventSystem = EventSystem.current;
        if (currentEventSystem == null)
            return;

        GameObject selected = currentEventSystem.currentSelectedGameObject;
        bool editingSlider = selected != null && selected.GetComponentInParent<Slider>() != null;

        // Only close a true nested submenu; never fall back to closing the whole page/screen.
        int menuIndexToClose = FindFirstOpenSubmenuIndex();

        if (menuIndexToClose >= 0)
        {
            CloseAndRemoveMenuAt(menuIndexToClose);

            // If no slider is currently selected, restore focus to revealed menu.
            if (!editingSlider && menusToManage.Count > 0)
                EnsureSelectionForMenu(menusToManage[0]);
        }
    }

    // Finds the first open submenu in the stack, if none is found
    // returns -1 
    private int FindFirstOpenSubmenuIndex()
    {
        if (menusToManage == null || menusToManage.Count == 0)
            return -1;

        int bestIndex = -1;
        int bestNestingScore = -1;

        for (int i = 0; i < menusToManage.Count; i++)
        {
            GameObject menu = menusToManage[i];
            if (menu == null)
                continue;

            bool isTopLevelSettingsPage = settingPageMenus != null && settingPageMenus.Contains(menu);
            if (isTopLevelSettingsPage)
                continue;

            if (!IsDescendantOfAnySettingsPage(menu))
                continue;

            int nestingScore = GetManagedAncestorCount(menu);
            if (nestingScore <= 0)
                continue;

            if (nestingScore > bestNestingScore)
            {
                bestNestingScore = nestingScore;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    // Counts how many ancestors of the given menu are also in the managed menu list,
    //  to determine how deeply nested it is within the stack.
    private int GetManagedAncestorCount(GameObject menu)
    {
        if (menu == null || menusToManage == null)
            return 0;

        int count = 0;
        for (int i = 0; i < menusToManage.Count; i++)
        {
            GameObject candidateAncestor = menusToManage[i];
            if (candidateAncestor == null || candidateAncestor == menu)
                continue;

            if (menu.transform.IsChildOf(candidateAncestor.transform))
                count++;
        }

        return count;
    }

    // Prevents menu swaps when interacting with sliders, to avoid unintentionally closing menus when adjusting slider values.
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

    // Blocks fade out and back in when trying to open certain menus,
    //  to prevent visual issues with important persistent menus
    private bool IsBlockedFromFade(GameObject menu)
    {
        if (menu == null || menusToBlock == null)
            return false;

        foreach (GameObject blockedMenu in menusToBlock)
        {
            if (blockedMenu == null)
                continue;

            if (menu == blockedMenu || menu.transform.IsChildOf(blockedMenu.transform))
                return true;
        }

        return false;
    }

    // Pushes the currently selected selectable onto the history stack before opening a new menu,
    private void PushCurrentSelectionToHistory()
    {
        if (EventSystem.current == null || EventSystem.current.currentSelectedGameObject == null)
            return;

        Selectable selected = EventSystem.current.currentSelectedGameObject.GetComponent<Selectable>();
        if (selected != null && (selectionHistory.Count == 0 || selectionHistory[0] != selected))
            selectionHistory.Insert(0, selected);
    }

    // Shows the given menu with a fade in if possible, or just enables it if not.
    private void ShowMenu(GameObject menu)
    {
        if (menu == null)
            return;

        FadeMenus fadeMenus = FadeMenusComponent;
        if (fadeMenus != null)
            fadeMenus.FadeMenuSafe(menu, fadeMenus.fadeDuration, true);
        else
            menu.SetActive(true);
    }

    // Closes the given menu with a fade out if possible, or just disables it if not.
    private void CloseMenu(GameObject menu)
    {
        if (menu == null)
            return;

        if (!IsBlockedFromFade(menu))
        {
            FadeMenus fadeMenus = FadeMenusComponent;
            if (fadeMenus != null)
            {
                fadeMenus.FadeMenuSafe(menu, fadeMenus.fadeDuration, false);
                return;
            }
        }

        menu.SetActive(false);
    }

    // Closes the menu at the given index in the stack and removes it from the list, with safety checks.
    private void CloseAndRemoveMenuAt(int index)
    {
        if (menusToManage == null || index < 0 || index >= menusToManage.Count)
            return;

        GameObject menu = menusToManage[index];
        CloseMenu(menu);
        menusToManage.RemoveAt(index);
    }

    // Clears the whole stack if not protected
    public void ClearMenuList()
    {
        foreach(GameObject menu in menusToManage)
        {
            if (!IsProtectedMenu(menu))
                menusToManage.Remove(menu);
            menu.SetActive(false);
        }

        selectionHistory.Clear();
    }

    // Checks if menu is protected
    private bool IsProtectedMenu(GameObject menu)
    {
        return menu != null && (menu == canvas || menu == firstMenuToOpen);
    }

    // sets selection to a valid Selectable (self or first child), or does nothing if none found
    public static void SetSelectedToFirstSelectable(GameObject target)
    {
        if (target == null || EventSystem.current == null)
            return;
        Selectable selectable = target.GetComponent<Selectable>();
        if (selectable == null)
            selectable = target.GetComponentInChildren<Selectable>(true);
        if (selectable != null)
        {
            selectable.Select();
        }
    }

}