using UnityEngine;
using UnityEngine.SceneManagement;
using Progression.Checkpoints;
using Unity.AppUI.UI;

/// <summary>
/// Handles common game actions like restarting, returning to menu, quitting.
/// Use this with ConfirmationDialog to execute these actions after confirmation.
/// Updated to work with SceneLoader and CheckpointSystem.
/// </summary>
public class GameActionHandler : MonoBehaviour
{
    [Header("References")]
    [SerializeField, Tooltip("Reference to PauseManager (optional)")]
    private PauseManager pauseManager;

    private MenuListManager menuListManager;

    private void Start()
    {
        // Try to find PauseManager if not assigned
        if (pauseManager == null)
        {
            pauseManager = PauseManager.Instance;

            if (pauseManager == null)
            {
                Debug.LogWarning("[GameActionHandler] No PauseManager found in the scene. Some functionality may not work properly.");
            }
        }

        if (pauseManager != null)
            menuListManager = pauseManager.GetComponent<MenuListManager>();
    }

    /// <summary>
    /// Restarts the game from the last checkpoint.
    /// Uses CheckpointSystem to reload the proper scene and spawn point.
    /// </summary>
    public void RestartFromCheckpoint()
    {
        Debug.Log("[GameActionHandler] RestartFromCheckpoint: Method called.");
        Debug.Log("[GameActionHandler] RestartFromCheckpoint: Calling PrepareForSceneLoad...");
        PrepareForSceneLoad(resumeImmediately: false);
        Debug.Log("[GameActionHandler] RestartFromCheckpoint: PrepareForSceneLoad completed.");
        
        Debug.Log("[GameActionHandler] RestartFromCheckpoint: Calling Player.TriggerRespawn()...");
        Player.TriggerRespawn();
        Debug.Log("[GameActionHandler] RestartFromCheckpoint: Player.TriggerRespawn() completed.");
        
        Debug.Log("[GameActionHandler] RestartFromCheckpoint: Calling ActsManager.Instance.ActivateAllImagesBefore()...");
        ActsManager.Instance.ActivateAllImagesBefore();
        Debug.Log("[GameActionHandler] RestartFromCheckpoint: ActsManager.Instance.ActivateAllImagesBefore() completed.");
    }

    public void RestartFromSelectedScene(SceneAsset sceneAsset)
    {
        Debug.Log($"[GameActionHandler] Restarting from selected scene '{sceneAsset.SceneName}'...");

        PrepareForSceneLoad(resumeImmediately: false);
        
        SceneLoader.LoadIntoGame(sceneAsset);
        ActsManager.Instance.ActivateAllImagesBefore();

    }

    /// <summary>
    /// Returns to the main menu.
    /// Properly cleans up DontDestroyOnLoad objects.
    /// </summary>
    public void ReturnToMainMenu()
    {
        Debug.Log("[GameActionHandler] ReturnToMainMenu: Method called.");
        Debug.Log("[GameActionHandler] ReturnToMainMenu: Calling PrepareForSceneLoad...");
        PrepareForSceneLoad(resumeImmediately: false);
        Debug.Log("[GameActionHandler] ReturnToMainMenu: PrepareForSceneLoad completed.");

        PauseManager.Instance.HideMenusForSceneTransition();

        Debug.Log("[GameActionHandler] ReturnToMainMenu: Calling SceneLoader.LoadMainMenu()...");
        SceneLoader.LoadMainMenu();
        Debug.Log("[GameActionHandler] ReturnToMainMenu: SceneLoader.LoadMainMenu() completed.");
    }

    /// <summary>
    /// Quits the game application.
    /// </summary>
    public void QuitGame()
    {
        Debug.Log("[GameActionHandler] Quitting game...");

    #if UNITY_EDITOR
        // Stop playing in editor
        UnityEditor.EditorApplication.isPlaying = false;
    #else
        // Quit the application
        Application.Quit();
    #endif
    }

    /// <summary>
    /// Just closes the dialog without doing anything (for cancel actions)
    /// </summary>
    public void OnDialogCanceled()
    {
        Debug.Log("[GameActionHandler] Dialog canceled");
        // Nothing to do, just logging
    }

    private void PrepareForSceneLoad(bool resumeImmediately)
    {
        MenuListManager menuListManager = pauseManager != null ? pauseManager.GetComponent<MenuListManager>() : null;
        if (menuListManager != null)
            menuListManager.menusToManage.RemoveAt(0); // Remove the confirmation dialog menu from the list of managed menus to prevent it from being hidden prematurely

        Debug.Log($"[GameActionHandler] PrepareForSceneLoad called (resumeImmediately={resumeImmediately}).");
        
        if (pauseManager == null)
        {
            Debug.Log("[GameActionHandler] PrepareForSceneLoad: pauseManager is null, searching for PauseManager.Instance...");
            pauseManager = PauseManager.Instance;
        }
        else
        {
            Debug.Log("[GameActionHandler] PrepareForSceneLoad: pauseManager already assigned.");
        }

        if (pauseManager != null)
        {
            if (resumeImmediately)
            {
                Debug.Log("[GameActionHandler] PrepareForSceneLoad: Calling pauseManager.ResumeGame().");
                pauseManager.ResumeGame();
                Debug.Log("[GameActionHandler] PrepareForSceneLoad: pauseManager.ResumeGame() completed.");
            }
            else
            {
                Debug.Log("[GameActionHandler] PrepareForSceneLoad: Calling pauseManager.HideMenusForSceneTransition().");
                pauseManager.HideMenusForSceneTransition();
                Debug.Log("[GameActionHandler] PrepareForSceneLoad: pauseManager.HideMenusForSceneTransition() completed.");
            }
        }
        else
        {
            Debug.LogWarning("[GameActionHandler] PrepareForSceneLoad: pauseManager is null and not found!");
            if (resumeImmediately)
            {
                Debug.Log("[GameActionHandler] PrepareForSceneLoad: Fallback - setting Time.timeScale = 1f.");
                Time.timeScale = 1f;
            }
        }
        
        Debug.Log("[GameActionHandler] PrepareForSceneLoad: Complete.");
    }
}
