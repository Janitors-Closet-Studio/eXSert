using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PauseManager : Singletons.Singleton<PauseManager>
{
    protected override bool ShouldPersistAcrossScenes => false;

    [Header("UI GameObjects")]
    [SerializeField] private GameObject pauseMenuHolder;
    [SerializeField] private GameObject navigationMenuHolder;
    [SerializeField] private GameObject settingsMenuContainer;
    [SerializeField] private GameObject unreadEntriesNotif;
    [SerializeField, Tooltip("Root canvas or parent that contains the in-game HUD (hide when menus are open).")]
    private GameObject playerHUDRoot;
    [SerializeField, Tooltip("Optional fallback name used to rebind the HUD root after scene reloads. Leave blank to capture from the initial reference.")]
    private string playerHUDRootNameHint;

    [Header("Back Button Blockers")]
    [SerializeField, Tooltip("If any of these are active while the pause menu is up, Back should not resume the game.")]
    private GameObject[] pauseMenuBlockingChildren;
    [SerializeField, Tooltip("If any of these are active while the navigation menu is up, Back should not close the menus.")]
    private GameObject[] navigationMenuBlockingChildren;
    [SerializeField, Tooltip("Global UI that should block Back from resuming (e.g., warning popups, overlays).")]
    private GameObject[] globalBackButtonBlockers;

    [Header("Input Actions")]
    [SerializeField] private InputActionReference _navigationMenuActionReference;
    [SerializeField] private InputActionReference _swapMenuActionReference;
    [SerializeField] internal InputActionReference _pauseActionReference;

    private MenuListManager menuListManager;

    public static bool IsPaused { get; private set; } = false;
    
    internal enum ActiveMenu
    {
        None,
        PauseMenu,
        NavigationMenu
    }
    
    internal ActiveMenu currentActiveMenu = ActiveMenu.None;
    private bool settingsMenuOpen = false;

    protected override void Awake()
    {
        base.Awake();
        CacheHudRootName();
        HideAllMenus();
        menuListManager = this.GetComponent<MenuListManager>();
    }

    private void OnEnable()
    {
        // Navigation Menu action
        if (_navigationMenuActionReference == null || _navigationMenuActionReference.action == null)
            Debug.LogWarning($"Navigation Menu Input Action Reference is not set in the inspector. Keyboard/Controller Input won't open navigation menu properly");
        else
            _navigationMenuActionReference.action.performed += OnNavigationMenu;

        // Swap Menu action
        if (_swapMenuActionReference == null || _swapMenuActionReference.action == null)
            Debug.LogWarning($"Swap Menu Input Action Reference is not set in the inspector. UI swapping won't work properly");
        else
            _swapMenuActionReference.action.performed += OnSwapMenu;

        if(_pauseActionReference == null || _pauseActionReference.action == null)
            Debug.LogWarning($"Pause Input Action Reference is not set in the inspector. Pause/Back button won't work properly");
        else
            _pauseActionReference.action.performed += OnPauseOrBack;

        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        // Unsubscribe from runtime Pause action
        if (_pauseActionReference != null && _pauseActionReference.action != null)
            _pauseActionReference.action.performed -= OnPauseOrBack;

        if (_navigationMenuActionReference != null && _navigationMenuActionReference.action != null)
            _navigationMenuActionReference.action.performed -= OnNavigationMenu;

        if (_swapMenuActionReference != null && _swapMenuActionReference.action != null)
            _swapMenuActionReference.action.performed -= OnSwapMenu;

        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }


    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryResolveHudRoot();
        HideAllMenus();
    }


    private void OnPauseOrBack(InputAction.CallbackContext context)
    {
        if (ConfirmationDialog.AnyOpen)
        {
            Debug.Log("[PauseManager] OnPauseOrBack ignored - confirmation dialog open");
            return;
        }
        Debug.Log($"[PauseManager] OnPauseOrBack called - Current menu: {currentActiveMenu}, Menu count: {menuListManager.menusToManage.Count}, IsPaused: {IsPaused}");

        // Force pause timescale whenever Pause is triggered
        Time.timeScale = 0f;

        if(LogManager.Instance.unreadLogs.Count > 0 || DiaryManager.Instance.unreadDiaries.Count > 0)
            unreadEntriesNotif.SetActive(true);
        else
            unreadEntriesNotif.SetActive(false);

        // If we have more than 2 menus (canvas + first menu), just go back one level
        if (menuListManager.menusToManage.Count > 2)
        {
            GoBackOnce();
            return;
        }

        // If settings menu is open, close it and return to pause menu
        if (settingsMenuOpen)
        {
            CloseSettingsMenu();
            return;
        }

        // If navigation menu is active, back/pause returns to pause menu
        if(currentActiveMenu == ActiveMenu.NavigationMenu)
        {
            SwapToPauseMenu();
            return;
        }

        // If we're in the pause menu with only one level, back/pause resumes the game
        if(currentActiveMenu == ActiveMenu.PauseMenu)
        {
            ResumeGame();
            return;
        }

        // If no menu is active, open pause menu
        if (currentActiveMenu == ActiveMenu.None)
        {
            ShowPauseMenu();
            return;
        }

        if (HasBlockingSubmenuActive())
        {
            Debug.Log("[PauseManager] OnPauseOrBack ignored - submenu or popup still active");
            return;
        }
    }

    private void OnNavigationMenu(InputAction.CallbackContext context)
    {
        if (ConfirmationDialog.AnyOpen)
        {
            Debug.Log("[PauseManager] OnNavigationMenu ignored - confirmation dialog open");
            return;
        }
        Debug.Log($"[PauseManager] OnNavigationMenu called - Current menu: {currentActiveMenu}, IsPaused: {IsPaused}");
        
        if (currentActiveMenu == ActiveMenu.None)
        {
            // Open navigation menu
            ShowNavigationMenu();
        }
        else if (currentActiveMenu == ActiveMenu.NavigationMenu)
        {
            // Close navigation menu and resume game (same button to toggle)
            ResumeGame();
        }
        // If pause menu is active, navigation menu button is ignored (locked)
    }

    private void GoBackOnce()
    {
        menuListManager.GoBackToPreviousMenu();
        menuListManager.SelectFirstSelectOnBack(menuListManager.menusToManage[0]);
    }

    /// <summary>
    /// Closes the settings menu and returns to the pause menu.
    /// Call this from your Settings "Back" button as well.
    /// </summary>
    public void CloseSettingsMenu()
    {
        settingsMenuOpen = false;
        SetMenuStates(showPause: true, showNavigation: false, showSettings: false);
        currentActiveMenu = ActiveMenu.PauseMenu;
        Debug.Log("[PauseManager] Settings menu closed, returning to pause menu");
    }

    /// <summary>
    /// Opens the settings menu from pause menu.
    /// Call this from your Pause Menu "Settings" button.
    /// </summary>
    public void OpenSettingsMenu()
    {
        SetMenuStates(showPause: false, showNavigation: false, showSettings: true);
        Debug.Log("[PauseManager] Settings menu opened");
    }

    private void OnSwapMenu(InputAction.CallbackContext context)
    {
        if (ConfirmationDialog.AnyOpen)
        {
            Debug.Log("[PauseManager] OnSwapMenu ignored - confirmation dialog open");
            return;
        }
        // Only swap if game is paused and a menu is active
        if (!IsPaused || currentActiveMenu == ActiveMenu.None)
            return;

        if (currentActiveMenu == ActiveMenu.PauseMenu)
        {
            // Switch from pause menu to navigation menu
            SwapToNavigationMenu();
        }
        else if (currentActiveMenu == ActiveMenu.NavigationMenu)
        {
            // Switch from navigation menu to pause menu
            SwapToPauseMenu();
        }
    }

    private void ShowPauseMenu()
    {
        Debug.Log(Time.timeScale + "is the current timescale when showing pause menu.");  
        Time.timeScale = 0f;
        IsPaused = true;
        currentActiveMenu = ActiveMenu.PauseMenu;

        SetMenuStates(showPause: true, showNavigation: false, showSettings: false);

        Debug.Log("Pause Menu Opened");
        
        // Switch to UI input - make sure actions remain subscribed
        if (InputReader.PlayerInput != null)
        {
            InputReader.PlayerInput.SwitchCurrentActionMap("UI");
        }
        else
        {
            Debug.LogWarning("PlayerInput is null when trying to show pause menu. Make sure InputReader is set up correctly.");
        }
    }

    internal void ShowNavigationMenu()
    {
        Time.timeScale = 0f;
        IsPaused = true;
        currentActiveMenu = ActiveMenu.NavigationMenu;

        SetMenuStates(showPause: false, showNavigation: true, showSettings: false);

        Debug.Log("Navigation Menu Opened");
        
        // Switch to UI input
        if (InputReader.PlayerInput != null)
        {
            InputReader.PlayerInput.SwitchCurrentActionMap("UI");
        }
    }

    private void SwapToPauseMenu()
    {
        currentActiveMenu = ActiveMenu.PauseMenu;

        SetMenuStates(showPause: true, showNavigation: false, showSettings: false);

        Debug.Log("Swapped to Pause Menu");
    }

    private void SwapToNavigationMenu()
    {
        currentActiveMenu = ActiveMenu.NavigationMenu;

        SetMenuStates(showPause: false, showNavigation: true, showSettings: false);

        Debug.Log("Swapped to Navigation Menu");
    }

    public void ResumeGame()
    {
        Time.timeScale = 1f;
        IsPaused = false;
        currentActiveMenu = ActiveMenu.None;

        HideAllMenus();

        Debug.Log("Game Resumed");
        
        // Switch back to Gameplay input
        if (InputReader.PlayerInput != null)
        {
            InputReader.PlayerInput.SwitchCurrentActionMap("Gameplay");
        }
        else
        {
            Debug.LogWarning("PlayerInput is null when trying to resume game. Make sure InputReader is set up correctly.");
        }
    }

    /// <summary>
    /// Hides all pause UI in preparation for a scene load while leaving the timescale unchanged.
    /// Use this when a loading screen will manage pausing/resuming (e.g., restart checkpoint).
    /// </summary>
    public void HideMenusForSceneTransition()
    {
        IsPaused = false;
        currentActiveMenu = ActiveMenu.None;
        HideAllMenus();

        if (InputReader.PlayerInput != null)
        {
            InputReader.PlayerInput.SwitchCurrentActionMap("Gameplay");
        }
    }

    // Public methods for UI buttons to call
    public void OnResumeButtonClicked()
    {
        ResumeGame();
    }

    public void OnSwapMenuButtonClicked()
    {
        OnSwapMenu(new InputAction.CallbackContext());
    }

    private void HideAllMenus()
    {
        settingsMenuOpen = false;
        SetMenuStates(false, false, false);
    }

    private void SetMenuStates(bool showPause, bool showNavigation, bool showSettings)
    {
        settingsMenuOpen = showSettings;

        if (pauseMenuHolder != null)
            pauseMenuHolder.SetActive(showPause);

        if (navigationMenuHolder != null)
            navigationMenuHolder.SetActive(showNavigation);

        if (settingsMenuContainer != null)
            settingsMenuContainer.SetActive(showSettings);

        bool showHUD = !(showPause || showNavigation || showSettings);
        SetHUDVisible(showHUD);
    }

    private void SetHUDVisible(bool visible)
    {
        if (!TryResolveHudRoot())
            return;

        if (playerHUDRoot.activeSelf != visible)
            playerHUDRoot.SetActive(visible);
    }

    private bool TryResolveHudRoot()
    {
        if (playerHUDRoot != null)
            return true;

        if (string.IsNullOrEmpty(playerHUDRootNameHint))
            return false;

        var candidate = GameObject.Find(playerHUDRootNameHint);
        if (candidate == null)
            return false;

        playerHUDRoot = candidate;
        CacheHudRootName();
        return true;
    }

    private void CacheHudRootName()
    {
        if (playerHUDRoot != null && string.IsNullOrEmpty(playerHUDRootNameHint))
            playerHUDRootNameHint = playerHUDRoot.name;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
            CacheHudRootName();
    }
#endif

    private bool HasBlockingSubmenuActive()
    {
        if (IsAnyActive(globalBackButtonBlockers))
            return true;

        if (settingsMenuOpen)
            return true;

        return currentActiveMenu switch
        {
            ActiveMenu.PauseMenu => IsAnyActive(pauseMenuBlockingChildren),
            ActiveMenu.NavigationMenu => IsAnyActive(navigationMenuBlockingChildren),
            _ => false
        };
    }

    private static bool IsAnyActive(GameObject[] targets)
    {
        if (targets == null || targets.Length == 0)
            return false;

        foreach (var target in targets)
        {
            if (target != null && target.activeInHierarchy)
                return true;
        }

        return false;
    }
}

