using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using Managers.TimeLord;
using Unity.VisualScripting;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UI.Loading;


public class PauseManager : Singletons.Singleton<PauseManager>
{
    private const string GameplayInputBlockOwnerId = "PauseManager";

    protected override bool ShouldPersistAcrossScenes => false;

    [SerializeField, Tooltip("Assign the pause blur global volume profile here. PauseManager will toggle that profile's Depth Of Field during pause.")]
    private VolumeProfile pauseMenuVolumeProfile;

    [Header("UI GameObjects")]
    [SerializeField] internal GameObject pauseOverlay;
    
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
    [SerializeField, Tooltip("How many frames to suppress UI auto-selection right after unpausing.")]
    private int unpauseSelectionSuppressionFrames = 20;
    FadeMenus fadeMenus;


    private MenuListManager menuListManager;
    private FooterManager footerManager;

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

    // Music muffling state
    private bool musicIsMuffled = false;
    private AudioLowPassFilter cachedPauseLowPassFilter;
    private bool? cachedLowPassEnabledBeforePause;
    private float? cachedLowPassCutoffBeforePause;
    private bool blurEnabled;
    private bool blurProfileWarningLogged;
    private bool blurOverrideWarningLogged;

    protected override void Awake()
    {
        base.Awake();
        CacheHudRootName();
        HideAllMenus();
        menuListManager = this.GetComponent<MenuListManager>();
        footerManager = FindFirstObjectByType<FooterManager>(FindObjectsInactive.Include);
        pauseOverlay.SetActive(false);
        fadeMenus = this.GetComponent<FadeMenus>();
        SetBlurEnabled(false);
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
        SetBlurEnabled(false);
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
        MainMenu.isInMainMenu = scene.name == "MainMenu";
        MufffleMusicForMenu(false);
        TryResolveHudRoot();
        HideAllMenus();
        menuListManager?.ResetForGameplay();
        if (!MainMenu.isInMainMenu)
            SetHUDVisible(true);
        SetBlurEnabled(false);
    }

    private void SetBlurEnabled(bool enabled)
    {
        if (pauseMenuVolumeProfile == null)
        {
            if (!blurProfileWarningLogged)
            {
                Debug.LogWarning("PauseManager pauseMenuVolumeProfile is not assigned. Assign the global blur volume profile from PostProcessScene.");
                blurProfileWarningLogged = true;
            }

            blurEnabled = false;
            return;
        }

        if (!pauseMenuVolumeProfile.TryGet(out DepthOfField dof) || dof == null)
        {
            if (!blurOverrideWarningLogged)
            {
                Debug.LogWarning("No DepthOfField override found in the assigned pauseMenuVolumeProfile. Add one to the selected global blur profile to enable pause blur.");
                blurOverrideWarningLogged = true;
            }

            blurEnabled = false;
            return;
        }

        if (blurEnabled == enabled && dof.active == enabled)
            return;

        dof.active = enabled;
        blurEnabled = enabled;
    }

