/*
    Written by Brandon Wahl

    This script will handle the functionality of the warning buttons in the pause menu
*/


using UnityEngine;

/// <summary>
/// Centralizes the warning prompt logic so buttons no longer need to wire up multiple GameObject.SetActive calls.
/// </summary>
public class WarningButtonFunctionality : MonoBehaviour
{
    private enum WarningAction
    {
        None,
        RestartCheckpoint,
        ReturnToMainMenu,
        QuitGame
    }

    [Header("UI References")]
    [SerializeField] private GameObject warningCanvas;
    [SerializeField] private GameObject overlayCanvas;
    [SerializeField] private GameObject confirmIcon;
    [SerializeField] private GameObject rejectIcon;
    [SerializeField] private GameObject checkpointText;
    [SerializeField] private GameObject quitText;
    [SerializeField] private GameObject returnToMenuText;

    [Header("Action Handler")]
    [SerializeField] private GameActionHandler actionHandler;

    private WarningAction pendingAction = WarningAction.None;

    /// <summary>
    /// Existing buttons still call this via OnClick. It now simply delegates to the new confirm handler.
    /// </summary>
    public void WhichFunctionToCarryOut()
    {
        OnConfirmPressed();
    }

    public void ShowCheckpointWarning()
    {
        PrepareWarning(WarningAction.RestartCheckpoint, checkpointText);
    }

    public void ShowReturnToMenuWarning()
    {
        PrepareWarning(WarningAction.ReturnToMainMenu, returnToMenuText);
    }

    public void ShowQuitWarning()
    {
        PrepareWarning(WarningAction.QuitGame, quitText);
    }

    public void OnConfirmPressed()
    {
        var actionToRun = ResolvePendingAction();
        Debug.Log($"[WarningButtonFunctionality] Confirm pressed. Resolved action: {actionToRun}");
        if (actionToRun == WarningAction.None)
            return;

        HideWarningUI();
        ExecuteAction(actionToRun);
    }

    public void OnBackPressed()
    {
        HideWarningUI();
    }

    private void PrepareWarning(WarningAction action, GameObject textToEnable)
    {
        pendingAction = action;
        ActivateTextBlock(textToEnable);
        SetWarningVisible(true);
    }

    private void ActivateTextBlock(GameObject target)
    {
        if (checkpointText != null)
            checkpointText.SetActive(target == checkpointText);

        if (returnToMenuText != null)
            returnToMenuText.SetActive(target == returnToMenuText);

        if (quitText != null)
            quitText.SetActive(target == quitText);
    }

    private void SetWarningVisible(bool visible)
    {
        if (warningCanvas != null)
            warningCanvas.SetActive(visible);

        if (overlayCanvas != null)
            overlayCanvas.SetActive(visible);

        if (confirmIcon != null)
            confirmIcon.SetActive(visible);

        if (rejectIcon != null)
            rejectIcon.SetActive(visible);
    }

    private void HideWarningUI()
    {
        pendingAction = WarningAction.None;
        ActivateTextBlock(null);
        SetWarningVisible(false);
    }

    private WarningAction ResolvePendingAction()
    {
        if (pendingAction != WarningAction.None)
            return pendingAction;

        if (checkpointText != null && checkpointText.activeInHierarchy)
            return WarningAction.RestartCheckpoint;

        if (returnToMenuText != null && returnToMenuText.activeInHierarchy)
            return WarningAction.ReturnToMainMenu;

        if (quitText != null && quitText.activeInHierarchy)
            return WarningAction.QuitGame;

        return WarningAction.None;
    }

    private void ExecuteAction(WarningAction action)
    {
        var handler = ResolveActionHandler();

        switch (action)
        {
            case WarningAction.RestartCheckpoint:
                if (handler != null)
                {
                    handler.RestartFromCheckpoint();
                }
                else if (SceneLoader.Instance != null)
                {
                    SceneLoader.Instance.RestartFromCheckpoint();
                }
                else
                {
                    Debug.LogWarning("[WarningButtonFunctionality] Restart requested but no SceneLoader/GameActionHandler available.");
                }
                break;

            case WarningAction.ReturnToMainMenu:
                if (handler != null)
                {
                    handler.ReturnToMainMenu();
                }
                else
                {
                    PauseManager.Instance?.ResumeGame();
                    if (SceneLoader.Instance != null)
                    {
                        SceneLoader.Instance.LoadMainMenu();
                    }
                    else
                    {
                        Debug.LogWarning("[WarningButtonFunctionality] Return to menu requested but no SceneLoader/GameActionHandler available.");
                    }
                }
                break;

            case WarningAction.QuitGame:
                if (handler != null)
                {
                    handler.QuitGame();
                }
                else
                {
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#else
                    Application.Quit();
#endif
                }
                break;

            default:
                Debug.LogWarning("[WarningButtonFunctionality] Unknown warning action requested.");
                break;
        }
    }

    private GameActionHandler ResolveActionHandler()
    {
        if (actionHandler != null)
            return actionHandler;

        actionHandler = FindFirstObjectByType<GameActionHandler>(FindObjectsInactive.Include);
        return actionHandler;
    }
}
