using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Main menu controller. Handles New Game, Load Game, and Quit buttons.
/// Updated to work with new SceneLoader and CheckpointSystem.
/// </summary>
public class MainMenu : MonoBehaviour
{
    public static bool isInMainMenu = false;

    [Header("Menu Navigation")]
    [SerializeField] private SaveSlotsMenu saveSlotsMenu;

    [SerializeField] private Button loadGame;
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button quitButton;
    
    [SerializeField] private InputActionReference backButtonInputAction;

    private void RefreshLoadButtonState()
    {
        if (loadGame != null)
            loadGame.interactable = DataPersistenceManager.HasAnySavedProfiles();
    }

    private void Start()
    {
        isInMainMenu = true;
        RefreshLoadButtonState();

        if (loadGame != null) loadGame.onClick.AddListener(OnLoadGameClicked);
        if (newGameButton != null) newGameButton.onClick.AddListener(OnNewGameClicked);
    }

    private void OnDestroy()
    {
        if (loadGame != null) loadGame.onClick.RemoveListener(OnLoadGameClicked);
        if (newGameButton != null) newGameButton.onClick.RemoveListener(OnNewGameClicked);
    }

    protected void OnEnable()
    {
        RefreshLoadButtonState();

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

        isInMainMenu = false;
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

    }

    /// <summary>
    /// Called when New Game button is clicked.
    /// Opens save slot selection for starting a new game.
    /// </summary>
    public void OnNewGameClicked()
    {
        saveSlotsMenu.ActivateMenu(false);

    }

    /// <summary>
    /// Called when Quit button is clicked.
    /// Quits the application.
    /// </summary>
    

    public void ActivateMenu()
    {
        RefreshLoadButtonState();
        this.gameObject.SetActive(true);
    }

    public void DeactivateMenu()
    {
        this.gameObject.SetActive(false);
    }
}