    private void OnPause(InputAction.CallbackContext context)
    {
        if (MainMenu.isInMainMenu)
        {
            Debug.Log("[PauseManager] OnPause ignored - currently in main menu");
            return;
        }

        if (IsPaused && InputReader.PlayerInput != null)
        {
            InputActionMap currentActionMap = InputReader.PlayerInput.currentActionMap;
            if (currentActionMap != null && string.Equals(currentActionMap.name, "UI", System.StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log("[PauseManager] OnPause ignored - UI action map is active while paused, deferring Escape handling to UI/Back.");
                return;
            }
        }

        if (IsPauseBlockedByPuzzleMode())
            return;

        if (Time.unscaledTime < ignorePauseUntilTime)
            return;

        if(Hint.isHintActive)
        {
            return;
        }
        
        if (LoadingScreenController.IsLoading)
        {
            Debug.Log("[PauseManager] OnNavigationMenu ignored - currently loading");
            return;
        }

        if (CutsceneManager.IsCutscenePlaying)
        {
            return;
        }

        if (ConfirmationDialog.AnyOpen)
        {
            return;
        }

        RefreshNaviEntryIndicator();

            // If no menu is active, open pause menu
        if (currentActiveMenu == ActiveMenu.None)
        {
            ShowPauseMenu();
            return;
        }

        if (TryHandlePauseAsUiBack())
        {
            return;
        }

        if (currentActiveMenu == ActiveMenu.PauseMenu || currentActiveMenu == ActiveMenu.NavigationMenu)
        {
            ResumeGame();
            return;
        }

    }

    private void RefreshNaviEntryIndicator()
    {
        if (unreadEntriesNotif == null)
            return;

        if(LogManager.Instance.unreadLogs.Count > 0 || DiaryManager.Instance.unreadDiaries.Count > 0)
            unreadEntriesNotif.SetActive(true);
        else
            unreadEntriesNotif.SetActive(false);
    }
    private void OnBack(InputAction.CallbackContext context)
    {
        if (Time.unscaledTime < ignoreBackUntilTime)
            return;

        InputActionMap currentActionMap = InputReader.PlayerInput != null
            ? InputReader.PlayerInput.currentActionMap
            : null;
        bool isUiActionMapActive = currentActionMap != null
            && string.Equals(currentActionMap.name, "UI", System.StringComparison.OrdinalIgnoreCase);

        if (!IsPaused && !isUiActionMapActive)
            return;

        if (!IsPauseUiActive())
            return;

        if (MainMenu.isInMainMenu)
        {
            Debug.Log("[PauseManager] OnBack ignored - currently in main menu");
            return;
        }

        if (LoadingScreenController.IsLoading)
        {
            Debug.Log("[PauseManager] OnNavigationMenu ignored - currently loading");
            return;
        }

        SyncActiveMenuToStackTop();

        if (menuListManager == null || menuListManager.menusToManage == null)
        {
            Debug.LogWarning("[PauseManager] OnBack: menuListManager reference is missing. Falling back to resume if paused.");
            if (IsPaused)
                ResumeGame();
            return;
        }

        if (settingsMenuOpen)
        {
            if (menuListManager.CanGoBackOneLevel())
            {
                GoBackOnce();
            }
            else
            {
                CloseSettingsMenu();
            }

            ignorePauseUntilTime = Time.unscaledTime + inputDebounceSeconds;
            return;
        }

        if (menuListManager.menusToManage.Count > 0 && menuListManager.menusToManage[0] == menuListManager.firstMenuToOpen)
        {
            Debug.Log("[PauseManager] OnBack: At root menu, treating Back as unpause");
            ignorePauseUntilTime = Time.unscaledTime + inputDebounceSeconds;
            ResumeGame();
            return;
        }

        if (currentActiveMenu == ActiveMenu.NavigationMenu
            && menuListManager.menusToManage.Count > 0
            && menuListManager.menusToManage[0] == navigationMenuHolder)
        {
            ignorePauseUntilTime = Time.unscaledTime + inputDebounceSeconds;
            ResumeGame();
            return;
        }

        bool canGoBackOneLevel = menuListManager.CanGoBackOneLevel();

        // If pause is active but the active-menu tracker drifted (e.g. protected root menu on top),
        // treat Back as an unpause when there is no valid stack back target.
        if (currentActiveMenu == ActiveMenu.None && IsPaused && !canGoBackOneLevel)
        {
            ignorePauseUntilTime = Time.unscaledTime + inputDebounceSeconds;
            ResumeGame();
            return;
        }

        if (canGoBackOneLevel)
        {
            GoBackOnce();
            ignorePauseUntilTime = Time.unscaledTime + inputDebounceSeconds;
            return;
        }
        
        if(currentActiveMenu == ActiveMenu.NavigationMenu)
        {
            ResumeGame();
            ignorePauseUntilTime = Time.unscaledTime + inputDebounceSeconds;
            return;
        }

        // If we're in the pause menu with only one level, back/pause resumes the game
        if(currentActiveMenu == ActiveMenu.PauseMenu)
        {
            ignorePauseUntilTime = Time.unscaledTime + inputDebounceSeconds;
            ResumeGame();
            return;
        }
    }

    private bool TryHandlePauseAsUiBack()
    {
        SyncActiveMenuToStackTop();

        if (!IsPaused)
            return false;

        if (settingsMenuOpen)
        {
            if (menuListManager != null && menuListManager.menusToManage != null && menuListManager.CanGoBackOneLevel())
            {
                GoBackOnce();
            }
            else
            {
                CloseSettingsMenu();
            }

            ignoreBackUntilTime = Time.unscaledTime + inputDebounceSeconds;
            return true;
        }

        if (menuListManager == null || menuListManager.menusToManage == null)
            return false;

        if (menuListManager.CanGoBackOneLevel())
        {
            GoBackOnce();
            ignoreBackUntilTime = Time.unscaledTime + inputDebounceSeconds;
            return true;
        }

        GameObject currentRoot = GetCurrentMenuRoot();
        if (currentRoot == null)
            return HasBlockingSubmenuActive();

        if (menuListManager.menusToManage.Count == 0)
            return false;

        GameObject topMenu = menuListManager.menusToManage[0];
        bool isAtCurrentRoot = topMenu == currentRoot;
        if (!isAtCurrentRoot)
        {
            if (HasBlockingSubmenuActive())
                ignoreBackUntilTime = Time.unscaledTime + inputDebounceSeconds;

            return HasBlockingSubmenuActive();
        }

        return false;
    }

    private GameObject GetCurrentMenuRoot()
    {
        return currentActiveMenu switch
        {
            ActiveMenu.PauseMenu => pauseMenuHolder,
            ActiveMenu.NavigationMenu => navigationMenuHolder,
            _ => null
        };
    }
    private void OnNavigationMenu(InputAction.CallbackContext context)
    {
        if (Time.unscaledTime < ignorePauseUntilTime)
            return;

        if (IsPauseBlockedByPuzzleMode())
            return;
    
        if (MainMenu.isInMainMenu)
        {
            Debug.Log("[PauseManager] OnNavigationMenu ignored - currently in main menu");
            return;
        }

        if (LoadingScreenController.IsLoading)
        {
            Debug.Log("[PauseManager] OnNavigationMenu ignored - currently loading");
            return;
        }

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

        RefreshNaviEntryIndicator();
        
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

        RefreshNaviEntryIndicator();

        // Only skip one extra layer if the newly revealed top menu is still blocked.
        if (menuListManager.menusToManage.Count > 2
            && menuListManager.menusToBlock.Contains(menuListManager.menusToManage[0]))
        {
            menuListManager.GoBackToPreviousMenu();
        }

        SyncActiveMenuToStackTop();

    }

    private void SyncActiveMenuToStackTop()
    {
        if (menuListManager == null || menuListManager.menusToManage == null || menuListManager.menusToManage.Count == 0)
            return;

        GameObject topMenu = menuListManager.menusToManage[0];
        if (topMenu == pauseMenuHolder)
            currentActiveMenu = ActiveMenu.PauseMenu;
        else if (topMenu == navigationMenuHolder)
            currentActiveMenu = ActiveMenu.NavigationMenu;
    }

    /// <summary>
    /// Closes the settings menu and returns to the pause menu.
    /// Call this from your Settings "Back" button as well.
    /// </summary>
    public void CloseSettingsMenu()
    {
        settingsMenuOpen = false;
        ForceCloseSettingsPages();
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
        if (IsPauseBlockedByPuzzleMode())
            return;

        if (LoadingScreenController.IsLoading)
        {
            Debug.Log("[PauseManager] OnSwapMenu ignored - currently loading");
            return;
        }

        if (ConfirmationDialog.AnyOpen)
        {
            Debug.Log("[PauseManager] OnSwapMenu ignored - confirmation dialog open");
            return;
        }
        // Only swap if game is paused and a menu is active
        if (!IsPaused || currentActiveMenu == ActiveMenu.None)
            return;

        RefreshNaviEntryIndicator();

        if (currentActiveMenu == ActiveMenu.PauseMenu && menuListManager.menusToManage[0] == pauseMenuHolder)
        {
            // Switch from pause menu to navigation menu
            SwapToNavigationMenu();
        }
        else if (currentActiveMenu == ActiveMenu.NavigationMenu && menuListManager.menusToManage[0] == navigationMenuHolder)
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

        if (LoadingScreenController.IsLoading)
        {
            Debug.Log("[PauseManager] ShowPauseMenu ignored - currently loading");
            return;
        }

        EnterPausedUiShell();

        // Request pause through the coordinator (centralized time scale authority).
        PauseCoordinator.RequestPause(GameplayInputBlockOwnerId);

        // Block gameplay input while menus are active
        InputReader.RequestGameplayInputBlock(GameplayInputBlockOwnerId);
        currentActiveMenu = ActiveMenu.PauseMenu;

        if (menuListManager != null && pauseMenuHolder != null)
            menuListManager.AddToMenuList(pauseMenuHolder);

        RefreshNaviEntryIndicator();

        SetMenuStates(showPause: true, showNavigation: false, showSettings: false);
        SetBlurEnabled(true);

        // Prevent same physical key press from immediately firing Back after action map switch.
        ignoreBackUntilTime = Time.unscaledTime + inputDebounceSeconds;
        ignorePauseUntilTime = Time.unscaledTime + inputDebounceSeconds;

        DebugLogSettingsM.ConditionalLog(DebugLogCategory.UI, "Pause Menu Opened");
        
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
        if (LoadingScreenController.IsLoading)
        {
            Debug.Log("[PauseManager] ShowNavigationMenu ignored - currently loading");
            return;
        }

        EnterPausedUiShell();

        // Request pause through the coordinator (centralized time scale authority).
        PauseCoordinator.RequestPause(GameplayInputBlockOwnerId);

        InputReader.RequestGameplayInputBlock(GameplayInputBlockOwnerId);
        currentActiveMenu = ActiveMenu.NavigationMenu;

        if (menuListManager != null && navigationMenuHolder != null)
            menuListManager.AddToMenuList(navigationMenuHolder);

        SetMenuStates(showPause: false, showNavigation: true, showSettings: false);
        SetBlurEnabled(true);

        // Prevent same physical key press from immediately firing Back after action map switch.
        ignoreBackUntilTime = Time.unscaledTime + inputDebounceSeconds;
        ignorePauseUntilTime = Time.unscaledTime + inputDebounceSeconds;

        Debug.Log("Navigation Menu Opened");
        RefreshNaviEntryIndicator();

        ActsManager actsManager = FindAnyObjectByType<ActsManager>();
        if (actsManager != null)
            actsManager.RefreshNavigationMapDisplay();
        
        // Switch to UI input
        if (InputReader.PlayerInput != null)
        {
            InputReader.PlayerInput.SwitchCurrentActionMap("UI");
            CursorManager.RefreshPolicy();
        }
    }

    private void EnterPausedUiShell()
    {
        if (pauseOverlay != null && !pauseOverlay.activeInHierarchy)
        {
            if (fadeMenus != null)
                StartCoroutine(fadeMenus.FadeMenu(pauseOverlay, fadeMenus.fadeDuration, true));
            else
                pauseOverlay.SetActive(true);
        }

        if (SoundManager.Instance != null)
        {
            if (SoundManager.Instance.sfxSource != null)
                SoundManager.Instance.sfxSource.Pause();

            if (SoundManager.Instance.puzzleSource != null)
                SoundManager.Instance.puzzleSource.Pause();

            if (SoundManager.Instance.ambienceSource != null)
                SoundManager.Instance.ambienceSource.Pause();
        }
        else
        {
            Debug.LogWarning("[PauseManager] EnterPausedUiShell: SoundManager.Instance was null, skipping audio pause.");
        }

        RumbleManager.Instance.StopControllerRumble();
    }

    private void SwapToPauseMenu()
    {
        currentActiveMenu = ActiveMenu.PauseMenu;

        if (LoadingScreenController.IsLoading)
        {
            Debug.Log("[PauseManager] OnNavigationMenu ignored - currently loading");
            return;
        }

        if (menuListManager != null && pauseMenuHolder != null)
            menuListManager.AddToMenuList(pauseMenuHolder);

        if (menuListManager != null && menuListManager.menusToManage != null)
            menuListManager.menusToManage.Remove(navigationMenuHolder);

        RefreshNaviEntryIndicator();

        SetMenuStates(showPause: true, showNavigation: false, showSettings: false);
        SetBlurEnabled(true);

        Debug.Log("Swapped to Pause Menu");
    }

    private void SwapToNavigationMenu()
    {
        if (LoadingScreenController.IsLoading)
        {
            Debug.Log("[PauseManager] OnNavigationMenu ignored - currently loading");
            return;
        }    
        
        currentActiveMenu = ActiveMenu.NavigationMenu;

        if (menuListManager != null && navigationMenuHolder != null)
            menuListManager.AddToMenuList(navigationMenuHolder);

        RefreshNaviEntryIndicator();

        SetMenuStates(showPause: false, showNavigation: true, showSettings: false);
            SetBlurEnabled(true);

        Debug.Log("Swapped to Navigation Menu");
    }

    public void ResumeGame()
    {
        ForceCloseAllWarningUi();

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

        // Release gameplay input block
        InputReader.ReleaseGameplayInputBlock(GameplayInputBlockOwnerId);
        currentActiveMenu = ActiveMenu.None;
        // Release the coordinator ownership for pause
        PauseCoordinator.ReleaseTimeScale(GameplayInputBlockOwnerId);

        StartCoroutine(DelayAfterUnpausing());

        footerManager?.UpdateFooterForMenu(null);

        

        HideAllMenus();
        SetBlurEnabled(false);
        
        if (pauseOverlay.activeInHierarchy)
        {
            if (fadeMenus != null)
                StartCoroutine(fadeMenus.FadeMenu(pauseOverlay, fadeMenus.fadeDuration, false));
            else
                pauseOverlay.SetActive(false);
        }


        // Prevent immediate re-open from the same key press while returning to Gameplay.
        ignorePauseUntilTime = Time.unscaledTime + inputDebounceSeconds;
        ignoreBackUntilTime = Time.unscaledTime + inputDebounceSeconds;
        ApplyResumeUiGuard();


        if (SoundManager.Instance != null)
        {
            if (SoundManager.Instance.sfxSource != null)
                SoundManager.Instance.sfxSource.UnPause();

            if (SoundManager.Instance.puzzleSource != null)
                SoundManager.Instance.puzzleSource.UnPause();

            if (SoundManager.Instance.ambienceSource != null)
                SoundManager.Instance.ambienceSource.UnPause();
        }

        Debug.Log("Game Resumed");
        
        
    }

    private void ApplyResumeUiGuard()
    {
        MenuSelectionSuppression.SuppressForFrames(unpauseSelectionSuppressionFrames);

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
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
        ForceCloseAllWarningUi();

        // Ensure menu muffling does not leak into gameplay after scene transition.
        MufffleMusicForMenu(false);

        // Release this menu's pause ownership so restart transitions do not leave the game paused.
        PauseCoordinator.ReleaseTimeScale(GameplayInputBlockOwnerId);

        footerManager?.UpdateFooterForMenu(null);

        // Hide pause/navigation UI and release local gameplay input blocking.
        InputReader.ReleaseGameplayInputBlock(GameplayInputBlockOwnerId);
        currentActiveMenu = ActiveMenu.None;
        HideAllMenus();
        menuListManager?.ResetForGameplay();
        SetHUDVisible(true);
        SetBlurEnabled(false);

        if (pauseOverlay != null && pauseOverlay.activeInHierarchy)
        {
            if (fadeMenus != null)
                StartCoroutine(fadeMenus.FadeMenu(pauseOverlay, fadeMenus.fadeDuration, false));
            else
                pauseOverlay.SetActive(false);
        }

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
        ForceCloseSettingsPages();
        SetMenuStates(false, false, false);
    }

    private void ForceCloseSettingsPages()
    {
        if (menuListManager == null)
            return;

        if (menuListManager.menusToManage != null)
        {
            for (int i = menuListManager.menusToManage.Count - 1; i >= 0; i--)
            {
                GameObject openMenu = menuListManager.menusToManage[i];
                if (!IsSettingsMenuOrChild(openMenu))
                    continue;

                menuListManager.menusToManage.RemoveAt(i);
            }
        }

        if (menuListManager.settingPageMenus != null)
        {
            for (int i = 0; i < menuListManager.settingPageMenus.Count; i++)
            {
                GameObject settingsPage = menuListManager.settingPageMenus[i];
                if (settingsPage != null)
                    settingsPage.SetActive(false);
            }
        }

        if (settingsMenuContainer != null)
            settingsMenuContainer.SetActive(false);
    }

    private bool IsSettingsMenuOrChild(GameObject menu)
    {
        if (menu == null)
            return false;

        if (menu == settingsMenuContainer)
            return true;

        if (menuListManager == null || menuListManager.settingPageMenus == null)
            return false;

        for (int i = 0; i < menuListManager.settingPageMenus.Count; i++)
        {
            GameObject settingsPage = menuListManager.settingPageMenus[i];
            if (settingsPage == null)
                continue;

            if (menu == settingsPage || menu.transform.IsChildOf(settingsPage.transform))
                return true;
        }

        return false;
    }

    private void SetMenuStates(bool showPause, bool showNavigation, bool showSettings)
    {
        FadeMenus fadeMenus = this.GetComponent<FadeMenus>();
        settingsMenuOpen = showSettings;

        if (!showNavigation)
            SetNavigationBlockingChildrenVisible(false);

        if (pauseMenuHolder != null)
        {
            if (fadeMenus != null)
                StartCoroutine(fadeMenus.FadeMenu(pauseMenuHolder, fadeMenus.fadeDuration, showPause));
            else
                pauseMenuHolder.SetActive(showPause);
        }

        if (navigationMenuHolder != null)
        {
            if (fadeMenus != null)
                StartCoroutine(fadeMenus.FadeMenu(navigationMenuHolder, fadeMenus.fadeDuration, showNavigation));
            else
                navigationMenuHolder.SetActive(showNavigation);
        }

        if (settingsMenuContainer != null)
            settingsMenuContainer.SetActive(showSettings);

        bool showHUD = !(showPause || showNavigation || showSettings);

        if(CranePuzzle.IsCranePuzzleActive || ElevatorLift.ElevatorMenuActive)
            return;

        SetHUDVisible(showHUD);
    }

    private void SetNavigationBlockingChildrenVisible(bool visible)
    {
        if (navigationMenuBlockingChildren == null)
            return;

        foreach (GameObject child in navigationMenuBlockingChildren)
        {
            if (child == null)
                continue;

            child.SetActive(visible);
        }
    }

    private void SetHUDVisible(bool visible)
    {
        if (!TryResolveHudRoot())
            return;

        if (playerHUDRoot.activeSelf != visible)
            playerHUDRoot.SetActive(visible);
    }

    private bool IsPauseUiActive()
    {
        return IsPaused
            || currentActiveMenu != ActiveMenu.None
            || (pauseOverlay != null && pauseOverlay.activeInHierarchy)
            || (pauseMenuHolder != null && pauseMenuHolder.activeInHierarchy)
            || (navigationMenuHolder != null && navigationMenuHolder.activeInHierarchy)
            || (settingsMenuContainer != null && settingsMenuContainer.activeInHierarchy);
    }

    public void SetGameplayHUDVisible(bool visible)
    {
        SetHUDVisible(visible);
    }

    private void ForceCloseAllWarningUi()
    {
        WarningButtonFunctionality[] warningDialogs = FindObjectsByType<WarningButtonFunctionality>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (warningDialogs == null || warningDialogs.Length == 0)
            return;

        foreach (WarningButtonFunctionality warningDialog in warningDialogs)
        {
            if (warningDialog == null)
                continue;

            warningDialog.ForceHideWarningUI();
        }
    }

    private bool IsPauseBlockedByPuzzleMode()
    {
        if (!CranePuzzle.IsCranePuzzleActive && !ElevatorLift.ElevatorMenuActive)
            return false;

        Debug.Log("[PauseManager] Pause input ignored while puzzle interaction mode is active.");
        return true;
    }

    private bool TryResolveHudRoot()
    {
        if (playerHUDRoot != null)
            return true;

        if (string.IsNullOrEmpty(playerHUDRootNameHint))
            return false;

        var candidate = GameObject.Find(playerHUDRootNameHint);
        if (candidate == null)
            candidate = FindInactiveHudRootByName(playerHUDRootNameHint);

        if (candidate == null)
            return false;

        playerHUDRoot = candidate;
        CacheHudRootName();
        return true;
    }

    public void TurnOffFooterAndOverlay()
    {
        if (footerManager != null)
            footerManager.UpdateFooterForMenu(null);

        if (pauseOverlay != null && pauseOverlay.activeInHierarchy)
        {
            if (fadeMenus != null)
                StartCoroutine(fadeMenus.FadeMenu(pauseOverlay, fadeMenus.fadeDuration, false));
            else
                pauseOverlay.SetActive(false);
        }
    }

    private static GameObject FindInactiveHudRootByName(string targetName)
    {
        if (string.IsNullOrEmpty(targetName))
            return null;

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.IsValid() || !scene.isLoaded)
                continue;

            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                GameObject root = roots[rootIndex];
                if (root == null)
                    continue;

                if (root.name == targetName)
                    return root;

                Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
                for (int descendantIndex = 0; descendantIndex < descendants.Length; descendantIndex++)
                {
                    Transform descendant = descendants[descendantIndex];
                    if (descendant != null && descendant.name == targetName)
                        return descendant.gameObject;
                }
            }
        }

        return null;
    }

    private void MufffleMusicForMenu(bool shouldMuffle)
    {
        if (SoundManager.Instance == null || SoundManager.Instance.levelMusicSource == null)
            return;

        var musicSource = SoundManager.Instance.levelMusicSource;

        if (cachedPauseLowPassFilter != null && cachedPauseLowPassFilter.gameObject != musicSource.gameObject)
            cachedPauseLowPassFilter = null;

        AudioLowPassFilter lowPassFilter = cachedPauseLowPassFilter;
        if (lowPassFilter == null)
        {
            lowPassFilter = musicSource.GetComponent<AudioLowPassFilter>();
            if (lowPassFilter == null)
                lowPassFilter = musicSource.gameObject.AddComponent<AudioLowPassFilter>();

            cachedPauseLowPassFilter = lowPassFilter;
        }

        const float defaultCutoff = 22000f;
        if (shouldMuffle)
        {
            if (musicIsMuffled)
            {
                Debug.Log("[PauseManager] Music already muffled, skipping.");
                return;
            }
            Debug.Log("Muffling music for menu");

            cachedLowPassEnabledBeforePause = lowPassFilter.enabled;
            cachedLowPassCutoffBeforePause = lowPassFilter.cutoffFrequency;

            lowPassFilter.enabled = true;
            lowPassFilter.cutoffFrequency = 500f;

            musicIsMuffled = true;
        }
        else
        {
            if (!musicIsMuffled)
            {
                Debug.Log("[PauseManager] Music not muffled, skipping restore.");
                return;
            }
            Debug.Log("Restoring music after menu");

            lowPassFilter.cutoffFrequency = cachedLowPassCutoffBeforePause ?? defaultCutoff;
            lowPassFilter.enabled = cachedLowPassEnabledBeforePause ?? false;

            musicIsMuffled = false;
            cachedLowPassCutoffBeforePause = null;
            cachedLowPassEnabledBeforePause = null;
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

