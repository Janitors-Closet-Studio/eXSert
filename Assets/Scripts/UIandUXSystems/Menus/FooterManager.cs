using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

[Serializable]
public class MenusWithFooters
{
    public GameObject menuName;
    public string footerMessage;
    public bool keepFooterVisible;
    public List <GameObject> activateOnMenuOpen;
}


public class FooterManager : MonoBehaviour
{
    [SerializeField] private GameObject footerPanel;
    [SerializeField] private TMP_Text footerText;
    [SerializeField] private string defaultFooterMessage = "Explore Your Settings";
    [SerializeField] public List<MenusWithFooters> menuFooters = new List<MenusWithFooters>();

    [SerializeField] private bool isOnPause = false;
    private MenuListManager menuListManager;

    private FadeMenus fadeMenus;
    private bool skipFadeForNextUpdate;
    private bool forceImmediateFooterUpdates;
    private bool suppressFooterVisibility;
    private string lastMappedFooterMessage = string.Empty;
    private bool lastMenuHadMappedFooter;

    public static Action<string> OnFooterTextUpdated;

    private void Awake()
    {
        if (footerPanel != null)
            footerPanel.SetActive(false);

        menuListManager = GetComponent<MenuListManager>();
        if (menuListManager == null)
        {
            Debug.LogError("FooterManager requires a MenuListManager component on the same GameObject.");
        }

        fadeMenus = GetComponent<FadeMenus>();
    }

    private void OnEnable()
    {
        OnFooterTextUpdated += UpdateFooterText;
    }

    private void OnDisable()
    {
        OnFooterTextUpdated -= UpdateFooterText;
    }

    public void UpdateFooterText(string message)
    {
        if (footerText != null)
        {
            footerText.text = message;
        }

        if (suppressFooterVisibility)
        {
            skipFadeForNextUpdate = false;
            return;
        }

        bool shouldShow = !string.IsNullOrWhiteSpace(message);

        if (forceImmediateFooterUpdates || skipFadeForNextUpdate)
            SetFooterVisibilityImmediate(shouldShow);
        else
            SetFooterVisibilityWithFade(shouldShow);

        skipFadeForNextUpdate = false;
    }

    public void BeginSilentFooterInitialization()
    {
        suppressFooterVisibility = true;
    }

    public void EndSilentFooterInitialization()
    {
        suppressFooterVisibility = false;
    }

    private void SetFooterVisibilityImmediate(bool shouldShow)
    {
        if (footerPanel == null)
            return;

        footerPanel.SetActive(shouldShow);
    }

    private void SetFooterVisibilityWithFade(bool shouldShow)
    {
        if (footerPanel == null)
            return;

        if (fadeMenus != null)
        {
            if (shouldShow)
            {
                if (!footerPanel.activeSelf)
                    fadeMenus.FadeMenuSafe(footerPanel, fadeMenus.fadeDuration, true);
            }
            else
            {
                if (footerPanel.activeSelf)
                    fadeMenus.FadeMenuSafe(footerPanel, fadeMenus.fadeDuration, false);
            }

            return;
        }

        footerPanel.SetActive(shouldShow);
    }

    private bool CheckIfMenuIsParent(MenusWithFooters menuFooter, GameObject child)
    {
        if (menuFooter == null || child == null)
            return false;

        Transform currentParent = child.transform.parent;

        while (currentParent != null)
        {
            if (currentParent.gameObject == menuFooter.menuName)
                return true;

            currentParent = currentParent.parent;
        }

        return false;
    }

    private bool HasActiveMappedAncestor(GameObject menu)
    {
        if (menuListManager == null || menuListManager.menusToManage == null || menuFooters == null || menu == null)
            return false;

        foreach (MenusWithFooters menuFooter in menuFooters)
        {
            if (menuFooter == null || menuFooter.menuName == null)
                continue;

            if (!CheckIfMenuIsParent(menuFooter, menu))
                continue;

            if (menuListManager.menusToManage.Contains(menuFooter.menuName) && menuFooter.menuName.activeInHierarchy)
                return true;
        }

        return false;
    }

    private void SetActivateOnOpenObjects(MenusWithFooters activeMenuFooter)
    {
        if (menuFooters == null)
            return;

        foreach (MenusWithFooters menuFooter in menuFooters)
        {
            if (menuFooter == null || menuFooter.activateOnMenuOpen == null)
                continue;

            bool shouldActivate = menuFooter == activeMenuFooter;

            foreach (GameObject objectToToggle in menuFooter.activateOnMenuOpen)
            {
                if (objectToToggle != null)
                    objectToToggle.SetActive(shouldActivate);
            }
        }
    }

    private MenusWithFooters ResolveMappedMenuFooter(GameObject menu)
    {
        if (menu == null || menuFooters == null)
            return null;

        foreach (MenusWithFooters menuFooter in menuFooters)
        {
            if (menuFooter == null)
                continue;

            if (CheckIfMenuIsParent(menuFooter, menu))
                return menuFooter;
        }

        foreach (MenusWithFooters menuFooter in menuFooters)
        {
            if (menuFooter != null && menuFooter.menuName == menu)
                return menuFooter;
        }

        return null;
    }

    public void UpdateFooterForMenu(GameObject menu)
    {
        if (menu == null)
        {
            skipFadeForNextUpdate = false;
            SetActivateOnOpenObjects(null);
            OnFooterTextUpdated?.Invoke(string.Empty);
            DeactivateOtherActivateOnOpenObjects(null);
            lastMappedFooterMessage = string.Empty;
            lastMenuHadMappedFooter = false;
            return;
        }

        Debug.Log($"Updating footer for menu: {menu.name}");

        string footerMessage = string.Empty;
        skipFadeForNextUpdate = HasActiveMappedAncestor(menu);

        MenusWithFooters activeMenuFooter = ResolveMappedMenuFooter(menu);
        SetActivateOnOpenObjects(activeMenuFooter);
        DeactivateOtherActivateOnOpenObjects(activeMenuFooter);

        if (activeMenuFooter != null && activeMenuFooter.keepFooterVisible)
        {
            string inheritedFooterMessage = lastMenuHadMappedFooter
                ? lastMappedFooterMessage
                : defaultFooterMessage;

            OnFooterTextUpdated?.Invoke(inheritedFooterMessage);
            return;
        }

        if (activeMenuFooter != null && !string.IsNullOrWhiteSpace(activeMenuFooter.footerMessage))
        {
            footerMessage = activeMenuFooter.footerMessage;
            lastMappedFooterMessage = footerMessage;
            lastMenuHadMappedFooter = true;
        }
        else
        {
            lastMappedFooterMessage = string.Empty;
            lastMenuHadMappedFooter = false;
        }

        OnFooterTextUpdated?.Invoke(footerMessage);
    }

    public void SetToLastSibling()
    {
        if (footerPanel != null)
            footerPanel.transform.SetAsLastSibling();
    }

    private void DeactivateOtherActivateOnOpenObjects(MenusWithFooters activeMenuFooter)
    {
        if (menuFooters == null)
            return;

        foreach (MenusWithFooters menuFooter in menuFooters)
        {
            if (menuFooter == null || menuFooter.activateOnMenuOpen == null || menuFooter == activeMenuFooter)
                continue;

            foreach (GameObject objectToToggle in menuFooter.activateOnMenuOpen)
            {
                if (objectToToggle != null)
                    objectToToggle.SetActive(false);
            }
        }
    }


}

