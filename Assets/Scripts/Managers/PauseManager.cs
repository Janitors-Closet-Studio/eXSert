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
    [SerializeField] private InputActionReference _pauseActionReference;
    [SerializeField] private InputActionReference _navigationMenuActionReference;
    [SerializeField] private InputActionReference _swapMenuActionReference;
    [SerializeField] private InputActionReference _backActionReference; // UI/Back button

    private MenuListManager menuListManager;

    public static bool IsPaused { get; private set; } = false;
    
    private enum ActiveMenu
    {
        None,
        PauseMenu,
        NavigationMenu
    }
    
    private ActiveMenu currentActiveMenu = ActiveMenu.None;
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
        // Pause action
        if (_pauseActionReference == null || _pauseActionReference.action == null)
            Debug.LogWarning($"Pause Input Action Reference is not set in the inspector. Keyboard/Controller Input won't pause the game properly");
        else
            _pauseActionReference.action.performed += OnPause;

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

        // Back action (UI/Back button)
        if (_backActionReference == null || _backActionReference.action == null)
            Debug.LogWarning($"Back Input Action Reference is not set in the inspector. UI/Back won't work properly");
        else
            _backActionReference.action.performed += OnBackButton;

        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        if (_pauseActionReference != null && _pauseActionReference.action != null)
            _pauseActionReference.action.performed -= OnPause;

        if (_navigationMenuActionReference != null && _navigationMenuActionReference.action != null)
            _navigationMenuActionReference.action.performed -= OnNavigationMenu;

        if (_swapMenuActionReference != null && _swapMenuActionReference.action != null)
            _swapMenuActionReference.action.performed -= OnSwapMenu;

        if (_backActionReference != null && _backActionReference.action != null)
            _backActionReference.action.performed -= OnBackButton;

        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryResolveHudRoot();
        HideAllMenus();
    }

    private void OnPause(InputAction.CallbackContext context)
    {
        if (ConfirmationDialog.AnyOpen)
        {
            Debug.Log("[PauseManager] OnPause ignored - confirmation dialog open");
            return;
        }
        Debug.Log($"[PauseManager] OnPause called - Current menu: {currentActiveMenu}, IsPaused: {IsPaused}");
        
        // If submenus are open, let back button handle it
        if (menuListManager.menusToManage.Count > 2)
        {
            Debug.Log("[PauseManager] OnPause ignored - submenus active, use back button");
            return;
        }
        
        if (currentActiveMenu == ActiveMenu.None)
        {
            // Open pause menu
            ShowPauseMenu();
        }
        else if (currentActiveMenu == ActiveMenu.PauseMenu)
        {
            // Close pause menu and resume game (same button to toggle)
            ResumeGame();
        }
        // If navigation menu is active, pause button is ignored (locked)
    }

    private void OnNavigationMenu(InputAction.CallbackContext context)
    {
        if (ConfirmationDialog.AnyOpen)
        {
            Debug.Log("[PauseManager] OnNavigationMenu ignored - confirmation dialog open");
            return;
        }
        Debug.Log($"[PauseManager] OnNavigationMenu called - Current menu: {currentActiveMenu}, IsPaused: {IsPaused}");
        
        // If submenus are open, let back button handle it
        if (menuListManager.menusToManage.Count > 2)
        {
            Debug.Log("[PauseManager] OnNavigationMenu ignored - submenus active, use back button");
            return;
        }
        
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
        menuListManager.RemoveFirstItemInMenuList();
        menuListManager.SelectFirstSelectOnBack(menuListManager.menusToManage[0]);
    }

    private void OnBackButton(InputAction.CallbackContext context)
    {
        Debug.Log($"[PauseManager] OnBackButton called - Current menu: {currentActiveMenu}, Menu count: {menuListManager.menusToManage.Count}");

        // If we have more than 2 menus (canvas + first menu), just go back one level
        if (menuListManager.menusToManage.Count > 2)
        {
            GoBackOnce();
            return;
        }

        // Don't process back button if no menu is active and we're at base level
        if (currentActiveMenu == ActiveMenu.None)
        {
            Debug.Log("[PauseManager] OnBackButton ignored - no menu active");
            return;
        }

        if(currentActiveMenu == ActiveMenu.NavigationMenu)
        {
            // If we're in the navigation menu, back button takes us to the pause menu
            SwapToPauseMenu();
            return;
        }

        // At base level (canvas + first menu), close settings if open and resume game
        if (settingsMenuOpen)
        {
            CloseSettingsMenu();
            ResumeGame();
            return;
        }

        if (HasBlockingSubmenuActive())
        {
            Debug.Log("[PauseManager] OnBackButton ignored - submenu or popup still active");
            return;
        }
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

    private void ShowNavigationMenu()
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

