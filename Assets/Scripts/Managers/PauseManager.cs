using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Managers.TimeLord;
using Unity.VisualScripting;

public class PauseManager : Singletons.Singleton<PauseManager>
{
    private const string GameplayInputBlockOwnerId = "PauseManager";

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
    [SerializeField] private InputActionReference _pauseActionReference;
    [SerializeField] private InputActionReference _backActionReference;
    [SerializeField, Tooltip("Small debounce to prevent one key press from triggering Pause then Back after action map switch.")]
    private float inputDebounceSeconds = 0.15f;


    private MenuListManager menuListManager;

    // Proxy to coordinator's paused state
    public static bool IsPaused => PauseCoordinator.IsPaused;
    
    private enum ActiveMenu
    {
        None,
        PauseMenu,
        NavigationMenu
    }
    
    private ActiveMenu currentActiveMenu = ActiveMenu.None;
    private bool settingsMenuOpen = false;
    private bool isUnpausing = false; // Flag to indicate we're in the process of unpausing (used to delay input block release)
    private float ignorePauseUntilTime;
    private float ignoreBackUntilTime;

    protected override void Awake()
    {
        base.Awake();
        CacheHudRootName();
        HideAllMenus();
        menuListManager = this.GetComponent<MenuListManager>();
    }

    private void OnEnable()
    {
        // Subscribe to coordinator pause/resume events for global side-effects (audio muffling, etc.)
        PauseCoordinator.OnPaused += HandleCoordinatorPaused;
        PauseCoordinator.OnResumed += HandleCoordinatorResumed;

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
            Debug.LogWarning($"Pause Input Action Reference is not set in the inspector. Pause button won't work properly");
        else
            _pauseActionReference.action.performed += OnPause;

        if(_backActionReference == null || _backActionReference.action == null)
            Debug.LogWarning($"Back Input Action Reference is not set in the inspector. Back button won't work properly");
        else
            _backActionReference.action.performed += OnBack;

        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        // Unsubscribe from runtime Pause action
        if (_pauseActionReference != null && _pauseActionReference.action != null)
            _pauseActionReference.action.performed -= OnPause;

        if (_backActionReference != null && _backActionReference.action != null)
            _backActionReference.action.performed -= OnBack;

        if (_navigationMenuActionReference != null && _navigationMenuActionReference.action != null)
            _navigationMenuActionReference.action.performed -= OnNavigationMenu;

        if (_swapMenuActionReference != null && _swapMenuActionReference.action != null)
            _swapMenuActionReference.action.performed -= OnSwapMenu;

        InputReader.ReleaseGameplayInputBlock(GameplayInputBlockOwnerId);

        SceneManager.sceneLoaded -= HandleSceneLoaded;

        // Unsubscribe from coordinator
        PauseCoordinator.OnPaused -= HandleCoordinatorPaused;
        PauseCoordinator.OnResumed -= HandleCoordinatorResumed;
    }


    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryResolveHudRoot();
        HideAllMenus();
    }


    private void OnPause(InputAction.CallbackContext context)
    {
        if (Time.unscaledTime < ignorePauseUntilTime)
            return;

        if(Hint.isHintActive)
        {
            return;
        }

        if (ConfirmationDialog.AnyOpen)
        {
            return;
        }

        if(LogManager.Instance.unreadLogs.Count > 0 || DiaryManager.Instance.unreadDiaries.Count > 0)
            unreadEntriesNotif.SetActive(true);
        else
            unreadEntriesNotif.SetActive(false);

            // If no menu is active, open pause menu
        if (currentActiveMenu == ActiveMenu.None)
        {
            ShowPauseMenu();
            return;
        }

        if (HasBlockingSubmenuActive())
        {
            return;
        }

    }

    private void OnBack(InputAction.CallbackContext context)
    {
        if (Time.unscaledTime < ignoreBackUntilTime)
            return;

         
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
    }
    private void OnNavigationMenu(InputAction.CallbackContext context)
    {
        if (isUnpausing)
        {
            Debug.Log("[PauseManager] OnNavigationMenu ignored - currently unpausing");
            return;
        }

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
        if (menuListManager.menusToBlock.Contains(menuListManager.menusToManage[0]))
        {
            menuListManager.GoBackToPreviousMenu();
            menuListManager.GoBackToPreviousMenu();
        }
        else 
        {
            menuListManager.GoBackToPreviousMenu();
        }

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
        if (isUnpausing)
        {
            Debug.Log("[PauseManager] ShowPauseMenu ignored - currently unpausing");
            return;
        }

        Debug.Log(Time.timeScale + "is the current timescale when showing pause menu.");  

        // Request pause through the coordinator (centralized time scale authority).
        PauseCoordinator.RequestPause(GameplayInputBlockOwnerId);

        // Block gameplay input while menus are active
        InputReader.RequestGameplayInputBlock(GameplayInputBlockOwnerId);
        currentActiveMenu = ActiveMenu.PauseMenu;

        SetMenuStates(showPause: true, showNavigation: false, showSettings: false);

        // Prevent same physical key press from immediately firing Back after action map switch.
        ignoreBackUntilTime = Time.unscaledTime + inputDebounceSeconds;
        ignorePauseUntilTime = Time.unscaledTime + inputDebounceSeconds;

        Debug.Log("Pause Menu Opened");
        
        // Switch to UI input - make sure actions remain subscribed
        if (InputReader.PlayerInput != null)
        {
            InputReader.PlayerInput.SwitchCurrentActionMap("UI");
            CursorManager.RefreshPolicy();
        }
        else
        {
            Debug.LogWarning("PlayerInput is null when trying to show pause menu. Make sure InputReader is set up correctly.");
        }
    }

    private void ShowNavigationMenu()
    {
        // Request pause through the coordinator (centralized time scale authority).
        PauseCoordinator.RequestPause(GameplayInputBlockOwnerId);

        InputReader.RequestGameplayInputBlock(GameplayInputBlockOwnerId);
        currentActiveMenu = ActiveMenu.NavigationMenu;

        SetMenuStates(showPause: false, showNavigation: true, showSettings: false);

        // Prevent same physical key press from immediately firing Back after action map switch.
        ignoreBackUntilTime = Time.unscaledTime + inputDebounceSeconds;
        ignorePauseUntilTime = Time.unscaledTime + inputDebounceSeconds;

        Debug.Log("Navigation Menu Opened");
        
        // Switch to UI input
        if (InputReader.PlayerInput != null)
        {
            InputReader.PlayerInput.SwitchCurrentActionMap("UI");
            CursorManager.RefreshPolicy();
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
        // Release the coordinator ownership for pause
        PauseCoordinator.ReleaseTimeScale(GameplayInputBlockOwnerId);

        // Release gameplay input block
        InputReader.ReleaseGameplayInputBlock(GameplayInputBlockOwnerId);
        currentActiveMenu = ActiveMenu.None;

        HideAllMenus();

        // Prevent immediate re-open from the same key press while returning to Gameplay.
        ignorePauseUntilTime = Time.unscaledTime + inputDebounceSeconds;
        ignoreBackUntilTime = Time.unscaledTime + inputDebounceSeconds;

        StartCoroutine(DelayAfterUnpausing());

        Debug.Log("Game Resumed");
        
        // Switch back to Gameplay input
        if (InputReader.PlayerInput != null)
        {
            if(CranePuzzle.IsCranePuzzleActive)
                InputReader.PlayerInput.SwitchCurrentActionMap("CranePuzzle");
            else
                InputReader.PlayerInput.SwitchCurrentActionMap("Gameplay");
            CursorManager.RefreshPolicy();
        }
        else
        {
            Debug.LogWarning("PlayerInput is null when trying to resume game. Make sure InputReader is set up correctly.");
        }
    }

    private IEnumerator DelayAfterUnpausing()
    {
        isUnpausing = true;
        yield return new WaitForSeconds(0.25f);
        isUnpausing = false;
    }

    /// <summary>
    /// Hides all pause UI in preparation for a scene load while leaving the timescale unchanged.
    /// Use this when a loading screen will manage pausing/resuming (e.g., restart checkpoint).
    /// </summary>
    public void HideMenusForSceneTransition()
    {
        // Release this menu's pause ownership so restart transitions do not leave the game paused.
        PauseCoordinator.ReleaseTimeScale(GameplayInputBlockOwnerId);

        // Hide pause/navigation UI and release local gameplay input blocking.
        InputReader.ReleaseGameplayInputBlock(GameplayInputBlockOwnerId);
        currentActiveMenu = ActiveMenu.None;
        HideAllMenus();

        if (InputReader.PlayerInput != null)
        {
            InputReader.PlayerInput.SwitchCurrentActionMap("Gameplay");
            CursorManager.RefreshPolicy();
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
        FadeMenus fadeMenus = this.GetComponent<FadeMenus>();
        settingsMenuOpen = showSettings;

        if (pauseMenuHolder != null)
            StartCoroutine(fadeMenus.FadeMenu(pauseMenuHolder, fadeMenus.fadeDuration, showPause));

        if (navigationMenuHolder != null)
            StartCoroutine(fadeMenus.FadeMenu(navigationMenuHolder, fadeMenus.fadeDuration, showNavigation));

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

    public void SetGameplayHUDVisible(bool visible)
    {
        SetHUDVisible(visible);
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

    private void MufffleMusicForMenu(bool shouldMuffle)
    {
        if(SoundManager.Instance == null || SoundManager.Instance.levelMusicSource == null)
            return;

        var lowPassFilter = SoundManager.Instance.levelMusicSource.GetComponent<AudioLowPassFilter>();
        var oldCutoff = lowPassFilter != null ? lowPassFilter.cutoffFrequency : 22000f;


        if (shouldMuffle)
        {
            Debug.Log("Muffling music for menu");
            SoundManager.Instance.levelMusicSource.volume *= 0.5f; // Muffle music
            if (lowPassFilter != null)
                lowPassFilter.cutoffFrequency = 500f; // Apply low-pass filter
            else
                Debug.LogWarning("No AudioLowPassFilter found on level music source. Music will be muffled by volume reduction only.");
        }
        else
        {
            Debug.Log("Restoring music after menu");
            SoundManager.Instance.levelMusicSource.volume /= 0.5f; // Restore music volume
            if (lowPassFilter != null)
                lowPassFilter.cutoffFrequency = oldCutoff; // Revert low-pass filter
        }
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

    // Coordinator event handlers
    private void HandleCoordinatorPaused()
    {
        // Central pause side-effects: audio, etc.
        MufffleMusicForMenu(true);
    }

    private void HandleCoordinatorResumed()
    {
        MufffleMusicForMenu(false);
    }
}

