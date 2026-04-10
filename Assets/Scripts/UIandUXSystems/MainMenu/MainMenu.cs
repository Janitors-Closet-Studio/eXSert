using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Main menu controller. Handles New Game, Load Game, and Quit buttons.
/// Updated to work with new SceneLoader and CheckpointSystem.
/// </summary>
public class MainMenu : Menu
{
    [Header("Menu Navigation")]
    [SerializeField] private SaveSlotsMenu saveSlotsMenu;

    [SerializeField] private Button loadGame;
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button quitButton;
    
    [SerializeField] private InputActionReference backButtonInputAction;

    private void Start()
    {
        // Disable load game button if no save data exists
        if (!DataPersistenceManager.HasGameData()) loadGame.interactable = false;

        if (loadGame != null) loadGame.onClick.AddListener(OnLoadGameClicked);
        if (newGameButton != null) newGameButton.onClick.AddListener(OnNewGameClicked);
        if (quitButton != null) quitButton.onClick.AddListener(OnQuitGameClicked);
    }

    private void OnDestroy()
    {
        if (loadGame != null) loadGame.onClick.RemoveListener(OnLoadGameClicked);
        if (newGameButton != null) newGameButton.onClick.RemoveListener(OnNewGameClicked);
        if (quitButton != null) quitButton.onClick.RemoveListener(OnQuitGameClicked);
    }

    protected void OnEnable()
    {

        if (backButtonInputAction != null && backButtonInputAction.action != null)
        {
            backButtonInputAction.action.performed += OnBackButtonPressed;
            backButtonInputAction.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (backButtonInputAction != null && backButtonInputAction.action != null)
            backButtonInputAction.action.performed -= OnBackButtonPressed;
    }

    private void OnBackButtonPressed(InputAction.CallbackContext context)
    {
        var menuListManager = this.GetComponent<MenuListManager>();
        if (menuListManager != null)
            menuListManager.GoBackToPreviousMenu();
    }

    /// Called when Load Game button is clicked.
    /// Opens save slot selection for loading existing game.
    /// </summary>
    public void OnLoadGameClicked()
    {
        saveSlotsMenu.ActivateMenu(true);
        this.DeactivateMenu();
    }

    /// <summary>
    /// Called when New Game button is clicked.
    /// Opens save slot selection for starting a new game.
    /// </summary>
    public void OnNewGameClicked()
    {
        saveSlotsMenu.ActivateMenu(false);
        this.DeactivateMenu();
    }

    /// <summary>
    /// Called when Quit button is clicked.
    /// Quits the application.
    /// </summary>
    public void OnQuitGameClicked()
    {
        Debug.Log("[MainMenu] Quit button clicked");
        
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
        Application.Quit();
        #endif
    }

    public void ActivateMenu()
    {
        this.gameObject.SetActive(true);
    }

    public void DeactivateMenu()
    {
        this.gameObject.SetActive(false);
    }
}
