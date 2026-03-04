using UnityEngine;
using UnityEngine.SceneManagement;
using Progression.Checkpoints;

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

    private void Start()
    {
        // Try to find PauseManager if not assigned
        if (pauseManager == null)
        {
            pauseManager = PauseManager.Instance;
        }
    }

    /// <summary>
    /// Restarts the game from the last checkpoint.
    /// Uses CheckpointSystem to reload the proper scene and spawn point.
    /// </summary>
    public void RestartFromCheckpoint()
    {
        Debug.Log("[GameActionHandler] Restarting from checkpoint...");

        PrepareForSceneLoad(resumeImmediately: false);
        
        Player.TriggerRespawn();
    }

    /// <summary>
    /// Returns to the main menu.
    /// Properly cleans up DontDestroyOnLoad objects.
    /// </summary>
    public void ReturnToMainMenu()
    {
        Debug.Log("[GameActionHandler] Returning to main menu...");
        
        PrepareForSceneLoad(resumeImmediately: false);
        
        SceneAsset.Load("MainMenu");
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
        if (pauseManager == null)
            pauseManager = PauseManager.Instance;

        if (pauseManager != null)
        {
            if (resumeImmediately)
                pauseManager.ResumeGame();
            else
                pauseManager.HideMenusForSceneTransition();
        }
        else if (resumeImmediately)
        {
            Time.timeScale = 1f;
        }
    }
}
